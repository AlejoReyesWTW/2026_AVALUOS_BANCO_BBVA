namespace BotBBVA.Contratos;

/// <summary>
/// Configuración operativa del bot: ventana de ejecución, archivos por lote,
/// endpoints de DocLM e intervalo de consulta de estado.
/// </summary>
public sealed class ConfigOperativa
{
    /// <summary>Hora de inicio de la ventana de ejecución (formato 24 h).</summary>
    public TimeSpan HoraInicio { get; set; } = new(6, 0, 0);

    /// <summary>Hora de fin de la ventana de ejecución (formato 24 h).</summary>
    public TimeSpan HoraFin { get; set; } = new(22, 0, 0);

    /// <summary>Rangos de páginas con su cantidad de archivos por lote.</summary>
    public List<ConfigRango> Rangos { get; } =
    [
        new ConfigRango(RangoPaginas.De1a50, 3),
        new ConfigRango(RangoPaginas.De51a100, 2),
        new ConfigRango(RangoPaginas.De101a200, 1),
        new ConfigRango(RangoPaginas.Mayor200, 0)
    ];

    /// <summary>Endpoints externos (token OAuth y operaciones de la API DocLM).</summary>
    public List<ConfigEndpoint> Endpoints { get; } =
    [
        new("Token de autenticación", "POST", "https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token"),
        new("Crear documento", "POST", "https://api.doclm.ai.wtwco.com/v2/COLDOCSPROD/documents"),
        new("Consultar documento", "GET", "https://api.doclm.ai.wtwco.com/v2/COLDOCSPROD/documents/{id_document}"),
        new("Ejecutar assessment", "POST", "https://api.doclm.ai.wtwco.com/v2/COLDOCSPROD/documents/{id_document}/assessments?assessment_id={id_assessment}&batch=true"),
        new("Consultar assessment", "GET", "https://api.doclm.ai.wtwco.com/v2/COLDOCSPROD/documents/{id_document}/assessments/{id_run}")
    ];

    /// <summary>Espera entre consultas de estado de DocLM, en segundos.</summary>
    public int IntervaloConsultaSegundos { get; set; } = 20;

    /// <summary>Tenant de Entra ID para autenticación DocLM (secreto).</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Client id de la aplicación DocLM (secreto).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client secret de DocLM (muy sensible, nunca en logs ni en el repo).</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Scope OAuth solicitado a Entra ID (fijo para DocLM).</summary>
    public string Scope { get; set; } = "api://wtw-crbra-doclm-p/.default";

    /// <summary>Indica si la ventana cruza medianoche (por ejemplo 20:00–06:00).</summary>
    public bool VentanaCruzaMedianoche => HoraFin < HoraInicio;

    /// <summary>Devuelve los archivos por lote configurados para un rango.</summary>
    public int ArchivosPorLote(RangoPaginas rango)
        => Rangos.First(r => r.Rango == rango).ArchivosPorLote;

    /// <summary>
    /// Indica si una hora determinada cae dentro de la ventana de ejecución,
    /// contemplando ventanas que cruzan medianoche.
    /// </summary>
    public bool DentroDeVentana(TimeSpan hora)
    {
        if (VentanaCruzaMedianoche)
        {
            return hora >= HoraInicio || hora < HoraFin;
        }

        return hora >= HoraInicio && hora < HoraFin;
    }
}
