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

        // Rows that change something first — the rest is context, not the decision.
        Grid.ItemsSource = rows
            .OrderByDescending(r => r.WillChange)
            .ThenBy(r => r.IsRevert)
            .ThenBy(r => r.TweakName, StringComparer.CurrentCulture)
            .ToList();

        int applied = rows.Count(r => r.WillChange && !r.IsRevert);
        int reverted = rows.Count(r => r.WillChange && r.IsRevert);
        int locked = rows.Count(r => r.IsRevert && r.Status == "skipped");
        int missing = rows.Count(r => !r.IsRevert && r.Status == "skipped");

        if (showVerify)
        {
            int ok = rows.Count(r => r.VerifyStatus == "ok");
            int mismatch = rows.Count(r => r.VerifyStatus == "mismatch");

            HeaderText.Text = mismatch == 0
                ? $"{applied + reverted} paramètre(s) écrit(s), tous relus et confirmés dans le BIOS."
                : $"{applied + reverted} paramètre(s) écrit(s), dont {mismatch} qui ne sont pas revenus à la valeur attendue.";

            SubHeaderText.Text =
                $"{applied} activé(s) · {reverted} remis à leur valeur par défaut · {ok} confirmé(s) à la relecture."
                + " Les changements ne prennent effet qu'après un REDÉMARRAGE.";

            SummaryText.Text = mismatch == 0
                ? "Redémarrez, puis vérifiez la stabilité (Observateur d'événements, y-cruncher / OCCT)."
                : "Consultez les lignes en rouge ; au besoin, restaurez la sauvegarde depuis la fenêtre principale.";
        }
        else
        {
            VerifyColumn.Visibility = Visibility.Collapsed;

            int total = applied + reverted;
            HeaderText.Text = total == 0
                ? "Aucun changement : le BIOS correspond déjà à votre sélection."
                : $"{total} paramètre(s) seraient écrits — {applied} activé(s), {reverted} remis à leur valeur par défaut.";

            var notes = new List<string>();
            if (missing > 0) notes.Add($"{missing} paramètre(s) n'existent pas sur ce BIOS et sont ignorés");
            if (locked > 0) notes.Add($"{locked} paramètre(s) désactivés resteront en place, faute de valeur par défaut connue");
            SubHeaderText.Text = notes.Count > 0
                ? string.Join(" · ", notes) + ". Aucune écriture n'est faite depuis cette fenêtre."
                : "Aucune écriture n'est faite depuis cette fenêtre.";

            SummaryText.Text = total == 0
                ? ""
                : "Fermez, puis utilisez « Appliquer » pour écrire ces changements.";
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
