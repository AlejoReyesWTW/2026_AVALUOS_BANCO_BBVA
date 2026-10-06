using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BotBBVA.Panel.Converters;

/// <summary>
/// Convierte un booleano en Visibility. Con ConverterParameter="Invertir"
/// invierte el resultado (útil para mostrar botones alternativos).
/// </summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool activo = value is true;
        bool invertir = string.Equals(parameter?.ToString(), "Invertir", StringComparison.OrdinalIgnoreCase);
        bool mostrar = invertir ? !activo : activo;

        return mostrar ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
