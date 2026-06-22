using System.Windows;
using Bios.App.Models;
using Bios.App.Views;
using Microsoft.Win32;

namespace Bios.App.Services;

/// <summary>WPF implementation of <see cref="IUiService"/>; owns all window/dialog interaction.</summary>
public sealed class UiService : IUiService
{
    private readonly Window _owner;

    public UiService(Window owner) => _owner = owner;

    public bool Confirm(string title, string message)
        => MessageBox.Show(_owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void Info(string title, string message)
        => MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void Error(string title, string message)
        => MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowPlan(string title, IReadOnlyList<PlanRow> rows, bool showVerify)
    {
        var window = new PlanWindow(title, rows, showVerify) { Owner = _owner };
        window.ShowDialog();
    }

    public string? PickBackupFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Choisir un export SCEWIN à restaurer",
            Filter = "Export SCEWIN (*.txt)|*.txt|Tous les fichiers (*.*)|*.*",
        };
        return dlg.ShowDialog(_owner) == true ? dlg.FileName : null;
    }
}
