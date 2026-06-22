using System.IO;
using System.Reflection;
using System.Text.Json;
using Bios.App.Models;

namespace Bios.App.Services;

/// <summary>Loads the tweak catalog. Prefers a catalog.json next to the exe (user-editable), falls back to embedded.</summary>
public static class CatalogLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static Catalog Load()
    {
        string json = LoadJson();
        var catalog = JsonSerializer.Deserialize<Catalog>(json, Options)
            ?? throw new InvalidOperationException("Catalog deserialization returned null.");
        return catalog;
    }

    private static string LoadJson()
    {
        string exeDir = AppContext.BaseDirectory;
        string overridePath = Path.Combine(exeDir, "catalog.json");
        if (File.Exists(overridePath))
            return File.ReadAllText(overridePath);

        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("Bios.App.catalog.json")
            ?? throw new InvalidOperationException("Embedded catalog.json not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
