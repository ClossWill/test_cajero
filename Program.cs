using System.Text;
using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using IntegracionCajeroPruebas.Tests;

// Sandbox de hardware FINSO: NO es la aplicación ni un middleware. Cada opción llama directo a un Test_*.
// El SDK de GRG busca GRGDTATM_CommCfg.ini y escribe sus logs relativos al directorio de trabajo: debe ser el del .exe
// (con `dotnet run` sería la carpeta del proyecto).
Directory.SetCurrentDirectory(AppContext.BaseDirectory);
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;
Bitacora.Iniciar(); // después de fijar la codificación: envuelve Console.Out/In ya configurados

Console.WriteLine("Iniciando Pruebas de Hardware FINSO...");
Console.WriteLine($"Bitácora de la sesión: {Bitacora.Ruta ?? "(desactivada)"}");
Console.WriteLine($"Proceso de {(Environment.Is64BitProcess ? 64 : 32)} bits | config: {HardwareConfig.RutaArchivo}"
                  + (File.Exists(HardwareConfig.RutaArchivo) ? "" : " (no existe: se usan valores por defecto)"));

var menu = new (string Clave, string Texto, Action Accion)[]
{
    ("1", "Recicladora  - Inicializar (conecta, sin movimiento)", () => HardwareTests.Test_InicializarRecicladora()),
    ("11", "Recicladora - Reinicio mecánico (iPowerOnInitEx, SE MUEVE)", () => HardwareTests.Test_InicializarRecicladora(reinicioMecanico: true)),
    ("2", "Recicladora  - Aceptar billetes",         () => HardwareTests.Test_AceptarBilletes()),
    ("3", "Recicladora  - Dispensar billetes",       () => HardwareTests.Test_DispensarBilletes(PedirPedidoDispensa())),
    ("10", "Recicladora - Billete inválido (espera rechazo)", () => HardwareTests.Test_AceptarBilletes(esperaRechazo: true)),
    ("20", "Recicladora - Abrir shutter (control manual)",    HardwareTests.Test_AbrirShutter),
    ("21", "Recicladora - Cerrar shutter (control manual)",   HardwareTests.Test_CerrarShutter),
    ("4", "Monederos    - Ping ccTalk",              HardwareTests.Test_PingMonederos),
    ("5", "Monederos    - Dispensar monedas",        () => HardwareTests.Test_DispensarMonedas(
                                                         PedirEntero("Monedas de $1", 2), PedirEntero("Monedas de $5", 1))),
    ("6", "Impresora    - Voucher de prueba",        HardwareTests.Test_ImprimirVoucherPrueba),
    ("14", "Impresora   - Voucher de operación (logo + formato tradicional)", HardwareTests.Test_ImprimirVoucherOperacion),
    ("15", "Impresora   - Ping (conexión, sin imprimir)", HardwareTests.Test_PingImpresora),
    ("7", "Sensores     - Calibrar tramas ALB02",    HardwareTests.Test_CalibrarSensores),
    ("8", "Sensores     - Escuchar (puerta/vibración)", HardwareTests.Test_EscucharSensores),
    ("16", "Sensores    - Ping (abre el puerto COM)", HardwareTests.Test_PingSensores),
    ("9", "Escáner      - Leer documento",           HardwareTests.Test_EscanearDocumento),
    ("17", "Escáner     - Ping (inicializa SDK, sin leer)", HardwareTests.Test_PingEscaner),
    ("12", "LEDs        - Control de indicadores (18 canales)", HardwareTests.Test_ControlarLeds),
    ("18", "LEDs        - Ping (firmware)",          HardwareTests.Test_PingLeds),
    ("13", "Cámara      - Capturar foto (item 12 BEEX)",         HardwareTests.Test_CapturarRostro),
    ("19", "Cámara      - Ping (abre el dispositivo)", HardwareTests.Test_PingCamara),
};

while (true)
{
    Console.WriteLine();
    Console.WriteLine("========== MENÚ DE PRUEBAS ==========");
    Console.WriteLine("  --- FASE 1 (foco actual): recicladora de efectivo ---");
    foreach (var (clave, texto, _) in menu)
    {
        if (clave == "4") Console.WriteLine("  --- Fases posteriores (aún no se validan) ---");
        Console.WriteLine($"  {clave}. {texto}");
    }
    Console.WriteLine("  0. Salir");
    Console.Write("Opción: ");

    var opcion = Console.ReadLine()?.Trim();
    if (opcion == "0" || opcion is null) break;

    var elegida = menu.FirstOrDefault(m => m.Clave == opcion);
    if (elegida.Accion is null) { Consola.Error("Opción no válida."); continue; }

    try { elegida.Accion(); }
    catch (Exception ex) { Consola.Error($"Excepción no controlada: {ex.GetType().Name}: {ex.Message}"); }
}

HardwareTests.CerrarRecicladora();

static int PedirEntero(string pregunta, int porDefecto)
{
    Console.Write($"{pregunta} [{porDefecto}]: ");
    return int.TryParse(Console.ReadLine(), out var n) ? n : porDefecto;
}

static string PedirTexto(string pregunta, string porDefecto)
{
    Console.Write($"{pregunta} [{porDefecto}]: ");
    var t = Console.ReadLine()?.Trim();
    return string.IsNullOrEmpty(t) ? porDefecto : t;
}

// Arma una orden de dispensa de uno o varios casetes (ej. 500+200+100 MXN juntos) antes de mandarla como una
// sola llamada atómica a iDispenseEx.
static (string Moneda, int Denominacion, int Cantidad)[] PedirPedidoDispensa()
{
    var pedido = new List<(string Moneda, int Denominacion, int Cantidad)>();
    do
    {
        var moneda = PedirTexto("Moneda (MXN/USD)", "MXN");
        var denominacion = PedirEntero("Denominación (500/200/100 MXN, 20 USD)", 100);
        var cantidad = PedirEntero("Cantidad de billetes de esa denominación", 1);
        pedido.Add((moneda, denominacion, cantidad));
    } while (Consola.Confirmar("¿Agregar otra denominación a la misma orden?"));
    return pedido.ToArray();
}
