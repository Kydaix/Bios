using System.Windows;
using Bios.App.Models;
using Wpf.Ui.Controls;

namespace Bios.App.Views;

public partial class PlanWindow : FluentWindow
{
    public PlanWindow(string title, IReadOnlyList<PlanRow> rows, bool showVerify)
    {
        InitializeComponent();

        Title = title;
        TitleBarControl.Title = title;
        Grid.ItemsSource = rows;

        int changes = rows.Count(r => r.WillChange);
        int skipped = rows.Count(r => r.Status == "skipped");

        if (showVerify)
        {
            int ok = rows.Count(r => r.VerifyStatus == "ok");
            int mismatch = rows.Count(r => r.VerifyStatus == "mismatch");
            HeaderText.Text =
                $"{changes} paramètre(s) modifié(s) · {ok} vérifié(s) OK · {mismatch} non vérifié(s) · {skipped} absent(s).";
            SummaryText.Text = mismatch == 0 ? "Tout est conforme — REDÉMARREZ." : "Des écarts subsistent.";
        }
        else
        {
            VerifyColumn.Visibility = Visibility.Collapsed;
            HeaderText.Text =
                $"{changes} paramètre(s) seront modifiés · {skipped} absent(s) de ce BIOS. Aucune écriture n'est faite ici.";
            SummaryText.Text = $"{changes} changement(s)";
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
