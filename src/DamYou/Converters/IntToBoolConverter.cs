using System.Globalization;

namespace DamYou.Converters;

public sealed class IntToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Handle count-based conversions (int values)
        if (value is int i)
        {
            var invert = parameter as string == "invert";
            return invert ? i == 0 : i > 0;
        }

        // Handle null/non-null checks (for properties)
        if (value == null)
            return false;

        // For lists or collections, check Count property
        if (value is System.Collections.ICollection collection)
        {
            var invert = parameter as string == "invert";
            var hasItems = collection.Count > 0;
            return invert ? !hasItems : hasItems;
        }

        // For any other object type, it's considered "present"
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
