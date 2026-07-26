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

    /// <summary>
    /// True when this row undoes a tweak the user turned off: the target is the BIOS default
    /// rather than the tweak's own value.
    /// </summary>
    public bool IsRevert { get; set; }

    /// <summary>Where the default came from, for revert rows only ("BIOS", "Auto", "catalogue").</summary>
    public string DefaultSource { get; set; } = "";

    // Verification (filled by VerifyService after re-export).
    public string Actual { get; set; } = "";
    public string VerifyStatus { get; set; } = ""; // ok | mismatch | (empty)

    // Display helpers for the grid.
    public string Menu => string.IsNullOrEmpty(Token) ? "(tous menus)" : $"Token {Token} / Off {Offset}";

    /// <summary>Direction of the row, shown as its own column so the two senses never blur together.</summary>
    public string Action => IsRevert ? "Rétablir le défaut" : "Activer";

    /// <summary>Plain-French outcome for the plan grid.</summary>
    public string Outcome => Status switch
    {
        "skipped" => Message,
        "unchanged" => "Déjà en place",
        _ => IsRevert ? "Sera rétabli" : "Sera écrit",
    };

    /// <summary>Post-write verification, in words rather than a status code.</summary>
    public string VerifyLabel => VerifyStatus switch
    {
        "ok" => "Confirmé",
        "mismatch" => "Valeur inattendue",
        _ => "",
    };
}
