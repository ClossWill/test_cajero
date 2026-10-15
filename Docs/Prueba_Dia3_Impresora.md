# Prueba Día 3 – Impresora térmica KP-80A

## Objetivo

Imprimir un voucher mínimo con el texto **"Hola Mundo FINSO"** en negritas y ejecutar el corte de papel, demostrando control directo por comandos ESC/POS **y** validando con certeza el canal que se usará en el despliegue productivo (ver "Spooler vs. SDK" más abajo).

## Protocolo / librería

- **ESC/POS** estándar. Comandos: `ESC @` (inicializar), `ESC E 1/0` (negritas), `ESC a n` (alineación), `GS V 66 0` (avanzar y corte parcial), `DLE EOT 1` (estado en línea, solo por serial).
- Tres canales, elegidos con `Impresora.Modo`:
  - `Spooler` (USB): se envían bytes crudos a la cola de Windows con `winspool.Drv` (`datatype = RAW`), sin usar el driver gráfico (`Protocolos/EscPos.cs`, clase `ImpresoraRaw`).
  - `Serial`: escritura directa al COM con `System.IO.Ports`.
  - **`UsbSdk` (recomendado para producción):** usa el SDK nativo del fabricante (`autoreplyprint.dll`, modo "autoreply" por USB) en vez del spooler de Windows. Permite consultar el estado real (papel/tapa/cortador) antes de imprimir y confirmar con `QueryPrintResult` que el ticket salió, no solo que se envió el comando (`Protocolos/AutoReplyPrintSdk.cs`).

### Spooler vs. SDK: por qué importa para producción

El modo `Spooler` basta para esta prueba de hardware, pero **no** es el canal recomendado para la app oficial:

1. **No confirma impresión real.** `WritePrinter` confirma que el spooler de Windows recibió los bytes, no que la impresora los imprimió. El servicio de cola puede encolar, reintentar o colgarse sin que la app se entere — en un flujo de dispensación de efectivo, no saber si el comprobante salió es un problema de conciliación.
2. **Sin estado antes de imprimir.** Hay que saber si hay papel, si la tapa está cerrada y si el cortador está libre *antes* de imprimir, no después de que el cliente ya espera el ticket. El SDK, con `autoreplymode=1`, hace que la impresora reporte su estado solo y permite consultarlo con `CP_Pos_QueryRTStatus`.
3. **Dependencia del servicio Spooler de Windows**, que se puede detener, bloquear por otro trabajo o fallar en frío tras un reinicio — un punto de falla fuera del control de la app en un kiosco.

Por eso esta prueba implementa también el modo `UsbSdk`: valida en la máquina real el mismo canal (estado + confirmación de impresión) que se extraerá a la app oficial, en vez de validar solo el camino corto del spooler.

## Precondiciones

- Impresora encendida, con rollo de 80 mm y tapa cerrada.
- Modo Spooler: driver instalado y nombre exacto de la cola (`Get-Printer | Select Name`).
- Modo Serial: puerto y baudios correctos (115200 por defecto; confirmar con el autotest de la impresora).
- Modo UsbSdk: `autoreplyprint.dll` (win32) junto al `.exe` — la compila el `.csproj` desde `referencias_integracion/Receipt Printer(KP-80A)/autoreplyprint_dll_win32/`. Solo una impresora USB conectada (si hay varias, hace falta implementar `CP_Port_EnumUsb` para elegir).

## Configuración

```json
"Impresora": {
  "Modo": "UsbSdk",
  "NombreImpresora": "POS80 Printer",
  "Puerto": "COM4",
  "BaudRate": 115200,
  "NombreUsb": "POS80"
}
```

## Código

### `Protocolos/EscPos.cs`

```csharp
using System.Runtime.InteropServices;
using System.Text;

namespace IntegracionCajeroPruebas.Protocolos;

/// <summary>Comandos ESC/POS estándar usados en la prueba de la KP-80A.</summary>
public static class EscPos
{
    public static readonly byte[] Inicializar = { 0x1B, 0x40 };          // ESC @
    public static readonly byte[] NegritaOn = { 0x1B, 0x45, 0x01 };      // ESC E 1
    public static readonly byte[] NegritaOff = { 0x1B, 0x45, 0x00 };     // ESC E 0
    public static readonly byte[] Centrar = { 0x1B, 0x61, 0x01 };        // ESC a 1
    public static readonly byte[] Izquierda = { 0x1B, 0x61, 0x00 };      // ESC a 0
    public static readonly byte[] SaltoLinea = { 0x0A };
    public static readonly byte[] AvanzarYCortar = { 0x1D, 0x56, 0x42, 0x00 }; // GS V 66 0: avanza y corte parcial
    public static readonly byte[] EstadoEnLinea = { 0x10, 0x04, 0x01 };  // DLE EOT 1 (solo canal serial)

    public static byte[] Texto(string s) => Encoding.ASCII.GetBytes(s);

    public static byte[] Concatenar(params byte[][] partes) => partes.SelectMany(p => p).ToArray();

    /// <summary>Respuesta a DLE EOT 1: bit 3 en 1 = fuera de línea.</summary>
    public static bool EstaEnLinea(byte estado) => (estado & 0x08) == 0;
}

/// <summary>Envía bytes crudos a una impresora instalada en Windows (USB) a través del spooler, sin driver gráfico.</summary>
public static class ImpresoraRaw
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class DocInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string? DocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string? DataType;
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string nombre, out IntPtr h, IntPtr defaults);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr h);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool StartDocPrinter(IntPtr h, int nivel, [In] DocInfo doc);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr h);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr h);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr h);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr h, IntPtr datos, int cantidad, out int escritos);

    public static void Enviar(string nombreImpresora, byte[] bytes)
    {
        if (!OpenPrinter(nombreImpresora, out var h, IntPtr.Zero))
            throw new InvalidOperationException(
                $"No se pudo abrir la impresora '{nombreImpresora}' (Win32 {Marshal.GetLastWin32Error()}). Revisar Get-Printer.");

        var p = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, p, bytes.Length);
            var doc = new DocInfo { DocName = "Prueba FINSO", DataType = "RAW" };
            if (!StartDocPrinter(h, 1, doc)) throw new InvalidOperationException($"StartDocPrinter Win32 {Marshal.GetLastWin32Error()}");
            try
            {
                StartPagePrinter(h);
                if (!WritePrinter(h, p, bytes.Length, out var escritos) || escritos != bytes.Length)
                    throw new InvalidOperationException($"WritePrinter escribió {escritos}/{bytes.Length} bytes (Win32 {Marshal.GetLastWin32Error()})");
                EndPagePrinter(h);
            }
            finally { EndDocPrinter(h); }
        }
        finally
        {
            Marshal.FreeHGlobal(p);
            ClosePrinter(h);
        }
    }
}
```

### `Protocolos/AutoReplyPrintSdk.cs` (modo `UsbSdk`)

Wrapper propio por P/Invoke directo a `autoreplyprint.dll` nativa (cdecl, cadenas ANSI) en vez del wrapper administrado `autoreplyprint.cs.dll` del SDK (es una DLL .NET Framework antigua; se evita esa dependencia, igual que `EscPos.cs`/`CcTalk.cs` implementan el protocolo propio).

```csharp
using System.Runtime.InteropServices;

namespace IntegracionCajeroPruebas.Protocolos;

/// <summary>
/// Wrapper mínimo propio sobre el SDK nativo del fabricante de la KP-80A (<c>autoreplyprint.dll</c>,
/// carpeta <c>referencias_integracion/Receipt Printer(KP-80A)/.../windowsSDK.rar/C#/.../autoreplyprint.c/dll/win32</c>).
/// Se invoca el .dll nativo directamente (P/Invoke, calling convention cdecl por defecto en el .h del SDK,
/// cadenas ANSI) en vez de referenciar el wrapper administrado "autoreplyprint.cs.dll" que trae el SDK:
/// es una DLL .NET Framework antigua y así se evita esa dependencia, igual que <see cref="EscPos"/> y
/// <see cref="CcTalkBus"/> implementan su protocolo propio en vez de usar la librería del fabricante.
///
/// Este canal (USB + modo "autoreply") es el que GRG/BEEX documentan para producción: a diferencia del
/// spooler de Windows (<see cref="ImpresoraRaw"/>), permite consultar estado real (papel, tapa, cortador)
/// antes de imprimir y confirmar con <see cref="QueryPrintResult"/> que el ticket salió, no solo que se
/// envió. Ver "Supuestos y riesgos" en Docs/Prueba_Dia3_Impresora.md.
/// </summary>
public static class AutoReplyPrintSdk
{
    private const string Dll = "autoreplyprint.dll";

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr CP_Port_OpenUsb(string name, int autoreplymode);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Port_Close(IntPtr handle);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Pos_ResetPrinter(IntPtr handle);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Pos_SetAlignment(IntPtr handle, int nAlignment);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Pos_SetTextBold(IntPtr handle, int nBold);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int CP_Pos_PrintText(IntPtr handle, string str);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Pos_FeedLine(IntPtr handle, int numLines);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Pos_FullCutPaper(IntPtr handle);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Pos_QueryRTStatus(IntPtr handle, uint timeout);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Pos_QueryPrintResult(IntPtr handle, uint timeout);

    public const int AlineacionIzquierda = 0;
    public const int AlineacionCentro = 1;
    public const int AlineacionDerecha = 2;

    /// <summary>Abre el puerto USB en modo autoreply (la impresora reporta su estado sin que se le pregunte).
    /// <paramref name="nombre"/> puede ser cualquier texto si solo hay una impresora USB conectada
    /// (así lo documenta el SDK); null/handle inválido si no la encuentra.</summary>
    public static IntPtr Abrir(string nombre) => CP_Port_OpenUsb(nombre, autoreplymode: 1);

    public static bool EsValido(IntPtr handle) => handle != IntPtr.Zero;

    public static void Cerrar(IntPtr handle)
    {
        if (EsValido(handle)) CP_Port_Close(handle);
    }

    public static bool Reset(IntPtr handle) => CP_Pos_ResetPrinter(handle) != 0;
    public static bool Alinear(IntPtr handle, int alineacion) => CP_Pos_SetAlignment(handle, alineacion) != 0;
    public static bool Negrita(IntPtr handle, bool activa) => CP_Pos_SetTextBold(handle, activa ? 1 : 0) != 0;
    public static bool Texto(IntPtr handle, string texto) => CP_Pos_PrintText(handle, texto) != 0;
    public static bool AvanzarLineas(IntPtr handle, int lineas) => CP_Pos_FeedLine(handle, lineas) != 0;
    public static bool CorteCompleto(IntPtr handle) => CP_Pos_FullCutPaper(handle) != 0;

    /// <summary>true si pudo pedir/leer el estado; <paramref name="rtstatus"/> queda en 0 si no responde a tiempo.</summary>
    public static bool ConsultarEstado(IntPtr handle, uint timeoutMs, out int rtstatus)
    {
        rtstatus = CP_Pos_QueryRTStatus(handle, timeoutMs);
        return rtstatus != 0;
    }

    /// <summary>Confirma si el último trabajo de impresión realmente salió (no solo que se envió).</summary>
    public static bool ConsultarResultadoImpresion(IntPtr handle, uint timeoutMs) =>
        CP_Pos_QueryPrintResult(handle, timeoutMs) != 0;

    // Máscaras de CP_RTSTATUS_XXX según autoreplyprint.h (bytes: [0]=general [1]=estado [2]=error [3]=papel).
    public static bool FueraDeLinea(int rtstatus) => ((rtstatus >> 0) & 0x08) == 0x08;
    public static bool TapaAbierta(int rtstatus) => ((rtstatus >> 8) & 0x04) == 0x04;
    public static bool SinPapel(int rtstatus) => ((rtstatus >> 8) & 0x20) == 0x20;
    public static bool ErrorOcurrido(int rtstatus) => ((rtstatus >> 8) & 0x40) == 0x40;
    public static bool ErrorCortador(int rtstatus) => ((rtstatus >> 16) & 0x08) == 0x08;
    public static bool ErrorIrrecuperable(int rtstatus) => ((rtstatus >> 16) & 0x20) == 0x20;
    public static bool PapelPorAgotarse(int rtstatus) => ((rtstatus >> 24) & 0x0C) == 0x0C;

    public static string DescribirEstado(int rtstatus)
    {
        var problemas = new List<string>();
        if (FueraDeLinea(rtstatus)) problemas.Add("fuera de línea");
        if (TapaAbierta(rtstatus)) problemas.Add("tapa abierta");
        if (SinPapel(rtstatus)) problemas.Add("sin papel");
        if (PapelPorAgotarse(rtstatus)) problemas.Add("papel por agotarse");
        if (ErrorCortador(rtstatus)) problemas.Add("error de cortador");
        if (ErrorIrrecuperable(rtstatus)) problemas.Add("error irrecuperable");
        if (ErrorOcurrido(rtstatus)) problemas.Add("error reportado");
        return problemas.Count == 0 ? "sin alertas" : string.Join(", ", problemas);
    }
}
```

### `Tests/Impresora.cs`

```csharp
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
```

## Modalidad remota (desarrollador a distancia + asistente en sitio)

En los tres modos, la prueba solo necesita al asistente al final, para confirmar lo que no se puede leer por software:

| Momento | Qué pasa en consola | Qué hace el asistente |
| :-- | :-- | :-- |
| Después de imprimir | "¿Salió un ticket con 'Hola Mundo FINSO' en negritas?" | Mira el ticket y dice "sí" o "no" (ver `Guia_Asistente_Sitio.md`, sección Impresora) |
| Después de imprimir | "¿El ticket salió cortado (suelto)?" | Lo confirma |

No hay `AntesDeMover`: no hay un mecanismo donde el asistente pueda tener la mano dentro.

## Cómo ejecutar

Menú `6`. Equivalente desde `Main`:

```csharp
HardwareTests.Test_ImprimirVoucherPrueba();
```

## Criterios de aceptación

| ID | Criterio | Cómo se verifica |
| :-- | :-- | :-- |
| I-01 | Los bytes/comandos se entregan sin excepción (cola abierta, COM abierto o handle USB-SDK válido) | Criterio automático |
| I-02 | Modo Serial: la impresora responde a `DLE EOT 1` y está en línea | Criterio automático (si no responde, se avisa; no bloquea) |
| I-03 | Se imprime "Hola Mundo FINSO" legible, centrado y **en negritas**, con el resto del texto en normal | Inspección del voucher |
| I-04 | El autocortador corta el papel al final | Inspección del voucher |
| I-05 | Dos impresiones seguidas funcionan sin reiniciar | Repetir la prueba |
| I-06 | Con la tapa abierta o sin papel la prueba falla de forma controlada (sin colgar el programa) | Prueba negativa opcional |
| I-07 | Modo UsbSdk: antes de imprimir no reporta sin papel / tapa abierta / error de cortador | Criterio automático (`CP_Pos_QueryRTStatus`) |
| I-08 | Modo UsbSdk: el SDK confirma que el ticket se imprimió, no solo que se envió | Criterio automático (`CP_Pos_QueryPrintResult`) |

**Aprobado** = I-01, I-03, I-04 e I-05 (I-02 solo aplica en Serial; I-07/I-08 solo aplican en UsbSdk, y son los que dan certeza para el despliegue productivo).

## Supuestos y riesgos

- **Nombre de la cola confirmado en la máquina (2026-10-14):** `POS80 Printer` (driver `POS80`, puerto `CP001`), no "KP-80A". Los baudios por serial siguen sin confirmar (la impresora también expone un puente CP210x en COM13/COM14; no se ha determinado cuál es de la impresora). Esto solo afecta a los modos Spooler/Serial.
- **Modo UsbSdk sin probar aún en la máquina real** (2026-10-14): el código compila y la DLL se copia al `.exe`, pero falta ejecutarlo frente al hardware. Si `CP_Port_OpenUsb` no encuentra la impresora, hace falta implementar `CP_Port_EnumUsb` (el SDK usa un buffer multi-string, no un `String[]` directo como el wrapper C#) para listar los nombres válidos.
- **`QueryRTStatus` puede devolver 0 por dos motivos indistinguibles:** falla de lectura o, según el propio manual del SDK, "no hay verificación, no se garantiza que el resultado sea correcto". Por eso el criterio I-07 solo se evalúa cuando hubo respuesta; si no la hay, se avisa y se continúa (igual que `DLE EOT 1` en Serial).
- El texto es ASCII; los acentos requieren seleccionar la tabla de caracteres (`ESC t n` en ESC/POS, o `CP_Pos_PrintTextInUTF8` en el SDK), fuera del alcance de la prueba.
- `GS V 66 0` es el corte parcial con avance; si el modelo no lo soporta, probar `GS V 0` o `GS V 1`. En modo UsbSdk se usa `CP_Pos_FullCutPaper` (corte completo); si el hardware no lo soporta, usar `CP_Pos_HalfCutPaper` o `CP_Pos_FeedAndHalfCutPaper`.

## Evidencia a guardar

El voucher físico (pegado en el reporte) y la salida de consola.
