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

    public void RaiseSelectedCount()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }
}
