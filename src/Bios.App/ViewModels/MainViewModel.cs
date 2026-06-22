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
    [ObservableProperty] private string _statusText = "BIOS non importé. Cliquez sur « Importer le BIOS actuel ».";
    [ObservableProperty] private string _hardwareText = "";

    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    [ObservableProperty] private int _selectedChangeCount;

    public string AppTitle => "BIOS Tuner";

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

        RecomputeSelectionCount();
        foreach (var c in Categories) c.RaiseSelectedCount();
    }

    private void RecomputeSelectionCount()
    {
        int total = 0;
        foreach (var tv in AllTweaks.Where(t => t.IsSelected))
            total += tv.HasImport ? tv.ChangeCount : tv.RuleCount;
        SelectedChangeCount = total;
    }

    private IEnumerable<TweakViewModel> SelectedInCatalogOrder => AllTweaks.Where(t => t.IsSelected);

    private IEnumerable<(Tweak, Rule)> SelectedRuleset()
        => PlanService.Flatten(SelectedInCatalogOrder.Select(t => t.Model));

    // ----------------------------------------------------------------- Import

    [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
    private async Task ImportAsync()
    {
        await RunBusy("Export du BIOS en cours (SCEWIN)…", () =>
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
            StatusText = $"BIOS importé le {DateTime.Now:dd/MM/yyyy HH:mm} — {_currentBlocks.Count} paramètres lus.";
        });
    }

    private void RefreshAllDiffs()
    {
        foreach (var tv in AllTweaks)
        {
            var clone = (string[])_currentLines.Clone();
            var rows = PlanService.BuildPlan(_currentBlocks, clone, PlanService.Flatten(new[] { tv.Model }), mutate: false);
            tv.UpdateDiff(rows);
        }
        RecomputeSelectionCount();
    }

    // ----------------------------------------------------------------- Preview

    [RelayCommand(CanExecute = nameof(CanPreview))]
    private void Preview()
    {
        if (!IsImported)
        {
            _ui.Info("Aperçu", "Importez d'abord le BIOS actuel.");
            return;
        }
        var clone = (string[])_currentLines.Clone();
        var rows = PlanService.BuildPlan(_currentBlocks, clone, SelectedRuleset(), mutate: false);
        _ui.ShowPlan("Aperçu du plan — aucun changement écrit", rows, showVerify: false);
    }

    private bool CanPreview() => !IsBusy && IsImported && SelectedInCatalogOrder.Any();

    // ----------------------------------------------------------------- Apply

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        var selected = SelectedInCatalogOrder.ToList();
        if (selected.Count == 0)
        {
            _ui.Info("Appliquer", "Aucun réglage sélectionné.");
            return;
        }

        // Always work from a FRESH export, then preview before writing.
        bool confirmed = _ui.Confirm(
            "Appliquer au BIOS ?",
            $"{selected.Count} réglage(s) sélectionné(s) vont être écrits dans les variables UEFI via SCEWIN.\n\n" +
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

            var rows = PlanService.BuildPlan(blocks, lines, PlanService.Flatten(selected.Select(t => t.Model)), mutate: true);

            string target = Path.Combine(runDir, "target.txt");
            File.WriteAllLines(target, lines);

            string backup = Path.Combine(runDir, "restore_before_apply.txt");
            File.Copy(before, backup, overwrite: true);
            _store.RecordLastBackup(backup);

            _runner.Import(target);

            string after = Path.Combine(runDir, "after.txt");
            _runner.Export(after);
            var afterLines = File.ReadAllLines(after);
            VerifyService.Verify(afterLines, rows);

            RunStore.WritePlanCsv(Path.Combine(runDir, "plan.csv"), rows);

            return (rows, afterLines, runDir);
        },
        result =>
        {
            // Refresh diffs against the post-apply state.
            _currentLines = result.afterLines;
            _currentBlocks = ScewinParser.ParseBlocks(result.afterLines);
            IsImported = true;
            RefreshAllDiffs();

            int changed = result.rows.Count(r => r.Status == "applied");
            int mismatch = result.rows.Count(r => r.VerifyStatus == "mismatch");
            StatusText = $"Appliqué : {changed} modifié(s), {mismatch} non vérifié(s). Sauvegarde : {Path.GetFileName(result.runDir)}.";

            _ui.ShowPlan("Résultat — vérifiez puis REDÉMARREZ", result.rows, showVerify: true);

            if (mismatch == 0)
                _ui.Info("Terminé",
                    "Tous les changements ont été écrits et vérifiés.\n\n" +
                    "REDÉMARREZ pour les appliquer. Après reboot, validez la stabilité " +
                    "(Observateur d'événements → aucun WHEA-Logger ; y-cruncher / OCCT).");
            else
                _ui.Error("Attention",
                    $"{mismatch} paramètre(s) n'ont pas pu être vérifiés après écriture. " +
                    "Consultez le détail, et au besoin restaurez la sauvegarde.");
        });
    }

    private bool CanApply() => !IsBusy && IsImported && SelectedInCatalogOrder.Any();

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
    private void SelectRecommended()
    {
        foreach (var tv in AllTweaks)
            tv.IsSelected = tv.Recommended;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var tv in AllTweaks)
            tv.IsSelected = false;
    }

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
    }

    partial void OnIsBusyChanged(bool value) => NotifyCommands();
    partial void OnIsImportedChanged(bool value) => NotifyCommands();
}
