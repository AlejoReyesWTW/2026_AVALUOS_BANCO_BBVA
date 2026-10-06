using System.Diagnostics;
using BotBBVA.Contratos;

namespace BotBBVA.Servicio;

/// <summary>
/// Motor simulado de la Fase 1: reproduce el ciclo de lotes, la ventana de
/// ejecución, las 10 etapas del procesamiento, la exclusión por páginas y la
/// parada ordenada, sin depender del servicio real, la base de datos ni DocLM.
/// </summary>
public sealed class OrquestadorBot : IControlBot
{
    private readonly Random _aleatorio = new();
    private CancellationTokenSource? _cancelacion;
    private Task? _tarea;
    private long _siguienteRegistro = 1000;
    private long _siguienteDocumento = 1;
    private bool _enMarcha;
    private bool _paradaSolicitada;
    private bool _dispuesto;
    private EstadoBot _estadoBot;
    private EstadoEjecucion _estadoEjecucion;
    private EtapaProceso _etapaActual;
    private ContextoProceso _contextoActual = ContextoProceso.Vacio;

    public OrquestadorBot(ConfigOperativa configuracion)
    {
        Configuracion = configuracion;
    }

    public event Action<LogEntry>? LogGenerado;
    public event Action<EstadoBot>? EstadoBotCambiado;
    public event Action<EstadoEjecucion>? EstadoEjecucionCambiado;
    public event Action<EtapaProceso>? EtapaCambiada;
    public event Action<ContextoProceso>? ContextoCambiado;

    public bool EnMarcha => _enMarcha;

    public ConfigOperativa Configuracion { get; }

    /// <summary>
    /// Obtiene una instantánea del estado actual del bot y su configuración.
    /// </summary>
    public EstadoCompleto ObtenerEstadoCompleto()
        => new(_estadoBot, _estadoEjecucion, _etapaActual, _contextoActual, Configuracion);

    public void Iniciar()
    {
        if (_enMarcha || _dispuesto)
        {
            return;
        }

        _enMarcha = true;
        _paradaSolicitada = false;
        _cancelacion = new CancellationTokenSource();
        _tarea = Task.Run(EjecutarCiclo, _cancelacion.Token);
    }

    public void Detener()
    {
        if (!_enMarcha || _paradaSolicitada)
        {
            return;
        }

        _paradaSolicitada = true;
        EmitirEjecucion(EstadoEjecucion.Deteniendo);
        EmitirLog(NivelLog.Advertencia, null, "Detención solicitada. El bot finalizará el lote actual antes de detenerse.", null);
    }

    /// <summary>
    /// Ciclo principal del bot simulado.
    /// </summary>
    private async Task EjecutarCiclo()
    {
        var token = _cancelacion!.Token;
        EmitirEstadoBot(EstadoBot.Iniciado);

        try
        {
            // Si está fuera de la ventana de ejecución, queda esperando su apertura.
            if (!Configuracion.DentroDeVentana(DateTime.Now.TimeOfDay))
            {
                EmitirEjecucion(EstadoEjecucion.FueraDeHorario);
                EmitirLog(NivelLog.Informacion, null,
                    $"Fuera de la ventana de ejecución ({Hora(Configuracion.HoraInicio)}–{Hora(Configuracion.HoraFin)}). El bot espera la apertura de la ventana.", null);

                while (!token.IsCancellationRequested
                       && !_paradaSolicitada
                       && !Configuracion.DentroDeVentana(DateTime.Now.TimeOfDay))
                {
                    await Esperar(token, 1000);
                }

                if (_paradaSolicitada)
                {
                    FinalizarParada();
                    return;
                }
            }

            EmitirEjecucion(EstadoEjecucion.Ejecutando);

            while (!token.IsCancellationRequested && !_paradaSolicitada)
            {
                await ProcesarLote(token);

                if (_paradaSolicitada)
                {
                    break;
                }

                // Ocasionalmente se simula que no quedan pendientes (modo escucha).
                if (_aleatorio.NextDouble() < 0.20)
                {
                    EmitirLog(NivelLog.Informacion, null, "Sin registros pendientes. Modo escucha activo.", null);
                    await Esperar(token, _aleatorio.Next(2500, 4500));

                    if (!_paradaSolicitada)
                    {
                        EmitirLog(NivelLog.Informacion, null, "Se detectaron nuevos registros pendientes. Reanudando backlog.", null);
                    }
                }
            }

            if (_paradaSolicitada)
            {
                FinalizarParada();
            }
        }
        catch (OperationCanceledException)
        {
            // Parada esperada del ciclo de trabajo.
        }
    }

    /// <summary>
    /// Ejecuta las 10 etapas sobre un lote SQL simulado de hasta 5 registros.
    /// </summary>
    private async Task ProcesarLote(CancellationToken token)
    {
        var reloj = Stopwatch.StartNew();

        EmitirEtapa(EtapaProceso.ConsultandoSp);
        EmitirLog(NivelLog.Informacion, null, "Consultando SP de registros pendientes (hasta 5).", null);
        await Esperar(token, _aleatorio.Next(400, 700));

        var registros = GenerarLote();

        EmitirEtapa(EtapaProceso.ObteniendoArchivo);
        EmitirLog(NivelLog.Informacion, null, "Obteniendo archivos desde el servidor de archivos.", null);
        await Esperar(token, _aleatorio.Next(400, 700));

        EmitirEtapa(EtapaProceso.ValidandoArchivos);
        await Esperar(token, _aleatorio.Next(400, 700));

        // Se valida cada documento: conteo de páginas y exclusión de >200.
        var excluidos = new List<RegistroSimulado>();
        var validos = new List<RegistroSimulado>();

        foreach (var registro in registros)
        {
            EmitirLog(NivelLog.Informacion, registro.Id, $"Documento '{registro.Documento}': {registro.Paginas} páginas.", null, registro.Documento);
            await Esperar(token, 150);

            if (registro.Paginas > 200)
            {
                excluidos.Add(registro);
            }
            else
            {
                validos.Add(registro);
            }
        }

        foreach (var registro in excluidos)
        {
            EmitirLog(NivelLog.Advertencia, registro.Id,
                $"Documento '{registro.Documento}': excluido por superar 200 páginas.", ResultadoProceso.ExcluidoPorPaginas, registro.Documento);
        }

        // Agrupación por rango y envío a DocLM.
        foreach (var grupo in validos.GroupBy(r => RangoPaginasInfo.Clasificar(r.Paginas)))
        {
            int capacidad = Configuracion.ArchivosPorLote(grupo.Key);
            foreach (var sublote in grupo.Chunk(capacidad))
            {
                if (_paradaSolicitada)
                {
                    return;
                }

                string documentos = string.Join(", ", sublote.Select(r => r.Documento));
                string tarea = $"LOTE-{sublote[0].Id}";

                EmitirEtapa(EtapaProceso.EnviandoDocLm);
                EmitirContexto(new ContextoProceso(
                    tarea,
                    documentos,
                    RangoPaginasInfo.Etiqueta(grupo.Key),
                    sublote.Length,
                    capacidad,
                    $"{(int)reloj.Elapsed.TotalSeconds} s",
                    EtapaProcesoInfo.Etiqueta(EtapaProceso.EnviandoDocLm)));
                EmitirLog(NivelLog.Informacion, null,
                    $"Enviando lote de {sublote.Length} archivo(s) a DocLM (rango {RangoPaginasInfo.Etiqueta(grupo.Key)}).", null);
                await Esperar(token, _aleatorio.Next(500, 900));

                // Simulación del flujo DocLM: crear → consultar estado → assessment → consultar resultado.
                EmitirEtapa(EtapaProceso.EsperandoDocLm);
                long idDocumento = _siguienteDocumento++;
                EmitirLog(NivelLog.Informacion, null, $"Documento creado (id_document: DOC-{idDocumento}). Estado: pending.", null);
                await Esperar(token, 700);
                EmitirLog(NivelLog.Informacion, null,
                    $"Consultando estado cada {Configuracion.IntervaloConsultaSegundos}s (simulado)... Estado: processed.", null);
                await Esperar(token, 700);
                EmitirLog(NivelLog.Informacion, null, $"Ejecutando assessment (id_run: RUN-{idDocumento}). Estado: pending.", null);
                await Esperar(token, 700);
                EmitirLog(NivelLog.Informacion, null, "Estado del assessment: processed. Resultados recibidos.", null);
                await Esperar(token, 400);

                EmitirEtapa(EtapaProceso.LeyendoInformacion);
                EmitirLog(NivelLog.Informacion, null, "Leyendo respuesta de DocLM para actualizar la base de datos.", null);
                await Esperar(token, 300);

                EmitirEtapa(EtapaProceso.BaseActualizada);
                foreach (var registro in sublote)
                {
                    var (resultado, mensaje) = ProcesarResultado(registro);
                    EmitirLog(
                        resultado == ResultadoProceso.Error ? NivelLog.Error : resultado == ResultadoProceso.SinDatos ? NivelLog.Advertencia : NivelLog.Exito,
                        registro.Id,
                        mensaje,
                        resultado,
                        registro.Documento);
                    await Esperar(token, 120);
                }
            }
        }

        EmitirEtapa(EtapaProceso.GenerandoExcel);
        EmitirLog(NivelLog.Informacion, null, "Generando Excel operativo del lote.", null);
        await Esperar(token, 400);

        EmitirEtapa(EtapaProceso.ExcelGenerado);
        EmitirLog(NivelLog.Informacion, null, "Excel operativo consolidado.", null);
        await Esperar(token, 300);

        EmitirEtapa(EtapaProceso.FinProceso);
        EmitirLog(NivelLog.Informacion, null, "Fin de proceso del lote.", null);
        EmitirContexto(ContextoProceso.Vacio);
    }

    /// <summary>
    /// Simula el resultado individual de DocLM para un documento.
    /// </summary>
    private (ResultadoProceso Resultado, string Mensaje) ProcesarResultado(RegistroSimulado registro)
    {
        double sorteo = _aleatorio.NextDouble();
        return sorteo switch
        {
            < 0.08 => (ResultadoProceso.SinDatos, $"Registro {registro.Id}: DocLM respondió sin valores. Se marca SIN_DATOS."),
            < 0.14 => (ResultadoProceso.Error, $"Registro {registro.Id}: falló el envío a DocLM (timeout). Se marca ERROR."),
            _ => (ResultadoProceso.Actualizado, $"Registro {registro.Id}: datos extraídos y actualizados correctamente.")
        };
    }

    /// <summary>
    /// Genera un lote simulado de 1 a 5 registros con páginas aleatorias.
    /// </summary>
    private List<RegistroSimulado> GenerarLote()
    {
        int cantidad = _aleatorio.Next(1, 6);
        var registros = new List<RegistroSimulado>(cantidad);

        for (int i = 0; i < cantidad; i++)
        {
            long id = _siguienteRegistro++;
            int paginas = _aleatorio.Next(1, 260);
            registros.Add(new RegistroSimulado(id, $"doc-{id}.pdf", paginas));
        }

        return registros;
    }

    /// <summary>
    /// Marca el estado final tras una parada ordenada.
    /// </summary>
    private void FinalizarParada()
    {
        EmitirEtapa(EtapaProceso.ExcelGenerado);
        EmitirLog(NivelLog.Informacion, null, "Excel operativo consolidado. El bot se detiene.", null);
        EmitirContexto(ContextoProceso.Vacio);
        EmitirEjecucion(EstadoEjecucion.Detenido);
        EmitirEstadoBot(EstadoBot.Detenido);
        _enMarcha = false;
    }

    private static async Task Esperar(CancellationToken token, int milisegundos)
    {
        try
        {
            await Task.Delay(milisegundos, token);
        }
        catch (OperationCanceledException)
        {
            // La cancelación es el camino normal de parada.
        }
    }

    private static string Hora(TimeSpan hora) => $"{hora.Hours:00}:{hora.Minutes:00}";

    private void EmitirLog(NivelLog nivel, long? registroId, string mensaje, ResultadoProceso? resultado, string? documento = null)
        => LogGenerado?.Invoke(new LogEntry(DateTime.Now, nivel, registroId, mensaje, resultado, documento));

    private void EmitirEstadoBot(EstadoBot estado)
    {
        _estadoBot = estado;
        EstadoBotCambiado?.Invoke(estado);
    }

    private void EmitirEjecucion(EstadoEjecucion estado)
    {
        _estadoEjecucion = estado;
        EstadoEjecucionCambiado?.Invoke(estado);
    }

    private void EmitirEtapa(EtapaProceso etapa)
    {
        _etapaActual = etapa;
        EtapaCambiada?.Invoke(etapa);
    }

    private void EmitirContexto(ContextoProceso contexto)
    {
        _contextoActual = contexto;
        ContextoCambiado?.Invoke(contexto);
    }

    public void Dispose()
    {
        if (_dispuesto)
        {
            return;
        }

        _dispuesto = true;
        _enMarcha = false;
        _cancelacion?.Cancel();
        _cancelacion?.Dispose();
    }

    private sealed record RegistroSimulado(long Id, string Documento, int Paginas);
}
