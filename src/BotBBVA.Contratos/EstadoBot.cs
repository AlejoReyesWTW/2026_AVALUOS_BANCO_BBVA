namespace BotBBVA.Contratos;

/// <summary>
/// Estado del bot: si está activado para procesar o detenido.
/// </summary>
public enum EstadoBot
{
    /// <summary>El bot no está procesando.</summary>
    Detenido,

    /// <summary>El bot está activado y respeta la ventana de ejecución.</summary>
    Iniciado
}
