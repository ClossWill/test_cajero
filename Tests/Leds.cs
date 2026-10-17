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
            // (confirmado contra la hoja del fabricante de la tarjeta: solo define OUT1-OUT18 genéricos). Se
            // prueba uno por uno, parado el flujo en cada canal: se enciende, se le pregunta al asistente
            // (que está viendo el gabinete) si vio alguna luz, y si dice que sí se le pregunta cuál. Se
            // detiene en cuanto se identifican los periféricos esperados, sin necesidad de agotar los 18.
            Consola.Asistente("Párese donde pueda ver los 5 íconos del gabinete (recibo, moneda, pasaporte, "
                + "QR, efectivo). Voy a encender los canales uno por uno.");

            var mapeo = new Dictionary<byte, string>();
            var fallaron = new List<byte>();
            byte canal = 1;
            for (; canal <= cfg.Canales && mapeo.Count < cfg.PerifericosEsperados; canal++)
            {
                var ok = bus.Encender(canal);
                var vioLuz = Consola.PreguntarAsistente($"Canal {canal}: ¿se encendió alguna luz?");
                if (vioLuz)
                {
                    var icono = Consola.PreguntarTextoAsistente("¿Cuál de los 5 (recibo, moneda, pasaporte, QR, efectivo)?");
                    mapeo[canal] = icono;
                    Consola.Info($"        Canal {canal} -> {icono}");
                }
                ok &= bus.Apagar(canal);
                if (!ok) fallaron.Add(canal);
            }

            r.Criterio($"Se identificaron los {cfg.PerifericosEsperados} periféricos esperados",
                mapeo.Count >= cfg.PerifericosEsperados,
                mapeo.Count >= cfg.PerifericosEsperados ? null
                    : $"solo {mapeo.Count}/{cfg.PerifericosEsperados}, probados canales 1-{canal - 1}: "
                      + string.Join(", ", mapeo.Select(kv => $"{kv.Key}={kv.Value}")));
            r.Criterio("Los canales probados aceptaron encender y apagar (ACK)",
                fallaron.Count == 0, fallaron.Count == 0 ? null : $"canales sin ACK: {string.Join(',', fallaron)}");

            r.Criterio("Comando \"parpadear todos\" aceptado (ACK)", bus.Parpadear(LedControlBus.ParaTodos), bus.UltimoError);
            Thread.Sleep(3000);
            bus.Apagar(LedControlBus.ParaTodos);
            r.Criterio("El asistente vio parpadear los indicadores",
                Consola.PreguntarAsistente("¿Vio parpadear los indicadores durante unos segundos?"));

            if (mapeo.Count > 0)
            {
                Consola.Info("        Mapeo canal -> ícono:");
                foreach (var (c, icono) in mapeo.OrderBy(kv => kv.Key))
                    Consola.Info($"          Canal {c,2} = {icono}");
            }
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }
}
