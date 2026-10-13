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

        using var puerto = new SerialPort(Sensores.Puerto, Sensores.BaudRate, Parity.None, 8, StopBits.One);
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
        using var puerto = new SerialPort(Sensores.Puerto, Sensores.BaudRate, Parity.None, 8, StopBits.One);
        puerto.Open();

        var pasos = new (string Evento, string Instruccion)[]
        {
            ("REPOSO", "No toque nada (puerta cerrada)."),
            ("PUERTA_ABIERTA", "ABRA la puerta del gabinete."),
            ("PUERTA_CERRADA", "CIERRE la puerta del gabinete."),
            ("VIBRACION", "Golpee suavemente el gabinete."),
        };

        foreach (var (evento, instruccion) in pasos)
        {
            Consola.Esperar($"[{evento}] {instruccion} Presione ENTER y realice la acción durante 6 s...");
            puerto.DiscardInBuffer();

            var distintas = new Dictionary<string, int>();
            var fin = DateTime.UtcNow.AddSeconds(6);
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
