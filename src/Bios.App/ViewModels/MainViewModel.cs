using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Bios.App.Models;
using Bios.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bios.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IUiService _ui;
    private readonly RunStore _store = new();
    private readonly ScewinRunner _runner;
    private readonly Catalog _catalog;

    private string[] _currentLines = Array.Empty<string>();
    private List<ScewinBlock> _currentBlocks = new();

    public ObservableCollection<CategoryViewModel> Categories { get; } = new();
    public List<TweakViewModel> AllTweaks { get; } = new();

    [ObservableProperty] private CategoryViewModel? _selectedCategory;
    [ObservableProperty] private bool _isImported;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyMessage = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _hardwareText = "";

    /// <summary>Writes the current selection would perform: tweaks to activate plus tweaks to reset.</summary>
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    [ObservableProperty] private int _pendingChangeCount;

    /// <summary>Share of <see cref="PendingChangeCount"/> that activates a tweak.</summary>
    [ObservableProperty] private int _pendingApplyCount;

    /// <summary>Share of <see cref="PendingChangeCount"/> that restores BIOS defaults.</summary>
    [ObservableProperty] private int _pendingRevertCount;

    /// <summary>Active settings this BIOS exposes no default for — they cannot be reset by the app.</summary>
    [ObservableProperty] private int _lockedCount;

    public string AppTitle => "BIOS Tuner";

    public bool HasPendingChanges => PendingChangeCount > 0;

    /// <summary>Drives the "read the BIOS first" panel; the switches mean nothing before an import.</summary>
    public bool IsNotImported => !IsImported;

    /// <summary>One-line read-out of what Apply would do, split by direction.</summary>
    public string PendingSummary
    {
        get
        {
            if (!IsImported) return "Lisez le BIOS pour voir ce qui changerait.";
            if (PendingChangeCount == 0) return "Le BIOS correspond à votre sélection — rien à écrire.";

            var parts = new List<string>();
            if (PendingApplyCount > 0) parts.Add($"{PendingApplyCount} à activer");
            if (PendingRevertCount > 0) parts.Add($"{PendingRevertCount} à remettre par défaut");
            return string.Join("  ·  ", parts);
        }
    }

    /// <summary>Warning shown when some active settings cannot be reset because this BIOS gives no default.</summary>
    public string LockedNotice => LockedCount == 0
        ? ""
        : $"{LockedCount} paramètre(s) désactivé(s) resteront en place : ce BIOS n'expose aucune valeur par défaut pour eux.";

    public bool HasLockedNotice => LockedCount > 0;

    partial void OnPendingChangeCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(PendingSummary));
    }

    partial void OnPendingApplyCountChanged(int value) => OnPropertyChanged(nameof(PendingSummary));
    partial void OnPendingRevertCountChanged(int value) => OnPropertyChanged(nameof(PendingSummary));

    partial void OnLockedCountChanged(int value)
    {
        OnPropertyChanged(nameof(LockedNotice));
        OnPropertyChanged(nameof(HasLockedNotice));
    }

    public MainViewModel(IUiService ui)
    {
        _ui = ui;
        _runner = new ScewinRunner(_store.BaseDir);
        _catalog = CatalogLoader.Load();
        HardwareText = _catalog.Hardware;
        BuildViewModels();
    }

    private void BuildViewModels()
    {
        foreach (var category in _catalog.Categories)
        {
            var tweakVms = category.Tweaks.Select(t => new TweakViewModel(t)).ToList();
            foreach (var tv in tweakVms)
            {
                tv.PropertyChanged += OnTweakPropertyChanged;
                AllTweaks.Add(tv);
            }
            Categories.Add(new CategoryViewModel(category, tweakVms));
        }
        SelectedCategory = Categories.FirstOrDefault();
    }

    private bool _suppressExclusivity;

    /// <summary>Set while a batch toggles many tweaks, so the plan is recomputed once at the end.</summary>
    private bool _suppressRecompute;

    private void OnTweakPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TweakViewModel.IsSelected) || sender is not TweakViewModel changed)
            return;

        if (changed.IsSelected && !_suppressExclusivity && !string.IsNullOrEmpty(changed.ExclusiveGroup))
        {
            _suppressExclusivity = true;
            foreach (var other in AllTweaks)
            {
                if (!ReferenceEquals(other, changed) && other.ExclusiveGroup == changed.ExclusiveGroup && other.IsSelected)
                    other.IsSelected = false;
            }
            _suppressExclusivity = false;
        }

        if (_suppressRecompute)
            return;

        RecomputeSelectionCount();
        foreach (var c in Categories) c.RaiseSelectedCount();
    }

    /// <summary>Toggle many tweaks, then recompute the plan a single time.</summary>
    private void Batch(Action change)
    {
        _suppressRecompute = true;
        try { change(); }
        finally { _suppressRecompute = false; }

        RecomputeSelectionCount();
        foreach (var c in Categories) c.RaiseSelectedCount();
    }

    /// <summary>
    /// Counts come from the whole plan, not from per-tweak sums: an enabled tweak can own a
    /// setting a disabled one also targets, and only the full plan resolves that.
    /// </summary>
    private void RecomputeSelectionCount()
    {
        if (!IsImported)
        {
            PendingChangeCount = PendingApplyCount = PendingRevertCount = LockedCount = 0;
            foreach (var tv in AllTweaks) tv.SetPending(Array.Empty<PlanRow>());
            return;
        }

        var rows = BuildCurrentPlan(mutate: false);
        PendingApplyCount = rows.Count(r => r.WillChange && !r.IsRevert);
        PendingRevertCount = rows.Count(r => r.WillChange && r.IsRevert);
        PendingChangeCount = PendingApplyCount + PendingRevertCount;
        LockedCount = rows.Count(r => r.IsRevert && r.Status == "skipped");

        // Distribute the real plan back onto the tweaks so every badge and summary agrees with it.
        var byTweak = rows.GroupBy(r => r.TweakId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PlanRow>)g.ToList());
        foreach (var tv in AllTweaks)
            tv.SetPending(byTweak.TryGetValue(tv.Model.Id, out var own) ? own : Array.Empty<PlanRow>());
    }

    private IEnumerable<TweakViewModel> SelectedInCatalogOrder => AllTweaks.Where(t => t.IsSelected);
    private IEnumerable<TweakViewModel> UnselectedInCatalogOrder => AllTweaks.Where(t => !t.IsSelected);

    private IEnumerable<(Tweak, Rule)> SelectedRuleset()
        => PlanService.Flatten(SelectedInCatalogOrder.Select(t => t.Model));

    private IEnumerable<(Tweak, Rule)> UnselectedRuleset()
        => PlanService.Flatten(UnselectedInCatalogOrder.Select(t => t.Model));

    /// <summary>The plan for the current selection, computed against the current export.</summary>
    private List<PlanRow> BuildCurrentPlan(bool mutate)
    {
        var clone = (string[])_currentLines.Clone();
        return PlanService.BuildPlan(_currentBlocks, clone, SelectedRuleset(), UnselectedRuleset(), mutate);
    }

    // ----------------------------------------------------------------- Import

    [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
    private async Task ImportAsync()
    {
        await RunBusy("Lecture du BIOS en cours (SCEWIN)…", () =>
        {
            string runDir = _store.NewRunDir("import");
            string before = Path.Combine(runDir, "before.txt");
            _runner.Export(before);
            var lines = File.ReadAllLines(before);
            var blocks = ScewinParser.ParseBlocks(lines);
            return (lines, blocks);
        },
        result =>
        {
            _currentLines = result.lines;
            _currentBlocks = result.blocks;
            IsImported = true;
            RefreshAllDiffs();
            int active = AllTweaks.Count(t => t.IsSelected);
            StatusText = $"BIOS lu le {DateTime.Now:dd/MM/yyyy HH:mm} · {_currentBlocks.Count} paramètres · "
                         + $"{active} réglage(s) du catalogue déjà actifs, cochés ci-dessous.";
        });
    }

    private void RefreshAllDiffs()
    {
        var none = Array.Empty<(Tweak, Rule)>();
        foreach (var tv in AllTweaks)
        {
            var one = PlanService.Flatten(new[] { tv.Model }).ToList();
            // Each tweak is measured on its own: what turning it on would write, and what
            // turning it off would restore. The real plan arbitrates between tweaks later.
            var applyRows = PlanService.BuildPlan(_currentBlocks, (string[])_currentLines.Clone(), one, none, mutate: false);
            var revertRows = PlanService.BuildPlan(_currentBlocks, (string[])_currentLines.Clone(), none, one, mutate: false);
            tv.UpdateDiff(applyRows, revertRows);
        }

        // Already-conform tweaks are auto-checked so the UI reflects the current BIOS state.
        // (Conform tweaks contribute zero changes, so Apply still writes only the real diff.)
        _suppressExclusivity = true;
        foreach (var tv in AllTweaks)
        {
            if (tv.IsConform)
                tv.IsSelected = true;
        }
        _suppressExclusivity = false;

        RecomputeSelectionCount();
        foreach (var c in Categories) c.RaiseSelectedCount();
    }

    // ----------------------------------------------------------------- Preview

    [RelayCommand(CanExecute = nameof(CanPreview))]
    private void Preview()
    {
        if (!IsImported)
        {
            _ui.Info("Aperçu", "Lisez d'abord le BIOS.");
            return;
        }
        _ui.ShowPlan("Aperçu — rien n'est écrit dans le BIOS", BuildCurrentPlan(mutate: false), showVerify: false);
    }

    private bool CanPreview() => !IsBusy && IsImported;

    // ----------------------------------------------------------------- Apply

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        var selected = SelectedInCatalogOrder.Select(t => t.Model).ToList();
        var unselected = UnselectedInCatalogOrder.Select(t => t.Model).ToList();

        // Preview against the current import to know how many REAL changes there are.
        var previewRows = BuildCurrentPlan(mutate: false);
        int applyCount = previewRows.Count(r => r.WillChange && !r.IsRevert);
        int revertCount = previewRows.Count(r => r.WillChange && r.IsRevert);
        int pendingChanges = applyCount + revertCount;

        if (pendingChanges == 0)
        {
            _ui.Info("Rien à écrire",
                "Le BIOS correspond déjà à votre sélection : les réglages activés sont en place, " +
                "et aucun réglage désactivé n'est encore appliqué.\n\n" +
                "Aucune écriture n'est nécessaire.");
            return;
        }

        // Always work from a FRESH export, then write only the diff.
        var detail = new List<string>();
        if (applyCount > 0) detail.Add($"{applyCount} à activer");
        if (revertCount > 0) detail.Add($"{revertCount} à remettre à leur valeur par défaut");

        bool confirmed = _ui.Confirm(
            "Écrire ces changements dans le BIOS ?",
            $"{pendingChanges} paramètre(s) seront écrits dans les variables UEFI via SCEWIN " +
            $"({string.Join(", ", detail)}).\n\n" +
            "Une sauvegarde de l'état actuel est créée automatiquement.\n" +
            "Un REDÉMARRAGE sera nécessaire pour appliquer les changements.\n\n" +
            "Continuer ?");
        if (!confirmed)
            return;

        await RunBusy("Écriture du BIOS (export → import → vérification)…", () =>
        {
            string runDir = _store.NewRunDir("apply");
            string before = Path.Combine(runDir, "before.txt");
            _runner.Export(before);
            var lines = File.ReadAllLines(before).ToList();
            var blocks = ScewinParser.ParseBlocks(lines);

            // Mutate only the lines that actually change; conform rules are a no-op on the file.
            var rows = PlanService.BuildPlan(blocks, lines,
                PlanService.Flatten(selected), PlanService.Flatten(unselected), mutate: true);
            var changedRows = rows.Where(r => r.WillChange).ToList();

            // No diff against the fresh export -> nothing to write, skip the import entirely.
            if (changedRows.Count == 0)
                return (changedRows, File.ReadAllLines(before), runDir, false);

            string target = Path.Combine(runDir, "target.txt");
            File.WriteAllLines(target, lines);

            string backup = Path.Combine(runDir, "restore_before_apply.txt");
            File.Copy(before, backup, overwrite: true);
            _store.RecordLastBackup(backup);

            _runner.Import(target);

            string after = Path.Combine(runDir, "after.txt");
            _runner.Export(after);
            var afterLines = File.ReadAllLines(after);
            VerifyService.Verify(afterLines, changedRows);

            RunStore.WritePlanCsv(Path.Combine(runDir, "plan.csv"), changedRows);

            return (changedRows, afterLines, runDir, true);
        },
        result =>
        {
            // Refresh diffs against the post-apply state.
            _currentLines = result.Item2;
            _currentBlocks = ScewinParser.ParseBlocks(result.Item2);
            IsImported = true;
            RefreshAllDiffs();

            if (!result.Item4)
            {
                _ui.Info("Rien à écrire",
                    "Le BIOS correspondait déjà à votre sélection au moment de l'écriture. Aucune modification effectuée.");
                StatusText = "Aucun changement à écrire — le BIOS correspond à la sélection.";
                return;
            }

            int changed = result.Item1.Count;
            int reverted = result.Item1.Count(r => r.IsRevert);
            int mismatch = result.Item1.Count(r => r.VerifyStatus == "mismatch");
            string what = reverted > 0
                ? $"{changed - reverted} activé(s), {reverted} remis par défaut"
                : $"{changed} activé(s)";
            StatusText = $"Écrit : {what} · {mismatch} non vérifié(s) · sauvegarde {Path.GetFileName(result.Item3)}.";

            _ui.ShowPlan("Résultat — changements écrits, vérifiez puis REDÉMARREZ", result.Item1, showVerify: true);

            if (mismatch == 0)
                _ui.Info("Terminé",
                    $"{changed} paramètre(s) écrit(s) et vérifié(s) ({what}).\n\n" +
                    "REDÉMARREZ pour les appliquer. Après reboot, validez la stabilité " +
                    "(Observateur d'événements → aucun WHEA-Logger ; y-cruncher / OCCT).");
            else
                _ui.Error("Vérification incomplète",
                    $"{mismatch} paramètre(s) ne sont pas revenus à la valeur attendue après écriture. " +
                    "Consultez le détail, et au besoin restaurez la sauvegarde.");
        });
    }

    private bool CanApply() => !IsBusy && IsImported && PendingChangeCount > 0;

    // ----------------------------------------------------------------- Restore

    [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
    private async Task RestoreAsync()
    {
        string? backup = _store.GetLastBackup() ?? _ui.PickBackupFile();
        if (string.IsNullOrEmpty(backup))
        {
            _ui.Info("Restaurer", "Aucune sauvegarde trouvée. Sélectionnez un fichier d'export à réimporter.");
            return;
        }

        bool confirmed = _ui.Confirm(
            "Restaurer le BIOS ?",
            $"Le fichier suivant va être réimporté dans le BIOS :\n\n{backup}\n\n" +
            "Un redémarrage sera nécessaire. Continuer ?");
        if (!confirmed)
            return;

        await RunBusy("Restauration du BIOS…", () =>
        {
            string runDir = _store.NewRunDir("restore");
            _runner.Import(backup);
            string after = Path.Combine(runDir, "after_restore.txt");
            _runner.Export(after);
            return File.ReadAllLines(after);
        },
        afterLines =>
        {
            _currentLines = afterLines;
            _currentBlocks = ScewinParser.ParseBlocks(afterLines);
            IsImported = true;
            RefreshAllDiffs();
            StatusText = "Sauvegarde restaurée. REDÉMARREZ pour appliquer.";
            _ui.Info("Restauré", "La sauvegarde a été réimportée. REDÉMARREZ pour appliquer.");
        });
    }

    // ----------------------------------------------------------------- Selection helpers

    [RelayCommand]
    private void SelectRecommended() => Batch(() =>
    {
        foreach (var tv in AllTweaks)
            tv.IsSelected = tv.Recommended;
    });

    /// <summary>Turns every tweak off — which schedules a reset to BIOS defaults, not a no-op.</summary>
    [RelayCommand]
    private void ClearSelection() => Batch(() =>
    {
        foreach (var tv in AllTweaks)
            tv.IsSelected = false;
    });

    /// <summary>Matches the switches to the BIOS as it is now: nothing left to write either way.</summary>
    [RelayCommand(CanExecute = nameof(CanMatchBios))]
    private void MatchBios() => Batch(() =>
    {
        foreach (var tv in AllTweaks)
            tv.IsSelected = tv.IsConform;
    });

    private bool CanMatchBios() => !IsBusy && IsImported;

    // ----------------------------------------------------------------- Busy plumbing

    private bool CanRunWhenIdle() => !IsBusy;

    private async Task RunBusy<T>(string message, Func<T> work, Action<T> onSuccess)
    {
        IsBusy = true;
        BusyMessage = message;
        NotifyCommands();
        try
        {
            T result = await Task.Run(work);
            onSuccess(result);
        }
        catch (Exception ex)
        {
            _ui.Error("Erreur SCEWIN", ex.Message);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = "";
            NotifyCommands();
        }
    }

    private void NotifyCommands()
    {
        ImportCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
        PreviewCommand.NotifyCanExecuteChanged();
        RestoreCommand.NotifyCanExecuteChanged();
        MatchBiosCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value) => NotifyCommands();

    partial void OnIsImportedChanged(bool value)
    {
        NotifyCommands();
        OnPropertyChanged(nameof(PendingSummary));
        OnPropertyChanged(nameof(IsNotImported));
    }
}
