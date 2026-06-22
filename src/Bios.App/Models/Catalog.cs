namespace Bios.App.Models;

/// <summary>Root of the tweak catalog (embedded catalog.json, optionally overridden next to the exe).</summary>
public sealed class Catalog
{
    public int SchemaVersion { get; set; } = 1;
    public string Hardware { get; set; } = "";
    public List<Category> Categories { get; set; } = new();
}

public sealed class Category
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Risk { get; set; } = "Low";
    public List<Tweak> Tweaks { get; set; } = new();
}

/// <summary>A single user-facing toggle. Maps to one or more SCEWIN rules.</summary>
public sealed class Tweak
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Risk { get; set; } = "Low";
    public bool Recommended { get; set; }
    public bool Verified { get; set; }

    /// <summary>Tweaks sharing a non-null group are mutually exclusive (e.g. FCLK steps).</summary>
    public string? ExclusiveGroup { get; set; }

    public List<Rule> Rules { get; set; } = new();
}

/// <summary>One concrete change in the SCEWIN export, matched by question (+ optional token/offset/occurrence).</summary>
public sealed class Rule
{
    public string Question { get; set; } = "";
    public string? Token { get; set; }
    public string? Offset { get; set; }
    public int? Occurrence { get; set; }
    public string? Code { get; set; }
    public string? Value { get; set; }
    public string Reason { get; set; } = "";
}
