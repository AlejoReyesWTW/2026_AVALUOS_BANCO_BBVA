using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BotBBVA.Contratos;
using BotBBVA.Panel.Servicios;

namespace BotBBVA.Panel.ViewModels;

/// <summary>
/// ViewModel principal del panel: une el motor simulado con la vista,
/// expone indicadores, etapas, contexto, dependencias y navegación.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private const int LimiteEntradas = 1000;
    private const int LimiteConectividad = 200;
    private const int PendientesIniciales = 2_400_000;
    private const int TotalHistorico = 18_420_000;

    private readonly IControlBot _motor;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _temporizador;
    private readonly ConcurrentQueue<LogEntry> _colaLogs = new();
    private readonly DocLmAutenticador _autenticador;

    private Vista _vistaActual = Vista.PanelPrincipal;
    private EstadoBot _estadoBot = EstadoBot.Detenido;
    private EstadoEjecucion _estadoEjecucion = EstadoEjecucion.Detenido;
    private ContextoProceso _contextoActual = ContextoProceso.Vacio;
    private int _pendientes = PendientesIniciales;
    private int _enProcesamiento;
    private DateTime? _horaInicio;
    private DateTime? _horaUltimaEjecucion;
    private bool _dispuesto;
    private DateTime _ultimaValidacionConexiones = DateTime.Now;
    private bool _logConectividadVisible = true;
    private bool _logDetalladoVisible = true;

    public MainViewModel(IControlBot motor)
    {
        _motor = motor;
        _dispatcher = Application.Current.Dispatcher;

        Contadores = new ContadoresDiarios();
        Configuracion = new ConfiguracionViewModel(motor.Configuracion, () => VistaActual = Vista.PanelPrincipal, AlGuardarConfiguracion);

        IniciarCommand = new RelayCommand(Iniciar, () => EstadoBot == EstadoBot.Detenido && CredencialesCompletas);
        DetenerCommand = new RelayCommand(Detener, () => EstadoBot == EstadoBot.Iniciado && EstadoEjecucion != EstadoEjecucion.Deteniendo);
        AbrirExcelCommand = new RelayCommand(AbrirExcel);
        ProbarConexionCommand = new RelayCommand(parametro => ProbarConexion(parametro as DependenciaEstado));
        MostrarPanelPrincipalCommand = new RelayCommand(() => Navegar(Vista.PanelPrincipal));
        MostrarConfiguracionCommand = new RelayCommand(() => Navegar(Vista.Configuracion));
        AlternarLogConectividadCommand = new RelayCommand(() => LogConectividadVisible = !LogConectividadVisible);
        AlternarLogDetalladoCommand = new RelayCommand(() => LogDetalladoVisible = !LogDetalladoVisible);

        Etapas = new ObservableCollection<EtapaVisual>(EtapaProcesoInfo.Todas.Select(e => new EtapaVisual(e)));

        DependenciaSql = new DependenciaEstado("SQL Server", true);
        DependenciaArchivos = new DependenciaEstado("Servidor de archivos", true);
        DependenciaDocLm = new DependenciaEstado("DocLM", true);
        _autenticador = new DocLmAutenticador(motor.Configuracion);

        // El motor emite desde un hilo de fondo: se encola el log y el resto se
        // envía al hilo de la interfaz.
        _motor.LogGenerado += entrada => _colaLogs.Enqueue(entrada);
        _motor.EstadoBotCambiado += estado => _dispatcher.BeginInvoke(() => EstadoBot = estado);
        _motor.EstadoEjecucionCambiado += estado => _dispatcher.BeginInvoke(() => EstadoEjecucion = estado);
        _motor.EtapaCambiada += etapa => _dispatcher.BeginInvoke(() => AplicarEtapa(etapa));
        _motor.ContextoCambiado += contexto => _dispatcher.BeginInvoke(() => ContextoActual = contexto);

        _temporizador = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _temporizador.Tick += (_, _) =>
        {
            VaciarCola();
            ActualizarTiempo();
            ValidarConexionesPeriodicamente();
            OcultarMensajesVencidos();
        };
        _temporizador.Start();
    }

    public ObservableCollection<LogEntry> Log { get; } = new();

    public ObservableCollection<LogEntry> LogConectividad { get; } = new();

    public ContadoresDiarios Contadores { get; }

    public ObservableCollection<EtapaVisual> Etapas { get; }

    public DependenciaEstado DependenciaSql { get; }

    public DependenciaEstado DependenciaArchivos { get; }

    public DependenciaEstado DependenciaDocLm { get; }

    public ConfiguracionViewModel Configuracion { get; }

    public Vista VistaActual
    {
        get => _vistaActual;
        private set
        {
            if (Establecer(ref _vistaActual, value))
            {
                Notificar(nameof(MostrarPanelPrincipal));
                Notificar(nameof(MostrarConfiguracion));
            }
        }
    }

    public bool MostrarPanelPrincipal => VistaActual == Vista.PanelPrincipal;

    public bool MostrarConfiguracion => VistaActual == Vista.Configuracion;

    public EstadoBot EstadoBot
    {
        get => _estadoBot;
        private set
        {
            if (Establecer(ref _estadoBot, value))
            {
                Notificar(nameof(TextoEstadoBot));
                Notificar(nameof(BotDetenido));
                Notificar(nameof(BotIniciado));
                RefrescarComandos();
            }
        }
    }

    public EstadoEjecucion EstadoEjecucion
    {
        get => _estadoEjecucion;
        private set
        {
            if (Establecer(ref _estadoEjecucion, value))
            {
                Notificar(nameof(TextoEstadoEjecucion));
                Configuracion.CamposHorarioBloqueados = value != EstadoEjecucion.Detenido;
                RefrescarComandos();

                if (value == EstadoEjecucion.Ejecutando)
                {
                    _horaInicio ??= DateTime.Now;
                    Notificar(nameof(TextoHoraInicio));
                }
            }
        }
    }

    public ContextoProceso ContextoActual
    {
        get => _contextoActual;
        private set
        {
            if (Establecer(ref _contextoActual, value))
            {
                EnProcesamiento = value == ContextoProceso.Vacio ? 0 : value.ArchivosLote;
                Notificar(nameof(HayLoteEnCurso));
            }
        }
    }

    public bool HayLoteEnCurso => ContextoActual != ContextoProceso.Vacio;

    public int Pendientes
    {
        get => _pendientes;
        private set => Establecer(ref _pendientes, value);
    }

    public int EnProcesamiento
    {
        get => _enProcesamiento;
        private set => Establecer(ref _enProcesamiento, value);
    }

    public string TextoEstadoBot => EstadoBot == EstadoBot.Iniciado ? "Iniciado" : "Detenido";

    public bool BotDetenido => EstadoBot == EstadoBot.Detenido;

    public bool BotIniciado => EstadoBot == EstadoBot.Iniciado;

    public bool CredencialesCompletas =>
        !string.IsNullOrWhiteSpace(_motor.Configuracion.TenantId) &&
        !string.IsNullOrWhiteSpace(_motor.Configuracion.ClientId) &&
        !string.IsNullOrWhiteSpace(_motor.Configuracion.ClientSecret);

    public bool FaltanCredenciales => !CredencialesCompletas;

    public string TextoEstadoEjecucion => EstadoEjecucion switch
    {
        EstadoEjecucion.FueraDeHorario => "Fuera de horario",
        EstadoEjecucion.Ejecutando => "Ejecutando",
        EstadoEjecucion.Deteniendo => "Deteniendo",
        _ => "Detenido"
    };

    public string TextoEstadoServicio => "Conectado";

    public string TextoHoraInicio => _horaInicio?.ToString("HH:mm:ss") ?? "—";

    public string TextoHoraUltimaEjecucion => _horaUltimaEjecucion?.ToString("HH:mm:ss") ?? "—";

    public string TextoTiempoEjecucion { get; private set; } = "00:00:00";

    public int ProcesadosDelDia => Contadores.Procesados;

    public int TotalProcesados => TotalHistorico + Contadores.Procesados;

    public bool LogConectividadVisible
    {
        get => _logConectividadVisible;
        set => Establecer(ref _logConectividadVisible, value);
    }

    public bool LogDetalladoVisible
    {
        get => _logDetalladoVisible;
        set => Establecer(ref _logDetalladoVisible, value);
    }

    public ICommand IniciarCommand { get; }

    public ICommand DetenerCommand { get; }

    public ICommand AbrirExcelCommand { get; }

    public ICommand ProbarConexionCommand { get; }

    public ICommand MostrarPanelPrincipalCommand { get; }

    public ICommand MostrarConfiguracionCommand { get; }

    public ICommand AlternarLogConectividadCommand { get; }

    public ICommand AlternarLogDetalladoCommand { get; }

    private void Iniciar() => _motor.Iniciar();

    private void Detener() => _motor.Detener();

    private void AbrirExcel()
        => AgregarEntrada(new LogEntry(DateTime.Now, NivelLog.Informacion, null, "Solicitado abrir carpeta del Excel operativo (se conecta en Fase 4).", null));

    /// <summary>
    /// Se invoca al guardar la configuración: refresca la validación de credenciales y registra el cambio.
    /// </summary>
    private void AlGuardarConfiguracion()
    {
        if (_motor is CanalControlCliente cliente) cliente.GuardarConfiguracion();
        Notificar(nameof(CredencialesCompletas));
        Notificar(nameof(FaltanCredenciales));
        RefrescarComandos();
        AgregarEntrada(new LogEntry(DateTime.Now, NivelLog.Informacion, null, "Configuración operativa actualizada (ventana, endpoints, credenciales).", null));
    }

    private void Navegar(Vista destino)
    {
        if (VistaActual == Vista.Configuracion && destino != Vista.Configuracion)
        {
            Configuracion.Salir(() => VistaActual = destino);
            return;
        }

        VistaActual = destino;
    }

    /// <summary>
    /// Marca la etapa actual y las completadas/pendientes en el componente visual.
    /// </summary>
    private void AplicarEtapa(EtapaProceso etapa)
    {
        foreach (var item in Etapas)
        {
            if (item.Etapa == etapa)
            {
                item.Estado = EstadoEtapa.EnCurso;
            }
            else if ((int)item.Etapa < (int)etapa)
            {
                item.Estado = EstadoEtapa.Completado;
            }
            else
            {
                item.Estado = EstadoEtapa.Pendiente;
            }
        }

        if (etapa == EtapaProceso.FinProceso)
        {
            _horaUltimaEjecucion = DateTime.Now;
            Notificar(nameof(TextoHoraUltimaEjecucion));
        }
    }

    private void ProbarConexion(DependenciaEstado? dependencia) => ValidarConexion(dependencia);

    /// <summary>
    /// Simula la validación de la conexión de una dependencia y muestra el mensaje.
    /// </summary>
    private void ValidarConexion(DependenciaEstado? dependencia)
    {
        if (dependencia is null)
        {
            return;
        }

        if (ReferenceEquals(dependencia, DependenciaDocLm))
        {
            _ = ValidarDocLmAsync(dependencia);
            return;
        }

        // SQL Server y servidor de archivos: simulado (aún no hay backend real).
        AgregarEntradaConectividad(new LogEntry(DateTime.Now, NivelLog.Informacion, null, $"Comprobando conexión con {dependencia.Nombre}...", null));
        dependencia.Disponible = true;
        dependencia.UltimoMensaje = "Conexión verificada · se volverá a validar en 2 minutos";
        dependencia.FechaUltimoMensaje = DateTime.Now;
        dependencia.MostrarMensaje = true;
        AgregarEntradaConectividad(new LogEntry(DateTime.Now, NivelLog.Exito, null, $"Conexión verificada: {dependencia.Nombre} disponible.", null));
    }

    /// <summary>
    /// Realiza la prueba de conexión real a DocLM (OAuth2) y actualiza el estado.
    /// </summary>
    private async Task ValidarDocLmAsync(DependenciaEstado dependencia)
    {
        AgregarEntradaConectividad(new LogEntry(DateTime.Now, NivelLog.Informacion, null, "Comprobando conexión con DocLM (OAuth2 real)...", null));

        var resultado = await _autenticador.ProbarConexionAsync();

        dependencia.Disponible = resultado.Exito;
        dependencia.UltimoMensaje = resultado.Exito
            ? "Conexión verificada · se volverá a validar en 2 minutos"
            : $"Error de conexión: {resultado.Mensaje}";
        dependencia.FechaUltimoMensaje = DateTime.Now;
        dependencia.MostrarMensaje = true;

        AgregarEntradaConectividad(new LogEntry(
            DateTime.Now,
            resultado.Exito ? NivelLog.Exito : NivelLog.Error,
            null,
            resultado.Exito ? "Conexión verificada: DocLM disponible." : $"Error de conexión DocLM: {resultado.Mensaje}",
            null));
    }

    /// <summary>
    /// Agrega una entrada al log de conectividad, separado del log de ejecución.
    /// </summary>
    private void AgregarEntradaConectividad(LogEntry entrada)
    {
        LogConectividad.Add(entrada);

        if (LogConectividad.Count > LimiteConectividad)
        {
            LogConectividad.RemoveAt(0);
        }
    }

    /// <summary>
    /// Oculta los mensajes flotantes de conexión transcurridos 15 segundos.
    /// </summary>
    private void OcultarMensajesVencidos()
    {
        foreach (var dependencia in new[] { DependenciaSql, DependenciaArchivos, DependenciaDocLm })
        {
            if (dependencia.MostrarMensaje && (DateTime.Now - dependencia.FechaUltimoMensaje).TotalSeconds >= 15)
            {
                dependencia.MostrarMensaje = false;
            }
        }
    }

    /// <summary>
    /// Revalida automáticamente las conexiones de las dependencias cada 2 minutos.
    /// </summary>
    private void ValidarConexionesPeriodicamente()
    {
        if ((DateTime.Now - _ultimaValidacionConexiones).TotalMinutes >= 2)
        {
            _ultimaValidacionConexiones = DateTime.Now;
            ValidarConexion(DependenciaSql);
            ValidarConexion(DependenciaArchivos);
            ValidarConexion(DependenciaDocLm);
        }
    }

    private void VaciarCola()
    {
        while (_colaLogs.TryDequeue(out var entrada))
        {
            AgregarEntrada(entrada);
        }
    }

    private void AgregarEntrada(LogEntry entrada)
    {
        Log.Add(entrada);

        if (entrada.Resultado is { } resultado)
        {
            Contadores.Registrar(resultado);

            if (Pendientes > 0)
            {
                Pendientes--;
            }

            Notificar(nameof(ProcesadosDelDia));
            Notificar(nameof(TotalProcesados));
        }

        if (Log.Count > LimiteEntradas)
        {
            Log.RemoveAt(0);
        }
    }

    private void ActualizarTiempo()
    {
        if (EstadoEjecucion == EstadoEjecucion.Ejecutando && _horaInicio is { } inicio)
        {
            var transcurrido = DateTime.Now - inicio;
            TextoTiempoEjecucion = $"{(int)transcurrido.TotalHours:00}:{transcurrido.Minutes:00}:{transcurrido.Seconds:00}";
            Notificar(nameof(TextoTiempoEjecucion));
        }
    }

    private void RefrescarComandos()
    {
        ((RelayCommand)IniciarCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DetenerCommand).RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_dispuesto)
        {
            return;
        }

        _dispuesto = true;
        _temporizador.Stop();
        _motor.Dispose();
    }
}
