using System.Text.Json;

namespace IntegracionCajeroPruebas.Config;

/// <summary>Puertos COM, direcciones y supuestos del hardware. Se lee de hardware.json (junto al .exe).</summary>
public sealed class HardwareConfig
{
    public RecicladoraConfig Recicladora { get; set; } = new();
    public MonederosConfig Monederos { get; set; } = new();
    public EscanerConfig Escaner { get; set; } = new();
    public ImpresoraConfig Impresora { get; set; } = new();
    public SensoresConfig Sensores { get; set; } = new();
    public LedsConfig Leds { get; set; } = new();
    public CamaraConfig Camara { get; set; } = new();

    public static string RutaArchivo => Path.Combine(AppContext.BaseDirectory, "hardware.json");

    public static HardwareConfig Actual { get; } = Cargar();

    private static HardwareConfig Cargar()
    {
        if (!File.Exists(RutaArchivo)) return new HardwareConfig();

        var opciones = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        return JsonSerializer.Deserialize<HardwareConfig>(File.ReadAllText(RutaArchivo), opciones)
               ?? new HardwareConfig();
    }
}

public sealed class RecicladoraConfig
{
    // Valores tomados de la demo y el PDF de GRG (CRM9250NDev_V2.3.7.6): el nombre lógico es el nodo [CRM9250NDev]
    // de GRGDTATM_CommCfg.ini, éxito = rc 0, byDispType 1 = dispensar por casete RC, casetes RC1..RC4 = 1..4.
    public string NombreDispositivoLogico { get; set; } = "CRM9250NDev";
    public int CodigoExito { get; set; } = 0;
    public byte ModoAtc { get; set; } = 1;
    public byte ModoTrabajo { get; set; } = 0;
    public byte AceptarTodasDenominaciones { get; set; } = 1;
    public byte ConfigInicio { get; set; } = 0;
    public int LimiteDeposito { get; set; } = 10;
    public byte TipoDispensa { get; set; } = 1;
    public byte DestinoRechazoDispensa { get; set; } = 1;   // byRejectToAc: 1 = AC1, 2 = AC2, 3 = LRB
    public byte DestinoRechazoDeposito { get; set; } = 1;   // byRejectPos: 1 = AC1, 2 = AC2, 3 = LRB
    public int MaxBilletesPrueba { get; set; } = 5;
    public int SegundosRetiroBilletes { get; set; } = 60;   // espera a que retiren lo dispensado antes de cerrar el shutter
    public int SegundosColocarBilletes { get; set; } = 60;  // depósito: espera a que coloquen billetes tras abrir el shutter
    public int SegundosAcomodoBilletes { get; set; } = 3;   // depósito: margen tras detectar billetes para que terminen de acomodarlos
    public int MaxLotesDeposito { get; set; } = 10;         // depósito: tope de conteos (iCashCount) por transacción
    public byte DestinoRetract { get; set; } = 2;           // byRetractPos: 1 = LRB, 2 = AC, 3 = NE (eCRM9250N_RetractPos)
    public int IntentosRecuperacion { get; set; } = 3;      // reinicios tras una dispensa fallida antes de pedir mantenimiento
    // NOTEVALIDMODE de CRM9250NDevCfg.ini (0 = billete real, 15 = billete simulado, como CRM9250NTest.exe de GRG).
    // El SDK solo lo lee del .ini al conectar: no hay campo equivalente en iDispenseEx/iCashCount.
    public int ModoValidacionBilletes { get; set; } = 15;

    // Indice = número RC del SDK (base 1: RC1..RC4, como en la demo de GRG). Orden de denominaciones según
    // observación física de la gaveta (2026-10-15): al jalarla, el primero en salir es USD 20, luego MXN 100,
    // 200 y 500, y al final el recycler de banda azul. Reemplaza el orden del TAC_Logical_Tecnica §2 (que
    // ponía MXN 500 antes de MXN 100) — sigue pendiente confirmar que "posición en la gaveta" == "número RC".
    public List<CaseteConfig> Casetes { get; set; } = new()
    {
        new() { Indice = 1, Nombre = "Cassette 1", Moneda = "USD", Denominacion = 20 },
        new() { Indice = 2, Nombre = "Cassette 2", Moneda = "MXN", Denominacion = 100 },
        new() { Indice = 3, Nombre = "Cassette 3", Moneda = "MXN", Denominacion = 200 },
        new() { Indice = 4, Nombre = "Cassette 4", Moneda = "MXN", Denominacion = 500 },
    };

    // Código de denominación del SDK (wCode) -> etiqueta legible. Se llena con lo observado en Test_AceptarBilletes.
    public Dictionary<int, string> EtiquetaDenominacion { get; set; } = new();
}

public sealed class CaseteConfig
{
    public int Indice { get; set; }
    public string Nombre { get; set; } = "";
    public string Moneda { get; set; } = "";
    public int Denominacion { get; set; }
}

public sealed class HopperConfig
{
    public string Nombre { get; set; } = "";
    public int Denominacion { get; set; }
    public string Puerto { get; set; } = "COM3";
    public byte Direccion { get; set; } = 3;
}

public sealed class MonederosConfig
{
    public int BaudRate { get; set; } = 9600;
    public int MaxMonedasPrueba { get; set; } = 10;
    // Puerto y direcciones confirmados en la máquina real (2026-10-16) con el software oficial SCH2 y luego
    // con Test_PingMonederos: COM1, no COM3 (el COM3 original era un supuesto nunca verificado). COM1 es de
    // la tarjeta serial multi-puerto interna (ACPI\PNP0501), no del adaptador USB CP210x (ese es COM13/14,
    // de otro periférico). Hacía falta además activar DTR/RTS al abrir el puerto (ver CcTalk.cs).
    public List<HopperConfig> Hoppers { get; set; } = new()
    {
        new() { Nombre = "MK4 #1 ($1 MXN)", Denominacion = 1, Puerto = "COM1", Direccion = 3 },
        new() { Nombre = "MK4 #2 ($5 MXN)", Denominacion = 5, Puerto = "COM1", Direccion = 4 },
    };
}

public sealed class EscanerConfig
{
    public bool Simulado { get; set; } = true;
    public int TimeoutSegundos { get; set; } = 30;
}

public sealed class ImpresoraConfig
{
    public string Modo { get; set; } = "Spooler";
    public string NombreImpresora { get; set; } = "POS80 Printer";
    public string Puerto { get; set; } = "COM4";
    public int BaudRate { get; set; } = 115200;
    public string NombreUsb { get; set; } = "POS80";
    public string RutaLogo { get; set; } = "logo.png";
}

public sealed class SensoresConfig
{
    public string Puerto { get; set; } = "COM5";
    public int BaudRate { get; set; } = 9600;
    public int SilencioTramaMs { get; set; } = 30;
    public Dictionary<string, string> Tramas { get; set; } = new();
}

// Tarjeta "18-Channel LED Light Bar Control Board" (modelo 24002-2): no es el ALB02, es una tarjeta
// RS232 aparte para los 18 indicadores de función. Baudios confirmados en la hoja de protocolo del fabricante.
// Puerto confirmado en la máquina real (2026-10-16) con Test_PingLeds: COM2 (no COM6, el valor original
// nunca verificado), firmware "ADK LEDBOX V2.0-20240613". Necesitó DTR/RTS activados (ver LedControlSerial.cs).
public sealed class LedsConfig
{
    public string Puerto { get; set; } = "COM2";
    public int BaudRate { get; set; } = 19200;
    public int Canales { get; set; } = 18;
    // Cuántos de los 18 canales están conectados a un ícono identificable del gabinete (recibo, moneda,
    // pasaporte, QR, efectivo). La prueba prueba canales uno por uno y se detiene en cuanto los identifica.
    public int PerifericosEsperados { get; set; } = 5;
}

// Item 12 del BEEX (GDZS-DC004-MG3021): cámara USB estándar, detectada por Windows sin driver propio.
public sealed class CamaraConfig
{
    public int IndiceDispositivo { get; set; } = 0;
    public string CarpetaCapturas { get; set; } = "Capturas";
}
