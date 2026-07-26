using System.Collections.ObjectModel;
using System.Windows.Media;
using Bios.App.Models;
using Bios.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bios.App.ViewModels;

public partial class CategoryViewModel : ObservableObject
{
    public Category Model { get; }

    public CategoryViewModel(Category model, IEnumerable<TweakViewModel> tweaks)
    {
        Model = model;
        Tweaks = new ObservableCollection<TweakViewModel>(tweaks);
    }

    public string Id => Model.Id;
    public string Name => Model.Name;
    public string Description => Model.Description;
    public string Risk => Model.Risk;
    public string RiskLabel => RiskPalette.Label(Model.Risk);
    public Brush RiskBrush => RiskPalette.Brush(Model.Risk);

    public ObservableCollection<TweakViewModel> Tweaks { get; }

    public int SelectedCount => Tweaks.Count(t => t.IsSelected);
    public bool HasSelection => SelectedCount > 0;

    /// <summary>Writes this category contributes to the plan — the number worth acting on.</summary>
    public int PendingCount => Tweaks.Sum(t => t.PendingCount);
    public bool HasPending => PendingCount > 0;

    /// <summary>Amber when the pending writes only reset settings, accent when any activates one.</summary>
    public Brush PendingBrush => StatePalette.Pending(!Tweaks.Any(t => t.PendingCount > 0 && t.IsSelected));

    public string CountLabel => $"{SelectedCount}/{Tweaks.Count} activés";

    /// <summary>Counts only mean something once the BIOS has been read.</summary>
    public bool HasImport => Tweaks.Any(t => t.HasImport);

    public void RaiseSelectedCount()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasImport));
        OnPropertyChanged(nameof(CountLabel));
        OnPropertyChanged(nameof(PendingCount));
        OnPropertyChanged(nameof(HasPending));
        OnPropertyChanged(nameof(PendingBrush));
    }
}
