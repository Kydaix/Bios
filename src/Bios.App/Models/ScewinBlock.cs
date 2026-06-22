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

    /// <summary>Human-readable current state: "[code]label" for options, raw value otherwise.</summary>
    public string Selected =>
        (SelectedCode.Length > 0 || SelectedLabel.Length > 0)
            ? $"[{SelectedCode}]{SelectedLabel}".Trim()
            : Value;

    public bool IsOption => OptionCodes.Count > 0;
}
