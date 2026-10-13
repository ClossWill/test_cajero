# Prueba Día 1 – Recicladora de efectivo GRG 9250N

## Objetivo

Demostrar que desde C# podemos abrir el dispositivo, inicializarlo, **aceptar un billete físico** (shutter → conteo → guardado o devolución) y **dispensar billetes** de un casete por el shutter.

## Protocolo / librería

- `CRM9250NDev.dll` del fabricante, consumida con los `[DllImport]` y structs ya incluidos en `CRM9250N.cs` (**no se modifica**; se compila dentro del proyecto).
- Las funciones reciben punteros a structs `Pack = 1` con arreglos `ByValArray`; `Helpers/Native.cs` crea los structs con sus arreglos ya dimensionados y administra la memoria nativa (`NativeScope`).
- Secuencia usada:

| Prueba | Llamadas SDK, en orden |
| :-- | :-- |
| Inicializar | pre-vuelo de la DLL → `hOpenLogicDevice("CRM9250NDev")` → `iSetCommPara` → `iPowerOnInitEx` → `iGetDevVersionEx` → `iGetDevStatusEx` |
| Aceptar | `iPrepareDeposit` → `iOpenShutter` → *(usuario inserta)* → `iCloseShutter` → `iCashCount` → `iStoreMoneyEx` **o** `iCancelDepositEx` → `iPrepareNextTransaction` |
| Dispensar | `iPrepareDispense` → `iDispenseEx` → *(usuario retira)* → `iPrepareNextTransaction` |

## Precondiciones

- **Carpeta `dll` completa del SDK de GRG** (`CRM9250NDev_V2.3.7.6\dll`: la DLL, `GRGDTATM_CommCfg.ini`, `CRM9250NDevCfg.ini`, `mfc42.dll`, etc.) junto al `.exe`; el `.csproj` la copia sola desde esa ruta (se cambia con `-p:GrgSdkDir=<carpeta>`).
- **`DevBaseDll.dll`**: la importa `CRM9250NDev.dll` y **no viene en el zip del SDK V2.3.7.6**. Debe salir del paquete de instalación / driver de GRG. Sin ella no hay comunicación.
- **Driver USB de GRG** instalado y recicladora conectada por USB (`CommType=3`, VID `04B4` / PID `8016`, según `GRGDTATM_CommCfg.ini`). No usa puerto COM.
- **Proceso de 32 bits:** la DLL es x86 (PE machine `0x14C`), por eso el `.csproj` usa `PlatformTarget=x86`. En la máquina IoT hace falta el runtime x86 de .NET 8, o publicar con `dotnet publish -c Release -r win-x86 --self-contained`.
- Recicladora encendida, casetes instalados, al menos uno cargado con billetes de prueba.
- Billetes de prueba: al menos uno de MXN 500, 200 y 100 y de USD 20, y un billete inválido (dañado o de una moneda no soportada). Opcional: otras denominaciones USD para ver si van al recycler.
- Los cuatro casetes con unos pocos billetes de su denominación (anotar cuántos, para cuadrar al final).
- Límite de seguridad: `MaxBilletesPrueba` (5 por defecto) impide dispensar cantidades grandes por error.

## Configuración física (fuente: `Finso_TAC_Logical_Tecnica.pdf` §2)

| Recurso | Moneda / denominación | Función |
| :-- | :-- | :-- |
| Cassette 1 | USD 20 | Dispensa USD (única denominación USD de salida) |
| Cassette 2 | MXN 500 | Dispensa MXN |
| Cassette 3 | MXN 200 | Dispensa MXN |
| Cassette 4 | MXN 100 | Dispensa MXN |
| Recycler | Otras denominaciones USD / MXN | Recibe billetes válidos que no se dispensan |

Por eso `Test_DispensarBilletes` recibe moneda **y denominación**: `("MXN", 500)` va al casete 2, `("USD", 20)` al casete 1. Si la moneda tiene varios casetes y no se indica la denominación, la prueba se detiene y lo pide.

```json
"Recicladora": {
  "NombreDispositivoLogico": "CRM9250NDev",
  "CodigoExito": 0,
  "ModoAtc": 1,
  "AceptarTodasDenominaciones": 1,
  "TipoDispensa": 1,
  "DestinoRechazoDispensa": 1,
  "DestinoRechazoDeposito": 1,
  "MaxBilletesPrueba": 5,
  "Casetes": [
    { "Indice": 1, "Nombre": "Cassette 1", "Moneda": "USD", "Denominacion": 20 },
    { "Indice": 2, "Nombre": "Cassette 2", "Moneda": "MXN", "Denominacion": 500 },
    { "Indice": 3, "Nombre": "Cassette 3", "Moneda": "MXN", "Denominacion": 200 },
    { "Indice": 4, "Nombre": "Cassette 4", "Moneda": "MXN", "Denominacion": 100 }
  ],
  "EtiquetaDenominacion": {}
}
```

Valores tomados de la **demo WinForms y el PDF del SDK de GRG**: el nombre lógico es el nodo `[CRM9250NDev]` de `GRGDTATM_CommCfg.ini`; éxito = `rc == 0`; `byDispType = 1` dispensa por casete RC; el casete se indica como número RC con **base 1** (la demo pide `RC2` con `wDenomOrCassetteInfo = 2`).

`EtiquetaDenominacion` empieza vacío: el SDK devuelve un **código** de denominación (`wCode`), no "USD 20". Al aceptar un billete conocido, la prueba pregunta qué billete es y te indica qué par `código → etiqueta` agregar. Así se construye la tabla real del equipo durante la primera sesión.

## Fase 0 – Validar la comunicación (primer objetivo)

Antes de aceptar o dispensar nada, el menú `1` debe quedar en `APROBADO`. Esa opción hace un pre-vuelo y después el arranque real del SDK, y se detiene en el primer fallo diciendo qué pasó:

| Paso | Qué comprueba | Si falla, causa probable |
| :-- | :-- | :-- |
| 1 | `CRM9250NDev.dll` está junto al `.exe` o en el `PATH` | Falta copiar la carpeta `dll` del SDK de GRG |
| 2 | Arquitectura de la DLL (lee el encabezado PE) igual a la del proceso | La DLL es x86: el proceso debe ser de 32 bits |
| 3 | `GRGDTATM_CommCfg.ini` junto a la DLL con el nodo `[CRM9250NDev]` | El nombre lógico debe coincidir con el nodo del `.ini`; ahí el SDK lee el tipo de enlace (USB) |
| 4 | Windows ha enumerado el USB `VID_04B4&PID_8016` (registro) | Recicladora nunca conectada a ese equipo, cable suelto o falta el driver de GRG |
| 5 | Todas las DLL que importa `CRM9250NDev.dll` existen (lee la tabla de importación) | Falta p. ej. `DevBaseDll.dll`: no viene en el zip del SDK |
| 6 | Windows carga la DLL (126 = dependencia, 193 = arquitectura) | Ver la línea del paso 5 |
| 7 | `hOpenLogicDevice` devuelve handle ≠ 0 | Nombre lógico incorrecto o SDK sin su `.ini` |
| 8 | `iSetCommPara` e `iPowerOnInitEx` devuelven 0 | Ver `logic`/`phy`/`msg` impresos; consultar `CRM9250N LogicalCode.xls` del SDK |
| 9 | `iGetDevVersionEx` devuelve firmware legible y se listan los casetes presentes | Comparar con lo instalado físicamente |
| 10 | `iGetDevStatusEx` responde (consulta en vivo, sin mover nada) | Confirma el enlace de ida y vuelta con la máquina |

Los pasos 1 a 6 se evalúan **sin necesitar la recicladora**: sirven para preparar el equipo antes de ir a la máquina. Del 7 en adelante requieren la recicladora encendida y conectada.

**Resultado a observar:** la consola termina en `RESULTADO Recicladora GRG 9250N - Inicialización: APROBADO`, con firmware legible y los casetes presentes iguales a los instalados. Solo con eso se pasa a aceptar y dispensar.

## Código (`Tests/Recicladora.cs`)

```csharp
using CRM9250N_WinFormDemo;
using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using IntegracionCajeroPruebas.Protocolos;

namespace IntegracionCajeroPruebas.Tests;

// Recicladora GRG 9250N vía CRM9250NDev.dll (P/Invoke ya declarado en CRM9250N.cs).
public static partial class HardwareTests
{
    private static IntPtr _crm = IntPtr.Zero;
    private static RecicladoraConfig Crm => HardwareConfig.Actual.Recicladora;

    public static void Test_InicializarRecicladora()
    {
        var r = new Reporte("Recicladora GRG 9250N - Inicialización");
        try
        {
            InicializarRecicladora(r);
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException)
        {
            r.Criterio("Carga de CRM9250NDev.dll", false,
                $"{ex.GetType().Name}: {ex.Message}. Proceso de {(Environment.Is64BitProcess ? 64 : 32)} bits: " +
                "copiar la DLL junto al .exe y alinear PlatformTarget con su arquitectura.");
        }
        catch (EntryPointNotFoundException ex)
        {
            r.Criterio("La DLL exporta las funciones esperadas", false,
                $"{ex.Message} ¿Es la DLL correcta (CRM9250NDev.dll del SDK de la 9250N) y la versión que coincide con CRM9250N.cs?");
        }
        catch (Exception ex)
        {
            r.Criterio("Ejecución sin excepciones", false, $"{ex.GetType().Name}: {ex.Message}");
        }
        r.Finalizar();
    }

    /// <param name="esperaRechazo">true = se inserta un billete inválido/no aceptado y se espera que la recicladora lo rechace.</param>
    public static void Test_AceptarBilletes(bool esperaRechazo = false)
    {
        var r = new Reporte(esperaRechazo
            ? "Recicladora GRG 9250N - Billete inválido (se espera rechazo)"
            : "Recicladora GRG 9250N - Aceptar billetes");
        try
        {
            if (!AsegurarRecicladora(r)) { r.Finalizar(); return; }

            using var s = new NativeScope();
            var pStatus = s.Salida<tDevReturn>();

            var listo = CrmOk(r, "iPrepareDeposit", CRM9250N.iPrepareDeposit(_crm, pStatus), pStatus)
                     && CrmOk(r, "iOpenShutter", CRM9250N.iOpenShutter(_crm, pStatus), pStatus);
            if (!listo) { r.Finalizar(); return; }

            Consola.Esperar("Inserte UN billete físico por el shutter y presione ENTER...");

            var cerrado = CrmOk(r, "iCloseShutter", CRM9250N.iCloseShutter(_crm, pStatus), pStatus);

            var conteo = Native.Cero<tCRM9250N_CashCount>();
            conteo.iDepositLimit = Crm.LimiteDeposito;
            var pConteo = s.Entrada(conteo);
            var contado = cerrado && CrmOk(r, "iCashCount", CRM9250N.iCashCount(_crm, pConteo, pStatus), pStatus);
            if (!contado) { r.Finalizar(); return; }

            var res = Native.Leer<tCRM9250N_CashCount>(pConteo);
            var detalle = $"total={res.iTotalCount} rechazados={res.iRejectCount} desconocidos={res.sDenomNumInfo.iUnknownCount}";
            if (esperaRechazo)
                r.Criterio("El billete inválido fue rechazado (rechazados ≥ 1 y ninguno contado)",
                    res.iRejectCount >= 1 && res.iTotalCount == 0, detalle);
            else
                r.Criterio("Se contó al menos un billete", res.iTotalCount > 0, detalle);
            ImprimirDenominaciones(res.sDenomNumInfo);

            if (!esperaRechazo && res.iTotalCount > 0)
            {
                Console.Write("¿Qué billete insertó? (ej. USD 20, MXN 500; ENTER para omitir): ");
                var descripcion = Console.ReadLine()?.Trim();
                if (!string.IsNullOrEmpty(descripcion))
                    Consola.Aviso($"        EVIDENCIA: asociar el código de arriba a \"{descripcion}\" en Recicladora.EtiquetaDenominacion (hardware.json)");
            }

            // Con billetes contados: guardarlos (confirma el depósito) o devolverlos al usuario.
            if (res.iTotalCount > 0 && Consola.Confirmar("¿Guardar los billetes en la recicladora (iStoreMoneyEx)? n = devolver"))
            {
                var store = Native.Cero<tCRM9250N_StoreMoneyEx>();
                store.byRejectPos = Crm.DestinoRechazoDeposito;
                store.byNvMode = 0;
                // Prioridad 1..4 para RC1..RC4 (0x01-0x0F, menor = más prioridad), como en la demo de GRG.
                for (var i = 0; i < 4; i++) store.abyCassettePriority[i] = (byte)(i + 1);
                var pStore = s.Entrada(store);
                CrmOk(r, "iStoreMoneyEx", CRM9250N.iStoreMoneyEx(_crm, pStore, pStatus), pStatus);
            }
            else
            {
                var cancel = Native.Cero<tCRM9250N_CancelDepositEx>();
                var pCancel = s.Entrada(cancel);
                CrmOk(r, "iCancelDepositEx", CRM9250N.iCancelDepositEx(_crm, pCancel, pStatus), pStatus);
                Consola.Esperar("Retire cualquier billete devuelto y presione ENTER...");
            }

            CrmOk(r, "iPrepareNextTransaction", CRM9250N.iPrepareNextTransaction(_crm, pStatus), pStatus);
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    /// <param name="denominacion">Valor del billete (500, 200, 100, 20). 0 = deducirlo si la moneda tiene un solo casete.</param>
    public static void Test_DispensarBilletes(int cantidad, string moneda, int denominacion = 0)
    {
        var r = new Reporte($"Recicladora GRG 9250N - Dispensar {cantidad} billete(s) {moneda} {(denominacion > 0 ? denominacion : "")}".TrimEnd());
        try
        {
            if (cantidad < 1 || cantidad > Crm.MaxBilletesPrueba)
            {
                r.Criterio($"Cantidad entre 1 y {Crm.MaxBilletesPrueba} (límite de seguridad de la prueba)", false);
                r.Finalizar(); return;
            }

            var candidatos = Crm.Casetes
                .Where(c => c.Moneda.Equals(moneda, StringComparison.OrdinalIgnoreCase)
                            && (denominacion == 0 || c.Denominacion == denominacion))
                .ToList();
            if (candidatos.Count != 1)
            {
                var opciones = string.Join(", ", Crm.Casetes.Select(c => $"{c.Moneda} {c.Denominacion}"));
                r.Criterio($"{moneda} {denominacion} corresponde a un único casete en hardware.json", false,
                    $"coincidencias={candidatos.Count}. Casetes configurados: {opciones}. Indique la denominación.");
                r.Finalizar(); return;
            }
            var cassette = candidatos[0];
            var casete = cassette.Indice;
            Consola.Info($"        Casete elegido: {cassette.Nombre} ({cassette.Moneda} {cassette.Denominacion}), índice SDK {casete}");

            if (!AsegurarRecicladora(r)) { r.Finalizar(); return; }

            using var s = new NativeScope();
            var pStatus = s.Salida<tDevReturn>();

            if (!CrmOk(r, "iPrepareDispense", CRM9250N.iPrepareDispense(_crm, pStatus), pStatus))
            { r.Finalizar(); return; }

            var disp = Native.Cero<tCRM9250N_DispenseInfoEx>();
            disp.byDispType = Crm.TipoDispensa;
            disp.byRejectToAc = Crm.DestinoRechazoDispensa;
            disp.asReqDispInfo[0] = new tCRM9250N_ReqDispInfo { wDenomOrCassetteInfo = (ushort)casete, iNum = cantidad };
            var pDisp = s.Entrada(disp);

            var ok = CrmOk(r, "iDispenseEx", CRM9250N.iDispenseEx(_crm, pDisp, pStatus), pStatus);
            if (ok)
            {
                var res = Native.Leer<tCRM9250N_DispenseInfoEx>(pDisp);
                var dispensados = res.aiDispensedInfo.Sum();
                var rechazados = res.aiRejectInfo.Sum();
                r.Criterio($"La recicladora reporta {cantidad} billete(s) dispensado(s)", dispensados == cantidad,
                    $"dispensados={dispensados} rechazados={rechazados}");
                // El TAC exige conciliar contadores físicos: una orden de dispensa no equivale a entrega exitosa.
                for (var i = 0; i < res.aiDispensedInfo.Length; i++)
                    if (res.aiDispensedInfo[i] != 0 || res.aiRejectInfo[i] != 0)
                        Consola.Info($"        Contador RC{i + 1}: dispensados={res.aiDispensedInfo[i]} rechazados={res.aiRejectInfo[i]}");
                r.Criterio("Confirmación física: los billetes están en el shutter y la cantidad coincide",
                    Consola.Confirmar($"¿Hay {cantidad} billete(s) en el shutter?"));
                Consola.Esperar("Retire los billetes del shutter y presione ENTER...");
            }

            CrmOk(r, "iPrepareNextTransaction", CRM9250N.iPrepareNextTransaction(_crm, pStatus), pStatus);
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    // ------------------------------------------------------------------ helpers

    private static bool AsegurarRecicladora(Reporte r)
    {
        if (_crm != IntPtr.Zero) return true;
        return InicializarRecicladora(r);
    }

    /// <summary>Cierra comunicación y handle (PDF §4.2 y §4.4: "es el último paso"). Seguro de llamar sin haber abierto.</summary>
    public static void CerrarRecicladora()
    {
        if (_crm == IntPtr.Zero) return;
        try
        {
            using var s = new NativeScope();
            Crm9250NExtra.iCloseComm(_crm, s.Salida<tDevReturn>());
            Crm9250NExtra.vCloseLogicDevice(_crm);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) { }
        finally { _crm = IntPtr.Zero; }
    }

    private static bool InicializarRecicladora(Reporte r)
    {
        CerrarRecicladora(); // reinicializar desde cero si ya había un handle abierto
        if (!DiagnosticarDllRecicladora(r)) return false;

        _crm = CRM9250N.hOpenLogicDevice(Crm.NombreDispositivoLogico);
        if (!r.Criterio($"hOpenLogicDevice('{Crm.NombreDispositivoLogico}') devuelve handle", _crm != IntPtr.Zero,
                _crm != IntPtr.Zero ? $"handle=0x{_crm:X}"
                    : "Handle nulo: el nombre lógico no existe en la configuración del SDK, o la recicladora no está " +
                      "encendida/conectada. Revisar Recicladora.NombreDispositivoLogico (VERIFICAR con la demo de GRG)."))
            return false;

        using var s = new NativeScope();
        var pStatus = s.Salida<tDevReturn>();
        if (!CrmOk(r, "iSetCommPara", CRM9250N.iSetCommPara(_crm, pStatus), pStatus)) return false;

        var cfg = Native.Cero<tCRM9250N_InitSettingEx>();
        cfg.byAtcMode = Crm.ModoAtc;
        cfg.byWorkingMode = Crm.ModoTrabajo;
        cfg.byDcAccepetAllDenom = Crm.AceptarTodasDenominaciones;
        cfg.byInitCfg = Crm.ConfigInicio;
        var pCfg = s.Entrada(cfg);
        var pCasetes = s.Salida<tCRM9250N_CassetteInfoEx>();
        var pEstado = s.Salida<tCRM9250N_CassetteStatusEx>();
        var pInit = s.Salida<tCRM9250N_InitInfoEx>();

        if (!CrmOk(r, "iPowerOnInitEx", CRM9250N.iPowerOnInitEx(_crm, pCfg, pCasetes, pEstado, pInit, pStatus), pStatus))
            return false;

        var estado = Native.Leer<tCRM9250N_CassetteStatusEx>(pEstado);
        for (var i = 0; i < estado.asRcStatus.Length; i++)
            if (estado.asRcStatus[i].byExist != 0)
                Consola.Info($"        Casete RC{i + 1}: status={estado.asRcStatus[i].byStatus} presente={estado.asRcStatus[i].byExist}");

        var pVer = s.Salida<tCRM9250N_DevVersionInfoEx>();
        if (CrmOk(r, "iGetDevVersionEx", CRM9250N.iGetDevVersionEx(_crm, pVer, pStatus), pStatus))
        {
            var v = Native.Leer<tCRM9250N_DevVersionInfoEx>(pVer);
            Consola.Info($"        Firmware placa principal: '{Native.Texto(v.acFwMainBoard)}'");
        }

        // iGetDevStatusEx consulta la máquina en vivo y no mueve nada: confirma que el enlace responde de ida y vuelta.
        var pDev = s.Salida<tCRM9250N_DevStatusEx>();
        if (CrmOk(r, "iGetDevStatusEx", Crm9250NExtra.iGetDevStatusEx(_crm, pDev, pStatus), pStatus))
        {
            var d = Native.Leer<tCRM9250N_DevStatusEx>(pDev);
            Consola.Info($"        Estado: shutter abierto={d.byShutterOpen} billetes en shutter={d.byShutterAreaHasNotes} " +
                         $"billetes en NF={d.byNfHasNotes} billetes en NE={d.byNeHasNotes} casete extraído={d.byCassettePlugRemove}");
            Consola.Info($"        Casetes extraídos: RC1={d.byRC1PlugRemove} RC2={d.byRC2PlugRemove} RC3={d.byRC3PlugRemove} RC4={d.byRC4PlugRemove}");
        }
        return true;
    }

    private static bool CrmOk(Reporte r, string funcion, int rc, IntPtr pStatus)
    {
        var st = Native.Leer<tDevReturn>(pStatus);
        var significado = CodigosGrg.Describir(st.iLogicCode);
        return r.Criterio($"{funcion} (rc={rc})", rc == Crm.CodigoExito,
            $"logic={st.iLogicCode} phy={st.iPhyCode} tipo={st.iType} msg='{Native.Texto(st.acDevReturn)}'" +
            (significado.Length > 0 ? $" | {significado}" : ""));
    }

    private static void ImprimirDenominaciones(tCRM9250N_DenomNumInfo info)
    {
        var grupos = new (string Nombre, tCRM9250N_DenomAndNum[] Datos)[]
        {
            ("L4A", info.asDenomInfoL4A), ("L4B", info.asDenomInfoL4B),
            ("L3", info.asDenomInfoL3), ("L2", info.asDenomInfoL2),
        };
        foreach (var (nombre, datos) in grupos)
            foreach (var d in datos.Where(d => d.iNum > 0))
            {
                var etiqueta = Crm.EtiquetaDenominacion.GetValueOrDefault(d.wCode, "sin etiqueta");
                Consola.Info($"        Nivel {nombre}: código de denominación {d.wCode} ({etiqueta}) x {d.iNum}");
            }
    }
}
```

### Pre-vuelo de la DLL (`Tests/RecicladoraDiagnostico.cs`)

```csharp
using System.Runtime.InteropServices;
using IntegracionCajeroPruebas.Helpers;

namespace IntegracionCajeroPruebas.Tests;

// Pre-vuelo de la recicladora: antes de llamar al SDK, comprobar que la DLL existe, que su
// arquitectura coincide con la del proceso y que Windows la puede cargar (con sus dependencias).
public static partial class HardwareTests
{
    private const string DllRecicladora = "CRM9250NDev.dll";

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string ruta);

    /// <returns>true si la DLL quedó cargada en el proceso (los [DllImport] posteriores la reutilizan por nombre).</returns>
    private static bool DiagnosticarDllRecicladora(Reporte r)
    {
        var ruta = LocalizarDll(DllRecicladora);
        if (!r.Criterio($"{DllRecicladora} existe junto al .exe o en el PATH", ruta is not null,
                ruta ?? $"No está en {AppContext.BaseDirectory}. Copiar la DLL del SDK de GRG (y sus dependencias) a esa carpeta."))
            return false;

        var bitsDll = LeerArquitecturaPe(ruta!);
        var bitsProceso = Environment.Is64BitProcess ? "x64" : "x86";
        r.Criterio($"Arquitectura de la DLL ({bitsDll}) coincide con la del proceso ({bitsProceso})",
            bitsDll == bitsProceso,
            bitsDll == bitsProceso ? null
                : $"Cambiar <PlatformTarget> a {bitsDll} en el .csproj y recompilar (BadImageFormatException si no).");
        if (bitsDll != bitsProceso && bitsDll is "x86" or "x64") return false;

        // hOpenLogicDevice/iSetCommPara no reciben puerto: el SDK lo lee del nodo [<nombre lógico>] de este .ini (PDF §4.1 y §4.3).
        var carpeta = Path.GetDirectoryName(ruta)!;
        var ini = Path.Combine(carpeta, "GRGDTATM_CommCfg.ini");
        var seccion = LeerSeccionIni(ini, Crm.NombreDispositivoLogico);
        r.Criterio($"GRGDTATM_CommCfg.ini junto a la DLL con el nodo [{Crm.NombreDispositivoLogico}]", seccion is not null,
            seccion is null ? $"Falta {ini} o no tiene ese nodo. El nombre lógico de hardware.json debe coincidir con el nodo del .ini."
                            : string.Join(" | ", seccion.Where(l => l.StartsWith("CommType") || l.StartsWith("ExtendStr") || l.StartsWith("ComID"))));
        if (seccion is not null) VerificarUsb(r, seccion);

        // Dependencias directas: se listan antes de cargar para nombrar exactamente lo que falta.
        var faltan = DependenciasFaltantes(ruta!, carpeta);
        r.Criterio("Todas las DLL que importa CRM9250NDev.dll están disponibles", faltan.Count == 0,
            faltan.Count == 0 ? null
                : $"Faltan: {string.Join(", ", faltan)}. Copiarlas junto al .exe (son del paquete/driver de GRG; " +
                  "devbasedll.dll no viene en el zip del SDK V2.3.7.6).");

        var h = LoadLibraryW(ruta!);
        var err = Marshal.GetLastWin32Error();
        r.Criterio("Windows carga la DLL (con sus dependencias)", h != IntPtr.Zero,
            h != IntPtr.Zero ? $"módulo=0x{h:X}" : err switch
            {
                126 => "Error 126: falta una dependencia (ver línea anterior; también puede ser una indirecta de otra DLL de GRG).",
                193 => "Error 193: DLL de otra arquitectura que el proceso.",
                _ => $"Error de Windows {err}.",
            });
        return h != IntPtr.Zero;
    }

    /// <summary>Importaciones directas de la DLL que no están en su carpeta ni en el sistema (ignora api-ms-*).</summary>
    private static List<string> DependenciasFaltantes(string ruta, string carpeta)
    {
        var faltan = new List<string>();
        foreach (var dll in LeerImportaciones(ruta))
        {
            if (dll.StartsWith("api-ms-", StringComparison.OrdinalIgnoreCase)) continue;
            var existe = File.Exists(Path.Combine(carpeta, dll))
                         || File.Exists(Path.Combine(AppContext.BaseDirectory, dll))
                         || File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), dll))
                         || File.Exists(Path.Combine(Environment.SystemDirectory, dll));
            if (!existe) faltan.Add(dll);
        }
        return faltan;
    }

    /// <summary>Nombres de DLL de la tabla de importación PE (soporta PE32 y PE32+).</summary>
    private static List<string> LeerImportaciones(string ruta)
    {
        var nombres = new List<string>();
        try
        {
            using var f = File.OpenRead(ruta);
            using var br = new BinaryReader(f);
            f.Position = 0x3C;
            var pe = br.ReadInt32();
            f.Position = pe + 6;
            var nSecciones = br.ReadUInt16();
            f.Position = pe + 20;
            var tamOpcional = br.ReadUInt16();
            var opt = pe + 24;
            f.Position = opt;
            var pe32Plus = br.ReadUInt16() == 0x20B;
            f.Position = opt + (pe32Plus ? 120 : 104); // DataDirectory[1] = tabla de importación
            var rvaImport = br.ReadUInt32();
            if (rvaImport == 0) return nombres;

            var secciones = new List<(uint Rva, uint Tam, uint Raw)>();
            f.Position = opt + tamOpcional;
            for (var i = 0; i < nSecciones; i++)
            {
                f.Position = opt + tamOpcional + i * 40 + 8;
                var virtualSize = br.ReadUInt32();
                var rva = br.ReadUInt32();
                var rawSize = br.ReadUInt32();
                var raw = br.ReadUInt32();
                secciones.Add((rva, Math.Max(virtualSize, rawSize), raw));
            }
            long ARaw(uint rva) { foreach (var s in secciones) if (rva >= s.Rva && rva < s.Rva + s.Tam) return s.Raw + (rva - s.Rva); return -1; }

            for (var d = ARaw(rvaImport); d >= 0; d += 20)
            {
                f.Position = d + 12;
                var rvaNombre = br.ReadUInt32();
                if (rvaNombre == 0) break;
                var pos = ARaw(rvaNombre);
                if (pos < 0) break;
                f.Position = pos;
                var sb = new System.Text.StringBuilder();
                for (int c; (c = f.ReadByte()) > 0;) sb.Append((char)c);
                nombres.Add(sb.ToString());
            }
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or UnauthorizedAccessException) { }
        return nombres;
    }

    /// <summary>Líneas "clave=valor" del nodo [nombre] de un .ini; null si el archivo o el nodo no existen.</summary>
    private static List<string>? LeerSeccionIni(string ruta, string nombre)
    {
        if (!File.Exists(ruta)) return null;
        var lineas = new List<string>();
        var dentro = false;
        var encontrada = false;
        foreach (var l in File.ReadLines(ruta).Select(x => x.Trim()))
        {
            if (l.StartsWith('[')) { dentro = l.Equals($"[{nombre}]", StringComparison.OrdinalIgnoreCase); encontrada |= dentro; continue; }
            if (dentro && l.Length > 0 && !l.StartsWith(';')) lineas.Add(l);
        }
        return encontrada ? lineas : null;
    }

    /// <summary>Si el nodo declara CommType=3 (USB) con "VID:xxxx, PID:yyyy", busca ese dispositivo en el registro de Windows.</summary>
    private static void VerificarUsb(Reporte r, List<string> seccion)
    {
        if (!OperatingSystem.IsWindows()) return;
        var extra = seccion.FirstOrDefault(l => l.StartsWith("ExtendStr", StringComparison.OrdinalIgnoreCase));
        var m = extra is null ? null : System.Text.RegularExpressions.Regex.Match(
            extra, @"VID:\s*([0-9A-Fa-f]{4}).*PID:\s*([0-9A-Fa-f]{4})");
        if (m is null || !m.Success) return;

        var vid = m.Groups[1].Value.ToUpperInvariant();
        var pid = m.Groups[2].Value.ToUpperInvariant();
        var clave = $@"SYSTEM\CurrentControlSet\Enum\USB\VID_{vid}&PID_{pid}";
        using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(clave);
        r.Criterio($"Windows ha enumerado el USB de la recicladora (VID_{vid}&PID_{pid})", k is not null,
            k is not null ? $"instancias={string.Join(",", k.GetSubKeyNames())}"
                          : "No aparece en el registro: la recicladora nunca se ha conectado a este equipo, el cable USB no está puesto o falta el driver de GRG. " +
                            "Revisar el Administrador de dispositivos.");
    }

    private static string? LocalizarDll(string nombre)
    {
        var local = Path.Combine(AppContext.BaseDirectory, nombre);
        if (File.Exists(local)) return local;
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidato = Path.Combine(dir.Trim(), nombre);
                if (File.Exists(candidato)) return candidato;
            }
            catch (ArgumentException) { /* entrada de PATH inválida */ }
        }
        return null;
    }

    /// <summary>Lee el campo Machine del encabezado PE: "x86", "x64", "arm64" u "desconocida".</summary>
    private static string LeerArquitecturaPe(string ruta)
    {
        try
        {
            using var f = File.OpenRead(ruta);
            using var br = new BinaryReader(f);
            f.Position = 0x3C;
            var pe = br.ReadInt32();
            f.Position = pe;
            if (br.ReadUInt32() != 0x00004550) return "desconocida"; // "PE\0\0"
            return br.ReadUInt16() switch
            {
                0x014C => "x86",
                0x8664 => "x64",
                0xAA64 => "arm64",
                var m => $"desconocida (0x{m:X})",
            };
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or UnauthorizedAccessException)
        {
            return "desconocida";
        }
    }
}
```

## Cómo ejecutar

Menú `1` (inicializar), `2` (aceptar), `3` (dispensar; pide cantidad, moneda y denominación), `10` (billete inválido, espera rechazo). Equivalente directo desde `Main`:

```csharp
HardwareTests.Test_InicializarRecicladora();
HardwareTests.Test_AceptarBilletes();
HardwareTests.Test_AceptarBilletes(esperaRechazo: true);
HardwareTests.Test_DispensarBilletes(2, "MXN", 500);
HardwareTests.Test_DispensarBilletes(1, "USD", 20);
```

## Escenarios de aceptación de billetes (derivados del TAC §14)

Para cada billete se repite el flujo de `Test_AceptarBilletes` y se anota el código de denominación que devuelve el SDK.

| Billete a insertar | Resultado esperado |
| :-- | :-- |
| MXN 500, 200 y 100 (uno por vez) | Contado, denominación identificada. Es el flujo "Compra de USD" (el cliente entrega MXN). |
| USD 20 | Contado. Si el hardware lo permite, puede ir al cassette 1. |
| Otra denominación USD (1, 5, 10, 50 o 100) | Contado y dirigido al recycler (no dispensable). Anotar si lo acepta o lo rechaza: es una decisión pendiente con el proveedor. |
| Billete inválido (dañado, fotocopia, moneda no soportada) | Opción `10`: `rechazados ≥ 1`, ninguno contado, y el billete se devuelve. |

## Criterios de aceptación

| ID | Criterio | Cómo se verifica |
| :-- | :-- | :-- |
| R-01 | `CRM9250NDev.dll` carga sin `DllNotFound` ni `BadImageFormat` | Prueba 1 no muestra el error de carga |
| R-02 | `hOpenLogicDevice` devuelve un handle distinto de cero | Criterio automático |
| R-03 | `iSetCommPara` e `iPowerOnInitEx` devuelven el código de éxito | Criterio automático (rc y `tDevReturn` impresos) |
| R-04 | El estado de casetes lista exactamente los casetes instalados físicamente | Comparar la salida con la máquina |
| R-05 | `iGetDevVersionEx` devuelve una versión de firmware legible | Salida de consola |
| R-06 | El shutter abre y cierra físicamente y `iCashCount` cuenta ≥ 1 billete | Observación + criterio automático |
| R-07 | La denominación reportada corresponde al billete insertado | Comparación manual del código de denominación |
| R-08 | `iStoreMoneyEx` guarda el billete **o** `iCancelDepositEx` lo devuelve por el shutter | Rc de éxito + observación física |
| R-09 | `iDispenseEx` reporta exactamente `cantidad` billetes dispensados y 0 rechazados | Criterio automático |
| R-10 | La cantidad física en el shutter coincide con lo reportado | Confirmación por consola |
| R-11 | Tres ciclos consecutivos (aceptar + dispensar) sin reiniciar el programa ni la máquina | Repetición manual |
| R-12 | Cada billete MXN 500/200/100 y USD 20 se identifica con un código de denominación distinto y estable (mismo billete → mismo código) | Tabla `EtiquetaDenominacion` completada |
| R-13 | Un billete inválido se rechaza (`rechazados ≥ 1`, 0 contados) y el programa no se bloquea | Opción `10` |
| R-14 | Se dispensa de cada casete configurado (USD 20, MXN 500, 200 y 100) y el contador del casete correcto coincide con lo entregado | Contadores por casete en consola + conteo físico |
| R-15 | *(negativa, opcional)* Pedir billetes a un casete vacío o fuera de servicio devuelve un error controlado, sin dispensado parcial silencioso | Requiere un casete vacío. El TAC exige no marcar COMPLETED ante una dispensa parcial |

**Aprobado** = R-01 a R-14 cumplidos (R-15 es recomendable). Cualquier `rc` distinto del código de éxito se documenta con `iLogicCode`/`iPhyCode`/mensaje para consulta con el fabricante.

## Supuestos y riesgos

- **Ya confirmado con el SDK de GRG:** nombre lógico `CRM9250NDev`, éxito `rc == 0`, `byDispType = 1` (por casete), casetes RC1..RC4 = 1..4, enlace USB y DLL x86. El mapa de denominaciones por casete sale del TAC §2.
- **Sigue sin confirmarse:** qué significan los `LogicCode`/`PhyCode` que devuelva (están en `doc\CRM9250N LogicalCode.xls`), los códigos de denominación (`wCode`) de MXN/USD reales (la demo usa códigos de billetes de prueba de GRG, p. ej. `0xEE37`) y el contenido de `DevBaseDll.dll` y del driver USB.
- **Discrepancia a aclarar con el proveedor:** el BEEX describe "4 casetes de **aceptación** (2,850 notas c/u)", mientras el TAC los trata como casetes de **dispensa**. El TAC §15 lista "qué denominaciones acepta y cuáles puede reciclar/dispensar" como decisión pendiente. Si los casetes resultan ser de otro tipo (`AC` en lugar de `RC` en `CRM9250N.cs`), cambia qué estructura de estado hay que leer.
- Cualquier dispensado debe verificarse físicamente: el TAC establece que una orden de dispensa no equivale a una entrega exitosa. Por eso la prueba imprime los contadores por casete y pide la confirmación humana.
- Si el SDK exige mapear códigos de denominación a moneda/valor, esta prueba solo imprime el código crudo (`wCode`).
- No se prueba `iRetractEx` (billetes olvidados en el shutter); si el usuario no retira los billetes, hacerlo manualmente o ampliar la prueba.

## Evidencia a guardar

Captura de consola de las tres pruebas, foto de los casetes y del shutter con los billetes dispensados, y los valores `tDevReturn` de cualquier falla.
