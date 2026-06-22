using System.Windows;
using Bios.App.Services;
using Bios.App.ViewModels;
using Wpf.Ui.Controls;

namespace Bios.App.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        try
        {
            var ui = new UiService(this);
            DataContext = new MainViewModel(ui);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "Impossible d'initialiser l'application :\n\n" + ex.Message,
                "Erreur de démarrage",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }
}
