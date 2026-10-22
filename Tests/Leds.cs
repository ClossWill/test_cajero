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
            // directamente por videollamada durante el barrido (sin pausas para confirmar) y anota el mapeo
            // a mano si lo necesita.
            Consola.Aviso("  >> Observe el gabinete: se van a encender los 18 canales uno por uno, con pausa breve.");

            var fallaron = new List<byte>();
            for (byte canal = 1; canal <= cfg.Canales; canal++)
            {
                var ok = bus.Encender(canal);
                Thread.Sleep(800);
                ok &= bus.Apagar(canal);
                if (!ok) fallaron.Add(canal);
            }

            r.Criterio("Todos los canales aceptaron encender y apagar (ACK)",
                fallaron.Count == 0, fallaron.Count == 0 ? null : $"canales sin ACK: {string.Join(',', fallaron)}");

            r.Criterio("Comando \"parpadear todos\" aceptado (ACK)", bus.Parpadear(LedControlBus.ParaTodos), bus.UltimoError);
            Thread.Sleep(3000);
            bus.Apagar(LedControlBus.ParaTodos);
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }
}
