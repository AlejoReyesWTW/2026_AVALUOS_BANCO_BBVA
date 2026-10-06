namespace BotBBVA.Contratos;

/// <summary>
/// Estado visual de cada etapa del procesamiento.
/// </summary>
public enum EstadoEtapa
{
    /// <summary>Aún no se ejecuta.</summary>
    Pendiente,

    /// <summary>Es la etapa actual del bot.</summary>
    EnCurso,

    /// <summary>La etapa ya terminó.</summary>
    Completado
}
