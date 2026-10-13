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

    // Secuencia ccTalk de hopper: serie (242) -> Enable (164) -> Dispense (167) -> sondeo de estado (166).
    // SUPUESTO A VERIFICAR con el manual del MK4: Dispense = [serie x3][cantidad] sin cifrado.
    // Si el MK4 responde NAK, probablemente exige el modo seguro (Pump RNG 161 / Request cipher key 160).
    private static void DispensarEnHopper(Reporte r, CcTalkBus bus, HopperConfig h, int cantidad)
    {
        var serie = bus.Enviar(h.Direccion, CcTalkBus.HRequestSerialNumber);
        if (!r.Criterio($"{h.Nombre}: lectura del número de serie", serie is { Datos.Length: >= 3 }, bus.UltimoError)) return;

        var enable = bus.Enviar(h.Direccion, CcTalkBus.HEnableHopper, new byte[] { 165 });
        if (!r.Criterio($"{h.Nombre}: Enable Hopper aceptado (ACK)", enable?.EsAck == true, bus.UltimoError)) return;

        var datos = serie!.Datos.Take(3).Append((byte)cantidad).ToArray();
        var disp = bus.Enviar(h.Direccion, CcTalkBus.HDispenseHopperCoins, datos);
        if (!r.Criterio($"{h.Nombre}: comando Dispense aceptado (ACK)", disp?.EsAck == true,
                disp is null ? bus.UltimoError : $"header respuesta={disp.Header} datos={Consola.Hex(disp.Datos)}")) return;

        // El hopper entrega ~3 monedas/s; margen amplio para atascos.
        var limite = DateTime.UtcNow.AddSeconds(cantidad + 8);
        byte pagadas = 0, noPagadas = 0, restantes = (byte)cantidad;
        while (DateTime.UtcNow < limite)
        {
            var estado = bus.Enviar(h.Direccion, CcTalkBus.HRequestHopperStatus);
            if (estado is { Datos.Length: >= 4 })
            {
                restantes = estado.Datos[1]; pagadas = estado.Datos[2]; noPagadas = estado.Datos[3];
                if (restantes == 0) break;
            }
            Thread.Sleep(250);
        }

        r.Criterio($"{h.Nombre}: el hopper reporta {cantidad} moneda(s) pagadas y 0 sin pagar",
            pagadas == cantidad && noPagadas == 0 && restantes == 0,
            $"pagadas={pagadas} sinPagar={noPagadas} restantes={restantes}");
        r.Criterio($"{h.Nombre}: confirmación física de {cantidad} moneda(s) de ${h.Denominacion} en la bandeja",
            Consola.Confirmar($"¿Cayeron {cantidad} moneda(s) de ${h.Denominacion}?"));
    }
}
