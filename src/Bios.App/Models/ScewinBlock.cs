namespace Bios.App.Models;

/// <summary>A parsed "Setup Question" block from a SCEWIN export. Port of the Python Block dataclass.</summary>
public sealed class ScewinBlock
{
    public int Index;
    public int Start;
    public int End;
    public string Question = "";
    public string Token = "";
    public string Offset = "";
    public string Width = "";
    public string SelectedCode = "";
    public string SelectedLabel = "";
    public string Value = "";
    public HashSet<string> OptionCodes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Label of each option code, for human-readable plan rows.</summary>
    public Dictionary<string, string> OptionLabels = new(StringComparer.OrdinalIgnoreCase);

    // --- Factory default, as exported by SCEWIN on the "BIOS Default" line. ---
    // Present on most blocks but NOT all: some AMD Overclocking entries (Curve Optimizer,
    // PBO Scalar, boost override…) omit it entirely, which is why a rule can carry its own.

    /// <summary>Option code from "BIOS Default =[XX]Label", empty when the line is absent or not an option.</summary>
    public string DefaultCode = "";

    /// <summary>Raw payload from "BIOS Default =…" for value-type blocks (e.g. "&lt;0&gt;"), empty when absent.</summary>
    public string DefaultValue = "";

    /// <summary>Code of the option literally labelled "Auto", used as a fallback default. Empty when none.</summary>
    public string AutoCode = "";

    /// <summary>Human-readable current state: "[code]label" for options, raw value otherwise.</summary>
    public string Selected =>
        (SelectedCode.Length > 0 || SelectedLabel.Length > 0)
            ? $"[{SelectedCode}]{SelectedLabel}".Trim()
            : Value;

    public bool IsOption => OptionCodes.Count > 0;

    /// <summary>"[code]label" for an option code, or "[code]" when the label is unknown.</summary>
    public string Describe(string code) =>
        OptionLabels.TryGetValue(code, out var label) && label.Length > 0
            ? $"[{code}]{label}"
            : $"[{code}]";
}
