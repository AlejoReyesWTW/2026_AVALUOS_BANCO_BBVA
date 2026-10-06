namespace BotBBVA.Contratos;

/// <summary>
/// Nivel de una entrada del log técnico, usado para colorear la consola.
/// </summary>
public enum NivelLog
{
    /// <summary>Mensaje informativo (azul).</summary>
    Informacion,

    /// <summary>Operación completada con éxito (verde).</summary>
    Exito,

    /// <summary>Aviso o registro pendiente de atención (ámbar).</summary>
    Advertencia,

    /// <summary>Fallo en algún paso del flujo (rojo).</summary>
    Error
}
