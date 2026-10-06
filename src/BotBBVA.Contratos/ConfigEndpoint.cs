using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BotBBVA.Contratos;

/// <summary>
/// Endpoint externo configurable del bot (token de autenticación y API de DocLM),
/// con su método HTTP (GET, POST, etc.).
/// </summary>
public sealed class ConfigEndpoint : INotifyPropertyChanged
{
    private string _url;

    public ConfigEndpoint(string nombre, string metodo, string url)
    {
        Nombre = nombre;
        Metodo = metodo;
        _url = url;
    }

    /// <summary>Nombre legible del endpoint.</summary>
    public string Nombre { get; }

    /// <summary>Método HTTP de la operación (POST, GET, etc.).</summary>
    public string Metodo { get; }

    /// <summary>URL del endpoint.</summary>
    public string Url
    {
        get => _url;
        set
        {
            if (_url == value)
            {
                return;
            }

            _url = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Url)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
