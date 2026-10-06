namespace BotBBVA.Contratos;

/// <summary>
/// Instantánea completa del estado del bot que el servicio envía al panel
/// al conectarse o cuando este lo solicita con ObtenerEstado.
/// </summary>
public sealed record EstadoCompleto(
    EstadoBot EstadoBot,
    EstadoEjecucion EstadoEjecucion,
    EtapaProceso Etapa,
    ContextoProceso Contexto,
    ConfigOperativa Configuracion);
