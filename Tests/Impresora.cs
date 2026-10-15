using System.IO.Ports;
using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using IntegracionCajeroPruebas.Protocolos;

namespace IntegracionCajeroPruebas.Tests;

// Impresora térmica KP-80A con ESC/POS: por cola de Windows (USB, RAW), directo por puerto serial,
// o por el SDK nativo del fabricante en modo USB autoreply (canal recomendado para la app oficial:
// ver AutoReplyPrintSdk.cs y "Supuestos y riesgos" en Docs/Prueba_Dia3_Impresora.md).
public static partial class HardwareTests
{
    public static void Test_ImprimirVoucherPrueba()
    {
        var cfg = HardwareConfig.Actual.Impresora;
        var r = new Reporte($"Impresora KP-80A - Voucher de prueba (modo {cfg.Modo})");
        try
        {
            if (cfg.Modo.Equals("UsbSdk", StringComparison.OrdinalIgnoreCase))
            {
                ImprimirPorSdk(r, cfg);
                r.Finalizar();
                return;
            }

            var voucher = EscPos.Concatenar(
                EscPos.Inicializar,
                EscPos.Centrar,
                EscPos.NegritaOn, EscPos.Texto("Hola Mundo FINSO"), EscPos.NegritaOff, EscPos.SaltoLinea,
                EscPos.Izquierda, EscPos.Texto("Prueba de hardware - texto normal"), EscPos.SaltoLinea,
                EscPos.Texto(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")), EscPos.SaltoLinea,
                EscPos.SaltoLinea, EscPos.SaltoLinea,
                EscPos.AvanzarYCortar);

            if (cfg.Modo.Equals("Serial", StringComparison.OrdinalIgnoreCase))
                EnviarPorSerial(r, cfg, voucher);
            else
            {
                ImpresoraRaw.Enviar(cfg.NombreImpresora, voucher);
                r.Criterio($"{voucher.Length} bytes entregados al spooler de '{cfg.NombreImpresora}'", true);
            }

            r.Criterio("Imprimió 'Hola Mundo FINSO' legible y en negritas", Consola.PreguntarAsistente("¿Salió un ticket con \"Hola Mundo FINSO\" en negritas?"));
            r.Criterio("El autocortador cortó el papel", Consola.PreguntarAsistente("¿El ticket salió cortado (suelto)?"));
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    // Modo "UsbSdk": usa autoreplyprint.dll (SDK del fabricante) en vez del spooler de Windows.
    // Valida lo que la app oficial necesitará: estado real antes de imprimir (papel/tapa/cortador)
    // y confirmación de que el ticket salió (QueryPrintResult), no solo que se envió el comando.
    private static void ImprimirPorSdk(Reporte r, ImpresoraConfig cfg)
    {
        var h = AutoReplyPrintSdk.Abrir(cfg.NombreUsb);
        if (!r.Criterio($"Apertura de impresora USB '{cfg.NombreUsb}' vía SDK (autoreply)", AutoReplyPrintSdk.EsValido(h)))
            return;

        try
        {
            // "0" de QueryRTStatus es ambiguo (falla de lectura o, por diseño del SDK, estado sin bits
            // encendidos): el fabricante advierte que esta instrucción "no tiene verificación, no se
            // puede garantizar que el resultado sea correcto". Por eso el criterio solo se evalúa si
            // hubo respuesta; si no hay respuesta, se avisa y se continúa (igual que el DLE EOT serial).
            if (AutoReplyPrintSdk.ConsultarEstado(h, 1000, out var estadoPrevio))
            {
                Consola.Info($"        Estado antes de imprimir: {AutoReplyPrintSdk.DescribirEstado(estadoPrevio)}");
                r.Criterio("Sin papel/tapa/cortador reportado antes de imprimir",
                    !(AutoReplyPrintSdk.SinPapel(estadoPrevio) || AutoReplyPrintSdk.TapaAbierta(estadoPrevio) || AutoReplyPrintSdk.ErrorCortador(estadoPrevio)),
                    AutoReplyPrintSdk.DescribirEstado(estadoPrevio));
            }
            else
            {
                Consola.Aviso("        Sin respuesta de estado antes de imprimir (se continúa).");
            }

            AutoReplyPrintSdk.Reset(h);
            AutoReplyPrintSdk.Alinear(h, AutoReplyPrintSdk.AlineacionCentro);
            AutoReplyPrintSdk.Negrita(h, true);
            AutoReplyPrintSdk.Texto(h, "Hola Mundo FINSO");
            AutoReplyPrintSdk.Negrita(h, false);
            AutoReplyPrintSdk.AvanzarLineas(h, 1);
            AutoReplyPrintSdk.Alinear(h, AutoReplyPrintSdk.AlineacionIzquierda);
            AutoReplyPrintSdk.Texto(h, "Prueba de hardware - texto normal");
            AutoReplyPrintSdk.AvanzarLineas(h, 1);
            AutoReplyPrintSdk.Texto(h, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            AutoReplyPrintSdk.AvanzarLineas(h, 3);
            r.Criterio("Comandos de texto/negrita entregados por el SDK", true);

            r.Criterio("Corte completo aceptado por el SDK", AutoReplyPrintSdk.CorteCompleto(h));

            // QueryPrintResult confirma que el trabajo anterior salió realmente, no solo que se envió.
            var salioImpreso = AutoReplyPrintSdk.ConsultarResultadoImpresion(h, 3000);
            r.Criterio("El SDK confirma que el ticket se imprimió (QueryPrintResult)", salioImpreso);

            if (AutoReplyPrintSdk.ConsultarEstado(h, 1000, out var estadoFinal))
                Consola.Info($"        Estado después de imprimir: {AutoReplyPrintSdk.DescribirEstado(estadoFinal)}");

            r.Criterio("Imprimió 'Hola Mundo FINSO' legible y en negritas", Consola.PreguntarAsistente("¿Salió un ticket con \"Hola Mundo FINSO\" en negritas?"));
            r.Criterio("El autocortador cortó el papel", Consola.PreguntarAsistente("¿El ticket salió cortado (suelto)?"));
        }
        finally { AutoReplyPrintSdk.Cerrar(h); }
    }

    private static void EnviarPorSerial(Reporte r, ImpresoraConfig cfg, byte[] datos)
    {
        using var puerto = new SerialPort(cfg.Puerto, cfg.BaudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 500,
            WriteTimeout = 2000,
        };
        puerto.Open();
        r.Criterio($"Apertura de {cfg.Puerto} @ {cfg.BaudRate}", puerto.IsOpen);

        // DLE EOT 1: no todas las KP-80A lo responden por serial; si no hay respuesta solo se avisa.
        puerto.Write(EscPos.EstadoEnLinea, 0, EscPos.EstadoEnLinea.Length);
        Thread.Sleep(200);
        if (puerto.BytesToRead > 0)
        {
            var estado = (byte)puerto.ReadByte();
            r.Criterio("Impresora en línea (DLE EOT 1)", EscPos.EstaEnLinea(estado), $"estado=0x{estado:X2}");
        }
        else
        {
            Consola.Aviso("        Sin respuesta a DLE EOT 1 (se continúa con la impresión).");
        }

        puerto.Write(datos, 0, datos.Length);
        r.Criterio($"{datos.Length} bytes enviados por {cfg.Puerto}", true);
    }
}
