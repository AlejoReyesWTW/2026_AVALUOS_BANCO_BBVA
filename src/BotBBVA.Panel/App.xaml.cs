using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace BotBBVA.Panel;

/// <summary>
/// Punto de entrada del panel de control.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Acento BBVA Aqua sobre el tema oscuro Fluent (definido en App.xaml).
        ApplicationAccentColorManager.Apply(
            Color.FromRgb(45, 204, 205),
            ApplicationTheme.Dark,
            systemGlassColor: false,
            systemAccentColor: false);
    }
}
