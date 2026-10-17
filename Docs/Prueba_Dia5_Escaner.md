# Prueba Día 5 – Escáner de pasaporte / INE MEPR500+

## Objetivo

Inicializar el SDK del escáner, detectar un documento colocado sobre la ventana, disparar la lectura y mostrar por consola **Nombre, ID, Nacionalidad, fecha de nacimiento y fecha de vencimiento** (las dos últimas, relevantes para una operación de cambio de divisas: verificar que el documento no esté caducado y registrar el dato para el expediente de identificación). Más importante que agregar campos: **garantizar que lo leído corresponde de verdad al documento**, no solo que los campos llegaron no vacíos — para eso se usa el propio dígito verificador de la MRZ que calcula el SDK (`bCheckSum`), como criterio automático independiente de la comparación visual del asistente.

## Protocolo / librería

- **SDK propietario del fabricante** (`TWRPassportAPI.dll`, OCR en segundo plano y lectura RFID/infrarrojo, ICAO 9303), recibido en `referencias_integracion/Passport scanner(MEPR500+)/MEPR500 Windows SDK 20230815` (demo WinForms en C# y en VC++, header `TWRPassportAPI.h`, manuales en `Doc/`). El código separa la prueba del SDK mediante la interfaz `IEscanerSdk`:
  - `EscanerMeprSdk`: adaptador real sobre `Protocolos/TwrPassportSdk.cs`, ya implementado (ver más abajo). Usa el **modo automático por callback** (`TW_SetAutoModel(true)` + `TW_Capture_RegisterCallbackResultImageEX`, la variante "EX" que entrega `PassportInfo` con cadenas ya parseadas, en vez de punteros crudos a buffers de imagen). **Corrección (2026-10-16, tras revisar `Example/c#/IDRead/Form1.cs` a fondo):** esta ruta EX solo se activa en el demo si se descomenta `#define API_EX` (línea 1); tal como lo distribuye el fabricante, ese define está apagado y el demo corre con la variante no-EX. Es decir, EX es código real y completo del fabricante — y la más práctica de marshalizar en .NET — pero no es la ruta que ellos mismos ejercitan por defecto. El header también declara un camino síncrono por sondeo (`TW_MonitoringDocEX` + `TW_Capture_Manual_GetInfo`) que **no se implementó**: ningún demo lo usa bajo ningún define, así que no hay ni un ejemplo funcionando de su firma real.
  - `EscanerSimulado`: devuelve datos ficticios para ejercitar el flujo. Con `Escaner.Simulado = true` la prueba **no puede aprobarse**: el criterio de coincidencia con el documento físico queda en falla a propósito.

## Precondiciones

- `TWRPassportAPI.dll`, el resto de DLLs, y la carpeta `tessdata/` de `BinTW-20230815 DEMO/` junto al `.exe` (el `.csproj` los copia solo si existe `ScannerSdkDir`; genera una advertencia de build si no). **`tessdata/` es obligatoria, no opcional:** confirmado en la máquina real (2026-10-16) que sin ella `TWRPassportAPI.dll` ni siquiera carga — usa Tesseract/OCR internamente y falla en su rutina de inicialización ("DLL initialization routine failed", Win32 `0x8007045A`) antes de que el ping pueda hacer nada. El `.csproj` original la excluía a propósito asumiendo que no hacía falta; quedó corregido.
- **Confirmado:** la DLL es de 32 bits (PE32, Intel i386) — compatible con el `PlatformTarget=x86` que ya exige la DLL de GRG, sin cambios.
- SDK instalado (drivers USB) y escáner reconocido por Windows. `Drivers/` trae `DRPassport Drivers_SCM(v2.4).exe` y un instalador de `com0com` (puerto serial virtual): sugiere que la lectura del chip RFID del documento pasa por un COM virtual, no solo USB — **sin confirmar en la máquina real**.
- Documentos de prueba: un pasaporte (con MRZ) y una INE.
- La lectura del chip RFID (ICAO 9303, `docInfo.bRFID`/`TW_Capture_GetDGData`) **no quedó implementada**: `TwrPassportSdk` solo expone el callback EX con los campos OCR/MRZ de `PassportInfo`, suficiente para el criterio E-03/E-04 (Nombre/ID/Nacionalidad) pero no para los datos del chip.

## Configuración

```json
"Escaner": {
  "Simulado": true,
  "TimeoutSegundos": 30
}
```

`Simulado` sigue en `true` por defecto: el adaptador real ya está escrito pero no se ha probado contra el lector físico. Cambiarlo a `false` el día de la prueba con el asistente.

## Código

### `Protocolos/TwrPassportSdk.cs`

Wrapper propio sobre `TWRPassportAPI.dll`, con los `[DllImport]`, estructuras (`PassportInfo`, `DocDataEX`) y el delegado de callback copiados del header y del wrapper de ejemplo del fabricante (única referencia con el marshaling ya probado).

```csharp
using System.Runtime.InteropServices;

namespace IntegracionCajeroPruebas.Protocolos;

/// <summary>
/// Datos de pasaporte/visa que entrega el callback "EX" (sin los punteros crudos de imagen de
/// <c>DocData</c>). Layout y tamaños copiados tal cual de <c>TWRPassportAPI.h</c> y del wrapper
/// del propio fabricante (SDK/Example/c#/IDRead/IDRead/DLL.cs), que es la única referencia que
/// demuestra el marshaling correcto de estas cadenas de ancho fijo.
/// </summary>
public struct PassportInfo
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name_CH;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Name_EN;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Surname;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string GivenName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string MiddleName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 6)] public string Sex;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 6)] public string Nationality;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 70)] public string BirthPlace;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 18)] public string BirthDate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 38)] public string PassportNo;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 38)] public string OtherID;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string IssuePlace;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 30)] public string IssueDate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 30)] public string ExpiryDate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 72)] public string Address;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Remark;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Remark1;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Remark2;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string chMRZ1;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string chMRZ2;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string chMRZ3;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)] public string PhotoFileName;
    public bool bCheckSum;
}

/// <summary>Metadatos de la captura (tipo de documento, tamaño, si trae chip RFID) sin los buffers de imagen crudos.</summary>
public struct DocDataEX
{
    public int iType;
    public uint FaceWidth;
    public uint FaceHeight;
    public uint Width;
    public uint Height;
    public bool bRFID;
    public bool bID2;
    public int iReserve1;
    public int iReserve2;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)] public string ColorFileName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)] public string IRFileName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)] public string UVFileName;
}

/// <summary>
/// Wrapper propio sobre el SDK nativo del lector MEPR500+ (<c>TWRPassportAPI.dll</c>, carpeta
/// <c>referencias_integracion/Passport scanner(MEPR500+)/MEPR500 Windows SDK 20230815/BinTW-20230815 DEMO</c>).
/// Como con <see cref="AutoReplyPrintSdk"/>, se declaran los <c>[DllImport]</c> directamente a partir de
/// <c>TWRPassportAPI.h</c> en vez de usar el wrapper administrado del demo del fabricante.
///
/// Camino elegido: modo automático por *callback* — <c>TW_SetAutoModel(true)</c> +
/// <c>TW_Capture_RegisterCallbackResultImageEX</c> ("EX": entrega <c>PassportInfo</c> con cadenas ya
/// parseadas, en vez de los punteros crudos a buffers de imagen de la variante no-EX). <b>Ojo:</b> en el demo
/// de ejemplo (IDRead/Form1.cs) esta ruta EX solo se activa si se descomenta <c>#define API_EX</c> en la
/// línea 1 — tal como se entrega el demo, ese define está comentado y usa la variante no-EX. Es decir, la
/// ruta EX es código real y completo del fabricante (y la más práctica de marshalizar en .NET, porque ya
/// trae las cadenas de ancho fijo listas), pero no es la que corre por defecto en el demo que ellos
/// distribuyen, así que no hay evidencia de que la hayan probado tanto como la no-EX. El header también
/// declara <c>TW_MonitoringDocEX</c> + <c>TW_Capture_Manual_GetInfo</c> para un flujo síncrono por sondeo
/// (sin callback), pero ningún demo lo ejercita bajo ningún define: implementarlo sería confiar en una firma
/// de la que no hay ni un solo ejemplo funcionando. Ambos puntos quedan anotados en
/// "Supuestos y riesgos" de Docs/Prueba_Dia5_Escaner.md.
/// </summary>
public static class TwrPassportSdk
{
    private const string Dll = "TWRPassportAPI.dll";

    // El tipo de función del callback nativo usa la macro CALLBACK (= __stdcall) en el .h; la función que
    // lo registra (esta de abajo) es una función normal exportada (= __cdecl, igual que el resto del SDK).
    // Son dos convenciones distintas en la misma declaración: PassportInfoCallback no lleva
    // [UnmanagedFunctionPointer] porque el default de .NET para delegados en Windows x86 (el proyecto está
    // fijado a x86 por la DLL de GRG) ya es StdCall, igual que hace el propio wrapper del fabricante
    // (SDK/Example/c#/IDRead/IDRead/DLL.cs) sin ese atributo.
    public delegate void PassportInfoCallback(IntPtr contexto, DocDataEX datos, PassportInfo info);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int TW_Capture_RegisterCallbackResultImageEX(PassportInfoCallback callback, IntPtr context);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool TW_Initialize(int nSpeed);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern bool TW_InitIDCard(string path, int[] com);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool TW_SetCapture_Start();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool TW_SetAutoModel(bool bAuto);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void TW_StopCapture();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool TW_DeInitialize();

    // Debe quedar en un campo estático: si el delegado solo viviera como variable local, el recolector de
    // basura podría liberarlo entre el registro y la siguiente vez que la DLL nativa invoque el callback,
    // porque desde el punto de vista del GC nada administrado sigue referenciándolo.
    private static PassportInfoCallback? _callback;

    /// <summary>Se dispara en un hilo propio de la DLL nativa cada vez que detecta y captura un documento.</summary>
    public static event Action<PassportInfo>? DocumentoCapturado;

    /// <param name="velocidad">1 = Full speed, 2 = High speed (según TWRPassportAPI.h). El demo usa 2.</param>
    public static bool Inicializar(int velocidad = 2)
    {
        if (!TW_Initialize(velocidad)) return false;

        // Igual que el demo del fabricante (IDRead/Form1.cs, btnInit_Click): llama TW_InitIDCard justo después
        // de TW_Initialize. El demo tampoco aborta si devuelve false (solo lo reporta en pantalla), así que
        // aquí se ignora el resultado también; no hay documentación de qué rompe si se omite esta llamada,
        // por eso se agrega por si el SDK la necesita para dejar el subsistema completo listo.
        TW_InitIDCard("", new int[5]);

        _callback = (_, _, info) => DocumentoCapturado?.Invoke(info);
        TW_Capture_RegisterCallbackResultImageEX(_callback, IntPtr.Zero);

        if (!TW_SetCapture_Start()) return false;
        return TW_SetAutoModel(true);
    }

    public static void Detener()
    {
        TW_StopCapture();
        TW_DeInitialize();
        _callback = null;
    }
}
```

### `Tests/Escaner.cs`

```csharp
using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using IntegracionCajeroPruebas.Protocolos;

namespace IntegracionCajeroPruebas.Tests;

public sealed record DocumentoLeido(string Nombre, string NumeroDocumento, string Nacionalidad,
    string FechaNacimiento, string FechaVencimiento, bool ChecksumValido, string? Mrz);

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

    public void Inicializar()
    {
        TwrPassportSdk.DocumentoCapturado += OnDocumentoCapturado;
        if (!TwrPassportSdk.Inicializar())
            throw new InvalidOperationException(
                "TW_Initialize/TW_SetCapture_Start/TW_SetAutoModel devolvió false. " +
                "Revisar que el lector esté conectado por USB y que TWRPassportAPI.dll y sus dependencias " +
                "(carpeta BinTW-20230815 DEMO) estén junto al .exe.");
    }

    private void OnDocumentoCapturado(PassportInfo info)
    {
        _ultimo = info;
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
            _ultimo.BirthDate, _ultimo.ExpiryDate, _ultimo.bCheckSum, mrz.Length == 0 ? null : mrz);
    }

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
    public DocumentoLeido Leer() => new("PRUEBA SIMULADA", "X0000000", "MEX", "900101", "300101", true,
        "P<MEXPRUEBA<SIMULADA<<<<<<<<<<<<<<<<<<<<<<<<");
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
                Console.WriteLine($"  Nombre              : {doc.Nombre}");
                Console.WriteLine($"  ID                  : {doc.NumeroDocumento}");
                Console.WriteLine($"  Nacionalidad        : {doc.Nacionalidad}");
                Console.WriteLine($"  Fecha de nacimiento : {doc.FechaNacimiento}");
                Console.WriteLine($"  Fecha de vencimiento: {doc.FechaVencimiento}");
                if (doc.Mrz is not null) Console.WriteLine($"  MRZ                 : {doc.Mrz}");

                r.Criterio("Nombre, ID y Nacionalidad extraídos (no vacíos)",
                    new[] { doc.Nombre, doc.NumeroDocumento, doc.Nacionalidad }.All(c => !string.IsNullOrWhiteSpace(c)));
                // bCheckSum es el propio SDK validando el dígito verificador de la zona MRZ (ICAO 9303): a
                // diferencia del criterio anterior (que solo mira si los campos vinieron no vacíos), este es el
                // único criterio automático que garantiza que lo leído corresponde de verdad al documento, sin
                // depender de que el asistente lo compare a ojo.
                r.Criterio("Dígito verificador de la MRZ válido (bCheckSum del SDK)", doc.ChecksumValido);
                r.Criterio("Los datos impresos coinciden con el documento físico",
                    !cfg.Simulado && Consola.PreguntarAsistente("Le leo nombre, ID y nacionalidad: ¿coinciden con el documento?"),
                    cfg.Simulado ? "Modo SIMULADO: esta prueba no puede aprobarse sin el hardware real." : null);
            }
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }
}
```

## Cómo ejecutar

Menú `9` (lectura) o `17` (ping: inicializa el SDK y confirma que detecta el dispositivo, sin esperar un documento). Equivalente desde `Main`:

```csharp
HardwareTests.Test_PingEscaner();
HardwareTests.Test_EscanearDocumento();
```

## Criterios de aceptación

| ID | Criterio | Cómo se verifica |
| :-- | :-- | :-- |
| E-01 | El SDK se inicializa y detecta el dispositivo | Criterio automático |
| E-02 | El documento colocado se detecta dentro del timeout | Criterio automático |
| E-03 | Se extraen Nombre, ID, Nacionalidad, fecha de nacimiento y fecha de vencimiento no vacíos de un pasaporte | Salida de consola |
| E-04 | Se extraen los mismos campos de una INE | Salida de consola |
| E-05 | Los datos coinciden con el documento físico (comparación manual) | Confirmación por consola |
| E-06 | Cinco lecturas consecutivas sin reiniciar, incluida una con documento mal colocado (falla controlada) | Prueba repetida |
| E-07 | La prueba corrió con `Simulado = false` | Revisar la configuración |
| E-08 | El dígito verificador de la MRZ es válido (`bCheckSum` que calcula el propio SDK) | Criterio automático |

**Aprobado** = E-01 a E-08. E-08 es el criterio que de verdad garantiza que la lectura corresponde al documento (no solo que los campos no vinieron vacíos): si falla, la lectura no es confiable aunque E-03/E-04 pasen.

## Modalidad remota (desarrollador a distancia + asistente en sitio)

| Momento | Qué pasa en consola | Qué hace el asistente |
| :-- | :-- | :-- |
| Antes de esperar el documento | "Coloque el pasaporte o la INE sobre la ventana del escáner y no lo mueva" | Lo coloca y avisa cuando está listo (ver `Guia_Asistente_Sitio.md`, sección Escáner) |
| Después de leer los datos | "Le leo nombre, ID y nacionalidad: ¿coinciden con el documento?" | Compara contra el documento físico y dice "sí" o "no" |

No hay `AntesDeMover`: no hay un mecanismo motorizado, solo una ventana de lectura óptica fija.

## Supuestos y riesgos

- **Confirmado (TWRPassportAPI.h + demo del fabricante):** `TW_Initialize`/`TW_SetCapture_Start`/`TW_SetAutoModel(true)`/`TW_Capture_RegisterCallbackResultImageEX` es el camino de inicialización y captura; `PassportInfo` trae `Name_EN`, `PassportNo`, `Nationality`, `BirthDate`, `ExpiryDate`, `bCheckSum`, `chMRZ1..3`, suficiente para los criterios E-03/E-04/E-08.
- **`bCheckSum` sin documentar explícitamente:** ni el `.h` ni los manuales dicen qué valida exactamente; se asume por el nombre del campo y por ser parte de una estructura orientada a MRZ (`chMRZ1..3`) que es la validación del dígito verificador de la zona MRZ según ICAO 9303 (estándar de pasaportes). Falta confirmarlo con una lectura física: forzar una lectura con el documento mal colocado (ya cubierto por E-06) debería dar `bCheckSum = false` si la suposición es correcta.
- **`TW_InitIDCard("", new int[5])` agregado (2026-10-16) replicando la secuencia del demo** (`IDRead/Form1.cs`, `btnInit_Click`, que la llama justo después de `TW_Initialize`): no hay documentación de qué rompe si se omite, así que se agrega por si el SDK la necesita para dejar listo el subsistema completo, igual que el demo tampoco bloquea si devuelve `false`.
- **`Test_PingEscaner` confirmado en la máquina real (2026-10-16), con `Escaner.Simulado = false`:** `TW_Initialize` → `TW_InitIDCard` → registro del callback EX → `TW_SetCapture_Start` → `TW_SetAutoModel(true)` completan sin error y el SDK detecta el lector (`APROBADO 1/1`). El primer intento había fallado antes en la carga de la DLL (faltaba `tessdata/`, ver "Precondiciones"), no en el protocolo — ya corregido. Falta `Test_EscanearDocumento` con un documento físico real para validar la lectura de datos en sí (nombre, ID, nacionalidad, checksum de la MRZ).
- No se ha confirmado si el OCR cubre INE (tarjeta) igual que pasaporte (MRZ de 2-3 líneas) — el demo está orientado a documentos chinos/taiwaneses, `TWPassportType` no lista explícitamente "INE mexicana".
- La lectura del chip RFID (ICAO 9303) no está implementada (ver Precondiciones); si se necesita, falta investigar `TW_Capture_SetFunctions` (bit RFID) y `TW_Capture_GetDGData`, ninguno usado todavía.
- El modo síncrono por sondeo (`TW_MonitoringDocEX` + `TW_Capture_Manual_GetInfo`) existe en el header pero no se implementó: no hay ningún demo que lo ejercite para confiar en su firma a ciegas.
- **La ruta EX no es la que corre por defecto en el demo del fabricante** (ver "Protocolo / librería" arriba): está apagada tras `#define API_EX` comentado en `Form1.cs`. Se mantiene como elección porque es la única viable de marshalizar directo en .NET sin copiar buffers de imagen a mano, pero reduce la certeza de que esté tan probada como la variante no-EX.
- El callback nativo se registra una sola vez por proceso (campo estático en `TwrPassportSdk`); si `Test_EscanearDocumento` se corre varias veces en la misma sesión de consola, falta validar que `TW_DeInitialize` deja al SDK listo para un `TW_Initialize` siguiente sin reiniciar el `.exe`.

## Evidencia a guardar

Salida de consola por cada documento (con datos sensibles tachados en cualquier reporte compartido) y fotografía del documento usado.
