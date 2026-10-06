using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace BotBBVA.Panel.Converters;

/// <summary>
/// Convierte un valor enum en un pincel del tema, concatenando el prefijo
/// indicado en ConverterParameter con el nombre del valor (por ejemplo,
/// "Brocha.Nivel." + "Exito" = "Brocha.Nivel.Exito").
/// </summary>
public sealed class EnumToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Enum enumerado || parameter is not string prefijo)
        {
            return Brushes.Transparent;
        }

        return Application.Current.TryFindResource(prefijo + enumerado) as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
