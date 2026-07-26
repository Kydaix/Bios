using System.Globalization;
using System.Windows.Data;

namespace Bios.App.Services;

/// <summary>Colours a plan row by direction: accent when it activates a tweak, amber when it resets one.</summary>
public sealed class DirectionToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => StatePalette.Pending(value is true);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
