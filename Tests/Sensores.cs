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

    // Hallazgo (2026-10-28): COM5 no entrega ni un byte en reposo. El BEEX llama "Multifunctional serial control module"
    // al ALB02, y esa es la carpeta de la tarjeta de 18 canales (COM2): su hoja de protocolo documenta CMD 0x32, consulta
    // de 6 entradas de sensor de 5 V. Esta prueba la sondea y muestra la respuesta cruda y qué byte/bit cambia; el formato
    // exacto de RESP no se pudo leer del PDF, así que se descubre manipulando la puerta y golpeando el gabinete.
    public static void Test_EscucharSensoresTarjeta()
    {
        var leds = HardwareConfig.Actual.Leds;
        Consola.Titulo($"Sensores por tarjeta de 18 canales - Entradas (CMD 0x32) en {leds.Puerto} @ {leds.BaudRate}");
        Consola.Info("Abra/cierre la puerta y golpee el gabinete. Se imprime solo cuando la respuesta cambia. Q para terminar.");

        using var bus = new LedControlBus(leds.Puerto, leds.BaudRate);
        // Confirmado en la máquina real (2026-10-28): PARA 0x00 devuelve 6 bytes (uno por entrada, 0/1); PARA 0x31 lo rechaza ('N' 05).
        const byte para = 0x00;
        byte[]? anterior = null;
        var fallas = 0;

        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q))
        {
            var r = bus.Enviar(LedControlBus.CConsultarEntradas, para, 300);
            if (r is null)
            {
                if (fallas++ % 25 == 0) Consola.Error($"Sin respuesta: {bus.UltimoError}");
                Thread.Sleep(200);
                continue;
            }
            fallas = 0;

            if (!r.Ok)
            {
                Consola.Error($"La tarjeta rechazó CMD 0x32 con PARA 0x{para:X2} (respuesta 'N': {Consola.Hex(r.Datos)}).");
                return;
            }

            if (anterior is null || !r.Datos.AsSpan().SequenceEqual(anterior))
            {
                var estado = string.Join(' ', r.Datos.Select((v, i) => $"E{i + 1}={v}"));
                var detalle = "";
                if (anterior is not null && anterior.Length == r.Datos.Length)
                    detalle = "  << " + string.Join(", ", Enumerable.Range(0, r.Datos.Length)
                        .Where(i => r.Datos[i] != anterior[i])
                        .Select(i => $"ENTRADA {i + 1}: {anterior[i]}->{r.Datos[i]}"));
                Consola.Ok($"[{DateTime.Now:HH:mm:ss.fff}] {estado}   (RESP: {Consola.Hex(r.Datos)}){detalle}");
                anterior = r.Datos;
            }
            Thread.Sleep(150);
        }
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

        const int ventanaSegundos = 12;
        foreach (var (evento, instruccion) in pasos)
        {
            Consola.Esperar($"  >> [{evento}] Prepárese: {instruccion} ENTER para empezar a capturar {ventanaSegundos} s...");
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
