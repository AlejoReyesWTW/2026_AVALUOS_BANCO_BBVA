using BotBBVA.Panel.ViewModels;

namespace BotBBVA.Pruebas;

public sealed class RelayCommandTests
{
    [Fact]
    public void Execute_EjecutaLaAccion()
    {
        bool ejecutado = false;
        var comando = new RelayCommand(() => ejecutado = true);

        comando.Execute(null);

        Assert.True(ejecutado);
    }

    [Fact]
    public void CanExecute_RespetaElPredicado()
    {
        bool habilitado = false;
        var comando = new RelayCommand(() => { }, () => habilitado);

        Assert.False(comando.CanExecute(null));

        habilitado = true;
        Assert.True(comando.CanExecute(null));
    }

    [Fact]
    public void RaiseCanExecuteChanged_DisparaElEvento()
    {
        var comando = new RelayCommand(() => { });
        bool disparado = false;
        comando.CanExecuteChanged += (_, _) => disparado = true;

        comando.RaiseCanExecuteChanged();

        Assert.True(disparado);
    }
}
