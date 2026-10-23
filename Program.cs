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

var categorias = new (string Nombre, (string Clave, string Texto, Action Accion)[] Pruebas)[]
{
    ("Recicladora", new (string Clave, string Texto, Action Accion)[]
    {
        ("1", "Inicializar (conecta, sin movimiento)", () => HardwareTests.Test_InicializarRecicladora()),
        ("11", "Reinicio mecánico (iPowerOnInitEx, SE MUEVE)", () => HardwareTests.Test_InicializarRecicladora(reinicioMecanico: true)),
        ("25", "Configurar (iInitSettingEx, sin movimiento)", HardwareTests.Test_ConfigurarRecicladora),
        ("2", "Depositar pesos MXN (flujo GRG: abre, espera billetes, cuenta por lotes y guarda)", () => HardwareTests.Test_DepositarBilletes("MXN")),
        ("23", "Depositar dólares USD (mismo flujo, solo acepta USD)", () => HardwareTests.Test_DepositarBilletes("USD")),
        ("3", "Dispensar pesos MXN",      () => HardwareTests.Test_DispensarBilletes(PedirPedidoDispensa("MXN"))),
        ("24", "Dispensar dólares USD",   () => HardwareTests.Test_DispensarBilletes(PedirPedidoDispensa("USD"))),
        ("20", "Abrir shutter (control manual)",    HardwareTests.Test_AbrirShutter),
        ("21", "Cerrar shutter (control manual)",   HardwareTests.Test_CerrarShutter),
        ("22", "Consultar inventario (solo lectura)", HardwareTests.Test_ConsultarInventario),
    }),
    ("Monederos", new (string Clave, string Texto, Action Accion)[]
    {
        ("4", "Ping ccTalk",              HardwareTests.Test_PingMonederos),
        ("5", "Dispensar monedas",        () => HardwareTests.Test_DispensarMonedas(
                                               PedirEntero("Monedas de $1", 2), PedirEntero("Monedas de $5", 1))),
    }),
    ("Impresora", new (string Clave, string Texto, Action Accion)[]
    {
        ("6", "Voucher de prueba",        HardwareTests.Test_ImprimirVoucherPrueba),
        ("14", "Voucher de operación (logo + formato tradicional)", HardwareTests.Test_ImprimirVoucherOperacion),
        ("15", "Ping (conexión, sin imprimir)", HardwareTests.Test_PingImpresora),
        ("26", "Voucher de operación por SDK del fabricante (autoreplyprint.dll)", HardwareTests.Test_ImprimirVoucherSdk),
    }),
    ("Sensores", new (string Clave, string Texto, Action Accion)[]
    {
        ("7", "Calibrar tramas ALB02",    HardwareTests.Test_CalibrarSensores),
        ("8", "Escuchar (puerta/vibración)", HardwareTests.Test_EscucharSensores),
        ("16", "Ping (abre el puerto COM)", HardwareTests.Test_PingSensores),
    }),
    ("Escáner", new (string Clave, string Texto, Action Accion)[]
    {
        ("9", "Leer documento",           HardwareTests.Test_EscanearDocumento),
        ("17", "Ping (inicializa SDK, sin leer)", HardwareTests.Test_PingEscaner),
    }),
    ("LEDs", new (string Clave, string Texto, Action Accion)[]
    {
        ("12", "Control de indicadores (18 canales)", HardwareTests.Test_ControlarLeds),
        ("18", "Ping (firmware)",          HardwareTests.Test_PingLeds),
    }),
    ("Cámara", new (string Clave, string Texto, Action Accion)[]
    {
        ("13", "Capturar foto (item 12 BEEX)",         HardwareTests.Test_CapturarRostro),
        ("19", "Ping (abre el dispositivo)", HardwareTests.Test_PingCamara),
    }),
};

while (true)
{
    Console.WriteLine();
    Console.WriteLine("========== MENÚ DE PRUEBAS ==========");
    for (var i = 0; i < categorias.Length; i++)
        Console.WriteLine($"  {i + 1}. {categorias[i].Nombre}");
    Console.WriteLine("  0. Salir");
    Console.Write("Opción: ");

    var opcion = Console.ReadLine()?.Trim();
    if (opcion == "0" || opcion is null) break;

    if (!int.TryParse(opcion, out var indice) || indice < 1 || indice > categorias.Length)
    {
        Consola.Error("Opción no válida.");
        continue;
    }

    var categoria = categorias[indice - 1];
    while (true)
    {
        Console.WriteLine();
        Console.WriteLine($"----- {categoria.Nombre} -----");
        foreach (var (clave, texto, _) in categoria.Pruebas)
            Console.WriteLine($"  {clave}. {texto}");
        Console.WriteLine("  0. Volver al menú principal");
        Console.Write("Opción: ");

        var subopcion = Console.ReadLine()?.Trim();
        if (subopcion == "0" || subopcion is null) break;

        var elegida = categoria.Pruebas.FirstOrDefault(m => m.Clave == subopcion);
        if (elegida.Accion is null) { Consola.Error("Opción no válida."); continue; }

        try { elegida.Accion(); }
        catch (Exception ex) { Consola.Error($"Excepción no controlada: {ex.GetType().Name}: {ex.Message}"); }
    }
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
static (string Moneda, int Denominacion, int Cantidad)[] PedirPedidoDispensa(string moneda)
{
    var pedido = new List<(string Moneda, int Denominacion, int Cantidad)>();
    do
    {
        var denominacion = moneda == "USD"
            ? PedirEntero("Denominación en USD (20)", 20)
            : PedirEntero("Denominación en MXN (500/200/100)", 100);
        var cantidad = PedirEntero("Cantidad de billetes de esa denominación", 1);
        pedido.Add((moneda, denominacion, cantidad));
    } while (Consola.Confirmar("¿Agregar otra denominación a la misma orden?"));
    return pedido.ToArray();
}
