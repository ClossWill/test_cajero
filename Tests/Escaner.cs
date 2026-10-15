using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;

namespace IntegracionCajeroPruebas.Tests;

public sealed record DocumentoLeido(string Nombre, string NumeroDocumento, string Nacionalidad, string? Mrz);

/// <summary>Frontera con el SDK propietario del MEPR500+. Solo lo que la prueba necesita.</summary>
public interface IEscanerSdk : IDisposable
{
    string Descripcion { get; }
    void Inicializar();
    bool EsperarDocumento(TimeSpan timeout);
    DocumentoLeido Leer();
}

/// <summary>Adaptador real: completar con las funciones del SDK del fabricante (DLL y firmas aún no disponibles en el repo).</summary>
public sealed class EscanerMeprSdk : IEscanerSdk
{
    public string Descripcion => "MEPR500+ (SDK del fabricante)";

    private static NotImplementedException Pendiente(string paso) => new(
        $"Pendiente enlazar con el SDK MEPR500+: {paso}. Agregar la DLL, declarar los [DllImport] " +
        "(o referenciar el wrapper .NET si el SDK lo trae) y completar este método.");

    public void Inicializar() => throw Pendiente("inicialización / apertura del dispositivo");
    public bool EsperarDocumento(TimeSpan timeout) => throw Pendiente("detección de documento en la ventana de lectura");
    public DocumentoLeido Leer() => throw Pendiente("lectura OCR/MRZ (Nombre, ID, Nacionalidad)");
    public void Dispose() { }
}

/// <summary>Sustituto para ejercitar el flujo de la prueba sin hardware. Nunca valida el escáner real.</summary>
public sealed class EscanerSimulado : IEscanerSdk
{
    public string Descripcion => "SIMULADO (sin hardware)";
    public void Inicializar() { }
    public bool EsperarDocumento(TimeSpan timeout) => true;
    public DocumentoLeido Leer() => new("PRUEBA SIMULADA", "X0000000", "MEX", "P<MEXPRUEBA<SIMULADA<<<<<<<<<<<<<<<<<<<<<<<<");
    public void Dispose() { }
}

public static partial class HardwareTests
{
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

                Consola.Asistente("Coloque el pasaporte o la INE sobre la ventana del escáner y no lo mueva.");
                var presente = escaner.EsperarDocumento(TimeSpan.FromSeconds(cfg.TimeoutSegundos));
                if (!r.Criterio($"Documento detectado en {cfg.TimeoutSegundos} s", presente)) { r.Finalizar(); return; }

                var doc = escaner.Leer();
                Console.WriteLine($"  Nombre       : {doc.Nombre}");
                Console.WriteLine($"  ID           : {doc.NumeroDocumento}");
                Console.WriteLine($"  Nacionalidad : {doc.Nacionalidad}");
                if (doc.Mrz is not null) Console.WriteLine($"  MRZ          : {doc.Mrz}");

                r.Criterio("Nombre, ID y Nacionalidad extraídos (no vacíos)",
                    new[] { doc.Nombre, doc.NumeroDocumento, doc.Nacionalidad }.All(c => !string.IsNullOrWhiteSpace(c)));
                r.Criterio("Los datos impresos coinciden con el documento físico",
                    !cfg.Simulado && Consola.PreguntarAsistente("Le leo nombre, ID y nacionalidad: ¿coinciden con el documento?"),
                    cfg.Simulado ? "Modo SIMULADO: esta prueba no puede aprobarse sin el hardware real." : null);
            }
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }
}
