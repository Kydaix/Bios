using System.Windows.Media;
using Bios.App.Models;
using Bios.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bios.App.ViewModels;

public partial class TweakViewModel : ObservableObject
{
    public Tweak Model { get; }

    public TweakViewModel(Tweak model)
    {
        Model = model;
    }

    public string Name => Model.Name;
    public string Description => Model.Description;
    public string Risk => Model.Risk;
    public string RiskLabel => RiskPalette.Label(Model.Risk);
    public Brush RiskBrush => RiskPalette.Brush(Model.Risk);
    public bool Recommended => Model.Recommended;
    public bool Verified => Model.Verified;
    public string? ExclusiveGroup => Model.ExclusiveGroup;
    public int RuleCount => Model.Rules.Count;

    [ObservableProperty]
    private bool _isSelected;

    // --- Diff state, refreshed after an import ---

    [ObservableProperty]
    private bool _hasImport;

    /// <summary>Settings this tweak would still have to write if it stays on.</summary>
    [ObservableProperty]
    private int _changeCount;

    /// <summary>Settings currently held by this tweak that would go back to their BIOS default if it goes off.</summary>
    [ObservableProperty]
    private int _revertCount;

    /// <summary>Settings currently held by this tweak that the BIOS gives no default for.</summary>
    [ObservableProperty]
    private int _lockedCount;

    /// <summary>Rules this BIOS does not expose at all.</summary>
    [ObservableProperty]
    private int _missingCount;

    /// <summary>Rules that exist on this BIOS and can therefore be written.</summary>
    [ObservableProperty]
    private int _applicableCount;

    /// <summary>Rules whose value the BIOS currently holds.</summary>
    [ObservableProperty]
    private int _activeCount;

    /// <summary>Plan rows for turning this tweak on, against the current export (no mutation).</summary>
    public IReadOnlyList<PlanRow> ApplyDiff { get; private set; } = Array.Empty<PlanRow>();

    /// <summary>Plan rows for turning this tweak off — restoring BIOS defaults.</summary>
    public IReadOnlyList<PlanRow> RevertDiff { get; private set; } = Array.Empty<PlanRow>();

    /// <summary>Per-setting detail shown when the card is expanded.</summary>
    public IReadOnlyList<SettingRow> Settings { get; private set; } = Array.Empty<SettingRow>();

    /// <summary>
    /// True when the tweak has at least one applicable rule and none of them require a change:
    /// the BIOS already matches this tweak's target. Such tweaks are auto-checked after an import.
    /// </summary>
    public bool IsConform => HasImport && ApplicableCount > 0 && ChangeCount == 0;

    /// <summary>
    /// This tweak's share of the real plan. Comes from the whole-selection plan rather than from
    /// this tweak alone: a setting another, enabled tweak owns must not be counted here twice.
    /// </summary>
    public IReadOnlyList<PlanRow> PendingRows { get; private set; } = Array.Empty<PlanRow>();

    [ObservableProperty]
    private int _pendingCount;

    /// <summary>Settings in the plan this BIOS cannot reset, so they will stay as they are.</summary>
    [ObservableProperty]
    private int _pendingLockedCount;

    public bool HasPending => PendingCount > 0;

    /// <summary>Called by the main view-model once the plan for the whole selection is known.</summary>
    public void SetPending(IReadOnlyList<PlanRow> rows)
    {
        PendingRows = rows;
        PendingLockedCount = rows.Count(r => r.IsRevert && r.Status == "skipped");
        PendingCount = rows.Count(r => r.WillChange);
        OnPropertyChanged(nameof(PendingSummary));
    }

    /// <summary>True when the pending writes restore defaults rather than activate the tweak.</summary>
    public bool PendingIsRevert => !IsSelected;

    partial void OnPendingCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasPending));
        OnPropertyChanged(nameof(PendingBrush));
    }

    /// <summary>Colour of the state dot: where the BIOS stands for this tweak right now.</summary>
    public Brush StateBrush => StatePalette.State(HasImport, IsUnavailable, ActiveCount, ApplicableCount);

    /// <summary>Colour of the pending-writes badge: accent to activate, amber to reset.</summary>
    public Brush PendingBrush => StatePalette.Pending(PendingIsRevert);

    /// <summary>Where the BIOS stands today, independent of the switch position.</summary>
    public string StateLabel
    {
        get
        {
            if (!HasImport) return "";
            if (ApplicableCount == 0) return "Indisponible sur ce BIOS";
            if (ActiveCount == 0) return "Inactif";
            return ActiveCount == ApplicableCount ? "Actif" : $"Partiel — {ActiveCount}/{ApplicableCount}";
        }
    }

    public bool IsActive => HasImport && ActiveCount > 0;
    public bool IsFullyActive => HasImport && ApplicableCount > 0 && ActiveCount == ApplicableCount;
    public bool IsUnavailable => HasImport && ApplicableCount == 0;

    /// <summary>What pressing Apply would do to this tweak, in one line.</summary>
    public string PendingSummary
    {
        get
        {
            if (!HasImport) return "";
            if (IsUnavailable) return $"Aucun des {RuleCount} paramètres n'existe sur ce BIOS.";

            string missing = MissingCount > 0 ? $" · {MissingCount} absent(s) de ce BIOS" : "";

            if (IsSelected)
            {
                return PendingCount > 0
                    ? $"{PendingCount} paramètre(s) à écrire{missing}"
                    : $"Déjà actif — rien à écrire{missing}";
            }

            if (PendingCount > 0)
                return $"{PendingCount} paramètre(s) à remettre par défaut"
                       + (PendingLockedCount > 0 ? $" · {PendingLockedCount} sans valeur par défaut connue" : "");
            if (PendingLockedCount > 0)
                return $"{PendingLockedCount} paramètre(s) actifs que ce BIOS ne sait pas réinitialiser";
            return IsActive ? "Rien à écrire — déjà au niveau par défaut" : "Inactif — rien à écrire";
        }
    }

    public void UpdateDiff(IReadOnlyList<PlanRow> applyRows, IReadOnlyList<PlanRow> revertRows)
    {
        ApplyDiff = applyRows;
        RevertDiff = revertRows;
        HasImport = true;

        ChangeCount = applyRows.Count(r => r.WillChange);
        MissingCount = applyRows.Count(r => r.Status == "skipped");
        ApplicableCount = applyRows.Count(r => r.Status != "skipped");
        ActiveCount = applyRows.Count(r => r.Status == "unchanged");

        RevertCount = revertRows.Count(r => r.WillChange);
        LockedCount = revertRows.Count(r => r.Status == "skipped");

        Settings = BuildSettings(applyRows, revertRows);
        RaiseDerived();
    }

    public void ClearDiff()
    {
        ApplyDiff = Array.Empty<PlanRow>();
        RevertDiff = Array.Empty<PlanRow>();
        Settings = Array.Empty<SettingRow>();
        HasImport = false;
        ChangeCount = 0;
        RevertCount = 0;
        LockedCount = 0;
        MissingCount = 0;
        ApplicableCount = 0;
        ActiveCount = 0;
        RaiseDerived();
    }

    private IReadOnlyList<SettingRow> BuildSettings(IReadOnlyList<PlanRow> applyRows, IReadOnlyList<PlanRow> revertRows)
    {
        var defaults = revertRows
            .GroupBy(r => (r.Question, r.Token, r.Offset))
            .ToDictionary(g => g.Key, g => g.First());

        return applyRows.Select(r =>
        {
            defaults.TryGetValue((r.Question, r.Token, r.Offset), out var rev);
            return new SettingRow
            {
                Question = r.Question,
                Menu = r.Menu,
                Current = r.Status == "skipped" ? "—" : r.Old,
                Target = r.Target,
                Default = rev is null ? "" : (rev.Status == "skipped" ? "inconnue" : rev.Target),
                Note = r.Status == "skipped" ? r.Message : r.Reason,
                IsMissing = r.Status == "skipped",
                IsActive = r.Status == "unchanged",
            };
        }).ToList();
    }

    public bool HasSettings => Settings.Count > 0;

    private void RaiseDerived()
    {
        OnPropertyChanged(nameof(IsConform));
        OnPropertyChanged(nameof(HasPending));
        OnPropertyChanged(nameof(PendingIsRevert));
        OnPropertyChanged(nameof(PendingSummary));
        OnPropertyChanged(nameof(PendingBrush));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(StateBrush));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsFullyActive));
        OnPropertyChanged(nameof(IsUnavailable));
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(HasSettings));
    }

    partial void OnIsSelectedChanged(bool value) => RaiseDerived();
}

/// <summary>One BIOS setting of a tweak, as shown in the expanded card.</summary>
public sealed class SettingRow
{
    public string Question { get; init; } = "";
    public string Menu { get; init; } = "";
    public string Current { get; init; } = "";
    public string Target { get; init; } = "";
    public string Default { get; init; } = "";
    public string Note { get; init; } = "";
    public bool IsMissing { get; init; }
    public bool IsActive { get; init; }
}
