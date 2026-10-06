using BotBBVA.Contratos;

namespace BotBBVA.Pruebas;

public sealed class RangoPaginasTests
{
    [Theory]
    [InlineData(1, RangoPaginas.De1a50)]
    [InlineData(50, RangoPaginas.De1a50)]
    [InlineData(51, RangoPaginas.De51a100)]
    [InlineData(100, RangoPaginas.De51a100)]
    [InlineData(101, RangoPaginas.De101a200)]
    [InlineData(200, RangoPaginas.De101a200)]
    [InlineData(201, RangoPaginas.Mayor200)]
    public void Clasificar_AgrupaSegunLimites(int paginas, RangoPaginas esperado)
    {
        Assert.Equal(esperado, RangoPaginasInfo.Clasificar(paginas));
    }

    [Fact]
    public void Etiqueta_DevuelveTextoLegible()
    {
        Assert.Equal("1–50", RangoPaginasInfo.Etiqueta(RangoPaginas.De1a50));
        Assert.Equal("> 200", RangoPaginasInfo.Etiqueta(RangoPaginas.Mayor200));
    }
}
