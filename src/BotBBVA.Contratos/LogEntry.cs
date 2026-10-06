namespace BotBBVA.Contratos;

/// <summary>
/// Entrada individual del log técnico en vivo.
/// </summary>
public sealed record LogEntry(
    DateTime FechaHora,
    NivelLog Nivel,
    long? RegistroId,
    string Mensaje,
    ResultadoProceso? Resultado,
    string? Documento = null)
{
    /// <summary>
    /// Hora formateada para la consola (HH:mm:ss).
    /// </summary>
    public string Hora => FechaHora.ToString("HH:mm:ss");

    /// <summary>
    /// Etiqueta corta del nivel entre corchetes (por ejemplo "[INFO]").
    /// </summary>
    public string EtiquetaNivel => Nivel switch
    {
        NivelLog.Informacion => "[INFO]",
        NivelLog.Exito => "[OK]",
        NivelLog.Advertencia => "[AVISO]",
        NivelLog.Error => "[ERROR]",
        _ => $"[{Nivel.ToString().ToUpperInvariant()}]"
    };

    /// <summary>
    /// Etiqueta del registro (por ejemplo "[REG-1001]") o cadena vacía si no aplica.
    /// </summary>
    public string EtiquetaRegistro => RegistroId.HasValue ? $"[REG-{RegistroId.Value}]" : string.Empty;

    /// <summary>
    /// Estado del documento, cuando la entrada corresponde a un resultado.
    /// </summary>
    public string EtiquetaEstado => Resultado switch
    {
        ResultadoProceso.Actualizado => "Actualizado",
        ResultadoProceso.SinDatos => "Sin datos",
        ResultadoProceso.SinRuta => "Sin ruta",
        ResultadoProceso.ExcluidoPorPaginas => "Excluido (>200)",
        ResultadoProceso.Error => "Error",
        _ => string.Empty
    };
}
