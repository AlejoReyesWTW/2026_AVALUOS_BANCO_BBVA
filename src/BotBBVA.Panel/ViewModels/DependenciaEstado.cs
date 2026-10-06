namespace BotBBVA.Panel.ViewModels;

/// <summary>
/// Estado de disponibilidad de una dependencia externa (SQL, archivos, DocLM).
/// </summary>
public sealed class DependenciaEstado : ObservableObject
{
    private bool _disponible;
    private string _ultimoMensaje = string.Empty;
    private bool _mostrarMensaje;

    public DependenciaEstado(string nombre, bool disponible)
    {
        Nombre = nombre;
        _disponible = disponible;
    }

    public string Nombre { get; }

    public bool Disponible
    {
        get => _disponible;
        set
        {
            if (Establecer(ref _disponible, value))
            {
                Notificar(nameof(TextoDisponible));
            }
        }
    }

    public string TextoDisponible => Disponible ? "Disponible" : "No disponible";

    /// <summary>Mensaje flotante que se muestra bajo la card tras una validación.</summary>
    public string UltimoMensaje
    {
        get => _ultimoMensaje;
        set => Establecer(ref _ultimoMensaje, value);
    }

    /// <summary>Indica si el mensaje flotante debe estar visible.</summary>
    public bool MostrarMensaje
    {
        get => _mostrarMensaje;
        set => Establecer(ref _mostrarMensaje, value);
    }

    /// <summary>Momento en que se mostró el último mensaje (para desvanecerlo).</summary>
    public DateTime FechaUltimoMensaje { get; set; } = DateTime.MinValue;
}
