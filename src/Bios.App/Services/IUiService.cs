using Bios.App.Models;

namespace Bios.App.Services;

/// <summary>Abstraction over dialogs so the ViewModel stays free of WPF window code.</summary>
public interface IUiService
{
    bool Confirm(string title, string message);
    void Info(string title, string message);
    void Error(string title, string message);
    void ShowPlan(string title, IReadOnlyList<PlanRow> rows, bool showVerify);
    string? PickBackupFile();
}
