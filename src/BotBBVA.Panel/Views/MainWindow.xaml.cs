using System.Windows;
using BotBBVA.Contratos;
using BotBBVA.Panel.Servicios;
using BotBBVA.Panel.ViewModels;
using Wpf.Ui.Controls;

namespace BotBBVA.Panel.Views;

/// <summary>
/// Ventana principal: aloja la navegación lateral y las vistas internas.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _vistaModelo;

    public MainWindow()
    {
        InitializeComponent();

        var motor = new CanalControlCliente();
        _vistaModelo = new MainViewModel(motor);
        DataContext = _vistaModelo;
    }

    protected override void OnClosed(EventArgs e)
    {
        _vistaModelo.Dispose();
        base.OnClosed(e);
    }
}
