# Prueba Día 3 – Impresora térmica KP-80A

## Objetivo

Imprimir un voucher mínimo con el texto **"Hola Mundo FINSO"** en negritas y ejecutar el corte de papel, demostrando control directo por comandos ESC/POS.

## Protocolo / librería

- **ESC/POS** estándar. Comandos: `ESC @` (inicializar), `ESC E 1/0` (negritas), `ESC a n` (alineación), `GS V 66 0` (avanzar y corte parcial), `DLE EOT 1` (estado en línea, solo por serial).
- Dos canales, elegidos con `Impresora.Modo`:
  - `Spooler` (USB, recomendado): se envían bytes crudos a la cola de Windows con `winspool.Drv` (`datatype = RAW`), sin usar el driver gráfico (`Protocolos/EscPos.cs`, clase `ImpresoraRaw`).
  - `Serial`: escritura directa al COM con `System.IO.Ports`.

## Precondiciones

- Impresora encendida, con rollo de 80 mm y tapa cerrada.
- Modo Spooler: driver instalado y nombre exacto de la cola (`Get-Printer | Select Name`).
- Modo Serial: puerto y baudios correctos (115200 por defecto; confirmar con el autotest de la impresora).

## Configuración

```json
"Impresora": {
  "Modo": "Spooler",
  "NombreImpresora": "KP-80A",
  "Puerto": "COM4",
  "BaudRate": 115200
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

### `Tests/Impresora.cs`

```csharp
using System.IO.Ports;
using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using IntegracionCajeroPruebas.Protocolos;

namespace IntegracionCajeroPruebas.Tests;

// Impresora térmica KP-80A con ESC/POS: por cola de Windows (USB, RAW) o directo por puerto serial.
public static partial class HardwareTests
{
    public static void Test_ImprimirVoucherPrueba()
    {
        var cfg = HardwareConfig.Actual.Impresora;
        var r = new Reporte($"Impresora KP-80A - Voucher de prueba (modo {cfg.Modo})");
        try
        {
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

            r.Criterio("Imprimió 'Hola Mundo FINSO' legible y en negritas", Consola.Confirmar("¿Se imprimió el texto en negritas?"));
            r.Criterio("El autocortador cortó el papel", Consola.Confirmar("¿Cortó el papel?"));
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
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

## Cómo ejecutar

Menú `6`. Equivalente desde `Main`:

```csharp
HardwareTests.Test_ImprimirVoucherPrueba();
```

## Criterios de aceptación

| ID | Criterio | Cómo se verifica |
| :-- | :-- | :-- |
| I-01 | Los bytes ESC/POS se entregan sin excepción (cola abierta o COM abierto) | Criterio automático |
| I-02 | Modo Serial: la impresora responde a `DLE EOT 1` y está en línea | Criterio automático (si no responde, se avisa; no bloquea) |
| I-03 | Se imprime "Hola Mundo FINSO" legible, centrado y **en negritas**, con el resto del texto en normal | Inspección del voucher |
| I-04 | El autocortador corta el papel al final | Inspección del voucher |
| I-05 | Dos impresiones seguidas funcionan sin reiniciar | Repetir la prueba |
| I-06 | Con la tapa abierta o sin papel la prueba falla de forma controlada (sin colgar el programa) | Prueba negativa opcional |

**Aprobado** = I-01, I-03, I-04 e I-05 (I-02 solo aplica en Serial).

## Supuestos y riesgos

- El nombre de la cola y los baudios por serial no están confirmados en el repositorio.
- El texto es ASCII; los acentos requieren seleccionar la tabla de caracteres (`ESC t n`), fuera del alcance de la prueba.
- `GS V 66 0` es el corte parcial con avance; si el modelo no lo soporta, probar `GS V 0` o `GS V 1`.

## Evidencia a guardar

El voucher físico (pegado en el reporte) y la salida de consola.
