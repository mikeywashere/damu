using System.Globalization;
using System.Text.Json;

namespace DamYou.Converters;

/// <summary>
/// Converts a JSON array of hex color strings into a comma-separated display string.
/// Example: ["#FF5733","#C70039"] → "#FF5733, #C70039"
/// </summary>
public sealed class ColorPaletteJsonToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string json || string.IsNullOrWhiteSpace(json))
            return "No colors detected";

        try
        {
            using var doc = JsonDocument.Parse(json);
            var colors = doc.RootElement
                .EnumerateArray()
                .Select(e => e.GetString())
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList();

            return colors.Count == 0
                ? "No colors detected"
                : string.Join(", ", colors);
        }
        catch
        {
            return "Invalid color data";
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
