namespace Bios.App.Models;

/// <summary>One computed change (or skip) for the plan/diff and verification views. Properties so WPF can bind.</summary>
public sealed class PlanRow
{
    public string TweakId { get; set; } = "";
    public string TweakName { get; set; } = "";
    public string Question { get; set; } = "";
    public string Token { get; set; } = "";
    public string Offset { get; set; } = "";
    public int Line { get; set; }
    public string Old { get; set; } = "";
    public string Target { get; set; } = "";

    /// <summary>applied | unchanged | skipped</summary>
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
    public string Reason { get; set; } = "";

    /// <summary>True when the target differs from the current value.</summary>
    public bool WillChange { get; set; }

    // Verification (filled by VerifyService after re-export).
    public string Actual { get; set; } = "";
    public string VerifyStatus { get; set; } = ""; // ok | mismatch | (empty)

    // Display helpers for the grid.
    public string Menu => string.IsNullOrEmpty(Token) ? "(tous menus)" : $"Token {Token} / Off {Offset}";
}
