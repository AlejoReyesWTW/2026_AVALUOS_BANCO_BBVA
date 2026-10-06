namespace BotBBVA.Contratos;

/// <summary>
/// Rangos fijos de páginas definidos en la sección 3.3.1. No son editables.
/// </summary>
public enum RangoPaginas
{
    De1a50 = 1,
    De51a100 = 2,
    De101a200 = 3,
    Mayor200 = 4
}

/// <summary>
/// Utilidades de los rangos de páginas.
/// </summary>
public static class RangoPaginasInfo
{
    /// <summary>
    /// Etiqueta del rango para mostrar en tablas y contexto.
    /// </summary>
    public static string Etiqueta(RangoPaginas rango) => rango switch
    {
        RangoPaginas.De1a50 => "1–50",
        RangoPaginas.De51a100 => "51–100",
        RangoPaginas.De101a200 => "101–200",
        RangoPaginas.Mayor200 => "> 200",
        _ => rango.ToString()
    };

    /// <summary>
    /// Clasifica una cantidad de páginas en su rango correspondiente.
    /// </summary>
    public static RangoPaginas Clasificar(int paginas) => paginas switch
    {
        <= 50 => RangoPaginas.De1a50,
        <= 100 => RangoPaginas.De51a100,
        <= 200 => RangoPaginas.De101a200,
        _ => RangoPaginas.Mayor200
    };
}
