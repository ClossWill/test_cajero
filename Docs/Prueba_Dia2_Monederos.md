# Prueba Día 2 – Dispensadores de monedas MK4 (x2)

## Objetivo

Confirmar que el PC se comunica por puerto serial con los dos hoppers MK4 (ping y datos de identificación) y que se puede ordenar la entrega de N monedas de $1 y de $5, verificando el conteo reportado por el hopper y la caída física.

## Protocolo / librería

- **ccTalk** sobre puerto COM (8N1, normalmente 9600 baudios). Implementación mínima propia en `Protocolos/CcTalk.cs` (sin dependencias): trama `[destino][nº datos][origen=1][header][datos…][checksum]`, con suma total ≡ 0 mod 256. El código descarta el eco de la propia trama, habitual en la línea ccTalk de un solo hilo.
- Headers usados: `254` Simple poll, `245` categoría de equipo, `244` código de producto, `241` revisión de software, `242` número de serie, `164` Enable hopper, `167` Dispense hopper coins, `166` Request hopper status.
- Puerto: `System.IO.Ports` (paquete NuGet `System.IO.Ports` 8.0.0).

## Precondiciones

- Hoppers alimentados (fuente Mean Well) y cableados al convertidor ccTalk/USB-serial; puerto identificado en el Administrador de dispositivos.
- Cada hopper con **dirección distinta** (se asumen 3 y 4) y con monedas cargadas (≥ 10 de $1 y de $5).
- Ninguna otra aplicación usando el COM.

## Configuración

```json
"Monederos": {
  "BaudRate": 9600,
  "MaxMonedasPrueba": 10,
  "Hoppers": [
    { "Nombre": "MK4 #1 ($1 MXN)", "Denominacion": 1, "Puerto": "COM3", "Direccion": 3 },
    { "Nombre": "MK4 #2 ($5 MXN)", "Denominacion": 5, "Puerto": "COM3", "Direccion": 4 }
  ]
}
```

Si cada hopper está en un COM distinto, basta cambiar `Puerto`. El código abre cada puerto una sola vez aunque lo compartan.

## Código

### `Protocolos/CcTalk.cs`

```csharp
using System.Diagnostics;
using System.IO.Ports;

namespace IntegracionCajeroPruebas.Protocolos;

/// <summary>Respuesta ccTalk ya validada. Header 0 sin datos = ACK; Header 5 = NAK; Header 6 = BUSY.</summary>
public sealed record CcTalkRespuesta(byte Origen, byte Header, byte[] Datos)
{
    public bool EsAck => Header == 0 && Datos.Length == 0;
    public bool EsNak => Header == 5;
}

/// <summary>Cliente ccTalk mínimo (trama sin cifrado, checksum simple de 8 bits). Maestro = dirección 1.</summary>
public sealed class CcTalkBus : IDisposable
{
    public const byte Host = 1;

    // Headers del estándar ccTalk (Generic Specification) usados en las pruebas.
    public const byte HSimplePoll = 254;
    public const byte HRequestEquipmentCategory = 245;
    public const byte HRequestProductCode = 244;
    public const byte HRequestSerialNumber = 242;
    public const byte HRequestSoftwareRevision = 241;
    public const byte HEnableHopper = 164;
    public const byte HRequestHopperStatus = 166;
    public const byte HDispenseHopperCoins = 167;

    private readonly SerialPort _puerto;

    public string UltimoError { get; private set; } = "";

    public CcTalkBus(string puerto, int baudRate)
    {
        _puerto = new SerialPort(puerto, baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 500,
            WriteTimeout = 500,
        };
        _puerto.Open();
    }

    /// <summary>[destino][nº datos][origen][header][datos...][checksum] con suma total = 0 (mod 256).</summary>
    public static byte[] ConstruirTrama(byte destino, byte header, byte[]? datos = null)
    {
        datos ??= Array.Empty<byte>();
        var trama = new byte[5 + datos.Length];
        trama[0] = destino;
        trama[1] = (byte)datos.Length;
        trama[2] = Host;
        trama[3] = header;
        datos.CopyTo(trama, 4);
        trama[^1] = (byte)((256 - trama.Take(trama.Length - 1).Sum(b => b) % 256) % 256);
        return trama;
    }

    public CcTalkRespuesta? Enviar(byte destino, byte header, byte[]? datos = null, int timeoutMs = 500)
    {
        var tx = ConstruirTrama(destino, header, datos);
        _puerto.DiscardInBuffer();
        _puerto.Write(tx, 0, tx.Length);

        var rx = LeerRespuesta(tx, timeoutMs);
        if (rx is null)
        {
            UltimoError = $"Sin respuesta a header {header} (dir {destino}) en {timeoutMs} ms";
            return null;
        }
        if (rx.Sum(b => b) % 256 != 0 || rx[0] != Host)
        {
            UltimoError = "Respuesta con checksum o destino inválido: " + BitConverter.ToString(rx);
            return null;
        }

        UltimoError = "";
        return new CcTalkRespuesta(rx[2], rx[3], rx[4..^1]);
    }

    public bool SimplePoll(byte direccion) => Enviar(direccion, HSimplePoll)?.EsAck == true;

    public string? LeerTexto(byte direccion, byte header)
    {
        var r = Enviar(direccion, header);
        return r is { EsNak: false } ? System.Text.Encoding.ASCII.GetString(r.Datos) : null;
    }

    /// <summary>
    /// Lee una trama de respuesta completa. En ccTalk (línea de un solo hilo) el maestro suele recibir
    /// primero su propio eco; si la trama enviada aparece al inicio del buffer se descarta.
    /// </summary>
    private byte[]? LeerRespuesta(byte[] tx, int timeoutMs)
    {
        var buf = new List<byte>();
        var reloj = Stopwatch.StartNew();

        while (reloj.ElapsedMilliseconds < timeoutMs)
        {
            var n = _puerto.BytesToRead;
            if (n > 0)
            {
                var tmp = new byte[n];
                _puerto.Read(tmp, 0, n);
                buf.AddRange(tmp);
            }

            var offset = buf.Count >= tx.Length && buf.Take(tx.Length).SequenceEqual(tx) ? tx.Length : 0;
            var disponibles = buf.Count - offset;
            if (disponibles >= 2 && disponibles >= 5 + buf[offset + 1])
                return buf.GetRange(offset, 5 + buf[offset + 1]).ToArray();

            Thread.Sleep(2);
        }
        return null;
    }

    public void Dispose() => _puerto.Dispose();
}
```

### `Tests/Monederos.cs`

```csharp
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
```

## Cómo ejecutar

Menú `4` (ping) y `5` (dispensar; pide cantidades). Equivalente desde `Main`:

```csharp
HardwareTests.Test_PingMonederos();
HardwareTests.Test_DispensarMonedas(2, 1); // dos monedas de $1, una de $5
```

## Criterios de aceptación

| ID | Criterio | Cómo se verifica |
| :-- | :-- | :-- |
| M-01 | El puerto COM abre sin excepción | Criterio automático |
| M-02 | Cada hopper responde ACK a Simple Poll en su dirección | Criterio automático |
| M-03 | Se lee categoría, código de producto, revisión de software y número de serie | Salida de consola (la categoría suele ser `Payout`) |
| M-04 | `Enable Hopper` y `Dispense` responden ACK (no NAK) | Criterio automático |
| M-05 | Al terminar, `Request hopper status` reporta `cantidad` monedas pagadas, 0 sin pagar y 0 restantes | Criterio automático |
| M-06 | Las monedas físicas caídas coinciden con lo pedido, y cada hopper entrega su denominación | Confirmación por consola |
| M-07 | Dispensar solo $1 (5, 0) y solo $5 (0, 2) por separado funciona; ambos hoppers a la vez también | Tres ejecuciones |
| M-08 | Velocidad coherente con ~3 monedas/s | Observación |
| M-09 | *(negativa)* Con el hopper de $1 vacío o casi vacío, pedir más monedas de las que tiene reporta `sin pagar > 0` y falla de forma controlada, sin colgarse ni marcar éxito | Requiere dejar pocas monedas. El TAC §14 exige probar "monedero $1 agotado" |

**Aprobado** = M-01 a M-08 (M-09 es recomendable: el TAC exige que una entrega parcial nunca se dé por completa). Guardar la trama hexadecimal de cualquier NAK o timeout.

## Supuestos y riesgos

- **Confirmado por el TAC §2:** Monedero 1 entrega MXN 1 y Monedero 2 entrega MXN 5; la configuración de `Hoppers` coincide.
- **Falta la fuente de los comandos:** el BEEX indica que los bytes específicos están en "las tablas de Excel de la carpeta de integración", que no se han recibido. Esas tablas deben contrastarse con `CcTalk.cs` antes de dar por buenos los puntos siguientes. Además el BEEX recomienda usar una librería ccTalk de código abierto; este código usa una implementación mínima propia.
- **Formato del comando Dispense (167):** se asume `[serie x3][cantidad]` sin cifrado. Si el MK4 responde NAK, requiere modo seguro (Pump RNG `161`, Request cipher key `160`); sería un trabajo adicional del día 2.
- Direcciones 3 y 4 son una suposición; si nadie responde, barrer las direcciones 1–10 con Simple Poll.
- Conviene que el primer intento use cantidades pequeñas (1 moneda) y vigilar atascos.

## Evidencia a guardar

Salida de consola de ping y dispensado, video corto de la bandeja y las tramas crudas en caso de falla.
