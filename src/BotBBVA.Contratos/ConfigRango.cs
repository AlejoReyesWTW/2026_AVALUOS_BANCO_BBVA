using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BotBBVA.Contratos;

/// <summary>
/// Configuración editable de un rango de páginas: la cantidad de archivos
/// por lote DocLM. El rango en sí es fijo y no se puede modificar.
/// </summary>
public sealed class ConfigRango : INotifyPropertyChanged
{
    private int _archivosPorLote;

    public ConfigRango(RangoPaginas rango, int archivosPorLote)
    {
        Rango = rango;
        Etiqueta = RangoPaginasInfo.Etiqueta(rango);
        EsEditable = rango != RangoPaginas.Mayor200;
        _archivosPorLote = archivosPorLote;
    }

    public RangoPaginas Rango { get; }

    public string Etiqueta { get; }

    /// <summary>Indica si el valor se puede editar (el rango >200 es fijo en 0).</summary>
    public bool EsEditable { get; }

    /// <summary>Cantidad de archivos por lote DocLM (1–5 en rangos procesables, 0 en >200).</summary>
    public int ArchivosPorLote
    {
        get => _archivosPorLote;
        set
        {
            if (_archivosPorLote == value)
            {
                return;
            }

            _archivosPorLote = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ArchivosPorLote)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
