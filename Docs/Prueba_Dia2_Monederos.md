# Prueba Día 2 – Dispensadores de monedas MK4 (x2)

## Objetivo

Confirmar que el PC se comunica por puerto serial con los dos hoppers MK4 (ping y datos de identificación) y que se puede ordenar la entrega de N monedas de $1 y de $5, verificando el conteo reportado por el hopper y la caída física.

## Protocolo / librería

- **ccTalk** sobre puerto COM (8N1, normalmente 9600 baudios). Implementación mínima propia en `Protocolos/CcTalk.cs` (sin dependencias): trama `[destino][nº datos][origen=1][header][datos…][checksum]`, con suma total ≡ 0 mod 256. El código descarta el eco de la propia trama, habitual en la línea ccTalk de un solo hilo.
- Headers usados: `254` Simple poll, `245` categoría de equipo, `244` código de producto, `241` revisión de software, `242` número de serie, `163` Test hopper, `164` Enable hopper, `167` Dispense hopper coins, `166` Request hopper status, `217` Request high/low status (nivel de monedas).
- Fuente de los comandos: `referencias_integracion/CoinDispenserMK4` (Shanghai Elan: "MK2 & MK4 Coin Dispensor Commands", "Programming Flow", "cctalk address"). Cubre poll, reset, enable, dispense, estado, test hopper y nivel de monedas.
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
    { "Nombre": "MK4 #1 ($1 MXN)", "Denominacion": 1, "Puerto": "COM1", "Direccion": 3 },
    { "Nombre": "MK4 #2 ($5 MXN)", "Denominacion": 5, "Puerto": "COM1", "Direccion": 4 }
  ]
}
```

Si cada hopper está en un COM distinto, basta cambiar `Puerto`. El código abre cada puerto una sola vez aunque lo compartan.

**Confirmado en la máquina real (2026-10-16):** el puerto es `COM1`, no `COM3` (el valor original era un supuesto nunca verificado). `COM1` es uno de los puertos de la tarjeta serial multi-puerto interna (`ACPI\PNP0501` en Device Manager), no del adaptador USB CP210x (ese expone `COM13`/`COM14`, de otro periférico — se probaron por error antes de encontrar el correcto). Se encontró comparando contra el software oficial del fabricante del hopper (**SCH2**, en `D:\SCH2`): Setup → puerto COM → `Hopper Address` 3 → Poll/Reset/Enable, todos `OK`. Direcciones 3 y 4 confirmadas con `Test_PingMonederos` (menú `4`): responden `Payout / SUH1-NOENCRYPT / SUH1-U3.0`.

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
    public const byte HResetDevice = 1;
    public const byte HSimplePoll = 254;
    public const byte HRequestEquipmentCategory = 245;
    public const byte HRequestProductCode = 244;
    public const byte HRequestSerialNumber = 242;
    public const byte HRequestSoftwareRevision = 241;
    public const byte HTestHopper = 163;
    public const byte HEnableHopper = 164;
    public const byte HRequestHopperStatus = 166;
    public const byte HRequestHighLowStatus = 217;
    public const byte HDispenseHopperCoins = 167;

    private readonly SerialPort _puerto;

    public string UltimoError { get; private set; } = "";

    public CcTalkBus(string puerto, int baudRate)
    {
        _puerto = new SerialPort(puerto, baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 500,
            WriteTimeout = 500,
            // Confirmado en la máquina real (2026-10-16): sin esto, Simple Poll no obtenía respuesta en
            // ningún puerto probado (COM3, COM13, COM14), aunque el puerto abría sin error. El adaptador
            // RS232<->ccTalk de esta máquina alimenta o habilita su transceptor desde DTR/RTS, que .NET deja
            // en false por defecto. Con esto activado + el puerto real (COM1, no COM3), Simple Poll respondió.
            DtrEnable = true,
            RtsEnable = true,
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
```

## Modalidad remota (desarrollador a distancia + asistente en sitio)

`Test_PingMonederos` no requiere al asistente: es puramente eléctrico/serial. `Test_DispensarMonedas` sí lo necesita, una vez por hopper, solo para contar lo que cayó:

| Momento | Qué pasa en consola | Qué hace el asistente |
| :-- | :-- | :-- |
| Al terminar de dispensar cada hopper | "¿Cuántas monedas de \$N cayeron en la bandeja?" | Las cuenta sin tocarlas y dice el número (ver `Guia_Asistente_Sitio.md`, sección Monederos) |

No hay `AntesDeMover`: el mecanismo del hopper es un tambor cerrado, no hay un shutter donde el asistente pueda tener la mano dentro.

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
- **Dispense (167), confirmado por el fabricante:** 9 bytes de datos = 8 en `00` + cantidad (1–255), sin cifrado (modo por defecto). El MK4 responde con 1 byte de datos (contador de eventos), por eso no se exige ACK vacío. Sondear `166` cada 200 ms con reintentos (~10 seguidos antes de declarar falla). El BEEX recomienda usar una librería ccTalk de código abierto; este código usa una implementación mínima propia, ya validada contra el manual del fabricante.
- Las direcciones ccTalk se fijan por pines del hopper (tabla en "MK2 cctalk address.pdf": 3 = por defecto, 4, 5...). Cada hopper debe tener una distinta; si nadie responde, barrer 1–10 con Simple Poll.
- Conviene que el primer intento use cantidades pequeñas (1 moneda) y vigilar atascos.
- **`DtrEnable`/`RtsEnable` necesarios, confirmado en la máquina real (2026-10-16):** sin activarlos al abrir el puerto, Simple Poll no obtenía respuesta en ningún COM probado, aunque el puerto abría sin error — el adaptador RS232↔ccTalk de esta máquina alimenta o habilita su transceptor desde esas líneas, que .NET deja en `false` por defecto. Ya corregido en `CcTalk.cs`.
- **`Test_PingMonederos` confirmado en la máquina real (2026-10-16):** los dos hoppers responden en `COM1`, direcciones 3 y 4 (`APROBADO 2/2`). Falta ejecutar `Test_DispensarMonedas` frente al hardware.
- Mientras se buscaba el puerto correcto, un programa externo (SCH2 u otro) puede dejar el COM abierto en exclusiva: si `Test_PingMonederos` falla con "Apertura de COMx" (no con "sin respuesta"), revisar que no haya otra herramienta de prueba serial abierta al mismo tiempo.

## Evidencia a guardar

Salida de consola de ping y dispensado, video corto de la bandeja y las tramas crudas en caso de falla.
