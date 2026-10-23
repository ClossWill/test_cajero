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
    public static void Test_PingImpresora()
    {
        var cfg = HardwareConfig.Actual.Impresora;
        var r = new Reporte($"Impresora KP-80A - Ping (modo {cfg.Modo})");
        try
        {
            if (cfg.Modo.Equals("UsbSdk", StringComparison.OrdinalIgnoreCase))
            {
                // Windows ve esta impresora como CP001/USBPort ("USB Printing Support" genuino, confirmado
                // 2026-10-17 en Propiedades > Puertos), así que CP_Port_OpenUsb es la ruta nativa correcta,
                // no CP_Port_OpenCom. El fallo anterior se sospecha del controlador de Windows reteniendo el
                // puerto mientras la cola "POS80 Printer" está instalada — ver Supuestos y riesgos.
                var h = AutoReplyPrintSdk.Abrir(cfg.NombreUsb);
                if (r.Criterio($"Apertura de impresora USB '{cfg.NombreUsb}' vía SDK (autoreply)", AutoReplyPrintSdk.EsValido(h)))
                {
                    if (AutoReplyPrintSdk.ConsultarEstado(h, 1000, out var estado))
                        Consola.Info($"        Estado: {AutoReplyPrintSdk.DescribirEstado(estado)}");
                    else
                        Consola.Aviso("        Sin respuesta de estado (sin verificación garantizada por el SDK; se avisa, no bloquea).");
                    AutoReplyPrintSdk.Cerrar(h);
                }
            }
            else if (cfg.Modo.Equals("Serial", StringComparison.OrdinalIgnoreCase))
            {
                using var puerto = new SerialPort(cfg.Puerto, cfg.BaudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 500,
                    WriteTimeout = 2000,
                    DtrEnable = true, // ver CcTalk.cs: algunos adaptadores necesitan esto para habilitarse
                    RtsEnable = true,
                };
                puerto.Open();
                r.Criterio($"Apertura de {cfg.Puerto} @ {cfg.BaudRate}", puerto.IsOpen);

                puerto.Write(EscPos.EstadoEnLinea, 0, EscPos.EstadoEnLinea.Length);
                Thread.Sleep(200);
                if (puerto.BytesToRead > 0)
                {
                    var estado = (byte)puerto.ReadByte();
                    r.Criterio("Impresora en línea (DLE EOT 1)", EscPos.EstaEnLinea(estado), $"estado=0x{estado:X2}");
                }
                else
                {
                    Consola.Aviso("        Sin respuesta a DLE EOT 1 (no todas las KP-80A lo responden por serial).");
                }
            }
            else
            {
                r.Criterio($"Cola '{cfg.NombreImpresora}' existe y se puede abrir", ImpresoraRaw.Existe(cfg.NombreImpresora));
            }
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

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
        }
        finally { AutoReplyPrintSdk.Cerrar(h); }
    }

    // Voucher con el formato de un comprobante real de cambio de divisas (logo + datos de la operación).
    // Requiere modo UsbSdk: el logo y los tamaños de fuente usan funciones del SDK del fabricante
    // (CP_Pos_PrintRasterImageFromFile / CP_Pos_SetTextScale) que no tienen equivalente ya implementado
    // en el camino ESC/POS crudo (Raw/Serial) de este proyecto.
    public static void Test_ImprimirVoucherOperacion()
    {
        var cfg = HardwareConfig.Actual.Impresora;
        var r = new Reporte($"Impresora KP-80A - Voucher de operación (logo + formato tradicional, modo {cfg.Modo})");
        try
        {
            // El SDK (autoreplyprint.dll) no se pudo destrabar en esta máquina (ver AutoReplyPrintSdk.Abrir):
            // logo y tamaños de fuente también son comandos ESC/POS estándar (GS v 0, GS !), así que
            // Spooler/Serial los arman a mano en vez de depender del SDK para esto.
            if (cfg.Modo.Equals("UsbSdk", StringComparison.OrdinalIgnoreCase))
                ImprimirVoucherPorSdk(r, cfg);
            else
                ImprimirVoucherPorEscPos(r, cfg);
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    private static void ImprimirVoucherPorEscPos(Reporte r, ImpresoraConfig cfg)
    {
        var ahora = DateTime.Now;
        var folio = ahora.ToString("yyMMdd-HHmmss");
        var montoEntregadoUsd = 100.00m;
        var tipoCambio = 18.50m;
        var comision = 0.00m;
        var montoRecibidoMxn = montoEntregadoUsd * tipoCambio - comision;

        var rutaLogo = Path.Combine(AppContext.BaseDirectory, cfg.RutaLogo);
        var logoExiste = File.Exists(rutaLogo);
        r.Criterio($"Logo encontrado en '{rutaLogo}' (Impresora.RutaLogo)", logoExiste);

        var partes = new List<byte[]> { EscPos.Inicializar, EscPos.Centrar };
        if (logoExiste)
        {
            // Mismo tamaño que en el SDK: 576 dots = ancho imprimible de 80mm @ 203dpi; 147 proporcional
            // al logo.png real (2560x655) para no deformarlo.
            var bits = EscPos.ImagenDesdeArchivo(rutaLogo, anchoDots: 576, altoDots: 147);
            partes.Add(EscPos.ImagenRaster(bits, 576, 147));
            partes.Add(EscPos.SaltoLinea);
        }

        partes.Add(EscPos.EscalaTexto(1, 1));
        partes.Add(EscPos.NegritaOn);
        partes.Add(EscPos.Texto("COMPROBANTE DE OPERACION"));
        partes.Add(EscPos.SaltoLinea);
        partes.Add(EscPos.NegritaOff);
        partes.Add(EscPos.EscalaTexto(0, 0));
        partes.Add(EscPos.Izquierda);

        var separador = new string('-', 32);
        void Linea(string texto) { partes.Add(EscPos.Texto(texto)); partes.Add(EscPos.SaltoLinea); }

        Linea(separador);
        Linea($"Fecha:        {ahora:yyyy-MM-dd HH:mm:ss}");
        Linea("Operacion:    COMPRA DE DOLARES");
        Linea(separador);
        Linea($"Entregado:    USD {montoEntregadoUsd:N2}");
        Linea("  5 x USD   20.00");
        Linea(separador);
        Linea($"Recibido:     MXN {montoRecibidoMxn:N2}");
        Linea("  3 x MXN  500.00");
        Linea("  1 x MXN  200.00");
        Linea("  1 x MXN  100.00");
        Linea(" 10 x MXN    5.00");
        Linea(separador);
        Linea($"Tipo de cambio: {tipoCambio:N2}");
        Linea($"Comision:       MXN {comision:N2}");
        Linea(separador);
        Linea("Identificacion: Nivel 2 - INE validada");
        Linea(separador);
        Linea("Terminal:     ATM-FINSO-01");
        Linea($"Folio:        {folio}");
        Linea(separador);
        partes.Add(EscPos.Centrar);
        Linea("Gracias por su preferencia");
        partes.Add(EscPos.SaltoLinea);
        partes.Add(EscPos.SaltoLinea);
        partes.Add(EscPos.AvanzarYCortar);

        var voucher = EscPos.Concatenar(partes.ToArray());
        r.Criterio("Contenido del voucher armado (logo + texto, ESC/POS crudo)", true);

        if (cfg.Modo.Equals("Serial", StringComparison.OrdinalIgnoreCase))
            EnviarPorSerial(r, cfg, voucher);
        else
        {
            ImpresoraRaw.Enviar(cfg.NombreImpresora, voucher);
            r.Criterio($"{voucher.Length} bytes entregados al spooler de '{cfg.NombreImpresora}'", true);
        }
    }

    private static void ImprimirVoucherPorSdk(Reporte r, ImpresoraConfig cfg)
    {
        var h = AutoReplyPrintSdk.Abrir(cfg.NombreUsb);
        if (!r.Criterio($"Apertura de impresora USB '{cfg.NombreUsb}' vía SDK (autoreply)", AutoReplyPrintSdk.EsValido(h)))
            return;
        ImprimirVoucherEnHandle(r, cfg, h);
    }

    /// <summary>
    /// Opción de menú propia del SDK del fabricante (autoreplyprint.dll), sin depender de Impresora.Modo: enumera lo que el
    /// SDK ve (USB y COM), abre la primera impresora que responda (con y sin autoreply) e imprime el voucher de operación.
    /// </summary>
    public static void Test_ImprimirVoucherSdk()
    {
        var cfg = HardwareConfig.Actual.Impresora;
        var r = new Reporte("Impresora KP-80A - Voucher de operación por SDK (autoreplyprint.dll)");
        try
        {
            Consola.Info($"        SDK autoreplyprint versión {AutoReplyPrintSdk.Version()}");
            var usb = AutoReplyPrintSdk.EnumerarUsb();
            var com = AutoReplyPrintSdk.EnumerarCom();
            Consola.Info($"        USB que ve el SDK: {(usb.Count == 0 ? "(ninguna)" : string.Join(" | ", usb))}");
            Consola.Info($"        COM que ve el SDK: {(com.Count == 0 ? "(ninguno)" : string.Join(" | ", com))}");

            // Primero lo que el SDK enumeró, luego el nombre de hardware.json: con una sola impresora USB el SDK abre
            // la que encuentre aunque el nombre sea cualquier texto.
            var nombres = usb.Append(cfg.NombreUsb).Distinct().ToList();
            var h = IntPtr.Zero;
            foreach (var nombre in nombres)
            {
                foreach (var autoreply in new[] { true, false })
                {
                    h = AutoReplyPrintSdk.AbrirUsb(nombre, autoreply);
                    var ok = AutoReplyPrintSdk.EsValido(h);
                    Consola.Info($"        Abrir USB '{nombre}' autoreply={(autoreply ? "sí" : "no")}: {(ok ? "OK" : "falló")}");
                    if (ok) break;
                }
                if (AutoReplyPrintSdk.EsValido(h)) break;
            }

            if (!r.Criterio("Apertura de la impresora vía SDK", AutoReplyPrintSdk.EsValido(h),
                    "Si falla con USB: el SDK solo abre si Windows la ve como 'USB Printing Support'; ver Docs/Prueba_Dia3_Impresora.md"))
            { r.Finalizar(); return; }

            ImprimirVoucherEnHandle(r, cfg, h);
        }
        catch (DllNotFoundException ex) { r.Criterio("autoreplyprint.dll disponible junto al .exe", false, ex.Message); }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    // Imprime el voucher con un handle ya abierto y lo cierra al terminar.
    private static void ImprimirVoucherEnHandle(Reporte r, ImpresoraConfig cfg, IntPtr h)
    {
        try
        {
            AutoReplyPrintSdk.Reset(h);

            // Datos de muestra: una operación de compra de dólares consistente con los casetes reales
            // (recicladora acepta USD20, dispensa MXN 500/200/100; monederos dispensan MXN $1/$5) —
            // no viene de una transacción real, es solo para probar el formato de impresión.
            var ahora = DateTime.Now;
            var folio = ahora.ToString("yyMMdd-HHmmss");
            var montoEntregadoUsd = 100.00m;
            var tipoCambio = 18.50m;
            var comision = 0.00m;
            var montoRecibidoMxn = montoEntregadoUsd * tipoCambio - comision;

            var rutaLogo = Path.Combine(AppContext.BaseDirectory, cfg.RutaLogo);
            var logoExiste = File.Exists(rutaLogo);
            if (logoExiste)
            {
                // 576 dots = ancho imprimible estándar de 80mm @ 203dpi (72mm; el resto son márgenes
                // del cabezal). 147 = alto proporcional al 2560x655 real de logo.png, para no deformarlo.
                AutoReplyPrintSdk.Alinear(h, AutoReplyPrintSdk.AlineacionCentro);
                r.Criterio("Logo entregado al SDK (CP_Pos_PrintRasterImageFromFile)",
                    AutoReplyPrintSdk.ImprimirImagen(h, rutaLogo, anchoDots: 576, altoDots: 147));
                AutoReplyPrintSdk.AvanzarLineas(h, 1);
            }
            else
            {
                r.Criterio($"Logo encontrado en '{rutaLogo}' (Impresora.RutaLogo)", false);
            }

            AutoReplyPrintSdk.EscalaTexto(h, 1, 1);
            AutoReplyPrintSdk.Negrita(h, true);
            AutoReplyPrintSdk.Texto(h, "COMPROBANTE DE OPERACION");
            AutoReplyPrintSdk.AvanzarLineas(h, 1);
            AutoReplyPrintSdk.Negrita(h, false);
            AutoReplyPrintSdk.EscalaTexto(h, 0, 0);
            AutoReplyPrintSdk.Alinear(h, AutoReplyPrintSdk.AlineacionIzquierda);

            var separador = new string('-', 32);
            void Linea(string texto) { AutoReplyPrintSdk.Texto(h, texto); AutoReplyPrintSdk.AvanzarLineas(h, 1); }

            Linea(separador);
            Linea($"Fecha:        {ahora:yyyy-MM-dd HH:mm:ss}");
            Linea("Operacion:    COMPRA DE DOLARES");
            Linea(separador);
            Linea($"Entregado:    USD {montoEntregadoUsd:N2}");
            Linea("  5 x USD   20.00");
            Linea(separador);
            Linea($"Recibido:     MXN {montoRecibidoMxn:N2}");
            Linea("  3 x MXN  500.00");
            Linea("  1 x MXN  200.00");
            Linea("  1 x MXN  100.00");
            Linea(" 10 x MXN    5.00");
            Linea(separador);
            Linea($"Tipo de cambio: {tipoCambio:N2}");
            Linea($"Comision:       MXN {comision:N2}");
            Linea(separador);
            Linea("Identificacion: Nivel 2 - INE validada");
            Linea(separador);
            Linea("Terminal:     ATM-FINSO-01");
            Linea($"Folio:        {folio}");
            Linea(separador);
            AutoReplyPrintSdk.Alinear(h, AutoReplyPrintSdk.AlineacionCentro);
            Linea("Gracias por su preferencia");
            AutoReplyPrintSdk.AvanzarLineas(h, 2);
            r.Criterio("Contenido del voucher entregado al SDK", true);

            r.Criterio("Corte completo aceptado por el SDK", AutoReplyPrintSdk.CorteCompleto(h));

            var salioImpreso = AutoReplyPrintSdk.ConsultarResultadoImpresion(h, 3000);
            r.Criterio("El SDK confirma que el ticket se imprimió (QueryPrintResult)", salioImpreso);
        }
        finally { AutoReplyPrintSdk.Cerrar(h); }
    }

    private static void EnviarPorSerial(Reporte r, ImpresoraConfig cfg, byte[] datos)
    {
        using var puerto = new SerialPort(cfg.Puerto, cfg.BaudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 500,
            WriteTimeout = 2000,
            DtrEnable = true,
            RtsEnable = true,
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
