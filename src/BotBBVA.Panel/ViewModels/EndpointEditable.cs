using System.Windows.Input;

namespace BotBBVA.Panel.ViewModels;

/// <summary>
/// Fila editable de un endpoint: se muestra en lectura y se edita con el lápiz.
/// </summary>
public sealed class EndpointEditable : ObservableObject
{
    private readonly Action _notificarCambio;
    private string _url;
    private bool _esEditando;

    public EndpointEditable(string nombre, string metodo, string url, Action notificarCambio)
    {
        Nombre = nombre;
        Metodo = metodo;
        _url = url;
        UrlOriginal = url;
        _notificarCambio = notificarCambio;
        EditarCommand = new RelayCommand(() => EsEditando = true);
    }

    public string Nombre { get; }

    /// <summary>Método HTTP de la operación (POST, GET, etc.).</summary>
    public string Metodo { get; }

    /// <summary>Último valor guardado, usado para detectar cambios.</summary>
    public string UrlOriginal { get; private set; }

    public string Url
    {
        get => _url;
        set
        {
            if (Establecer(ref _url, value))
            {
                Notificar(nameof(TieneCambios));
                _notificarCambio();
            }
        }
    }

    /// <summary>Indica si el campo está en modo edición.</summary>
    public bool EsEditando
    {
        get => _esEditando;
        set
        {
            if (Establecer(ref _esEditando, value))
            {
                Notificar(nameof(EsSoloLectura));
            }
        }
    }

    public bool EsSoloLectura => !EsEditando;

    public bool TieneCambios => Url != UrlOriginal;

    public ICommand EditarCommand { get; }

    /// <summary>
    /// Marca el valor actual como guardado y sale del modo edición.
    /// </summary>
    public void MarcarGuardado()
    {
        UrlOriginal = Url;
        EsEditando = false;
        Notificar(nameof(TieneCambios));
    }
}
