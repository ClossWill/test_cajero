using System.IO.Ports;
using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using IntegracionCajeroPruebas.Protocolos;

namespace IntegracionCajeroPruebas.Tests;

// Lector de QR XZG004 (item 10 del BEEX), en modo USB CDC (COM virtual). Las pruebas de iniciar y detener son el flujo del
// ejemplo del proveedor (referencias_integracion\QR\xzg-example), verificado en la máquina: ver XzgLector. La lectura es una
// sesión: Iniciar la deja corriendo y cada QR aparece en la consola en cuanto llega; Detener la termina.
public static partial class HardwareTests
{
    private static QrConfig Qr => HardwareConfig.Actual.Qr;

    private static readonly XzgLector LectorQr = new();

    // El lector no tiene consulta de estado documentada: lo máximo sin leer es comprobar que Windows ve el COM virtual del
    // lector (VID_26F1 PID_D001, lo que Iniciar necesita para esperar su reinicio) y que el puerto abre.
    public static void Test_PingQr()
    {
        var r = new Reporte("Lector QR XZG004 - Ping");
        try
        {
            if (LectorQr.Activo)
            {
                r.Criterio("Sesión de lectura activa (el puerto lo tiene abierto Iniciar)", true,
                    $"QR leídos hasta ahora: {LectorQr.Lecturas}. Use \"Detener lectura\" para liberar el puerto.");
                r.Finalizar();
                return;
            }

            var detectado = XzgLector.BuscarPuertoVirtual();
            r.Criterio("Lector detectado por Windows (VID_26F1 PID_D001)", true, $"COM virtual: {detectado}");

            var puerto = string.IsNullOrWhiteSpace(Qr.Puerto) ? detectado : Qr.Puerto.Trim();
            if (!puerto.Equals(detectado, StringComparison.OrdinalIgnoreCase))
                r.Criterio($"Qr.Puerto ({puerto}) coincide con el COM detectado", false, $"Windows ve el lector en {detectado}: corrija Qr.Puerto en hardware.json o déjelo vacío.");

            using var abierto = new SerialPort(puerto, Qr.BaudRate, Parity.None, 8, StopBits.One) { DtrEnable = true, RtsEnable = true };
            abierto.Open();
            r.Criterio($"Apertura de {puerto} @ {Qr.BaudRate}", abierto.IsOpen);
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    // Igual que "Start scan" del ejemplo: guarda la luz (el lector se REINICIA y el COM desaparece hasta ~15 s), reabre el
    // puerto y manda SW00001. Queda leyendo: los QR salen por consola hasta que se use Detener.
    public static void Test_IniciarQr()
    {
        var r = new Reporte($"Lector QR XZG004 - Iniciar lectura (luz {DescribirLuzQr(Qr.Luz)})");
        if (LectorQr.Activo)
        {
            r.Criterio("Lectura ya iniciada", true, $"QR leídos hasta ahora: {LectorQr.Lecturas}. Use \"Detener lectura\" para terminar.");
            r.Finalizar();
            return;
        }

        Consola.Info("Guardar la luz reinicia el lector: el COM desaparece unos segundos y vuelve. Espere...");
        var resultado = LectorQr.Iniciar(Qr.Puerto, Qr.BaudRate, Qr.Luz);
        r.Criterio("Lectura continua iniciada (SW00001 con ACK)", resultado.Ok, resultado.Mensaje);
        if (r.Finalizar())
            Consola.Aviso("Presente un QR frente al cristal: cada lectura se muestra aquí. Use \"Detener lectura\" al terminar.");
    }

    // Igual que "Stop scan": apaga la luz (otra vez reinicia el lector), manda SWFFFFF y cierra el puerto.
    public static void Test_DetenerQr()
    {
        var r = new Reporte("Lector QR XZG004 - Detener lectura");
        var resultado = LectorQr.Detener();
        r.Criterio("Lectura detenida y puerto cerrado", resultado.Ok, resultado.Mensaje);
        if (r.Finalizar())
            Consola.Info("El lector se reinicia al apagar la luz: el COM tarda unos segundos en volver antes de iniciar otra vez.");
    }

    // Iniciar + espera de la primera lectura + Detener, en una sola prueba. Detener corre siempre (también si vence el
    // tiempo, el operador pulsa Enter o algo falla), así el lector no queda leyendo. Detener tarda unos segundos (apaga la luz
    // antes de mandar SWFFFFF), y en ese lapso la lectura continua puede repetir el QR: el total sale en el mensaje final.
    public static void Test_LeerUnQr()
    {
        var r = new Reporte($"Lector QR XZG004 - Leer un QR (inicia, espera la primera lectura y detiene) [luz {DescribirLuzQr(Qr.Luz)}]");
        if (LectorQr.Activo)
        {
            r.Criterio("Sin lectura previa en curso", false, "Ya hay una lectura iniciada: use \"Detener lectura\" y vuelva a intentar.");
            r.Finalizar();
            return;
        }

        Consola.Info("Guardar la luz reinicia el lector: el COM desaparece unos segundos y vuelve. Espere...");
        var inicio = LectorQr.Iniciar(Qr.Puerto, Qr.BaudRate, Qr.Luz);
        if (!r.Criterio("Lectura continua iniciada (SW00001 con ACK)", inicio.Ok, inicio.Mensaje))
        {
            r.Finalizar();
            return;
        }

        Consola.Aviso($"Presente un QR frente al cristal (máximo {Qr.TimeoutSegundos} s; Enter cancela).");
        string? dato = null;
        ResultadoQr parada;
        try
        {
            dato = LectorQr.EsperarPrimeraLectura(TimeSpan.FromSeconds(Qr.TimeoutSegundos),
                () => !(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter));
        }
        finally
        {
            Consola.Info(dato is null ? "Sin lectura: deteniendo el lector..." : "QR detectado: deteniendo el lector...");
            parada = LectorQr.Detener();
        }

        r.Criterio($"QR leído en {Qr.TimeoutSegundos} s", dato is not null,
            dato is null ? null : $"Contenido: {dato}  ({dato.Length} caracteres)");
        r.Criterio("Lectura detenida y puerto cerrado", parada.Ok, parada.Mensaje);
        r.Finalizar();
    }

    // Al salir del menú: el lector no debe quedar leyendo ni con el puerto tomado.
    public static void CerrarQr()
    {
        if (LectorQr.Activo)
        {
            Consola.Aviso("Lector QR aún activo: deteniendo la lectura antes de salir...");
            var resultado = LectorQr.Detener();
            if (!resultado.Ok) Consola.Error(resultado.Mensaje);
        }
        LectorQr.Dispose();
    }

    private static string DescribirLuzQr(int luz) => luz switch
    {
        1 => "parpadeo",
        2 => "siempre encendida",
        3 => "encendida solo al leer",
        _ => $"valor no válido: {luz}",
    };
}
