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

    // Configuración física según TAC_Logical_Tecnica §2. Indice = número RC del SDK (base 1: RC1..RC4, como en la demo de GRG).
    public List<CaseteConfig> Casetes { get; set; } = new()
    {
        new() { Indice = 1, Nombre = "Cassette 1", Moneda = "USD", Denominacion = 20 },
        new() { Indice = 2, Nombre = "Cassette 2", Moneda = "MXN", Denominacion = 500 },
        new() { Indice = 3, Nombre = "Cassette 3", Moneda = "MXN", Denominacion = 200 },
        new() { Indice = 4, Nombre = "Cassette 4", Moneda = "MXN", Denominacion = 100 },
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
    public List<HopperConfig> Hoppers { get; set; } = new()
    {
        new() { Nombre = "MK4 #1 ($1 MXN)", Denominacion = 1, Puerto = "COM3", Direccion = 3 },
        new() { Nombre = "MK4 #2 ($5 MXN)", Denominacion = 5, Puerto = "COM3", Direccion = 4 },
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
    public string NombreImpresora { get; set; } = "KP-80A";
    public string Puerto { get; set; } = "COM4";
    public int BaudRate { get; set; } = 115200;
}

public sealed class SensoresConfig
{
    public string Puerto { get; set; } = "COM5";
    public int BaudRate { get; set; } = 9600;
    public int SilencioTramaMs { get; set; } = 30;
    public Dictionary<string, string> Tramas { get; set; } = new();
}
