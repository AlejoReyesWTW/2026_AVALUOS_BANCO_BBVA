using System.IO.Pipes;
using System.Text.Json;
using BotBBVA.Contratos;

namespace BotBBVA.Servicio;

/// <summary>Servidor de comandos y eventos mediante Named Pipe.</summary>
public sealed class CanalControlServidor : IDisposable
{
    private readonly OrquestadorBot _orquestador;
    private readonly EstadoPersistenteServicio _persistencia;
    private readonly JsonSerializerOptions _opciones = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _escritura = new(1, 1);
    private NamedPipeServerStream? _canal;

    public CanalControlServidor(OrquestadorBot orquestador, EstadoPersistenteServicio persistencia) { _orquestador = orquestador; _persistencia = persistencia; SuscribirEventos(); }

    public async Task EjecutarAsync(CancellationToken cancelacion)
    {
        while (!cancelacion.IsCancellationRequested)
        {
            try
            {
                using var canal = new NamedPipeServerStream("BotBBVA.Control", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                _canal = canal;
                await canal.WaitForConnectionAsync(cancelacion);
                await EnviarEstadoAsync(canal, cancelacion);
                using var lector = new StreamReader(canal);
                while (canal.IsConnected && !cancelacion.IsCancellationRequested)
                {
                    var linea = await lector.ReadLineAsync(cancelacion);
                    if (linea is null) break;
                    var mensaje = JsonSerializer.Deserialize<Mensaje>(linea, _opciones);
                    if (mensaje is not null) await ProcesarAsync(mensaje, canal, cancelacion);
                }
            }
            catch (OperationCanceledException) when (cancelacion.IsCancellationRequested) { break; }
            catch (IOException) { await Task.Delay(250, cancelacion); }
            finally { _canal = null; }
        }
    }

    private async Task ProcesarAsync(Mensaje mensaje, NamedPipeServerStream canal, CancellationToken token)
    {
        switch (mensaje.Tipo)
        {
            case TiposMensaje.IniciarBot: _orquestador.Iniciar(); break;
            case TiposMensaje.DetenerBot: _orquestador.Detener(); break;
            case TiposMensaje.ObtenerEstado: await EnviarEstadoAsync(canal, token); break;
            case TiposMensaje.GuardarConfiguracion:
                if (mensaje.Json is not null)
                {
                    var config = JsonSerializer.Deserialize<ConfigOperativa>(mensaje.Json, _opciones);
                    if (config is not null) { _persistencia.GuardarConfiguracion(config); }
                }
                break;
        }
    }

    private async Task EnviarEstadoAsync(NamedPipeServerStream canal, CancellationToken token)
    {
        var estado = _orquestador.ObtenerEstadoCompleto();
        await EnviarAsync(new Mensaje(TiposMensaje.EstadoCompleto, JsonSerializer.Serialize(estado, _opciones)), canal, token);
    }

    private void SuscribirEventos()
    {
        _orquestador.LogGenerado += e => _ = EnviarEventoAsync(TiposMensaje.Log, e);
        _orquestador.EstadoBotCambiado += e => { _persistencia.GuardarEstado(e); _ = EnviarEventoAsync(TiposMensaje.EstadoBot, e); };
        _orquestador.EstadoEjecucionCambiado += e => _ = EnviarEventoAsync(TiposMensaje.EstadoEjecucion, e);
        _orquestador.EtapaCambiada += e => _ = EnviarEventoAsync(TiposMensaje.Etapa, e);
        _orquestador.ContextoCambiado += e => _ = EnviarEventoAsync(TiposMensaje.Contexto, e);
    }

    private async Task EnviarEventoAsync<T>(string tipo, T valor)
    {
        var canal = _canal;
        if (canal is null || !canal.IsConnected) return;
        try { await EnviarAsync(new Mensaje(tipo, JsonSerializer.Serialize(valor, _opciones)), canal, CancellationToken.None); } catch (IOException) { }
    }

    private async Task EnviarAsync(Mensaje mensaje, NamedPipeServerStream canal, CancellationToken token)
    {
        await _escritura.WaitAsync(token);
        try { using var escritor = new StreamWriter(canal, leaveOpen: true) { AutoFlush = true }; await escritor.WriteLineAsync(JsonSerializer.Serialize(mensaje, _opciones)); }
        finally { _escritura.Release(); }
    }

    public void Dispose() { _canal?.Dispose(); _escritura.Dispose(); }
}
