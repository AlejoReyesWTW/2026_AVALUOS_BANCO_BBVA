using BotBBVA.Contratos;
using BotBBVA.Servicio;

namespace BotBBVA.Pruebas;

public sealed class MotorSimuladoTests
{
    [Fact]
    public void Iniciar_GeneraLogsYActivaElBot()
    {
        using var motor = new OrquestadorBot(ConfigSiempreDentro());
        var logs = new List<LogEntry>();
        EstadoBot? estadoBot = null;

        motor.LogGenerado += e => { lock (logs) { logs.Add(e); } };
        motor.EstadoBotCambiado += e => estadoBot = e;

        motor.Iniciar();

        Assert.True(EsperarHasta(() => estadoBot == EstadoBot.Iniciado && logs.Count > 0));
        motor.Detener();
    }

    [Fact]
    public void Detener_SolicitaParadaOrdenada()
    {
        using var motor = new OrquestadorBot(ConfigSiempreDentro());
        EstadoEjecucion? ejecucion = null;

        motor.EstadoEjecucionCambiado += e => ejecucion = e;
        motor.Iniciar();

        Assert.True(EsperarHasta(() => ejecucion == EstadoEjecucion.Ejecutando));
        motor.Detener();

        Assert.Equal(EstadoEjecucion.Deteniendo, ejecucion);
    }

    [Fact]
    public void Iniciar_ProduceResultadosDeProceso()
    {
        using var motor = new OrquestadorBot(ConfigSiempreDentro());
        var resultados = new List<ResultadoProceso>();

        motor.LogGenerado += e =>
        {
            lock (resultados)
            {
                if (e.Resultado is { } resultado)
                {
                    resultados.Add(resultado);
                }
            }
        };

        motor.Iniciar();
        Assert.True(EsperarHasta(() => resultados.Count > 0));
        motor.Detener();
    }

    /// <summary>
    /// Configuración con una ventana que cubre prácticamente todo el día,
    /// para que las pruebas no dependan de la hora real de ejecución.
    /// </summary>
    private static ConfigOperativa ConfigSiempreDentro()
        => new() { HoraInicio = TimeSpan.Zero, HoraFin = new TimeSpan(23, 59, 59) };

    private static bool EsperarHasta(Func<bool> condicion, int tiempoMaximoMs = 8000)
    {
        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        while (cronometro.ElapsedMilliseconds < tiempoMaximoMs)
        {
            if (condicion())
            {
                return true;
            }

            Thread.Sleep(50);
        }

        return condicion();
    }
}
