using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using IntegracionCajeroPruebas.Protocolos;

namespace IntegracionCajeroPruebas.Tests;

// Tarjeta "18-Channel LED Light Bar Control Board" (modelo 24002-2, Shenzhen Hongzhou): controla los
// indicadores de función del cajero (encendido fijo / parpadeo / apagado). No es el ALB02: es una tarjeta
// RS232 aparte, en otro puerto COM. Ver Protocolos/LedControlSerial.cs.
public static partial class HardwareTests
{
    public static void Test_PingLeds()
    {
        var cfg = HardwareConfig.Actual.Leds;
        var r = new Reporte($"LEDs (tarjeta 24002-2) - Ping ({cfg.Puerto} @ {cfg.BaudRate})");
        try
        {
            using var bus = new LedControlBus(cfg.Puerto, cfg.BaudRate);
            var version = bus.ConsultarFirmware();
            r.Criterio("Firmware responde a la consulta de versión", version is not null, bus.UltimoError);
            if (version is not null) Consola.Info($"        Versión: {version}");
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    public static void Test_ControlarLeds()
    {
        var cfg = HardwareConfig.Actual.Leds;
        var r = new Reporte($"Control de LEDs (18 canales) - {cfg.Puerto} @ {cfg.BaudRate}");
        try
        {
            using var bus = new LedControlBus(cfg.Puerto, cfg.BaudRate);

            var version = bus.ConsultarFirmware();
            r.Criterio("Firmware responde a la consulta de versión", version is not null, bus.UltimoError);
            if (version is not null) Consola.Info($"        Versión: {version}");

            // Solo 5 de los 18 canales están conectados a un ícono identificable en el gabinete (recibo,
            // monedas, pasaporte/INE, QR, efectivo); cuáles son esos 5 no está documentado en ningún lado
            // (confirmado contra la hoja del fabricante de la tarjeta: solo define OUT1-OUT18 genéricos). No
            // hay forma de leer por software cuál ícono enciende cada canal: el dev observa el gabinete
            // directamente durante el barrido y anota el mapeo a mano: cada canal queda encendido
            // Leds.SegundosPausaBarrido (60 s por defecto) y se anuncia en consola cuál es (OUT n).
            Consola.Aviso($"  >> Observe el gabinete: se enciende un canal a la vez y queda {cfg.SegundosPausaBarrido} s encendido; " +
                          "la consola indica cuál es. Enter adelanta al siguiente canal.");

            var fallaron = new List<byte>();
            try
            {
                for (byte canal = 1; canal <= cfg.Canales; canal++)
                {
                    cfg.Indicadores.TryGetValue(canal, out var nombre);
                    if (nombre is null && cfg.Indicadores.Count > 0 && !cfg.BarrerTodosLosCanales) continue;

                    var ok = bus.Encender(canal);
                    Consola.Info(nombre is null
                        ? $"        >> OUT{canal} ENCENDIDO ({canal}/{cfg.Canales}): anote qué ícono/luz es"
                        : $"        >> OUT{canal} ENCENDIDO ({canal}/{cfg.Canales}): debe iluminar {nombre}");
                    EsperarBarrido(cfg.SegundosPausaBarrido);
                    ok &= bus.Apagar(canal);
                    Consola.Info($"        OUT{canal} apagado");
                    if (!ok) fallaron.Add(canal);
                }
            }
            finally { bus.Apagar(LedControlBus.ParaTodos); }

            r.Criterio("Todos los canales aceptaron encender y apagar (ACK)",
                fallaron.Count == 0, fallaron.Count == 0 ? null : $"canales sin ACK: {string.Join(',', fallaron)}");

            r.Criterio("Comando \"parpadear todos\" aceptado (ACK)", bus.Parpadear(LedControlBus.ParaTodos), bus.UltimoError);
            Thread.Sleep(3000);
            bus.Apagar(LedControlBus.ParaTodos);
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    // Espera entre canales del barrido; Enter la corta. Con entrada redirigida (KeyAvailable lanza) espera completa.
    private static void EsperarBarrido(int segundos)
    {
        var fin = DateTime.UtcNow.AddSeconds(segundos);
        while (DateTime.UtcNow < fin)
        {
            try
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter) return;
            }
            catch (InvalidOperationException) { }
            Thread.Sleep(200);
        }
    }
}
