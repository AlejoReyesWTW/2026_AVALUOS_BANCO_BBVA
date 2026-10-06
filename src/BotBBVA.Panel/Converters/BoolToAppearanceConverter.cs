using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace BotBBVA.Panel.Converters;

/// <summary>
/// Convierte un booleano en una apariencia de control: Secondary cuando es
/// verdadero (elemento seleccionado) y Transparent en caso contrario.
/// </summary>
public sealed class BoolToAppearanceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? ControlAppearance.Secondary : ControlAppearance.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
