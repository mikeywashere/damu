using System.Globalization;

namespace DamYou.Converters;

/// <summary>
/// Truncates long strings (e.g. file paths) to a fixed display length,
/// preserving the start and end with "..." in the middle.
/// Full value is available via ToolTipProperties.Text for on-hover display.
/// </summary>
public sealed class TruncateMiddleConverter : IValueConverter
{
    private const int MaxLength = 50;
    private const int StartChars = 25;
    private const int EndChars = 20;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string text = value?.ToString() ?? string.Empty;
        if (text.Length <= MaxLength)
            return text;

        return text.Substring(0, StartChars) + "..." + text.Substring(text.Length - EndChars);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
