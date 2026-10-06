using BotBBVA.Contratos;

namespace BotBBVA.Panel.ViewModels;

/// <summary>
/// Estado visual de una etapa del procesamiento (sección 5.2).
/// </summary>
public sealed class EtapaVisual : ObservableObject
{
    private EstadoEtapa _estado;

    public EtapaVisual(EtapaProceso etapa)
    {
        Etapa = etapa;
    }

    public EtapaProceso Etapa { get; }

    public string Etiqueta => EtapaProcesoInfo.Etiqueta(Etapa);

    public int Numero => (int)Etapa;

    public EstadoEtapa Estado
    {
        get => _estado;
        set => Establecer(ref _estado, value);
    }
}
