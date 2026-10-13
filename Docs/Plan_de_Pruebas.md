# Plan de Pruebas de Hardware – Agente ATM FINSO

> **Alcance:** aplicación de consola (.NET 8) que valida la comunicación directa con cada periférico. **No** es la aplicación ni un middleware: sin lógica de negocio, sin UI, sin base de datos. Cada prueba es un método `Test_*` que se lanza desde un menú.
> **Referencia de hardware:** `especificaciones_de_hardware_finso.md` (HZ20260125).

## 1. Cronograma

| Día | Periférico | Protocolo / librería | Métodos `Test_*` | Documento |
| :-- | :-- | :-- | :-- | :-- |
| 0 | Entorno y estructura | .NET 8, `System.IO.Ports` | – (menú) | este documento |
| 1 | Recicladora GRG 9250N | `CRM9250NDev.dll` vía `CRM9250N.cs` | `Test_InicializarRecicladora`, `Test_AceptarBilletes`, `Test_DispensarBilletes` | [Prueba_Dia1_Recicladora.md](Prueba_Dia1_Recicladora.md) |
| 2 | Monederos MK4 (x2) | ccTalk sobre COM | `Test_PingMonederos`, `Test_DispensarMonedas` | [Prueba_Dia2_Monederos.md](Prueba_Dia2_Monederos.md) |
| 3 | Impresora KP-80A | ESC/POS (spooler USB o COM) | `Test_ImprimirVoucherPrueba` | [Prueba_Dia3_Impresora.md](Prueba_Dia3_Impresora.md) |
| 4 | Módulo ALB02 (sensores) | Serial reactivo (COM) | `Test_CalibrarSensores`, `Test_EscucharSensores` | [Prueba_Dia4_Sensores.md](Prueba_Dia4_Sensores.md) |
| 5 | Escáner MEPR500+ | SDK propietario | `Test_EscanearDocumento` | [Prueba_Dia5_Escaner.md](Prueba_Dia5_Escaner.md) |
| 6 | Cierre y regresión | – | Todos, en secuencia | sección 5 |

Orden por dependencias: la recicladora es el componente más crítico y el de más riesgo, por eso va primero. La impresora va antes que sensores y escáner porque sirve para imprimir la evidencia del resto. El escáner queda al final porque depende de recibir el SDK del fabricante.

## 2. Día 0 – Preparación del entorno

1. Instalar .NET 8 SDK en la máquina objetivo (Windows 11 IoT Enterprise LTSC 2024) o publicar con `dotnet publish -c Release -r win-x64 --self-contained`.
2. Identificar los puertos COM en *Administrador de dispositivos → Puertos (COM y LPT)* y anotarlos en `hardware.json` (recicladora no usa COM desde nuestro código; lo gestiona la DLL).
3. Copiar la carpeta `dll` del SDK de GRG junto al `.exe` (el `.csproj` lo hace solo desde `referencias_integracion\CRM9250NDev_V2.3.7.6\dll` del repo) **más `DevBaseDll.dll` y el driver USB de GRG**, que no vienen en el zip. La DLL es de 32 bits: el proyecto ya compila como x86 y la máquina IoT necesita el runtime x86 de .NET 8 (o publicar con `-r win-x86 --self-contained`).
4. Instalar el driver de la KP-80A y anotar el nombre exacto de la cola (`Get-Printer | Select Name`).
5. Verificar que ningún otro software (la app oficial, utilerías del fabricante) tenga abiertos los puertos.
6. Compilar y ejecutar: `dotnet run`. Debe aparecer el menú; las pruebas sin hardware conectado deben fallar de forma controlada.

### Estructura del proyecto

```
IntegracionCajeroPruebas.csproj
Program.cs                 menú de consola
hardware.json              puertos COM, direcciones y supuestos (se edita en la máquina)
CRM9250N.cs                fuente nativa del fabricante (sin modificar)
Config/HardwareConfig.cs   carga de hardware.json
Helpers/                   Consola (salida), Reporte (criterios), Native (marshaling)
Protocolos/                CcTalk, EscPos + ImpresoraRaw, SerialTramas
Tests/                     una clase parcial HardwareTests por periférico
Docs/                      estos documentos
```

### Program.cs (menú)

```csharp
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

Console.WriteLine("Iniciando Pruebas de Hardware FINSO...");
Console.WriteLine($"Proceso de {(Environment.Is64BitProcess ? 64 : 32)} bits | config: {HardwareConfig.RutaArchivo}"
                  + (File.Exists(HardwareConfig.RutaArchivo) ? "" : " (no existe: se usan valores por defecto)"));

var menu = new (string Clave, string Texto, Action Accion)[]
{
    ("1", "Recicladora  - Inicializar",              HardwareTests.Test_InicializarRecicladora),
    ("2", "Recicladora  - Aceptar billetes",         () => HardwareTests.Test_AceptarBilletes()),
    ("3", "Recicladora  - Dispensar billetes",       () => HardwareTests.Test_DispensarBilletes(
                                                         PedirEntero("Cantidad de billetes", 1), PedirTexto("Moneda (MXN/USD)", "MXN"),
                                                         PedirEntero("Denominación (500/200/100 MXN, 20 USD)", 100))),
    ("10", "Recicladora - Billete inválido (espera rechazo)", () => HardwareTests.Test_AceptarBilletes(esperaRechazo: true)),
    ("4", "Monederos    - Ping ccTalk",              HardwareTests.Test_PingMonederos),
    ("5", "Monederos    - Dispensar monedas",        () => HardwareTests.Test_DispensarMonedas(
                                                         PedirEntero("Monedas de $1", 2), PedirEntero("Monedas de $5", 1))),
    ("6", "Impresora    - Voucher de prueba",        HardwareTests.Test_ImprimirVoucherPrueba),
    ("7", "Sensores     - Calibrar tramas ALB02",    HardwareTests.Test_CalibrarSensores),
    ("8", "Sensores     - Escuchar (puerta/vibración)", HardwareTests.Test_EscucharSensores),
    ("9", "Escáner      - Leer documento",           HardwareTests.Test_EscanearDocumento),
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
```

### Helpers compartidos

`Reporte` acumula los criterios de aceptación de cada prueba y emite `APROBADO`/`RECHAZADO` al final. Las confirmaciones físicas (¿cayeron las monedas?, ¿cortó el papel?) se piden por consola con `Consola.Confirmar`.

```csharp
namespace IntegracionCajeroPruebas.Helpers;

/// <summary>Acumula los criterios de aceptación de una prueba y muestra el veredicto final.</summary>
public sealed class Reporte
{
    private readonly string _titulo;
    private readonly List<(string Descripcion, bool Cumple)> _criterios = new();

    public Reporte(string titulo)
    {
        _titulo = titulo;
        Consola.Titulo(titulo);
    }

    public bool Criterio(string descripcion, bool cumple, string? detalle = null)
    {
        _criterios.Add((descripcion, cumple));
        if (cumple) Consola.Ok($"[OK]    {descripcion}");
        else Consola.Error($"[FALLA] {descripcion}");
        if (!string.IsNullOrEmpty(detalle)) Consola.Info($"        {detalle}");
        return cumple;
    }

    public bool Finalizar()
    {
        var aprobados = _criterios.Count(c => c.Cumple);
        var ok = aprobados == _criterios.Count && _criterios.Count > 0;
        Console.WriteLine();
        var texto = $"RESULTADO {_titulo}: {(ok ? "APROBADO" : "RECHAZADO")} ({aprobados}/{_criterios.Count} criterios)";
        if (ok) Consola.Ok(texto); else Consola.Error(texto);
        return ok;
    }
}
```

```csharp
using System.Runtime.InteropServices;
using System.Text;

namespace IntegracionCajeroPruebas.Helpers;

/// <summary>Utilidades para pasar structs [StructLayout] con ByValArray (como los de CRM9250N.cs) a una DLL nativa.</summary>
public static class Native
{
    /// <summary>Struct con todos sus arrays ya instanciados al tamaño de SizeConst (evita nulls al hacer marshal).</summary>
    public static T Cero<T>() where T : struct
    {
        var p = AsignarCero(Marshal.SizeOf<T>());
        try { return Marshal.PtrToStructure<T>(p)!; }
        finally { Marshal.FreeHGlobal(p); }
    }

    public static T Leer<T>(IntPtr p) where T : struct => Marshal.PtrToStructure<T>(p)!;

    public static IntPtr AsignarCero(int bytes)
    {
        var p = Marshal.AllocHGlobal(bytes);
        Marshal.Copy(new byte[bytes], 0, p, bytes);
        return p;
    }

    /// <summary>Texto ASCII hasta el primer byte nulo.</summary>
    public static string Texto(byte[]? bytes)
    {
        if (bytes is null) return "";
        var fin = Array.IndexOf(bytes, (byte)0);
        return Encoding.ASCII.GetString(bytes, 0, fin < 0 ? bytes.Length : fin).Trim();
    }
}

/// <summary>Memoria no administrada que se libera toda junta al terminar la prueba.</summary>
public sealed class NativeScope : IDisposable
{
    private readonly List<IntPtr> _punteros = new();

    // La demo de GRG reserva 8 veces el tamaño de cada struct (y avisa de que el tamaño exacto "falla"):
    // se copia esa holgura para que el SDK nunca escriba fuera del buffer.
    private const int Holgura = 8;

    /// <summary>Buffer en cero para parámetros de salida.</summary>
    public IntPtr Salida<T>() where T : struct
    {
        var p = Native.AsignarCero(Marshal.SizeOf<T>() * Holgura);
        _punteros.Add(p);
        return p;
    }

    /// <summary>Copia el struct a memoria nativa (parámetro de entrada o entrada/salida).</summary>
    public IntPtr Entrada<T>(T valor) where T : struct
    {
        var p = Native.AsignarCero(Marshal.SizeOf<T>() * Holgura);
        Marshal.StructureToPtr(valor, p, false);
        _punteros.Add(p);
        return p;
    }

    public void Dispose()
    {
        foreach (var p in _punteros) Marshal.FreeHGlobal(p);
        _punteros.Clear();
    }
}
```

### Configuración (`hardware.json`)

```jsonc
{
  // Editar en la máquina real; no requiere recompilar. Los valores marcados VERIFICAR son supuestos.
  "Recicladora": {
    // Confirmados con la demo y el PDF del SDK de GRG (CRM9250NDev_V2.3.7.6).
    "NombreDispositivoLogico": "CRM9250NDev", // nodo [CRM9250NDev] de GRGDTATM_CommCfg.ini
    "CodigoExito": 0,                         // la demo compara ret == 0
    "ModoAtc": 1,                             // byAtcMode: 1 = auditoría y RC
    "ModoTrabajo": 0,                         // 0 = modo CRM
    "AceptarTodasDenominaciones": 1,          // byDcAccepetAllDenom: acepta todas las de awAcceptableDenom
    "ConfigInicio": 0,
    "LimiteDeposito": 10,
    "TipoDispensa": 1,                        // byDispType: 1 = dispensar por casete RC
    "DestinoRechazoDispensa": 1,              // byRejectToAc: 1 = AC1, 2 = AC2, 3 = LRB
    "DestinoRechazoDeposito": 1,              // byRejectPos: 1 = AC1, 2 = AC2, 3 = LRB
    "MaxBilletesPrueba": 5,
    // Mapa físico según TAC_Logical_Tecnica §2. "Indice" = número RC del SDK, base 1 (RC2 = 2 en la demo).
    "Casetes": [
      { "Indice": 1, "Nombre": "Cassette 1", "Moneda": "USD", "Denominacion": 20 },
      { "Indice": 2, "Nombre": "Cassette 2", "Moneda": "MXN", "Denominacion": 500 },
      { "Indice": 3, "Nombre": "Cassette 3", "Moneda": "MXN", "Denominacion": 200 },
      { "Indice": 4, "Nombre": "Cassette 4", "Moneda": "MXN", "Denominacion": 100 }
    ],
    // Se llena con lo observado en Test_AceptarBilletes: código del SDK -> etiqueta. Ej. "12": "USD 20"
    "EtiquetaDenominacion": {}
  },
  "Monederos": {
    "BaudRate": 9600,
    "MaxMonedasPrueba": 10,
    "Hoppers": [
      { "Nombre": "MK4 #1 ($1 MXN)", "Denominacion": 1, "Puerto": "COM3", "Direccion": 3 },
      { "Nombre": "MK4 #2 ($5 MXN)", "Denominacion": 5, "Puerto": "COM3", "Direccion": 4 }
    ]
  },
  "Escaner": {
    "Simulado": true,                        // true hasta tener el SDK MEPR500+ del fabricante
    "TimeoutSegundos": 30
  },
  "Impresora": {
    "Modo": "Spooler",                       // "Spooler" (USB, cola de Windows RAW) o "Serial"
    "NombreImpresora": "KP-80A",             // VERIFICAR nombre exacto: Get-Printer
    "Puerto": "COM4",
    "BaudRate": 115200
  },
  "Sensores": {
    "Puerto": "COM5",
    "BaudRate": 9600,
    "SilencioTramaMs": 30,
    // Tramas en hex (ej. "AA 01 02 55"). Obtenerlas con Test_CalibrarSensores. Vacío = sin configurar.
    "Tramas": {
      "PUERTA_ABIERTA": "",
      "PUERTA_CERRADA": "",
      "VIBRACION": "",
      "PROXIMIDAD": ""
    }
  }
}
```

## 3. Qué aportan los PDFs y qué sigue siendo supuesto

**Confirmado por `Finso_TAC_Logical_Tecnica.pdf` y `Finso_Guia_Integracion_Hardware BEEX.pdf`:**

- Mapa físico: Cassette 1 = USD 20, Cassette 2 = MXN 500, Cassette 3 = MXN 200, Cassette 4 = MXN 100; Monedero 1 = MXN 1, Monedero 2 = MXN 5; el resto de billetes válidos va al recycler. Está en `hardware.json` (`Casetes`, `Hoppers`).
- Escenarios de prueba que el TAC exige: billete inválido rechazado, billete válido no dispensable al recycler, monedero $1 agotado y dispensa parcial. Quedaron incorporados como criterios en los días 1 y 2.
- Regla del TAC: una orden de dispensa no equivale a entrega física. Por eso cada prueba imprime contadores y pide confirmación humana.
- Para el ALB02, el BEEX confirma que envía cadenas de bytes por COM y que importa el **cambio de estado**, que es lo que hace la escucha (ignora repeticiones idénticas).

**Los PDFs no incluyen bytes, funciones ni parámetros.** Para cerrar los puntos de la tabla faltan estos materiales, que el BEEX cita pero no se han recibido:

| Material | Lo que resuelve |
| :-- | :-- |
| Carpeta de GRG (demo WinForm y guía del SDK `CRM9250N`) | Puntos 1 y 2 |
| Tablas de Excel de ccTalk de la carpeta de integración | Puntos 3 y 4 |
| Documentación del ALB02 | Punto 5 |
| SDK y manual del MEPR500+ | Punto 6 |

**Discrepancia por aclarar:** el BEEX llama a los 4 casetes "de aceptación"; el TAC los usa como casetes de dispensa. El TAC §15 deja como decisión pendiente con el proveedor qué denominaciones se aceptan y cuáles se reciclan o dispensan.

**Supuestos aún abiertos** (marcados `VERIFICAR` en el código o en `hardware.json`):

| # | Supuesto | Dónde | Cómo validarlo |
| :-- | :-- | :-- | :-- |
| 1 | ~~Nombre lógico y código de éxito~~ **Confirmado:** `CRM9250NDev`, `rc == 0` (demo y PDF de GRG) | `Recicladora.*` | – |
| 2 | ~~`byDispType` e `Indice` de casete~~ **Confirmado:** `1` = por casete; RC1..RC4 = 1..4 | `TipoDispensa`, `Casetes[].Indice` | Probar con 1 billete por casete |
| 3 | Dispense de hopper = `[serie x3][cantidad]` sin cifrado | `Monederos.cs` | Manual MK4; si responde NAK, implementar Pump RNG (161) / cipher (160) |
| 4 | Direcciones ccTalk de los hoppers (3 y 4) | `Monederos.Hoppers` | Si no responden, hacer barrido de direcciones 1–10 |
| 5 | Formato de tramas del ALB02 | `Sensores.Tramas` | `Test_CalibrarSensores` |
| 6 | API del SDK del MEPR500+ | `EscanerMeprSdk` | Documentación y DLL del fabricante |
| 7 | ~~Bits de la DLL~~ **Confirmado:** x86. Falta `DevBaseDll.dll` en el zip del SDK | `.csproj` | El pre-vuelo del menú 1 lo detecta |

## 4. Convenciones de evidencia

Para cada prueba guardar en `Evidencia/DiaN/`: captura de la consola con el resultado `APROBADO`/`RECHAZADO`, foto o video corto del comportamiento físico y, en la impresora, el voucher físico. Las tramas crudas (ccTalk, ALB02) se pegan tal como aparecen en consola.

## 5. Día 6 – Cierre y regresión

1. Ejecutar las 9 opciones del menú en secuencia, sin reiniciar el programa; confirmar que no quedan puertos bloqueados entre pruebas.
2. Repetir 3 ciclos de aceptar/dispensar billetes y 3 de dispensar monedas.
3. Confirmar `hardware.json` final con los valores reales de la máquina.
4. Consolidar la lista de **métodos validados** para extraerlos a la aplicación oficial y la lista de **supuestos que resultaron falsos**.
5. Firmar el cierre solo si todos los criterios de los documentos de los días 1–5 están en `APROBADO`; el escáner no puede cerrarse mientras use el modo simulado.
