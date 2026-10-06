using BotBBVA.Contratos;

namespace BotBBVA.Pruebas;

public sealed class ConfigOperativaTests
{
    [Fact]
    public void DentroDeVentana_MismoDia_RespetaLosLimites()
    {
        var config = new ConfigOperativa { HoraInicio = new TimeSpan(6, 0, 0), HoraFin = new TimeSpan(22, 0, 0) };

        Assert.False(config.VentanaCruzaMedianoche);
        Assert.True(config.DentroDeVentana(new TimeSpan(6, 0, 0)));
        Assert.True(config.DentroDeVentana(new TimeSpan(12, 0, 0)));
        Assert.False(config.DentroDeVentana(new TimeSpan(5, 59, 59)));
        Assert.False(config.DentroDeVentana(new TimeSpan(22, 0, 0)));
    }

    [Fact]
    public void DentroDeVentana_CruzaMedianoche_CubreAmbosTramos()
    {
        var config = new ConfigOperativa { HoraInicio = new TimeSpan(20, 0, 0), HoraFin = new TimeSpan(6, 0, 0) };

        Assert.True(config.VentanaCruzaMedianoche);
        Assert.True(config.DentroDeVentana(new TimeSpan(23, 0, 0)));
        Assert.True(config.DentroDeVentana(new TimeSpan(3, 0, 0)));
        Assert.False(config.DentroDeVentana(new TimeSpan(12, 0, 0)));
    }

    [Fact]
    public void ArchivosPorLote_DevuelveElValorConfiguradoPorRango()
    {
        var config = new ConfigOperativa();
        config.Rangos.First(r => r.Rango == RangoPaginas.De1a50).ArchivosPorLote = 5;
        config.Rangos.First(r => r.Rango == RangoPaginas.Mayor200).ArchivosPorLote = 0;

        Assert.Equal(5, config.ArchivosPorLote(RangoPaginas.De1a50));
        Assert.Equal(0, config.ArchivosPorLote(RangoPaginas.Mayor200));
    }
}
