using System.Windows.Media;

namespace Bios.App.Services;

/// <summary>
/// Colours for the two things a row has to say at a glance: where the BIOS stands for a tweak,
/// and which direction its pending writes go. Deliberately separate from <see cref="RiskPalette"/>,
/// which colours the danger of a tweak rather than its state.
/// </summary>
public static class StatePalette
{
    private static readonly Brush Active = Freeze(0x4C, 0xAF, 0x6A);   // green — the BIOS holds it
    private static readonly Brush Partial = Freeze(0xE0, 0x8A, 0x1E);  // amber — some settings only
    private static readonly Brush Inactive = Freeze(0x6B, 0x70, 0x78); // grey  — not applied
    private static readonly Brush Unknown = Freeze(0x4A, 0x4E, 0x55);  // dim   — nothing imported yet
    private static readonly Brush Apply = Freeze(0x4C, 0xA0, 0xE8);    // blue  — writes that activate
    private static readonly Brush Revert = Freeze(0xE0, 0x8A, 0x1E);   // amber — writes that reset

    private static Brush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public static Brush State(bool hasImport, bool unavailable, int activeCount, int applicableCount)
    {
        if (!hasImport || unavailable) return Unknown;
        if (activeCount == 0) return Inactive;
        return activeCount == applicableCount ? Active : Partial;
    }

    public static Brush Pending(bool isRevert) => isRevert ? Revert : Apply;
}
