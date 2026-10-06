using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BotBBVA.Contratos;

/// <summary>
/// Contadores del día que alimentan los indicadores del panel.
/// </summary>
public sealed class ContadoresDiarios : INotifyPropertyChanged
{
    private int _actualizados;
    private int _sinDatos;
    private int _sinRuta;
    private int _excluidosPorPaginas;
    private int _errores;

    public int Actualizados
    {
        get => _actualizados;
        private set => Establecer(ref _actualizados, value);
    }

    public int SinDatos
    {
        get => _sinDatos;
        private set => Establecer(ref _sinDatos, value);
    }

    public int SinRuta
    {
        get => _sinRuta;
        private set => Establecer(ref _sinRuta, value);
    }

    public int ExcluidosPorPaginas
    {
        get => _excluidosPorPaginas;
        private set => Establecer(ref _excluidosPorPaginas, value);
    }

    public int Errores
    {
        get => _errores;
        private set => Establecer(ref _errores, value);
    }

    /// <summary>
    /// Total de documentos procesados (suma de todos los contadores).
    /// </summary>
    public int Procesados => Actualizados + SinDatos + SinRuta + ExcluidosPorPaginas + Errores;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Incrementa el contador correspondiente al resultado dado.
    /// </summary>
    public void Registrar(ResultadoProceso resultado)
    {
        switch (resultado)
        {
            case ResultadoProceso.Actualizado:
                Actualizados++;
                break;
            case ResultadoProceso.SinDatos:
                SinDatos++;
                break;
            case ResultadoProceso.SinRuta:
                SinRuta++;
                break;
            case ResultadoProceso.ExcluidoPorPaginas:
                ExcluidosPorPaginas++;
                break;
            case ResultadoProceso.Error:
                Errores++;
                break;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Procesados)));
    }

    /// <summary>
    /// Restablece todos los contadores a cero.
    /// </summary>
    public void Reiniciar()
    {
        Actualizados = 0;
        SinDatos = 0;
        SinRuta = 0;
        ExcluidosPorPaginas = 0;
        Errores = 0;
    }

    private void Establecer<T>(ref T campo, T valor, [CallerMemberName] string? nombre = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor))
        {
            return;
        }

        campo = valor;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
    }
}
