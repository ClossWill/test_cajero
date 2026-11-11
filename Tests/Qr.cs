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

    // Envío manual de los comandos del proveedor: bloque de luz + inicio de lectura, espera a que el operador pulse Enter
    // (observar el lector, y si se quiere escanear un QR, el dato aparece en pantalla) y detención. Sin timeout: la
    // detención siempre se envía al salir.
    public static void Test_ComandoManualQr()
    {
        if (!QrEsSerial)
        {
            Consola.Aviso("Requiere Qr.Modo = \"Serial\" (ver Qr.Puerto en hardware.json).");
            return;
        }

        if (Qr.ComandosActivar.Count == 0 || Qr.ComandosDesactivar.Count == 0)
        {
            Consola.Aviso("Qr.ComandosActivar / Qr.ComandosDesactivar vacíos en hardware.json.");
            return;
        }

        Consola.Titulo($"Lector QR XZG004 - Comando manual en {Qr.Puerto} @ {Qr.BaudRate}");
        try
        {
            using var puerto = AbrirPuertoQr();
            puerto.DiscardInBuffer();
            try
            {
                EnviarPrepararQr(puerto);
                EnviarComandosQr(puerto, Qr.ComandosActivar);
                Consola.Info("Presente un QR si desea; Enter para detener la lectura y terminar.");

                SerialTramas.Escuchar(puerto, Qr.SilencioTramaMs,
                    () => !(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter),
                    trama => Consola.Ok($"[{DateTime.Now:HH:mm:ss.fff}] RX: {DescribirRxQr(trama)}"));
            }
            finally
            {
                EnviarComandosQr(puerto, Qr.ComandosDesactivar);
            }
        }
        catch (Exception ex) { Consola.Error($"Excepción: {ex.Message}"); }
    }

    // Secuencia de observación: bloque de luz + inicio de lectura, SegundosPorComando mirando el lector (y lo que
    // responda), detención y otros SegundosPorComando. Sin confirmaciones: se observa el lector directamente. Q corta.
    public static void Test_ProbarComandosQr()
    {
        if (!QrEsSerial)
        {
            Consola.Aviso("Requiere Qr.Modo = \"Serial\" y el lector en USB CDC / RS-232 (ver Qr.Puerto en hardware.json).");
            return;
        }

        Consola.Titulo($"Lector QR XZG004 - Secuencia activar/desactivar en {Qr.Puerto} @ {Qr.BaudRate}");
        Consola.Info("Observe el cristal del lector: la luz debe encender al activar y apagar al desactivar. Q para terminar.");
        try
        {
            using var puerto = AbrirPuertoQr();
            var fases = new (string Nombre, Action<SerialPort>[] Pasos)[]
            {
                ("Activar", new Action<SerialPort>[] { EnviarPrepararQr, p => EnviarComandosQr(p, Qr.ComandosActivar) }),
                ("Desactivar", new Action<SerialPort>[] { p => EnviarComandosQr(p, Qr.ComandosDesactivar) }),
            };
            foreach (var (nombre, pasos) in fases)
            {
                puerto.DiscardInBuffer();
                Consola.Paso($"--- {nombre} ---");
                foreach (var paso in pasos) paso(puerto);

                var fin = DateTime.UtcNow.AddSeconds(Qr.SegundosPorComando);
                var cortar = false;
                SerialTramas.Escuchar(puerto, Qr.SilencioTramaMs,
                    () =>
                    {
                        if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q) cortar = true;
                        return !cortar && DateTime.UtcNow < fin;
                    },
                    trama => Consola.Ok($"[{DateTime.Now:HH:mm:ss.fff}] RX: {DescribirRxQr(trama)}"));
                if (cortar)
                {
                    // Si se corta antes de desactivar, el lector no debe quedar leyendo.
                    if (nombre == "Activar") EnviarComandosQr(puerto, Qr.ComandosDesactivar);
                    break;
                }
            }
        }
        catch (Exception ex) { Consola.Error($"Excepción: {ex.Message}"); }
        Consola.Info("Fin. Si la luz no reaccionó: verifique que se escanearon los QR de configuración del proveedor (puerto virtual USB y Key Mode).");
    }

    // Alterna Qr.Envoltorio solo en memoria (hardware.json no se toca) para probar ambos formatos sin reiniciar.
    public static void Test_AlternarEnvoltorioQr()
    {
        Qr.Envoltorio = !Qr.Envoltorio;
        Consola.Info($"Envoltorio {(Qr.Envoltorio ? "ACTIVADO" : "DESACTIVADO")}: ahora se envía {FormatoQr("SW00001")}");
    }

    // Alterna Qr.PrepararEnLote solo en memoria: bloque de luz como una sola cadena con ";" o comando por comando.
    public static void Test_AlternarLoteQr()
    {
        Qr.PrepararEnLote = !Qr.PrepararEnLote;
        Consola.Info($"Bloque de luz en {(Qr.PrepararEnLote ? "LOTE (una sola cadena con ';')" : "comandos SEPARADOS")}.");
    }

    // Envía ComandosPreparar. En lote, como el formato de "Batch Programming" del manual: una sola cadena, cada comando
    // seguido de ";" salvo BATCHST (ej. $>:BATCHST.<$$>:S01010F.<$;...;$>:BATCHET.<$;). Si no, uno por uno.
    private static void EnviarPrepararQr(SerialPort puerto)
    {
        if (!Qr.PrepararEnLote)
        {
            EnviarComandosQr(puerto, Qr.ComandosPreparar);
            return;
        }

        var lote = new StringBuilder();
        foreach (var codigo in Qr.ComandosPreparar.Where(c => !string.IsNullOrEmpty(c)))
        {
            lote.Append(FormatoQr(codigo));
            if (!codigo.Equals("BATCHST", StringComparison.OrdinalIgnoreCase)) lote.Append(';');
        }
        if (lote.Length == 0) return;

        var bytes = Encoding.ASCII.GetBytes(lote.ToString());
        puerto.Write(bytes, 0, bytes.Length);
        Consola.Paso($"[{DateTime.Now:HH:mm:ss.fff}] TX (lote): {lote}  ({bytes.Length} bytes)");
        EsperarRespuestaQr(puerto);
    }

    // El lector responde a los comandos con bytes de control (ACK 0x06 / NAK 0x15), no con datos de un código.
    private static bool EsRespuestaDeControlQr(byte[] t) => t.Length > 0 && t.All(b => b == 0x06 || b == 0x15);

    private static string DescribirRxQr(byte[] t) =>
        EsRespuestaDeControlQr(t)
            ? $"respuesta del lector: {t.Count(b => b == 0x06)}×ACK(06) {t.Count(b => b == 0x15)}×NAK(15)"
            : $"{Encoding.UTF8.GetString(t).TrimEnd('\r', '\n')}  ({Consola.Hex(t)})";

    // Tras cada envío: pausa y se muestra lo que el lector contestó (sirve para ver qué comando acepta y cuál rechaza).
    private static void EsperarRespuestaQr(SerialPort puerto)
    {
        Thread.Sleep(Qr.PausaEntreComandosMs);
        if (puerto.BytesToRead == 0) { Consola.Info("      (sin respuesta)"); return; }
        var rx = new byte[puerto.BytesToRead];
        var n = puerto.Read(rx, 0, rx.Length);
        Consola.Info($"      RX: {DescribirRxQr(rx[..n])}");
    }

    // Con Envoltorio: "$>:" + código + ".<$"; sin él, el código tal cual.
    private static string FormatoQr(string codigo) => Qr.Envoltorio ? $"$>:{codigo}.<$" : codigo;

    // Los comandos del XZG004 son ASCII literal, sin terminador. Se envían en orden, con una pausa entre ellos (el
    // proveedor no indica una mínima). Lista vacía = no se envía nada.
    private static void EnviarComandosQr(SerialPort puerto, IEnumerable<string> comandos)
    {
        foreach (var codigo in comandos)
        {
            if (string.IsNullOrEmpty(codigo)) continue;
            var texto = FormatoQr(codigo);
            var bytes = Encoding.ASCII.GetBytes(texto);
            puerto.Write(bytes, 0, bytes.Length);
            Consola.Paso($"[{DateTime.Now:HH:mm:ss.fff}] TX: {texto}  ({bytes.Length} bytes)");
            EsperarRespuestaQr(puerto);
        }
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

        puerto.DiscardInBuffer();
        if (Qr.ComandosActivar.Count == 0)
            Consola.Aviso("  >> Qr.ComandosActivar vacío: no se dispara el lector; solo se espera lo que él envíe.");
        else
        {
            EnviarPrepararQr(puerto);
            EnviarComandosQr(puerto, Qr.ComandosActivar);
            Consola.Info("  >> Lector activado.");
        }
        Consola.Aviso("  >> Presente el código QR frente al cristal del lector.");

        try
        {
            byte[]? recibido = null;
            var fin = DateTime.UtcNow + timeout;
            SerialTramas.Escuchar(puerto, Qr.SilencioTramaMs, () => recibido is null && DateTime.UtcNow < fin, t =>
            {
                // ACK/NAK no son un código leído: se muestran y se sigue esperando.
                if (EsRespuestaDeControlQr(t)) { Consola.Info($"  RX: {DescribirRxQr(t)} (ignorado)"); return; }
                recibido = t;
            });
            if (recibido is null) return null;
            Consola.Info($"  RX: {Consola.Hex(recibido)}");
            return Encoding.UTF8.GetString(recibido).TrimEnd('\r', '\n');
        }
        finally
        {
            if (Qr.ComandosDesactivar.Count > 0)
            {
                EnviarComandosQr(puerto, Qr.ComandosDesactivar);
                Consola.Info("  >> Lector desactivado.");
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
