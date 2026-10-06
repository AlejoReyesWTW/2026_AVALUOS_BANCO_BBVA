using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Controls;
using BotBBVA.Panel.ViewModels;

namespace BotBBVA.Panel.Views;

/// <summary>
/// Vista del panel principal: indicadores, etapas, contexto, log y dependencias.
/// </summary>
public partial class PanelPrincipalView : UserControl
{
    public PanelPrincipalView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => VincularLog();
        VincularLog();
    }

    /// <summary>
    /// Suscribe el desplazamiento automático al final del log cuando llegan entradas.
    /// </summary>
    private void VincularLog()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        vm.Log.CollectionChanged -= AlCambiarLog;
        vm.Log.CollectionChanged += AlCambiarLog;
    }

    private void AlCambiarLog(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && ListaLog.Items.Count > 0)
        {
            ListaLog.ScrollIntoView(ListaLog.Items[ListaLog.Items.Count - 1]);
        }
    }
}
