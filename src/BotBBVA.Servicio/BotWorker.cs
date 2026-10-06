using Microsoft.Extensions.Hosting;

namespace BotBBVA.Servicio;

/// <summary>Hospeda el orquestador y el servidor de control.</summary>
public sealed class BotWorker : BackgroundService
{
    private readonly OrquestadorBot _orquestador;
    private readonly CanalControlServidor _canal;
    private readonly EstadoPersistenteServicio _persistencia;
    public BotWorker(OrquestadorBot orquestador, CanalControlServidor canal, EstadoPersistenteServicio persistencia)
    {
        _orquestador = orquestador;
        _canal = canal;
        _persistencia = persistencia;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_persistencia.CargarEstado() == BotBBVA.Contratos.EstadoBot.Iniciado)
        {
            _orquestador.Iniciar();
        }

        return _canal.EjecutarAsync(stoppingToken);
    }
    public override Task StopAsync(CancellationToken cancellationToken) { _orquestador.Detener(); _orquestador.Dispose(); _canal.Dispose(); return base.StopAsync(cancellationToken); }
}
