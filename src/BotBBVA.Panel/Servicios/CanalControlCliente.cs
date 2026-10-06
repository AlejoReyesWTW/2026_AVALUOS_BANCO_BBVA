using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using BotBBVA.Contratos;

namespace BotBBVA.Panel.Servicios;

/// <summary>Cliente no bloqueante del canal de control del servicio.</summary>
public sealed class CanalControlCliente : IControlBot
{
    private readonly JsonSerializerOptions _opciones = new(JsonSerializerDefaults.Web);
    private readonly CancellationTokenSource _cancelacion = new();
    private NamedPipeClientStream? _canal;
    private ConfigOperativa _configuracion = new();
    private int _espera = 250;
    public event Action<LogEntry>? LogGenerado;
    public event Action<EstadoBot>? EstadoBotCambiado;
    public event Action<EstadoEjecucion>? EstadoEjecucionCambiado;
    public event Action<EtapaProceso>? EtapaCambiada;
    public event Action<ContextoProceso>? ContextoCambiado;
    public bool EnMarcha { get; private set; }
    public ConfigOperativa Configuracion => _configuracion;

    public CanalControlCliente() => _ = ConectarAsync();
    public void Iniciar() => Enviar<object?>(TiposMensaje.IniciarBot);
    public void Detener() => Enviar<object?>(TiposMensaje.DetenerBot);
    public void GuardarConfiguracion() => Enviar(TiposMensaje.GuardarConfiguracion, _configuracion);
    public void Dispose() { _cancelacion.Cancel(); _canal?.Dispose(); _cancelacion.Dispose(); }

    private async Task ConectarAsync()
    {
        while (!_cancelacion.IsCancellationRequested)
        {
            try
            {
                var canal = new NamedPipeClientStream(".", "BotBBVA.Control", PipeDirection.InOut, PipeOptions.Asynchronous);
                await canal.ConnectAsync(3000, _cancelacion.Token); _canal = canal; _espera = 250;
                using var lector = new StreamReader(canal);
                while (canal.IsConnected && !_cancelacion.IsCancellationRequested)
                { var linea = await lector.ReadLineAsync(_cancelacion.Token); if (linea is null) break; Procesar(linea); }
            }
            catch (OperationCanceledException) when (_cancelacion.IsCancellationRequested) { break; }
            catch (IOException) { }
            finally { _canal?.Dispose(); _canal = null; }
            await Task.Delay(_espera, _cancelacion.Token); _espera = Math.Min(_espera * 2, 5000);
        }
    }

    private void Procesar(string linea)
    {
        var mensaje = JsonSerializer.Deserialize<Mensaje>(linea, _opciones); if (mensaje?.Json is null) return;
        switch (mensaje.Tipo)
        {
            case TiposMensaje.EstadoCompleto:
                var completo = JsonSerializer.Deserialize<EstadoCompleto>(mensaje.Json, _opciones); if (completo is null) return;
                _configuracion = completo.Configuracion; EnMarcha = completo.EstadoBot == EstadoBot.Iniciado;
                EstadoBotCambiado?.Invoke(completo.EstadoBot); EstadoEjecucionCambiado?.Invoke(completo.EstadoEjecucion); EtapaCambiada?.Invoke(completo.Etapa); ContextoCambiado?.Invoke(completo.Contexto); break;
            case TiposMensaje.Log: LogGenerado?.Invoke(JsonSerializer.Deserialize<LogEntry>(mensaje.Json, _opciones)!); break;
            case TiposMensaje.EstadoBot: var bot = JsonSerializer.Deserialize<EstadoBot>(mensaje.Json, _opciones); EnMarcha = bot == EstadoBot.Iniciado; EstadoBotCambiado?.Invoke(bot); break;
            case TiposMensaje.EstadoEjecucion: EstadoEjecucionCambiado?.Invoke(JsonSerializer.Deserialize<EstadoEjecucion>(mensaje.Json, _opciones)); break;
            case TiposMensaje.Etapa: EtapaCambiada?.Invoke(JsonSerializer.Deserialize<EtapaProceso>(mensaje.Json, _opciones)); break;
            case TiposMensaje.Contexto: ContextoCambiado?.Invoke(JsonSerializer.Deserialize<ContextoProceso>(mensaje.Json, _opciones)); break;
        }
    }

    private void Enviar<T>(string tipo, T? valor = default)
    {
        var canal = _canal; if (canal is null || !canal.IsConnected) return;
        _ = Task.Run(async () => { using var escritor = new StreamWriter(canal, leaveOpen: true) { AutoFlush = true }; await escritor.WriteLineAsync(JsonSerializer.Serialize(new Mensaje(tipo, valor is null ? null : JsonSerializer.Serialize(valor, _opciones)), _opciones)); });
    }
}
