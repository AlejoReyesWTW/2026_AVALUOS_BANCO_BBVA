using BotBBVA.Contratos;

namespace BotBBVA.Pruebas;

public sealed class ContadoresDiariosTests
{
    [Fact]
    public void Registrar_Actualizado_IncrementaSoloActualizados()
    {
        var contadores = new ContadoresDiarios();

        contadores.Registrar(ResultadoProceso.Actualizado);

        Assert.Equal(1, contadores.Actualizados);
        Assert.Equal(0, contadores.SinDatos);
        Assert.Equal(0, contadores.SinRuta);
        Assert.Equal(0, contadores.ExcluidosPorPaginas);
        Assert.Equal(0, contadores.Errores);
    }

    [Fact]
    public void Registrar_CadaResultado_IncrementaSuContador()
    {
        var contadores = new ContadoresDiarios();

        contadores.Registrar(ResultadoProceso.SinDatos);
        contadores.Registrar(ResultadoProceso.SinRuta);
        contadores.Registrar(ResultadoProceso.ExcluidoPorPaginas);
        contadores.Registrar(ResultadoProceso.Error);

        Assert.Equal(1, contadores.SinDatos);
        Assert.Equal(1, contadores.SinRuta);
        Assert.Equal(1, contadores.ExcluidosPorPaginas);
        Assert.Equal(1, contadores.Errores);
        Assert.Equal(4, contadores.Procesados);
    }

    [Fact]
    public void Reiniciar_DejaTodosLosContadoresEnCero()
    {
        var contadores = new ContadoresDiarios();
        contadores.Registrar(ResultadoProceso.Actualizado);
        contadores.Registrar(ResultadoProceso.ExcluidoPorPaginas);

        contadores.Reiniciar();

        Assert.Equal(0, contadores.Actualizados);
        Assert.Equal(0, contadores.ExcluidosPorPaginas);
        Assert.Equal(0, contadores.Procesados);
    }
}
