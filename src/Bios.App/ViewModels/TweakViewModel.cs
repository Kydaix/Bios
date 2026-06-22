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

    [ObservableProperty]
    private int _changeCount;

    [ObservableProperty]
    private int _applicableCount;

    [ObservableProperty]
    private string _diffSummary = "";

    /// <summary>Plan rows computed for this tweak against the current export (no mutation).</summary>
    public IReadOnlyList<PlanRow> Diff { get; private set; } = Array.Empty<PlanRow>();

    public void UpdateDiff(IReadOnlyList<PlanRow> rows)
    {
        Diff = rows;
        HasImport = true;
        ChangeCount = rows.Count(r => r.WillChange);
        ApplicableCount = rows.Count(r => r.Status != "skipped");

        int missing = rows.Count(r => r.Status == "skipped");
        if (ChangeCount == 0 && missing == 0)
            DiffSummary = "Déjà conforme — aucun changement";
        else if (ChangeCount == 0 && missing > 0)
            DiffSummary = $"Déjà conforme ({missing} option(s) absente(s) de ce BIOS)";
        else
            DiffSummary = $"{ChangeCount} paramètre(s) seront modifiés"
                          + (missing > 0 ? $", {missing} absent(s)" : "");
    }

    public void ClearDiff()
    {
        Diff = Array.Empty<PlanRow>();
        HasImport = false;
        ChangeCount = 0;
        ApplicableCount = 0;
        DiffSummary = "";
    }
}
