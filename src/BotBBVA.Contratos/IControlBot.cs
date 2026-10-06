namespace BotBBVA.Contratos;

/// <summary>
/// Contrato de control del bot. Lo implementan tanto el orquestador (en el
/// servicio de Windows) como el cliente (en el panel), de modo que el panel
/// hable con el servicio igual que antes hablaba con el motor local.
/// </summary>
public interface IControlBot : IDisposable
{
    /// <summary>Se invoca con cada entrada de log generada.</summary>
    event Action<LogEntry>? LogGenerado;

    /// <summary>Se invoca cuando cambia el estado del bot (iniciado/detenido).</summary>
    event Action<EstadoBot>? EstadoBotCambiado;

    /// <summary>Se invoca cuando cambia el estado de ejecución.</summary>
    event Action<EstadoEjecucion>? EstadoEjecucionCambiado;

    /// <summary>Se invoca al cambiar la etapa actual del procesamiento.</summary>
    event Action<EtapaProceso>? EtapaCambiada;

    /// <summary>Se invoca al actualizarse el contexto del lote en curso.</summary>
    event Action<ContextoProceso>? ContextoCambiado;

    /// <summary>Indica si el motor está en marcha.</summary>
    bool EnMarcha { get; }

    /// <summary>Configuración operativa que gobierna el ciclo.</summary>
    ConfigOperativa Configuracion { get; }

    /// <summary>Activa el bot para que respete la ventana de ejecución.</summary>
    void Iniciar();

    /// <summary>Solicita una parada ordenada (termina el lote actual antes de detenerse).</summary>
    void Detener();
}
