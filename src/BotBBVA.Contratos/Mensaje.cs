namespace BotBBVA.Contratos;

/// <summary>
/// Tipos de mensaje del canal de control panel ↔ servicio (Named Pipes).
/// </summary>
public static class TiposMensaje
{
    // Comandos (panel → servicio)
    public const string IniciarBot = "IniciarBot";
    public const string DetenerBot = "DetenerBot";
    public const string ObtenerEstado = "ObtenerEstado";
    public const string GuardarConfiguracion = "GuardarConfiguracion";

    // Eventos (servicio → panel)
    public const string EstadoCompleto = "EstadoCompleto";
    public const string EstadoBot = "EstadoBot";
    public const string EstadoEjecucion = "EstadoEjecucion";
    public const string Etapa = "Etapa";
    public const string Contexto = "Contexto";
    public const string Log = "Log";
}

/// <summary>
/// Mensaje serializado en JSON que viaja por el canal de control.
/// </summary>
public sealed record Mensaje(string Tipo, string? Json);
