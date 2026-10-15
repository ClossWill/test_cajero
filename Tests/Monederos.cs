using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using IntegracionCajeroPruebas.Protocolos;

namespace IntegracionCajeroPruebas.Tests;

// Dispensadores de monedas MK4 (x2) por ccTalk sobre puerto COM.
public static partial class HardwareTests
{
    private static MonederosConfig Monederos => HardwareConfig.Actual.Monederos;

    public static void Test_PingMonederos()
    {
        var r = new Reporte("Monederos MK4 - Ping ccTalk");
        var buses = new Dictionary<string, CcTalkBus>();
        try
        {
            foreach (var h in Monederos.Hoppers)
            {
                var bus = ObtenerBus(r, buses, h);
                if (bus is null) continue;

                var responde = r.Criterio($"{h.Nombre} responde a Simple Poll (dir {h.Direccion}, {h.Puerto})",
                    bus.SimplePoll(h.Direccion), bus.UltimoError);
                if (!responde) continue;

                Consola.Info($"        Categoría : {bus.LeerTexto(h.Direccion, CcTalkBus.HRequestEquipmentCategory)}");
                Consola.Info($"        Producto  : {bus.LeerTexto(h.Direccion, CcTalkBus.HRequestProductCode)}");
                Consola.Info($"        Software  : {bus.LeerTexto(h.Direccion, CcTalkBus.HRequestSoftwareRevision)}");
                var serie = bus.Enviar(h.Direccion, CcTalkBus.HRequestSerialNumber);
                Consola.Info($"        Serie     : {(serie is null ? "n/d" : Consola.Hex(serie.Datos))}");
            }
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        finally { foreach (var b in buses.Values) b.Dispose(); }
        r.Finalizar();
    }

    public static void Test_DispensarMonedas(int cantidad_1_MXN, int cantidad_5_MXN)
    {
        var r = new Reporte($"Monederos MK4 - Dispensar {cantidad_1_MXN} x $1 y {cantidad_5_MXN} x $5");
        var buses = new Dictionary<string, CcTalkBus>();
        try
        {
            foreach (var (denominacion, cantidad) in new[] { (1, cantidad_1_MXN), (5, cantidad_5_MXN) })
            {
                if (cantidad <= 0) continue;
                if (cantidad > Monederos.MaxMonedasPrueba)
                {
                    r.Criterio($"Cantidad de ${denominacion} <= {Monederos.MaxMonedasPrueba} (límite de seguridad)", false);
                    continue;
                }

                var h = Monederos.Hoppers.FirstOrDefault(x => x.Denominacion == denominacion);
                if (!r.Criterio($"Hopper de ${denominacion} configurado en hardware.json", h is not null)) continue;

                var bus = ObtenerBus(r, buses, h!);
                if (bus is not null) DispensarEnHopper(r, bus, h!, cantidad);
            }
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        finally { foreach (var b in buses.Values) b.Dispose(); }
        r.Finalizar();
    }

    // ------------------------------------------------------------------ helpers

    private static CcTalkBus? ObtenerBus(Reporte r, Dictionary<string, CcTalkBus> buses, HopperConfig h)
    {
        if (buses.TryGetValue(h.Puerto, out var existente)) return existente;
        try
        {
            var bus = new CcTalkBus(h.Puerto, Monederos.BaudRate);
            buses[h.Puerto] = bus;
            return bus;
        }
        catch (Exception ex)
        {
            r.Criterio($"Apertura de {h.Puerto} para {h.Nombre}", false, ex.Message);
            return null;
        }
    }

    // Secuencia ccTalk de hopper MK2/MK4 (doc. Shanghai Elan "MK2&MK4 Coin Dispenser Programming Flow"):
    // Test hopper (163) -> Enable (164, dato 165) -> Dispense (167, 8 bytes en 0 + cantidad; sin cifrado) -> sondeo 166 cada 200 ms.
    // El MK4 responde al Dispense con 1 byte de datos (contador de eventos), no con ACK vacío.
    private static void DispensarEnHopper(Reporte r, CcTalkBus bus, HopperConfig h, int cantidad)
    {
        var test = bus.Enviar(h.Direccion, CcTalkBus.HTestHopper);
        if (!r.Criterio($"{h.Nombre}: Test Hopper responde", test is { Datos.Length: >= 2 }, bus.UltimoError)) return;
        Consola.Info($"        Flags de error: {Consola.Hex(test!.Datos)} (byte1 bit3/4/5 = fraude/opto bloqueado)");
        var nivel = bus.Enviar(h.Direccion, CcTalkBus.HRequestHighLowStatus);
        if (nivel is { Datos.Length: 1 })
            Consola.Info($"        Nivel de monedas: {nivel.Datos[0] switch { 0x30 => "suficiente", 0x31 => "casi vacío", 0x32 => "lleno", var x => $"desconocido ({x:X2})" }}");

        var enable = bus.Enviar(h.Direccion, CcTalkBus.HEnableHopper, new byte[] { 165 });
        if (!r.Criterio($"{h.Nombre}: Enable Hopper aceptado (ACK)", enable?.EsAck == true, bus.UltimoError)) return;

        var datos = new byte[9];
        datos[8] = (byte)cantidad;
        var disp = bus.Enviar(h.Direccion, CcTalkBus.HDispenseHopperCoins, datos);
        if (!r.Criterio($"{h.Nombre}: comando Dispense aceptado (header 0, no NAK)", disp is { Header: 0 },
                disp is null ? bus.UltimoError : $"header respuesta={disp.Header} datos={Consola.Hex(disp.Datos)}")) return;

        // Doc: sondear cada 200 ms; reintentar si no hay respuesta (el motor genera ruido); ~10 fallos seguidos = falla de hardware.
        var limite = DateTime.UtcNow.AddSeconds(cantidad + 8);
        byte pagadas = 0, noPagadas = 0, restantes = (byte)cantidad;
        int fallosSeguidos = 0;
        while (DateTime.UtcNow < limite && fallosSeguidos < 10)
        {
            var estado = bus.Enviar(h.Direccion, CcTalkBus.HRequestHopperStatus, null, 100);
            if (estado is { Datos.Length: >= 4 })
            {
                fallosSeguidos = 0;
                restantes = estado.Datos[1]; pagadas = estado.Datos[2]; noPagadas = estado.Datos[3];
                if (restantes == 0) break;
                Thread.Sleep(200);
            }
            else fallosSeguidos++;
        }

        r.Criterio($"{h.Nombre}: el hopper reporta {cantidad} moneda(s) pagadas y 0 sin pagar",
            pagadas == cantidad && noPagadas == 0 && restantes == 0,
            $"pagadas={pagadas} sinPagar={noPagadas} restantes={restantes}");
        var caidas = Consola.PreguntarNumeroAsistente($"¿Cuántas monedas de ${h.Denominacion} cayeron en la bandeja?");
        r.Criterio($"{h.Nombre}: el asistente cuenta {cantidad} moneda(s) de ${h.Denominacion} en la bandeja",
            caidas == cantidad, $"asistente={caidas?.ToString() ?? "sin respuesta"}");
    }
}
