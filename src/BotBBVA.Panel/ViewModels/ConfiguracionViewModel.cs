using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using BotBBVA.Contratos;

namespace BotBBVA.Panel.ViewModels;

/// <summary>
/// ViewModel de la vista de configuración operativa: ventana de ejecución,
/// archivos por lote, endpoints de DocLM e intervalo de consulta (sección 4.1).
/// </summary>
public sealed class ConfiguracionViewModel : ObservableObject
{
    private readonly ConfigOperativa _configuracion;
    private readonly Action _volver;
    private readonly Action? _onGuardado;
    private string _horaInicioTexto;
    private string _horaFinTexto;
    private string _intervaloConsultaTexto;
    private string _intervaloOriginalTexto;
    private bool _intervaloEsEditando;
    private bool _hayCambiosSinGuardar;
    private bool _camposHorarioBloqueados;
    private string _tenantIdTexto;
    private string _clientIdTexto;
    private string _clientSecretTexto;
    private string _scopeTexto;

    public ConfiguracionViewModel(ConfigOperativa configuracion, Action volver, Action? onGuardado = null)
    {
        _configuracion = configuracion;
        _volver = volver;
        _onGuardado = onGuardado;

        _horaInicioTexto = FormatearHora(configuracion.HoraInicio);
        _horaFinTexto = FormatearHora(configuracion.HoraFin);

        _intervaloConsultaTexto = configuracion.IntervaloConsultaSegundos.ToString();
        _intervaloOriginalTexto = _intervaloConsultaTexto;

        _tenantIdTexto = configuracion.TenantId;
        _clientIdTexto = configuracion.ClientId;
        _clientSecretTexto = configuracion.ClientSecret;
        _scopeTexto = configuracion.Scope;

        Rangos = new ObservableCollection<ConfigRango>(configuracion.Rangos);
        foreach (var rango in Rangos)
        {
            rango.PropertyChanged += (_, _) => HayCambiosSinGuardar = true;
        }

        Endpoints = new ObservableCollection<EndpointEditable>(
            configuracion.Endpoints.Select(e => new EndpointEditable(e.Nombre, e.Metodo, e.Url, () => HayCambiosSinGuardar = true)));

        GuardarCommand = new RelayCommand(() => Guardar());
        VolverCommand = new RelayCommand(() => Salir(_volver));
        EditarIntervaloCommand = new RelayCommand(() => IntervaloEsEditando = true);
    }

    public ObservableCollection<ConfigRango> Rangos { get; }

    public ObservableCollection<EndpointEditable> Endpoints { get; }

    public string HoraInicioTexto
    {
        get => _horaInicioTexto;
        set
        {
            if (Establecer(ref _horaInicioTexto, value))
            {
                HayCambiosSinGuardar = true;
            }
        }
    }

    public string HoraFinTexto
    {
        get => _horaFinTexto;
        set
        {
            if (Establecer(ref _horaFinTexto, value))
            {
                HayCambiosSinGuardar = true;
            }
        }
    }

    public string IntervaloConsultaTexto
    {
        get => _intervaloConsultaTexto;
        set
        {
            if (Establecer(ref _intervaloConsultaTexto, value))
            {
                Notificar(nameof(IntervaloTieneCambios));
                HayCambiosSinGuardar = true;
            }
        }
    }

    public bool IntervaloEsEditando
    {
        get => _intervaloEsEditando;
        set
        {
            if (Establecer(ref _intervaloEsEditando, value))
            {
                Notificar(nameof(IntervaloEsSoloLectura));
            }
        }
    }

    public bool IntervaloEsSoloLectura => !IntervaloEsEditando;

    public bool IntervaloTieneCambios => IntervaloConsultaTexto != _intervaloOriginalTexto;

    public string TenantIdTexto
    {
        get => _tenantIdTexto;
        set
        {
            if (Establecer(ref _tenantIdTexto, value))
            {
                HayCambiosSinGuardar = true;
            }
        }
    }

    public string ClientIdTexto
    {
        get => _clientIdTexto;
        set
        {
            if (Establecer(ref _clientIdTexto, value))
            {
                HayCambiosSinGuardar = true;
            }
        }
    }

    public string ClientSecretTexto
    {
        get => _clientSecretTexto;
        set
        {
            if (Establecer(ref _clientSecretTexto, value))
            {
                HayCambiosSinGuardar = true;
            }
        }
    }

    public string ScopeTexto
    {
        get => _scopeTexto;
        set
        {
            if (Establecer(ref _scopeTexto, value))
            {
                HayCambiosSinGuardar = true;
            }
        }
    }

    public bool HayCambiosSinGuardar
    {
        get => _hayCambiosSinGuardar;
        private set => Establecer(ref _hayCambiosSinGuardar, value);
    }

    /// <summary>
    /// Bloquea los campos de horario mientras el bot esté iniciado o ejecutándose.
    /// </summary>
    public bool CamposHorarioBloqueados
    {
        get => _camposHorarioBloqueados;
        set
        {
            if (Establecer(ref _camposHorarioBloqueados, value))
            {
                Notificar(nameof(HorarioEditable));
            }
        }
    }

    public bool HorarioEditable => !CamposHorarioBloqueados;

    public string MensajeAyudaHorario => "Para editar la hora de ejecución del bot, por favor detenga su ejecución.";

    public ICommand GuardarCommand { get; }

    public ICommand VolverCommand { get; }

    public ICommand EditarIntervaloCommand { get; }

    /// <summary>
    /// Confirma la salida de la vista. Si hay cambios sin guardar, consulta antes.
    /// </summary>
    public void Salir(Action continuar)
    {
        if (!HayCambiosSinGuardar)
        {
            continuar();
            return;
        }

        var resultado = MessageBox.Show("¿Guardar cambios?", "Configuración", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        switch (resultado)
        {
            case MessageBoxResult.Cancel:
                return;
            case MessageBoxResult.Yes:
                if (!Guardar())
                {
                    return;
                }

                break;
            default:
                HayCambiosSinGuardar = false;
                break;
        }

        continuar();
    }

    private bool Guardar()
    {
        if (!IntentarParsearHora(HoraInicioTexto, out var inicio) || !IntentarParsearHora(HoraFinTexto, out var fin))
        {
            MessageBox.Show("El formato de hora debe ser HH:mm (24 horas).", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (inicio == fin)
        {
            MessageBox.Show("La hora de inicio y la hora de fin no pueden ser iguales.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        foreach (var rango in Rangos.Where(r => r.EsEditable))
        {
            if (rango.ArchivosPorLote is < 1 or > 5)
            {
                MessageBox.Show($"El valor de archivos por lote para el rango {rango.Etiqueta} debe estar entre 1 y 5.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        foreach (var endpoint in Endpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint.Url))
            {
                MessageBox.Show($"El endpoint '{endpoint.Nombre}' no puede quedar vacío.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        if (!int.TryParse(IntervaloConsultaTexto.Trim(), out var intervalo) || intervalo <= 0)
        {
            MessageBox.Show("El tiempo de espera entre consultas debe ser un número entero mayor que 0.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        _configuracion.HoraInicio = inicio;
        _configuracion.HoraFin = fin;

        foreach (var rango in Rangos)
        {
            _configuracion.Rangos.First(c => c.Rango == rango.Rango).ArchivosPorLote = rango.ArchivosPorLote;
        }

        foreach (var endpoint in Endpoints)
        {
            _configuracion.Endpoints.First(e => e.Nombre == endpoint.Nombre).Url = endpoint.Url;
            endpoint.MarcarGuardado();
        }

        _configuracion.IntervaloConsultaSegundos = intervalo;
        _intervaloOriginalTexto = IntervaloConsultaTexto;
        IntervaloEsEditando = false;

        _configuracion.TenantId = TenantIdTexto;
        _configuracion.ClientId = ClientIdTexto;
        _configuracion.ClientSecret = ClientSecretTexto;
        _configuracion.Scope = ScopeTexto.Trim();

        HayCambiosSinGuardar = false;
        _onGuardado?.Invoke();
        MessageBox.Show("Configuración guardada correctamente.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Information);
        return true;
    }

    private static bool IntentarParsearHora(string texto, out TimeSpan hora)
    {
        hora = default;
        var partes = texto.Trim().Split(':');

        if (partes.Length != 2
            || !int.TryParse(partes[0], out var horas)
            || !int.TryParse(partes[1], out var minutos))
        {
            return false;
        }

        if (horas is < 0 or > 23 || minutos is < 0 or > 59)
        {
            return false;
        }

        hora = new TimeSpan(horas, minutos, 0);
        return true;
    }

    private static string FormatearHora(TimeSpan hora) => $"{hora.Hours:00}:{hora.Minutes:00}";
}
