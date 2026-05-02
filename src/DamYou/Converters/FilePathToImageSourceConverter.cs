using System.Globalization;

namespace DamYou.Converters;

public sealed class FilePathToImageSourceConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string filePath && !string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            return ImageSource.FromFile(filePath);
        }
        return null!;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
