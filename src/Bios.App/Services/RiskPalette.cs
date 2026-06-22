using System.Windows.Media;

namespace Bios.App.Services;

/// <summary>Maps a risk label to a colour + friendly French text for the UI badges.</summary>
public static class RiskPalette
{
    public static Brush Brush(string risk) => new SolidColorBrush(Color(risk));

    public static Color Color(string risk) => risk switch
    {
        "Safe" => System.Windows.Media.Color.FromRgb(0x3F, 0xB9, 0x50),       // green
        "Low" => System.Windows.Media.Color.FromRgb(0x2E, 0x9B, 0xD6),        // blue
        "Medium" => System.Windows.Media.Color.FromRgb(0xE0, 0x8A, 0x1E),     // orange
        "High" => System.Windows.Media.Color.FromRgb(0xD9, 0x3A, 0x3A),       // red
        "Experimental" => System.Windows.Media.Color.FromRgb(0x9A, 0x55, 0xD4), // purple
        _ => System.Windows.Media.Color.FromRgb(0x80, 0x80, 0x80),
    };

    public static string Label(string risk) => risk switch
    {
        "Safe" => "Sûr",
        "Low" => "Faible",
        "Medium" => "Moyen",
        "High" => "Élevé",
        "Experimental" => "Expérimental",
        _ => risk,
    };
}
