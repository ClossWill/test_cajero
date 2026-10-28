using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using IntegracionCajeroPruebas.Protocolos;

namespace IntegracionCajeroPruebas.Tests;

// Lector de QR XZG004 (item 10 del BEEX). El lector solo debe estar activo mientras se pide un escaneo: cada llamada
// a Test_LeerQr abre su propia ventana de captura (y, en Modo=Serial, dispara y detiene el lector).
public static partial class HardwareTests
{
    private static QrConfig Qr => HardwareConfig.Actual.Qr;

    private static bool QrEsSerial => Qr.Modo.Equals("Serial", StringComparison.OrdinalIgnoreCase);

    // El lector no tiene consulta de estado documentada: en Serial lo máximo sin escanear es abrir el puerto.
    public static void Test_PingQr()
    {
        if (!QrEsSerial)
        {
            Consola.Aviso("Qr.Modo = \"Teclado\" (HID-KBW): no hay canal de comandos que consultar. Use \"Leer código QR\".");
            return;
        }

        var r = new Reporte($"Lector QR XZG004 - Ping ({Qr.Puerto} @ {Qr.BaudRate})");
        try
        {
            using var puerto = AbrirPuertoQr();
            r.Criterio($"Apertura de {Qr.Puerto} @ {Qr.BaudRate}", puerto.IsOpen);
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    public static void Test_LeerQr()
    {
        var timeout = TimeSpan.FromSeconds(Qr.TimeoutSegundos);
        var r = new Reporte($"Lector QR XZG004 - Lectura bajo demanda [Modo {Qr.Modo}]");
        try
        {
            var dato = QrEsSerial ? LeerQrSerial(timeout) : LeerQrTeclado(timeout);
            if (!r.Criterio($"Código leído en {Qr.TimeoutSegundos} s", !string.IsNullOrEmpty(dato))) { r.Finalizar(); return; }

            Console.WriteLine($"  Contenido : {dato}");
            Console.WriteLine($"  Longitud  : {dato!.Length} caracteres");
            // Solo aparece si el lector tiene habilitado el prefijo AIM ID (manual §3.4); ]Q = QR Code.
            if (dato.StartsWith("]Q", StringComparison.Ordinal))
                Consola.Info("  Simbología: QR Code (prefijo AIM ]Q)");
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    // Descubrimiento del comando de disparo: envía cada candidato con su hora y deja unos segundos para ver si el lector
    // enciende el haz (o responde algo). Sin confirmaciones: se observa el lector directamente. Se corta con Q.
    public static void Test_ProbarComandosQr()
    {
        if (!QrEsSerial)
        {
            Consola.Aviso("Requiere Qr.Modo = \"Serial\" y el lector en USB CDC / RS-232 (ver Qr.Puerto en hardware.json).");
            return;
        }

        Consola.Titulo($"Lector QR XZG004 - Prueba de comandos de disparo en {Qr.Puerto} @ {Qr.BaudRate}");
        Consola.Info("Observe el cristal del lector: el candidato que encienda el haz es el comando de disparo. Q para terminar.");
        try
        {
            using var puerto = AbrirPuertoQr();
            foreach (var texto in Qr.ComandosCandidatos)
            {
                var comando = Consola.ParseHex(texto);
                if (comando is null) continue;

                puerto.DiscardInBuffer();
                puerto.Write(comando, 0, comando.Length);
                Consola.Paso($"[{DateTime.Now:HH:mm:ss.fff}] TX: {Consola.Hex(comando)}");

                var fin = DateTime.UtcNow.AddSeconds(Qr.SegundosPorComando);
                var cortar = false;
                SerialTramas.Escuchar(puerto, Qr.SilencioTramaMs,
                    () =>
                    {
                        if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q) cortar = true;
                        return !cortar && DateTime.UtcNow < fin;
                    },
                    trama => Consola.Ok($"[{DateTime.Now:HH:mm:ss.fff}] RX: {Consola.Hex(trama)}"));
                if (cortar) break;
            }
        }
        catch (Exception ex) { Consola.Error($"Excepción: {ex.Message}"); }
        Consola.Info("Fin. Si algún candidato encendió el haz, cópielo a Qr.ComandoActivar (y su pareja de apagado a ComandoDesactivar).");
    }

    private static SerialPort AbrirPuertoQr()
    {
        if (string.IsNullOrWhiteSpace(Qr.Puerto))
            throw new InvalidOperationException("Qr.Puerto vacío en hardware.json: con Modo=Serial indique el COM del lector.");
        var puerto = new SerialPort(Qr.Puerto, Qr.BaudRate, Parity.None, 8, StopBits.One)
            { DtrEnable = true, RtsEnable = true }; // ver CcTalk.cs: algunos adaptadores los necesitan para habilitarse
        puerto.Open();
        return puerto;
    }

    private static string? LeerQrSerial(TimeSpan timeout)
    {
        using var puerto = AbrirPuertoQr();
        var activar = Consola.ParseHex(Qr.ComandoActivar);
        var desactivar = Consola.ParseHex(Qr.ComandoDesactivar);

        puerto.DiscardInBuffer();
        if (activar is null)
            Consola.Aviso("  >> Qr.ComandoActivar vacío: no se dispara el lector; solo se espera lo que él envíe.");
        else
        {
            puerto.Write(activar, 0, activar.Length);
            Consola.Info($"  >> Lector activado (TX: {Consola.Hex(activar)}).");
        }
        Consola.Aviso("  >> Presente el código QR frente al cristal del lector.");

        try
        {
            byte[]? recibido = null;
            var fin = DateTime.UtcNow + timeout;
            SerialTramas.Escuchar(puerto, Qr.SilencioTramaMs, () => recibido is null && DateTime.UtcNow < fin, t => recibido = t);
            if (recibido is null) return null;
            Consola.Info($"  RX: {Consola.Hex(recibido)}");
            return Encoding.UTF8.GetString(recibido).TrimEnd('\r', '\n');
        }
        finally
        {
            if (desactivar is not null)
            {
                puerto.Write(desactivar, 0, desactivar.Length);
                Consola.Info($"  >> Lector desactivado (TX: {Consola.Hex(desactivar)}).");
            }
        }
    }

    // HID-KBW: el lector escribe en la consola como un teclado. Se vacía lo tecleado antes de empezar para que solo
    // cuente el escaneo pedido, y el dato se cierra con Enter o tras un silencio (por si el lector no manda terminador).
    private static string? LeerQrTeclado(TimeSpan timeout)
    {
        while (Console.KeyAvailable) Console.ReadKey(true);
        Consola.Aviso("  >> Mantenga esta ventana en primer plano y presente el código QR frente al cristal del lector.");

        var dato = new StringBuilder();
        var total = Stopwatch.StartNew();
        var sinTeclas = Stopwatch.StartNew();
        while (total.Elapsed < timeout)
        {
            if (!Console.KeyAvailable)
            {
                if (dato.Length > 0 && sinTeclas.ElapsedMilliseconds >= Qr.SilencioTramaMs) break;
                Thread.Sleep(5);
                continue;
            }

            var tecla = Console.ReadKey(true);
            sinTeclas.Restart();
            if (tecla.Key == ConsoleKey.Enter) { if (dato.Length > 0) break; continue; }
            if (tecla.KeyChar != '\0') dato.Append(tecla.KeyChar);
        }
        // Ventana cerrada: el lector ya no cuenta como activo. Se descarta lo que aún llegue (p. ej. el Enter final) para
        // que no se cuele como entrada del menú.
        Thread.Sleep(Qr.SilencioTramaMs);
        while (Console.KeyAvailable) Console.ReadKey(true);
        return dato.Length == 0 ? null : dato.ToString();
    }
}
