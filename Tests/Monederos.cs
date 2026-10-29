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

    /// <summary>
    /// Solo lectura (no hace Reset ni Enable, el motor no gira): nivel por sensores (217), contador de monedas
    /// pagadas de por vida (168), valor de moneda configurado (169) y flags de Test Hopper (163). El MK4 no
    /// reporta cuántas monedas contiene; el nivel es solo "casi vacío / suficiente / lleno".
    /// </summary>
    public static void Test_NivelMonederos()
    {
        var r = new Reporte("Monederos MK4 - Nivel de monedas (solo lectura)");
        var buses = new Dictionary<string, CcTalkBus>();
        try
        {
            foreach (var h in Monederos.Hoppers)
            {
                var bus = ObtenerBus(r, buses, h);
                if (bus is null) continue;
                if (!r.Criterio($"{h.Nombre} responde a Simple Poll (dir {h.Direccion}, {h.Puerto})",
                        bus.SimplePoll(h.Direccion), bus.UltimoError)) continue;

                var nivel = bus.Enviar(h.Direccion, CcTalkBus.HRequestHighLowStatus);
                if (r.Criterio($"{h.Nombre}: sensor de nivel (header 217) responde",
                        nivel is { EsNak: false, Datos.Length: 1 },
                        nivel is null ? bus.UltimoError : $"header respuesta={nivel.Header} datos={Consola.Hex(nivel.Datos)}"))
                    Consola.Info($"        Nivel de monedas: {nivel!.Datos[0] switch { 0x30 => "suficiente", 0x31 => "casi vacío", 0x32 => "lleno", var x => $"desconocido ({x:X2})" }}");

                // Informativos: no son criterio de aprobación, porque el estándar los marca opcionales.
                var cuenta = bus.Enviar(h.Direccion, CcTalkBus.HRequestHopperDispenseCount);
                if (cuenta is { EsNak: false, Datos.Length: 3 })
                    Consola.Info($"        Monedas pagadas de por vida (header 168): {cuenta.Datos[0] | cuenta.Datos[1] << 8 | cuenta.Datos[2] << 16}");
                else
                    Consola.Aviso($"        Contador de monedas pagadas (header 168): no disponible ({(cuenta is null ? bus.UltimoError : $"datos={Consola.Hex(cuenta.Datos)}")})");

                var valor = bus.Enviar(h.Direccion, CcTalkBus.HRequestHopperCoinValue);
                Consola.Info($"        Valor de moneda (header 169): {(valor is { EsNak: false } ? $"{System.Text.Encoding.ASCII.GetString(valor.Datos.Where(b => b is >= 0x20 and < 0x7F).ToArray())} [{Consola.Hex(valor.Datos)}]" : "no disponible")}");

                var test = bus.Enviar(h.Direccion, CcTalkBus.HTestHopper);
                if (test is { EsNak: false }) Consola.Info($"        Flags de Test Hopper (header 163): {Consola.Hex(test.Datos)}");
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
    // Reset (1) -> Test hopper (163) -> Enable (164, dato 165) -> Request cipher key (160) ->
    // Dispense (167, 8 bytes en 0 + cantidad; sin cifrado) -> sondeo 166 cada 200 ms.
    // El doc exige el Request cipher key antes de CADA Dispense aunque el hopper no cifre (la llave se ignora
    // y se mandan 8 ceros), y reintentarlo hasta 10 veces porque a veces no responde.
    // El doc del fabricante es explícito: con flags de error activos (fraude/opto bloqueado), el hopper
    // NAK-ea cualquier comando hasta recibir Reset ("要不然，找零器不执行任何指令").
    // El MK4 responde al Dispense con 1 byte de datos (contador de eventos), no con ACK vacío.
    private static void DispensarEnHopper(Reporte r, CcTalkBus bus, HopperConfig h, int cantidad)
    {
        var reset = bus.Enviar(h.Direccion, CcTalkBus.HResetDevice);
        if (!r.Criterio($"{h.Nombre}: Reset aceptado (ACK)", reset?.EsAck == true, bus.UltimoError)) return;

        // Tras el Reset el hopper hace un ciclo interno (recentrado/auto-chequeo) y no procesa comandos
        // durante ese lapso: una pausa breve + reintentos evita marcar falla por un simple "todavía ocupado".
        Thread.Sleep(400);
        CcTalkRespuesta? test = null;
        for (var intento = 0; intento < 3 && test is null; intento++)
        {
            test = bus.Enviar(h.Direccion, CcTalkBus.HTestHopper);
            if (test is null) Thread.Sleep(200);
        }
        if (!r.Criterio($"{h.Nombre}: Test Hopper responde", test is { Datos.Length: >= 2 }, bus.UltimoError)) return;
        Consola.Info($"        Flags de error: {Consola.Hex(test!.Datos)} (byte1 bit3/4/5 = fraude/opto bloqueado)");
        var nivel = bus.Enviar(h.Direccion, CcTalkBus.HRequestHighLowStatus);
        var nivelBajo = nivel is { Datos.Length: 1 } && nivel.Datos[0] == 0x31;
        if (nivel is { Datos.Length: 1 })
            Consola.Info($"        Nivel de monedas: {nivel.Datos[0] switch { 0x30 => "suficiente", 0x31 => "casi vacío", 0x32 => "lleno", var x => $"desconocido ({x:X2})" }}");

        var enable = bus.Enviar(h.Direccion, CcTalkBus.HEnableHopper, new byte[] { 165 });
        if (!r.Criterio($"{h.Nombre}: Enable Hopper aceptado (ACK)", enable?.EsAck == true, bus.UltimoError)) return;

        CcTalkRespuesta? llave = null;
        for (var intento = 0; intento < 10 && llave is not { Datos.Length: 8 }; intento++)
            llave = bus.Enviar(h.Direccion, CcTalkBus.HRequestCipherKey);
        if (!r.Criterio($"{h.Nombre}: Request cipher key responde (8 bytes)", llave is { Datos.Length: 8 },
                llave is null ? bus.UltimoError : $"header respuesta={llave.Header} datos={Consola.Hex(llave.Datos)}")) return;

        var datos = new byte[9];
        datos[8] = (byte)cantidad;
        var disp = bus.Enviar(h.Direccion, CcTalkBus.HDispenseHopperCoins, datos);
        if (!r.Criterio($"{h.Nombre}: comando Dispense aceptado (header 0, no NAK)", disp is { Header: 0 },
                disp is null ? bus.UltimoError : $"header respuesta={disp.Header} datos={Consola.Hex(disp.Datos)}")) return;

        // Doc: sondear cada 200 ms; reintentar si no hay respuesta (el motor genera ruido); ~10 fallos seguidos = falla de hardware.
        // Ventana amplia: sin monedas el motor sigue girando hasta su propio timeout interno antes de rendirse.
        var limite = DateTime.UtcNow.AddSeconds(cantidad + 20);
        byte pagadas = 0, noPagadas = 0, restantes = (byte)cantidad;
        int fallosSeguidos = 0, respuestas = 0;
        byte[]? ultimo = null;
        while (DateTime.UtcNow < limite && fallosSeguidos < 10)
        {
            var estado = bus.Enviar(h.Direccion, CcTalkBus.HRequestHopperStatus, null, 200);
            if (estado is { Datos.Length: >= 4 })
            {
                fallosSeguidos = 0;
                respuestas++;
                ultimo = estado.Datos;
                restantes = estado.Datos[1]; pagadas = estado.Datos[2]; noPagadas = estado.Datos[3];
                if (restantes == 0) break;
            }
            else fallosSeguidos++;
            Thread.Sleep(200);
        }
        var motivo = restantes == 0 ? "el hopper terminó"
            : fallosSeguidos >= 10 ? "10 sondeos seguidos sin respuesta"
            : "se agotó la ventana de espera con el hopper aún pagando";
        Consola.Info($"        Sondeo de estado: {respuestas} respuesta(s), último={(ultimo is null ? "ninguno" : Consola.Hex(ultimo))}, fin: {motivo}");

        // Generic spec ccTalk, Test hopper byte1 bit1 = timeout de pago (el motor giró sin detectar monedas saliendo).
        var testFinal = bus.Enviar(h.Direccion, CcTalkBus.HTestHopper);
        if (testFinal is { Datos.Length: >= 2 })
            Consola.Info($"        Flags después de dispensar: {Consola.Hex(testFinal.Datos)} (byte1 bit1 = timeout de pago / sin monedas)");

        var detalle = $"pagadas={pagadas} sinPagar={noPagadas} restantes={restantes}";
        // Con sensor de nivel bajo y el hopper respondiendo, pagar menos de lo pedido es falta de inventario,
        // no falla de comunicación (el doc dice que en nivel bajo el hopper ya no debe usarse).
        if (nivelBajo && respuestas > 0 && pagadas < cantidad)
        {
            Consola.Aviso($"        {h.Nombre}: no hay monedas suficientes — pagó {pagadas} de {cantidad} ({detalle}). Rellenar el hopper.");
            return;
        }
        r.Criterio($"{h.Nombre}: el hopper reporta {cantidad} moneda(s) pagadas y 0 sin pagar",
            pagadas == cantidad && noPagadas == 0 && restantes == 0, detalle);
    }
}
