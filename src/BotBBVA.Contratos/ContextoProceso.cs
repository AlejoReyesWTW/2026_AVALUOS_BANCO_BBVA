namespace BotBBVA.Contratos;

/// <summary>
/// Contexto del lote que se está procesando actualmente (sección 5.3).
/// </summary>
public sealed record ContextoProceso(
    string IdTarea,
    string Documentos,
    string RangoEtiqueta,
    int ArchivosLote,
    int CapacidadLote,
    string TiempoTranscurrido,
    string MensajeEtapa)
{
    /// <summary>Contexto vacío, usado cuando no hay un lote en ejecución.</summary>
    public static ContextoProceso Vacio { get; } = new(string.Empty, string.Empty, string.Empty, 0, 0, string.Empty, string.Empty);
}
