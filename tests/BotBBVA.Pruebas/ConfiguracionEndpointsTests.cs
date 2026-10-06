using BotBBVA.Contratos;
using BotBBVA.Panel.ViewModels;

namespace BotBBVA.Pruebas;

public sealed class ConfiguracionEndpointsTests
{
    [Fact]
    public void ConfigOperativa_Endpoints_ContieneLosCincoPorDefecto()
    {
        var config = new ConfigOperativa();

        Assert.Equal(5, config.Endpoints.Count);
        Assert.Contains(config.Endpoints, e => e.Nombre == "Token de autenticación");
        Assert.Contains(config.Endpoints, e => e.Nombre == "Crear documento");
        Assert.Contains(config.Endpoints, e => e.Nombre == "Consultar documento");
        Assert.Contains(config.Endpoints, e => e.Nombre == "Ejecutar assessment");
        Assert.Contains(config.Endpoints, e => e.Nombre == "Consultar assessment");
    }

    [Fact]
    public void ConfigOperativa_Endpoints_TienenMetodoHttp()
    {
        var config = new ConfigOperativa();

        Assert.Equal("POST", config.Endpoints.First(e => e.Nombre == "Crear documento").Metodo);
        Assert.Equal("GET", config.Endpoints.First(e => e.Nombre == "Consultar documento").Metodo);
        Assert.Equal("POST", config.Endpoints.First(e => e.Nombre == "Ejecutar assessment").Metodo);
        Assert.Equal("GET", config.Endpoints.First(e => e.Nombre == "Consultar assessment").Metodo);
    }

    [Fact]
    public void ConfigOperativa_IntervaloConsulta_PorDefectoEsVeinte()
    {
        var config = new ConfigOperativa();

        Assert.Equal(20, config.IntervaloConsultaSegundos);
    }

    [Fact]
    public void ConfigOperativa_Scope_PorDefectoEsElDeDocLm()
    {
        var config = new ConfigOperativa();

        Assert.Equal("api://wtw-crbra-doclm-p/.default", config.Scope);
    }

    [Fact]
    public void EndpointEditable_Editar_HabilitaLaEdicion()
    {
        var endpoint = new EndpointEditable("Token", "POST", "https://ejemplo.com", () => { });

        Assert.True(endpoint.EsSoloLectura);

        endpoint.EditarCommand.Execute(null);

        Assert.False(endpoint.EsSoloLectura);
    }

    [Fact]
    public void EndpointEditable_CambiarUrl_NotificaCambio()
    {
        bool notificado = false;
        var endpoint = new EndpointEditable("Token", "POST", "https://ejemplo.com", () => notificado = true);

        endpoint.Url = "https://nuevo.com";

        Assert.True(notificado);
        Assert.True(endpoint.TieneCambios);
    }

    [Fact]
    public void EndpointEditable_MarcarGuardado_ReseteaElCambio()
    {
        var endpoint = new EndpointEditable("Token", "POST", "https://ejemplo.com", () => { });
        endpoint.EditarCommand.Execute(null);
        endpoint.Url = "https://nuevo.com";

        endpoint.MarcarGuardado();

        Assert.False(endpoint.TieneCambios);
        Assert.Equal("https://nuevo.com", endpoint.UrlOriginal);
        Assert.True(endpoint.EsSoloLectura);
    }
}
