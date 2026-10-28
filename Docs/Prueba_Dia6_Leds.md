# Prueba Día 6 – Control de LEDs indicadores (tarjeta 24002-2)

> Esta prueba **no estaba en el alcance original** (no es uno de los 5 periféricos del documento de arranque del proyecto). Se agregó porque llegó la documentación del fabricante y porque los LEDs de esta tarjeta son los indicadores de función actual del cajero (qué operación está en curso, error, etc.) que la app oficial necesitará encender.

## Objetivo

Leer la versión de firmware, identificar cuáles de los 18 canales corresponden a los 5 íconos del gabinete (recibo, moneda, pasaporte, QR, efectivo) probándolos uno por uno con el asistente, y validar el parpadeo general.

## Protocolo / librería

- **RS232 propio de la tarjeta** (no ccTalk, no ESC/POS, no SDK de fabricante): `DLE STX [LEN(2, low-first) 'C' CMD PARA] DLE ETX BCC`, 19200 baudios 8N1, checksum BCC = XOR de los bytes entre STX y el DLE final. Documentado en `referencias_integracion/Multifunctional serial control module/` (hoja de protocolo + especificación de la tarjeta, modelo **24002-2**, Shenzhen Hongzhou Smart Technology).
- **No es el módulo ALB02** del punto 5 del documento de arranque: es una tarjeta RS232 aparte, en otro puerto COM, dedicada solo a las 18 salidas de 12V. El ALB02 (sensores de puerta/vibración) sigue siendo el de `Tests/Sensores.cs`, sin relación con esta tarjeta.
- Implementado en `Protocolos/LedControlSerial.cs` (`LedControlBus`): construcción de trama, envío y lectura de respuesta con validación de BCC.

## Precondiciones

- Tarjeta conectada por RS232 (DB9) o USB (puerto serie virtual) y alimentada con 12V/10A según su propia fuente — **no** se alimenta desde el puerto de datos.
- Puerto COM identificado en *Administrador de dispositivos* y anotado en `hardware.json` (`Leds.Puerto`).
- Al menos un indicador (LED) cableado a un canal de salida para poder confirmar visualmente; idealmente varios canales para el barrido.
- **Confirmado (hoja de protocolo del fabricante):** 19200 baudios, 8N1, checksum BCC. Verificado recalculando el BCC a mano contra ~40 ejemplos hexadecimales de la hoja de especificación (firmware, consulta de 6 entradas, encender/apagar/parpadear los 18 canales): todos coinciden.
- **Sin confirmar:** el protocolo menciona de pasada una sección de "caracteres de escape" (para cuando 0x10 aparece dentro de los datos) que no estaba en el PDF recibido. Para los comandos de esta prueba (LEN y CMD fijos, PARA ≤ 18) no hay colisión conocida con 0x10, pero no se implementó manejo de escape.

## Configuración

```json
"Leds": {
  "Puerto": "COM2",
  "BaudRate": 19200,
  "Canales": 18,
  "PerifericosEsperados": 5
}
```

`PerifericosEsperados` es cuántos íconos identificables tiene el gabinete (recibo, moneda, pasaporte, QR, efectivo); la prueba prueba canales uno por uno y se detiene apenas los identifica, sin necesidad de agotar los 18.

**Confirmado en la máquina real (2026-10-16) con `Test_PingLeds` (menú `18`):** el puerto es `COM2`, no `COM6` (valor original nunca verificado). Firmware reportado: `ADK LEDBOX V2.0-20240613`. Hizo falta activar `DtrEnable`/`RtsEnable` al abrir el puerto (mismo hallazgo que en monederos, ver `Prueba_Dia2_Monederos.md`) — sin eso, el puerto abría sin error pero el firmware nunca respondía, en ningún COM.

## Código

### `Protocolos/LedControlSerial.cs`

```csharp
using System.Diagnostics;
using System.IO.Ports;
using IntegracionCajeroPruebas.Helpers;

namespace IntegracionCajeroPruebas.Protocolos;

/// <summary>Respuesta ya separada de su framing. <c>Ok</c> = el tablero devolvió 'P' (comando correcto).</summary>
public sealed record LedRespuesta(bool Ok, byte Cmd, byte Para, byte[] Datos);

/// <summary>
/// Cliente para la tarjeta "18-Channel LED Light Bar Control Board" (modelo 24002-2, Shenzhen Hongzhou,
/// doc. <c>referencias_integracion/Multifunctional serial control module/</c>). No es el módulo ALB02: es
/// una tarjeta RS232 aparte que controla 18 salidas de 12V (indicadores de función).
///
/// Trama: <c>DLE STX [LEN(2, low-first) 'C' CMD PARA] DLE ETX BCC</c>, con BCC = XOR de todos los bytes
/// entre STX y el DLE final (LEN + 'C' + CMD + PARA). Para nuestros comandos (PARA de 1 byte) LEN siempre
/// vale <c>03 00</c> (longitud de 'C'+CMD+PARA), confirmado byte a byte contra las ~40 tramas de ejemplo
/// de la hoja de especificación del fabricante. La respuesta usa el mismo framing con 'P' (éxito, 0x50) o
/// 'N' (falla, 0x4E) en el lugar de 'C'. El protocolo no documenta caracteres de escape para cuando 0x10
/// aparece dentro de los datos (sí lo menciona de pasada, pero esa sección no estaba en el PDF recibido);
/// para los comandos usados aquí (LEN, CMD y PARA siempre &lt; 0x10 o fijos) no hay colisión conocida.
/// </summary>
public sealed class LedControlBus : IDisposable
{
    public const byte CConsultarFirmware = 0x31;
    public const byte CConsultarEntradas = 0x32; // estado de las 6 entradas de sensor de 5 V (hoja de protocolo, §consulta de entradas)
    public const byte CApagar = 0x33;
    public const byte CParpadear = 0x34;
    public const byte CEncender = 0x35;

    /// <summary>PARA = 0 controla los 18 canales a la vez; 1..18 direcciona un canal individual.</summary>
    public const byte ParaTodos = 0x00;

    private readonly SerialPort _puerto;

    public string UltimoError { get; private set; } = "";

    public LedControlBus(string puerto, int baudRate)
    {
        _puerto = new SerialPort(puerto, baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 1000,
            WriteTimeout = 500,
            // Mismo hallazgo que en CcTalk.cs (2026-10-16): el puerto puede abrir sin error y aun así no
            // recibir nada si el adaptador RS232 necesita DTR/RTS para alimentar o habilitar su transceptor.
            // .NET los deja en false por defecto; se activan aquí también por la misma razón.
            DtrEnable = true,
            RtsEnable = true,
        };
        _puerto.Open();
    }

    public static byte[] ConstruirComando(byte cmd, byte para)
    {
        byte[] mensaje = { 0x03, 0x00, 0x43, cmd, para }; // LEN(2) 'C' CMD PARA
        byte bcc = 0;
        foreach (var b in mensaje) bcc ^= b;

        var trama = new byte[2 + mensaje.Length + 3];
        trama[0] = 0x10; trama[1] = 0x02; // DLE STX
        mensaje.CopyTo(trama, 2);
        trama[^3] = 0x10; trama[^2] = 0x03; trama[^1] = bcc; // DLE ETX BCC
        return trama;
    }

    public LedRespuesta? Enviar(byte cmd, byte para, int timeoutMs = 1000)
    {
        _puerto.DiscardInBuffer();
        var tx = ConstruirComando(cmd, para);
        _puerto.Write(tx, 0, tx.Length);

        var rx = LeerRespuesta(timeoutMs);
        if (rx is null) UltimoError = $"Sin respuesta válida a CMD 0x{cmd:X2} PARA 0x{para:X2} en {timeoutMs} ms";
        return rx;
    }

    public bool Apagar(byte canal) => Enviar(CApagar, canal)?.Ok == true;
    public bool Encender(byte canal) => Enviar(CEncender, canal)?.Ok == true;
    public bool Parpadear(byte canal) => Enviar(CParpadear, canal)?.Ok == true;

    /// <summary>RESP trae la versión como texto, p. ej. "POWER BOX Vx.xx" (15 bytes, según la hoja de protocolo).</summary>
    public string? ConsultarFirmware()
    {
        var r = Enviar(CConsultarFirmware, 0x31); // PARA fijo = 0x31 en todos los ejemplos del fabricante
        return r is { Ok: true } ? System.Text.Encoding.ASCII.GetString(r.Datos).TrimEnd('\0', ' ') : null;
    }

    /// <summary>Busca DLE STX ('P'|'N') LEN(2) y, si ya llegó la trama completa, valida el BCC y la separa.</summary>
    private LedRespuesta? LeerRespuesta(int timeoutMs)
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

            var i = -1;
            for (var k = 0; k + 2 < buf.Count; k++)
            {
                if (buf[k] == 0x10 && buf[k + 1] == 0x02 && (buf[k + 2] == 0x50 || buf[k + 2] == 0x4E)) { i = k; break; }
            }

            if (i >= 0 && buf.Count >= i + 5)
            {
                var len = buf[i + 3] | (buf[i + 4] << 8);
                var total = i + 5 + len + 3; // DLE STX 'P/N' LEN(2) + payload(len) + DLE ETX BCC
                if (buf.Count >= total)
                {
                    var mensaje = buf.GetRange(i + 3, 2 + len).ToArray(); // LEN(2) + CMD + PARA + RESP/ERROR
                    byte bcc = 0;
                    foreach (var b in mensaje) bcc ^= b;
                    if (bcc != buf[total - 1])
                    {
                        UltimoError = "BCC inválido en la respuesta: " + Consola.Hex(buf.GetRange(i, total - i));
                        return null;
                    }

                    var payload = buf.GetRange(i + 5, len).ToArray(); // CMD PARA RESP/ERROR
                    var ok = buf[i + 2] == 0x50;
                    return new LedRespuesta(ok, payload[0], payload[1], payload[2..]);
                }
            }

            Thread.Sleep(2);
        }
        return null;
    }

    public void Dispose() => _puerto.Dispose();
}
```

### `Tests/Leds.cs`

```csharp
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
```

## Cómo ejecutar

Menú `12` (prueba completa) o `18` (ping: solo consulta el firmware). Equivalente desde `Main`:

```csharp
HardwareTests.Test_PingLeds();
HardwareTests.Test_ControlarLeds();
```

## Modalidad remota (desarrollador a distancia + asistente en sitio)

| Momento | Qué pasa en consola | Qué hace el asistente |
| :-- | :-- | :-- |
| Antes de empezar | "Párese donde pueda ver los 5 íconos del gabinete (recibo, moneda, pasaporte, QR, efectivo). Voy a encender los canales uno por uno." | Se coloca frente al gabinete |
| Por cada canal probado | "Canal N: ¿se encendió alguna luz?" | Dice "sí" o "no". Si dice "sí", sigue: "¿Cuál de los 5 (recibo, moneda, pasaporte, QR, efectivo)?" → el asistente lo nombra |
| Después de "parpadear todos" | "¿Vio parpadear los indicadores durante unos segundos?" | Confirma |

No hay `AntesDeMover`: son salidas eléctricas de 12V hacia luces, no hay ningún mecanismo donde el asistente pueda tener la mano dentro.

## Criterios de aceptación

| ID | Criterio | Cómo se verifica |
| :-- | :-- | :-- |
| L-01 | La tarjeta responde a la consulta de firmware | Criterio automático (BCC válido) |
| L-02 | Se identifican los 5 periféricos esperados (canal → ícono) | Confirmación por consola, canal por canal |
| L-03 | Los canales probados aceptan encender/apagar (ACK) | Criterio automático |
| L-04 | El comando "parpadear todos" es aceptado (ACK) | Criterio automático |
| L-05 | Se observa el parpadeo | Confirmación por consola |

**Aprobado** = L-01 a L-05. Si se agotan los 18 canales sin llegar a 5 identificados, L-02 falla y queda registrado cuántos y cuáles sí se identificaron.

## Supuestos y riesgos

- Sin probar aún en la tarjeta real (2026-10-15): el protocolo está confirmado por los ejemplos del fabricante (recálculo manual de BCC), pero `LedControlBus` no se ha ejecutado contra hardware.
- **Puerto COM confirmado en la máquina real (2026-10-16):** `COM2`, no `COM6` (el valor original era un supuesto nunca verificado, igual que pasó con los monederos). Sigue sin confirmarse si llega por RS232 o por el USB "de repuesto" que menciona la especificación.
- **`DtrEnable`/`RtsEnable` confirmados como necesarios (2026-10-16), mismo hallazgo que en monederos (`Protocolos/CcTalk.cs`):** con `COM6` (el puerto equivocado) el ping no obtenía respuesta; al corregir el puerto a `COM2` **y** tener ya `DtrEnable`/`RtsEnable` activados, el firmware respondió (`ADK LEDBOX V2.0-20240613`). No se aisló cuál de los dos cambios era el necesario (pudo ser solo el puerto), pero dejar DTR/RTS activados no tiene costo.
- No se investigó el comando de consulta de estado de entradas (`CMD 0x32`, 6 sensores de 5V) porque esta tarjeta es distinta del ALB02 y el alcance de esta prueba es solo el control de salidas; si en algún momento se confirma que es la misma tarjeta física que el ALB02, ese comando ya está documentado en la hoja de protocolo y se podría reutilizar para `Tests/Sensores.cs`.
- **El mapeo de qué canal (1-18) corresponde a qué ícono físico del gabinete (RECEIPT, COINS OUT, PASSPORT/ID CARD, QR CODE, CASH IN/OUT) no está documentado en ningún lado** — confirmado revisando la hoja de especificación del fabricante de la tarjeta (`24002-2 Specification.pdf`): solo define `OUT1`-`OUT18` genéricos, sin relación con el gabinete del cajero, porque esa relación la define el arnés de cables que armó el integrador, no el fabricante de la tarjeta. Por eso la prueba lo obtiene probando canal por canal con el asistente (ver "Modalidad remota") y se detiene en cuanto identifica los 5; no hay otra forma de obtenerlo.
- Se asume que, de los 18 canales, exactamente 5 están conectados a un ícono del gabinete y el resto no hace nada visible; si algún canal "de más" también enciende algo (indicador no listado en la foto de referencia), la prueba lo registraría igual al preguntar "¿cuál de los 5?" — el asistente podría nombrar algo fuera de la lista y habría que ajustar `PerifericosEsperados` o la lista de íconos.

## Evidencia a guardar

Salida de consola completa (bitácora de la sesión), con el mapeo canal → ícono que imprime la prueba al final.
