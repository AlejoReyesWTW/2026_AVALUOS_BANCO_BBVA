namespace BotBBVA.Contratos;

/// <summary>
/// Estado de ejecución del bot, independiente de si está iniciado o detenido.
/// </summary>
public enum EstadoEjecucion
{
    /// <summary>El bot está detenido.</summary>
    Detenido,

    /// <summary>Iniciado, pero fuera de la ventana de ejecución configurada.</summary>
    FueraDeHorario,

    /// <summary>Procesando dentro de la ventana de ejecución.</summary>
    Ejecutando,

    /// <summary>Parada ordenada en curso (termina el lote actual antes de detenerse).</summary>
    Deteniendo
}
