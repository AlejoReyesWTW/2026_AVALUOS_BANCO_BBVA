namespace BotBBVA.Contratos;

/// <summary>
/// Resultado final de un documento procesado por el motor simulado.
/// </summary>
public enum ResultadoProceso
{
    /// <summary>DocLM devolvió datos y la fila se actualizó.</summary>
    Actualizado,

    /// <summary>DocLM respondió sin valores útiles.</summary>
    SinDatos,

    /// <summary>El registro no tiene ruta de archivo.</summary>
    SinRuta,

    /// <summary>El documento supera las 200 páginas y se excluye de DocLM.</summary>
    ExcluidoPorPaginas,

    /// <summary>Falló algún paso (archivo, red, DocLM, base de datos).</summary>
    Error
}
