using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using IntegracionCajeroPruebas.Protocolos;

namespace IntegracionCajeroPruebas.Tests;

public sealed record DocumentoLeido(string Nombre, string NumeroDocumento, string Nacionalidad,
    string FechaNacimiento, string FechaVencimiento, bool ChecksumValido, string? Mrz,
    string? ImagenColor = null, string? ImagenIR = null, string? ImagenUV = null, int TipoSdk = 0)
{
    /// <summary>Pasaporte / INE / otra tarjeta, deducido de la MRZ (ICAO 9303), no del SDK.</summary>
    public string TipoDocumento => ClasificarPorMrz(Mrz);

    // Formato por número y largo de líneas: TD3 = 2x44 (pasaporte), TD2 = 2x36, TD1 = 3x30 (tarjeta, como el reverso de la INE).
    // La primera letra da el tipo (P pasaporte, V visa, I/A/C tarjeta de identificación); una MRZ "IDMEX" es la INE mexicana.
    public static string ClasificarPorMrz(string? mrz)
    {
        var lineas = (mrz ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lineas.Length == 0) return "No identificado (sin MRZ: ¿se escaneó el frente de la INE?)";

        var tipo = lineas[0][0];
        if (lineas.Length == 3)
            return lineas[0].StartsWith("IDMEX") ? "INE (credencial para votar, tarjeta TD1)"
                 : tipo is 'I' or 'A' or 'C' ? "Tarjeta de identificación (TD1)" : "Documento TD1 no reconocido";
        if (lineas.Length == 2)
            return tipo switch
            {
                'P' => "Pasaporte",
                'V' => "Visa",
                'I' or 'A' or 'C' => "Tarjeta de identificación (TD2)",
                _ => "Documento de 2 líneas no reconocido",
            };
        return $"MRZ de {lineas.Length} líneas no reconocida";
    }
}

/// <summary>Frontera con el SDK propietario del MEPR500+. Solo lo que la prueba necesita.</summary>
public interface IEscanerSdk : IDisposable
{
    string Descripcion { get; }
    void Inicializar();
    bool EsperarDocumento(TimeSpan timeout);
    DocumentoLeido Leer();
}

/// <summary>
/// Adaptador real sobre <see cref="TwrPassportSdk"/> (TWRPassportAPI.dll). Modo automático: el SDK dispara
/// un callback nativo cuando detecta y captura un documento sobre la ventana; aquí solo se espera esa señal
/// con un <see cref="ManualResetEventSlim"/> y se traduce el último <see cref="PassportInfo"/> recibido.
/// </summary>
public sealed class EscanerMeprSdk : IEscanerSdk
{
    public string Descripcion => "MEPR500+ (SDK del fabricante, TWRPassportAPI.dll)";

    private readonly ManualResetEventSlim _capturado = new(initialState: false);
    private PassportInfo _ultimo;
    private DocDataEX _ultimoDatos;

    public void Inicializar()
    {
        TwrPassportSdk.DocumentoCapturado += OnDocumentoCapturado;
        if (!TwrPassportSdk.Inicializar())
            throw new InvalidOperationException(
                "TW_Initialize/TW_SetCapture_Start/TW_SetAutoModel devolvió false. " +
                "Revisar que el lector esté conectado por USB y que TWRPassportAPI.dll y sus dependencias " +
                "(carpeta BinTW-20230815 DEMO) estén junto al .exe.");
    }

    private void OnDocumentoCapturado(PassportInfo info, DocDataEX datos)
    {
        _ultimo = info;
        _ultimoDatos = datos;
        _capturado.Set();
    }

    // El callback nativo corre en un hilo propio de la DLL: Wait/Set del ManualResetEventSlim son seguros
    // entre ese hilo y el hilo de la prueba, no hace falta un lock adicional para esta sola variable.
    public bool EsperarDocumento(TimeSpan timeout) => _capturado.Wait(timeout);

    public DocumentoLeido Leer()
    {
        var nombre = string.IsNullOrWhiteSpace(_ultimo.Name_EN)
            ? $"{_ultimo.Surname} {_ultimo.GivenName}".Trim()
            : _ultimo.Name_EN;
        var mrz = string.Join('\n', new[] { _ultimo.chMRZ1, _ultimo.chMRZ2, _ultimo.chMRZ3 }
            .Where(linea => !string.IsNullOrWhiteSpace(linea)));
        return new DocumentoLeido(nombre, _ultimo.PassportNo, _ultimo.Nationality,
            _ultimo.BirthDate, _ultimo.ExpiryDate, _ultimo.bCheckSum, mrz.Length == 0 ? null : mrz,
            ResolverRuta(_ultimoDatos.ColorFileName), ResolverRuta(_ultimoDatos.IRFileName),
            ResolverRuta(_ultimoDatos.UVFileName), _ultimoDatos.iType);
    }

    // El SDK deja las imágenes en disco y solo entrega su ruta (como hace el demo IDRead/Form1.cs, que las abre
    // con FileStream); si viene relativa se interpreta contra la carpeta del .exe (Program.cs fija ese cwd).
    private static string? ResolverRuta(string? ruta) =>
        string.IsNullOrWhiteSpace(ruta) ? null : Path.GetFullPath(ruta.Trim());

    public void Dispose()
    {
        TwrPassportSdk.DocumentoCapturado -= OnDocumentoCapturado;
        TwrPassportSdk.Detener();
        _capturado.Dispose();
    }
}

/// <summary>Sustituto para ejercitar el flujo de la prueba sin hardware. Nunca valida el escáner real.</summary>
public sealed class EscanerSimulado : IEscanerSdk
{
    public string Descripcion => "SIMULADO (sin hardware)";
    public void Inicializar() { }
    public bool EsperarDocumento(TimeSpan timeout) => true;
    public DocumentoLeido Leer()
    {
        // PNG de 1x1 píxel, solo para ejercitar el guardado de la imagen sin hardware.
        var ruta = Path.Combine(Path.GetTempPath(), "escaner_simulado.png");
        File.WriteAllBytes(ruta, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg=="));
        return new("PRUEBA SIMULADA", "X0000000", "MEX", "900101", "300101", true,
            "P<MEXPRUEBA<SIMULADA<<<<<<<<<<<<<<<<<<<<<<<<", ruta);
    }
    public void Dispose() { }
}

public static partial class HardwareTests
{
    public static void Test_PingEscaner()
    {
        var cfg = HardwareConfig.Actual.Escaner;
        IEscanerSdk escaner = cfg.Simulado ? new EscanerSimulado() : new EscanerMeprSdk();
        var r = new Reporte($"Escáner MEPR500+ - Ping [{escaner.Descripcion}]");
        try
        {
            using (escaner)
            {
                escaner.Inicializar();
                r.Criterio("SDK inicializado y dispositivo detectado", true);
            }
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    /// <summary>
    /// Copia la imagen que dejó el SDK a la carpeta de capturas (el SDK puede reutilizar/borrar el archivo en la
    /// siguiente lectura). La de color es la imagen escaneada del documento y es criterio; IR/UV solo se guardan
    /// si el lector las entregó.
    /// </summary>
    private static void GuardarImagen(Reporte r, string tipo, string? origen, string carpeta, bool obligatoria)
    {
        if (origen is null || !File.Exists(origen))
        {
            if (obligatoria)
                r.Criterio($"Imagen {tipo} del documento guardada", false,
                    origen is null ? "el SDK no entregó ruta de imagen" : $"no existe el archivo {origen}");
            return;
        }

        Directory.CreateDirectory(carpeta);
        var destino = Path.GetFullPath(Path.Combine(carpeta,
            $"documento_{tipo}_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(origen)}"));
        File.Copy(origen, destino, overwrite: true);
        var bytes = new FileInfo(destino).Length;
        Console.WriteLine($"  Imagen {tipo,-12}: {destino} ({bytes} bytes)");
        if (obligatoria) r.Criterio($"Imagen {tipo} del documento guardada (> 0 bytes)", bytes > 0);
    }

    public static void Test_EscanearDocumento()
    {
        var cfg = HardwareConfig.Actual.Escaner;
        IEscanerSdk escaner = cfg.Simulado ? new EscanerSimulado() : new EscanerMeprSdk();
        var r = new Reporte($"Escáner MEPR500+ - Lectura de documento [{escaner.Descripcion}]");
        try
        {
            using (escaner)
            {
                escaner.Inicializar();
                r.Criterio("SDK inicializado y dispositivo detectado", true);

                Consola.Aviso("  >> Coloque el pasaporte o la INE sobre la ventana del escáner y no lo mueva.");
                var presente = escaner.EsperarDocumento(TimeSpan.FromSeconds(cfg.TimeoutSegundos));
                if (!r.Criterio($"Documento detectado en {cfg.TimeoutSegundos} s", presente)) { r.Finalizar(); return; }

                var doc = escaner.Leer();
                Console.WriteLine($"  Tipo de documento   : {doc.TipoDocumento} (iType del SDK: {doc.TipoSdk})");
                Console.WriteLine($"  Nombre              : {doc.Nombre}");
                Console.WriteLine($"  ID                  : {doc.NumeroDocumento}");
                Console.WriteLine($"  Nacionalidad        : {doc.Nacionalidad}");
                Console.WriteLine($"  Fecha de nacimiento : {doc.FechaNacimiento}");
                Console.WriteLine($"  Fecha de vencimiento: {doc.FechaVencimiento}");
                if (doc.Mrz is not null) Console.WriteLine($"  MRZ                 : {doc.Mrz}");

                r.Criterio("Tipo de documento identificado (pasaporte / INE / tarjeta)", doc.Mrz is not null);
                r.Criterio("Nombre, ID y Nacionalidad extraídos (no vacíos)",
                    new[] { doc.Nombre, doc.NumeroDocumento, doc.Nacionalidad }.All(c => !string.IsNullOrWhiteSpace(c)));
                // bCheckSum es el propio SDK validando el dígito verificador de la zona MRZ (ICAO 9303): a
                // diferencia del criterio anterior (que solo mira si los campos vinieron no vacíos), este es el
                // único criterio automático que garantiza que lo leído corresponde de verdad al documento, sin
                // depender de que el asistente lo compare a ojo.
                r.Criterio("Dígito verificador de la MRZ válido (bCheckSum del SDK)", doc.ChecksumValido);

                GuardarImagen(r, "color", doc.ImagenColor, cfg.CarpetaCapturas, obligatoria: true);
                GuardarImagen(r, "infrarroja", doc.ImagenIR, cfg.CarpetaCapturas, obligatoria: false);
                GuardarImagen(r, "ultravioleta", doc.ImagenUV, cfg.CarpetaCapturas, obligatoria: false);
            }
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }
}
