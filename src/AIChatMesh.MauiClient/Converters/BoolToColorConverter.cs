using System.Globalization;

namespace AIChatMesh.MauiClient.Converters;

public sealed class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
            return Colors.Gray;

        return Application.Current?.RequestedTheme == AppTheme.Dark
            ? Colors.White
            : Colors.Black;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
