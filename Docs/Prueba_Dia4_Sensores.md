# Prueba Día 4 – Módulo de sensores y seguridad ALB02

## Objetivo

Leer el puerto COM del ALB02 en un bucle reactivo y mostrar `¡PUERTA ABIERTA!` o `¡VIBRACIÓN DETECTADA!` cuando se manipule físicamente el gabinete.

## Protocolo / librería

- **Escucha serial reactiva**: el módulo envía tramas de forma autónoma por el COM; el programa no envía comandos, solo lee.
- `System.IO.Ports` + `Protocolos/SerialTramas.cs`: agrupa los bytes en "tramas" cuando la línea queda en silencio (`SilencioTramaMs`, 30 ms por defecto) y detecta cada evento buscando un patrón de bytes dentro de la trama.
- **El formato de tramas del ALB02 no está en el repositorio.** Por eso la prueba tiene dos pasos: `Test_CalibrarSensores` captura lo que llega en reposo y ante cada evento; las tramas propias de cada evento se copian a `hardware.json`; `Test_EscucharSensores` las usa para disparar los mensajes.

## Precondiciones

- ALB02 alimentado y conectado al COM; puerto y baudios (9600 por defecto) confirmados con el manual del módulo.
- **Puerto confirmado en la máquina real (2026-10-16) con `Test_PingSensores`:** `COM5` (el valor por defecto) abre correctamente — a diferencia de monederos y LEDs, aquí el supuesto original resultó ser el correcto.
- Acceso al gabinete para abrir/cerrar la puerta y golpearlo con cuidado.
- Cerradura S&G operable (para abrir sin forzar).

## Configuración

```json
"Sensores": {
  "Puerto": "COM5",
  "BaudRate": 9600,
  "SilencioTramaMs": 30,
  "Tramas": {
    "PUERTA_ABIERTA": "AA 01 01 55",
    "PUERTA_CERRADA": "AA 01 00 55",
    "VIBRACION": "AA 02 01 55",
    "PROXIMIDAD": ""
  }
}
```

Los valores de `Tramas` del ejemplo son **ilustrativos**; deben sustituirse con lo capturado en la calibración.

## Código

### `Protocolos/SerialTramas.cs`

```csharp
using System.Diagnostics;
using System.IO.Ports;

namespace IntegracionCajeroPruebas.Protocolos;

public static class SerialTramas
{
    /// <summary>
    /// Lee el puerto de forma continua y entrega una "trama" cada vez que la línea queda en silencio
    /// <paramref name="silencioMs"/> milisegundos tras recibir bytes. Termina cuando continuar() devuelve false.
    /// </summary>
    public static void Escuchar(SerialPort puerto, int silencioMs, Func<bool> continuar, Action<byte[]> alRecibirTrama)
    {
        var buf = new List<byte>();
        var ultimoByte = Stopwatch.StartNew();

        while (continuar())
        {
            var n = puerto.BytesToRead;
            if (n > 0)
            {
                var tmp = new byte[n];
                puerto.Read(tmp, 0, n);
                buf.AddRange(tmp);
                ultimoByte.Restart();
            }
            else if (buf.Count > 0 && ultimoByte.ElapsedMilliseconds >= silencioMs)
            {
                alRecibirTrama(buf.ToArray());
                buf.Clear();
            }
            else
            {
                Thread.Sleep(5);
            }
        }
    }

    /// <summary>¿La trama contiene el patrón como subsecuencia contigua?</summary>
    public static bool Contiene(byte[] trama, byte[] patron)
    {
        if (patron.Length == 0 || patron.Length > trama.Length) return false;
        for (var i = 0; i <= trama.Length - patron.Length; i++)
            if (trama.AsSpan(i, patron.Length).SequenceEqual(patron)) return true;
        return false;
    }
}
```

### `Tests/Sensores.cs`

```csharp
using System.IO.Ports;
using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using IntegracionCajeroPruebas.Protocolos;

namespace IntegracionCajeroPruebas.Tests;

// Módulo ALB02: escucha serial reactiva. El formato de tramas no está documentado en el repositorio,
// así que primero se calibra (Test_CalibrarSensores) y luego se copian las tramas a hardware.json.
public static partial class HardwareTests
{
    private static SensoresConfig Sensores => HardwareConfig.Actual.Sensores;

    private static readonly Dictionary<string, string> MensajesSensor = new()
    {
        ["PUERTA_ABIERTA"] = "¡PUERTA ABIERTA!",
        ["PUERTA_CERRADA"] = "Puerta cerrada",
        ["VIBRACION"] = "¡VIBRACIÓN DETECTADA!",
        ["PROXIMIDAD"] = "Proximidad detectada",
    };

    // El ALB02 es reactivo (solo manda tramas cuando algo lo dispara): no hay comando de consulta
    // documentado, así que lo máximo que se puede confirmar sin manipular el gabinete es que el puerto abre.
    public static void Test_PingSensores()
    {
        var r = new Reporte($"Sensores ALB02 - Ping ({Sensores.Puerto} @ {Sensores.BaudRate})");
        try
        {
            using var puerto = new SerialPort(Sensores.Puerto, Sensores.BaudRate, Parity.None, 8, StopBits.One)
                { DtrEnable = true, RtsEnable = true }; // ver CcTalk.cs: algunos adaptadores los necesitan para habilitarse
            puerto.Open();
            r.Criterio($"Apertura de {Sensores.Puerto} @ {Sensores.BaudRate}", puerto.IsOpen);
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    public static void Test_EscucharSensores()
    {
        var patrones = Sensores.Tramas
            .Select(t => (Evento: t.Key, Patron: Consola.ParseHex(t.Value)))
            .Where(t => t.Patron is not null)
            .ToList();

        Consola.Titulo($"Sensores ALB02 - Escucha en {Sensores.Puerto} @ {Sensores.BaudRate}");
        if (patrones.Count == 0)
            Consola.Aviso("Sin tramas configuradas: solo se mostrarán las tramas crudas. Use Test_CalibrarSensores.");
        Consola.Info("Manipule la puerta / golpee el gabinete. Presione Q para terminar.");

        using var puerto = new SerialPort(Sensores.Puerto, Sensores.BaudRate, Parity.None, 8, StopBits.One)
            { DtrEnable = true, RtsEnable = true };
        puerto.Open();

        byte[]? anterior = null;
        SerialTramas.Escuchar(puerto, Sensores.SilencioTramaMs, QueSigaEscuchando, trama =>
        {
            if (anterior is not null && trama.AsSpan().SequenceEqual(anterior)) return; // ignora repeticiones idénticas
            anterior = trama;

            var hora = DateTime.Now.ToString("HH:mm:ss.fff");
            Consola.Info($"[{hora}] RX: {Consola.Hex(trama)}");
            foreach (var (evento, patron) in patrones)
                if (SerialTramas.Contiene(trama, patron!))
                    Consola.Error(MensajesSensor.GetValueOrDefault(evento, evento));
        });

        static bool QueSigaEscuchando() =>
            !(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q);
    }

    // Captura qué tramas llegan en reposo y al provocar cada evento, para copiarlas a hardware.json.
    public static void Test_CalibrarSensores()
    {
        Consola.Titulo($"Sensores ALB02 - Calibración en {Sensores.Puerto} @ {Sensores.BaudRate}");
        using var puerto = new SerialPort(Sensores.Puerto, Sensores.BaudRate, Parity.None, 8, StopBits.One)
            { DtrEnable = true, RtsEnable = true };
        puerto.Open();

        var pasos = new (string Evento, string Instruccion)[]
        {
            ("REPOSO", "No toque nada (puerta cerrada)."),
            ("PUERTA_ABIERTA", "ABRA la puerta del gabinete."),
            ("PUERTA_CERRADA", "CIERRE la puerta del gabinete."),
            ("VIBRACION", "Golpee suavemente el gabinete."),
        };

        // Ventana amplia: entre el ENTER del desarrollador y la acción del asistente hay latencia de llamada.
        const int ventanaSegundos = 12;
        foreach (var (evento, instruccion) in pasos)
        {
            Consola.Asistente($"[{evento}] Prepárese: {instruccion} Hágalo cuando le diga \"AHORA\".");
            Consola.Esperar($"     ENTER y diga \"AHORA\" al asistente: se capturan {ventanaSegundos} s...");
            puerto.DiscardInBuffer();

            var distintas = new Dictionary<string, int>();
            var fin = DateTime.UtcNow.AddSeconds(ventanaSegundos);
            SerialTramas.Escuchar(puerto, Sensores.SilencioTramaMs, () => DateTime.UtcNow < fin, trama =>
            {
                var hex = Consola.Hex(trama);
                distintas[hex] = distintas.GetValueOrDefault(hex) + 1;
            });

            if (distintas.Count == 0) Consola.Error($"  {evento}: no llegó ningún byte (revisar puerto/baudios/cableado).");
            foreach (var (hex, veces) in distintas)
                Consola.Ok($"  {evento}: \"{hex}\"  (x{veces})");
        }
        Consola.Info("Copie las tramas propias de cada evento (las que no aparecen en REPOSO) a \"Sensores.Tramas\" en hardware.json.");
    }
}
```

## Cómo ejecutar

1. Menú `7` (calibración): seguir las instrucciones de reposo, puerta abierta, puerta cerrada y vibración; copiar las tramas a `hardware.json`.
2. Menú `8` (escucha): manipular el gabinete; `Q` termina.
3. Menú `16` (ping: solo abre el puerto COM; el ALB02 es reactivo, no hay comando de consulta que responder). Equivalente desde `Main`:

```csharp
HardwareTests.Test_PingSensores();
HardwareTests.Test_CalibrarSensores();
HardwareTests.Test_EscucharSensores();
```

## Criterios de aceptación

| ID | Criterio | Cómo se verifica |
| :-- | :-- | :-- |
| S-01 | El puerto COM abre y se reciben bytes en reposo o ante eventos | Calibración |
| S-02 | Cada evento (puerta abierta, cerrada, vibración) produce una trama distinta de la de reposo | Calibración |
| S-03 | Al abrir la puerta aparece `¡PUERTA ABIERTA!` en menos de 1 s | Observación en la escucha |
| S-04 | Al golpear el gabinete aparece `¡VIBRACIÓN DETECTADA!` | Observación en la escucha |
| S-05 | Cerrar la puerta produce "Puerta cerrada" y no repite alarmas falsas | Observación |
| S-06 | 10 aperturas/cierres consecutivos se detectan las 10 veces (sin pérdidas) | Prueba repetida |
| S-07 | Sin manipulación durante 5 min no hay falsas alarmas | Prueba de estabilidad |

**Aprobado** = S-01 a S-07 para los eventos de puerta y vibración (proximidad es opcional).

## Supuestos y riesgos

- Si el módulo envía un latido periódico, las repeticiones idénticas se ignoran para no inundar la consola; los cambios sí se muestran.
- Si una trama contiene varios estados a la vez (p. ej. un byte de bits), el patrón por subsecuencia será insuficiente y habrá que decodificar el byte; se decide con los datos de la calibración.
- Baudios o paridad incorrectos producen basura; probar 9600/19200/115200 si la calibración sale ilegible.

## Evidencia a guardar

Salida de la calibración, `hardware.json` con las tramas finales y un video corto de la puerta abierta junto a la consola.
