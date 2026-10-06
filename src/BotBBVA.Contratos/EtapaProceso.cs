namespace BotBBVA.Contratos;

/// <summary>
/// Etapas del procesamiento de un lote, en el orden definido en la sección 5.2.
/// </summary>
public enum EtapaProceso
{
    ConsultandoSp = 1,
    ObteniendoArchivo = 2,
    ValidandoArchivos = 3,
    EnviandoDocLm = 4,
    EsperandoDocLm = 5,
    LeyendoInformacion = 6,
    BaseActualizada = 7,
    GenerandoExcel = 8,
    ExcelGenerado = 9,
    FinProceso = 10
}

/// <summary>
/// Utilidades de presentación y navegación de las etapas.
/// </summary>
public static class EtapaProcesoInfo
{
    /// <summary>
    /// Etiqueta corta de la etapa para mostrar en el componente de progreso.
    /// </summary>
    public static string Etiqueta(EtapaProceso etapa) => etapa switch
    {
        EtapaProceso.ConsultandoSp => "Consultando SP",
        EtapaProceso.ObteniendoArchivo => "Obteniendo archivo",
        EtapaProceso.ValidandoArchivos => "Validando archivos",
        EtapaProceso.EnviandoDocLm => "Enviando a DocLM",
        EtapaProceso.EsperandoDocLm => "Esperando DocLM",
        EtapaProceso.LeyendoInformacion => "Leyendo información",
        EtapaProceso.BaseActualizada => "BD actualizada",
        EtapaProceso.GenerandoExcel => "Generando Excel",
        EtapaProceso.ExcelGenerado => "Excel generado",
        EtapaProceso.FinProceso => "Fin de proceso",
        _ => etapa.ToString()
    };

    /// <summary>
    /// Devuelve la lista de todas las etapas en orden.
    /// </summary>
    public static IReadOnlyList<EtapaProceso> Todas { get; } =
        Enum.GetValues<EtapaProceso>().OrderBy(e => (int)e).ToArray();
}
