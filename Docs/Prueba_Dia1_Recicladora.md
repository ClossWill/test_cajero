# Prueba Día 1 – Recicladora de efectivo GRG 9250N

## Objetivo

Demostrar que desde C# podemos abrir el dispositivo, inicializarlo, **aceptar un billete físico** (shutter → conteo → guardado o devolución) y **dispensar billetes** de un casete por el shutter.

## Protocolo / librería

- `CRM9250NDev.dll` del fabricante, consumida con los `[DllImport]` y structs ya incluidos en `CRM9250N.cs` (**no se modifica**; se compila dentro del proyecto).
- Las funciones reciben punteros a structs `Pack = 1` con arreglos `ByValArray`; `Helpers/Native.cs` crea los structs con sus arreglos ya dimensionados y administra la memoria nativa (`NativeScope`).
- Secuencia usada:

| Prueba | Llamadas SDK, en orden |
| :-- | :-- |
| Inicializar (menú 1, **no mueve nada**) | pre-vuelo de la DLL → `hOpenLogicDevice("CRM9250NDev")` → `iSetCommPara` → `iGetCassetteInfoEx` (informativa) → `iGetDevVersionEx` → `iGetDevStatusEx` |
| Reinicio mecánico (menú 11, **se mueve**) | lo anterior + `iPowerOnInitEx` (= `iInitSettingEx` + `iGetCassetteInfoEx` + `iInitEx`), previa confirmación del asistente. Lo piden solas las opciones 2 y 3, una vez por sesión |
| Aceptar | `iPrepareDeposit` → `iOpenShutter` → *(usuario inserta)* → `iCloseShutter` → `iCashCount` → `iStoreMoneyEx` **o** `iCancelDepositEx` → `iPrepareNextTransaction` |
| Dispensar | `iPrepareDispense` → `iDispenseEx` (1 a 4 casetes en la misma orden) → `iOpenShutter` → *(usuario retira)* → `iCloseShutter` → `iPrepareNextTransaction` |

## Precondiciones

- **Carpeta `dll` completa del SDK de GRG** (`CRM9250NDev_V2.3.7.6\dll`: la DLL, `GRGDTATM_CommCfg.ini`, `CRM9250NDevCfg.ini`, `mfc42.dll`, etc.) junto al `.exe`; el `.csproj` la copia sola desde esa ruta (se cambia con `-p:GrgSdkDir=<carpeta>`).
- **`DevBaseDll.dll`**: la importa `CRM9250NDev.dll` y **no viene en el zip del SDK V2.3.7.6**. Debe salir del paquete de instalación / driver de GRG. Sin ella no hay comunicación.
- **Driver USB de GRG** instalado y recicladora conectada por USB (`CommType=3`, VID `04B4` / PID `8016`, según `GRGDTATM_CommCfg.ini`). No usa puerto COM.
- **Proceso de 32 bits:** la DLL es x86 (PE machine `0x14C`), por eso el `.csproj` usa `PlatformTarget=x86`. En la máquina IoT hace falta el runtime x86 de .NET 8, o publicar con `dotnet publish -c Release -r win-x86 --self-contained`.
- Recicladora encendida, casetes instalados, al menos uno cargado con billetes de prueba.
- Billetes de prueba: al menos uno de MXN 500, 200 y 100 y de USD 20, y un billete inválido (dañado o de una moneda no soportada). Opcional: otras denominaciones USD para ver si van al recycler.
- Los cuatro casetes con unos pocos billetes de su denominación (anotar cuántos, para cuadrar al final).
- Límite de seguridad: `MaxBilletesPrueba` (5 por defecto) impide dispensar cantidades grandes por error.

## Configuración física

Orden confirmado por observación directa de la gaveta física (2026-10-15): al jalarla, lo primero que sale es
el casete de USD 20, luego MXN 100, 200 y 500, y al final el recycler de banda azul. Esto **reemplaza** el
orden que traía `Finso_TAC_Logical_Tecnica.pdf` §2 (que ponía MXN 500 antes de MXN 100 — ver "Supuestos y
riesgos" más abajo, sigue pendiente confirmar que la posición física == número RC del SDK).

| Posición en la gaveta | Recurso | Moneda / denominación | Función |
| :-- | :-- | :-- | :-- |
| 1ª (sale primero) | Cassette 1 | USD 20 | Dispensa USD (única denominación USD de salida) |
| 2ª | Cassette 2 | MXN 100 | Dispensa MXN |
| 3ª | Cassette 3 | MXN 200 | Dispensa MXN |
| 4ª | Cassette 4 | MXN 500 | Dispensa MXN |
| 5ª (al final) | Recycler (banda azul) | Otras denominaciones USD / MXN | Recibe billetes válidos que no se dispensan |

Por eso `Test_DispensarBilletes` recibe una lista de renglones **(moneda, denominación, cantidad)**: `("MXN", 100, 1)` va al casete 2, `("USD", 20, 1)` al casete 1. El recycler de banda azul no aparece en `Casetes`, así que nunca es una opción para dispensar. Se puede pedir más de un renglón en la misma llamada — ej. `("MXN", 500, 1), ("MXN", 200, 1), ("MXN", 100, 1)` para armar $800 MXN — y los tres salen en **una sola orden atómica** a `iDispenseEx` (el struct del SDK soporta hasta 16 casetes por orden; aquí hay 4 configurados como máximo).

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
    { "Indice": 2, "Nombre": "Cassette 2", "Moneda": "MXN", "Denominacion": 100 },
    { "Indice": 3, "Nombre": "Cassette 3", "Moneda": "MXN", "Denominacion": 200 },
    { "Indice": 4, "Nombre": "Cassette 4", "Moneda": "MXN", "Denominacion": 500 }
  ],
  "EtiquetaDenominacion": {}
}
```

Valores tomados de la **demo WinForms y el PDF del SDK de GRG**: el nombre lógico es el nodo `[CRM9250NDev]` de `GRGDTATM_CommCfg.ini`; éxito = `rc == 0`; `byDispType = 1` dispensa por casete RC; el casete se indica como número RC con **base 1** (la demo pide `RC2` con `wDenomOrCassetteInfo = 2`).

`EtiquetaDenominacion` empieza vacío: el SDK devuelve un **código** de denominación (`wCode`), no "USD 20". Al aceptar un billete conocido, la prueba pregunta qué billete es y te indica qué par `código → etiqueta` agregar. Así se construye la tabla real del equipo durante la primera sesión.

## Modalidad remota (desarrollador a distancia + asistente en sitio)

El desarrollador opera la consola a distancia y un asistente en sitio mete y saca los billetes. Antes de empezar, el asistente lee la sección 1 ("Recicladora de billetes") de `Guia_Asistente_Sitio.md` y los dos quedan en llamada o videollamada durante toda la prueba.

| Momento | Qué pasa en consola | Qué hace el asistente |
| :-- | :-- | :-- |
| Antes de abrir el shutter o dispensar | `!! VA A MOVERSE` + "¿El asistente está listo?" | Aleja las manos y dice "listo". Si responde que no, la prueba se detiene sin mover nada |
| Aceptar: antes de insertar | Pregunta qué billete(s) tiene a la mano (uno o varios, separados por coma) | Los dice (quedan en la bitácora junto al código del SDK de cada uno) |
| Aceptar: shutter abierto | "¿Ve el shutter abierto?" | Lo confirma (R-06 físico) |
| Aceptar: al insertar | Bloquea hasta "listo"; **al dar ENTER el shutter se cierra** | Inserta todos juntos, en la misma apertura, retira la mano por completo y solo entonces dice "listo" |
| Devolución | Aviso antes de abrir; luego "¿Cuántos billetes retiró?" | Retira todos y dice el número total |
| Dispensa | "¿Cuántos billetes hay en el shutter?" (también si `iDispenseEx` falló) | Los cuenta **sin tocarlos**, dice el número y la denominación, luego los retira |

Los conteos se piden como número y no como sí/no para no inducir la respuesta. Toda la sesión queda en `bitacoras\sesion_*.txt` junto al `.exe`: es la evidencia que se comparte.

## Fase 0 – Validar la comunicación (primer objetivo)

Antes de aceptar o dispensar nada, el menú `1` debe quedar en `APROBADO`. Esa opción hace un pre-vuelo y después abre y consulta el SDK **sin mover la máquina** (`iPowerOnInitEx` hace un reinicio mecánico y va aparte, en el menú `11`), y se detiene en el primer fallo diciendo qué pasó:

| Paso | Qué comprueba | Si falla, causa probable |
| :-- | :-- | :-- |
| 1 | `CRM9250NDev.dll` está junto al `.exe` o en el `PATH` | Falta copiar la carpeta `dll` del SDK de GRG |
| 2 | Arquitectura de la DLL (lee el encabezado PE) igual a la del proceso | La DLL es x86: el proceso debe ser de 32 bits |
| 3 | `GRGDTATM_CommCfg.ini` junto a la DLL con el nodo `[CRM9250NDev]` | El nombre lógico debe coincidir con el nodo del `.ini`; ahí el SDK lee el tipo de enlace (USB) |
| 4 | Windows ha enumerado el USB `VID_04B4&PID_8016` (registro) | Recicladora nunca conectada a ese equipo, cable suelto o falta el driver de GRG |
| 5 | Todas las DLL que importa `CRM9250NDev.dll` existen (lee la tabla de importación) | Falta p. ej. `DevBaseDll.dll`: no viene en el zip del SDK |
| 6 | Windows carga la DLL (126 = dependencia, 193 = arquitectura) | Ver la línea del paso 5 |
| 7 | `hOpenLogicDevice` devuelve handle ≠ 0 | Nombre lógico incorrecto o SDK sin su `.ini` |
| 8 | `iSetCommPara` devuelve 0 (e `iPowerOnInitEx` si se ejecuta el menú 11) | Ver `logic`/`phy`/`msg` impresos; consultar `CRM9250N LogicalCode.xls` del SDK |
| 9 | `iGetDevVersionEx` devuelve firmware legible y se listan los casetes presentes | Comparar con lo instalado físicamente |
| 10 | `iGetDevStatusEx` responde (consulta en vivo, sin mover nada) | Confirma el enlace de ida y vuelta con la máquina |

Los pasos 1 a 6 se evalúan **sin necesitar la recicladora**: sirven para preparar el equipo antes de ir a la máquina. Del 7 en adelante requieren la recicladora encendida y conectada.

**Resultado a observar:** la consola termina en `RESULTADO Recicladora GRG 9250N - Inicialización: APROBADO`, con firmware legible y los casetes presentes iguales a los instalados. Solo con eso se pasa a aceptar y dispensar.

## Código (`Tests/Recicladora.cs`)

```csharp
using System.Runtime.InteropServices;
using CRM9250N_WinFormDemo;
using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using IntegracionCajeroPruebas.Protocolos;

namespace IntegracionCajeroPruebas.Tests;

// Recicladora GRG 9250N vía CRM9250NDev.dll (P/Invoke ya declarado en CRM9250N.cs).
public static partial class HardwareTests
{
    private static IntPtr _crm = IntPtr.Zero;
    private static bool _reinicioMecanicoHecho;
    private static RecicladoraConfig Crm => HardwareConfig.Actual.Recicladora;

    /// <param name="reinicioMecanico">true = además ejecuta iPowerOnInitEx (la recicladora se mueve). Por defecto solo conecta y lee.</param>
    public static void Test_InicializarRecicladora(bool reinicioMecanico = false)
    {
        var r = new Reporte(reinicioMecanico
            ? "Recicladora GRG 9250N - Inicialización con reinicio mecánico"
            : "Recicladora GRG 9250N - Inicialización (conexión, sin movimiento)");
        try
        {
            if (!reinicioMecanico) InicializarRecicladora(r);
            else EncenderRecicladora(r);
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

    /// <summary>
    /// Depósito según el flujo recomendado por GRG (PDF §5.3), sin preguntas (el dev ve todo por videollamada):
    /// iPrepareNextTransaction -> iPrepareDeposit -> iOpenShutter -> sondeo de iGetDevStatusEx hasta que coloquen
    /// billetes (o "Cancel" por tiempo agotado) -> iCloseShutter -> iCashCount por lotes -> iStoreMoneyEx si se contó
    /// algo ("Confirm deposit = Y"); si no, iCancelDepositEx + iOpenShutter para devolverlos.
    /// "Continue count?" se decide solo: otro lote si el conteo se cortó por LimiteDeposito (25120) y quedan billetes
    /// en la ranura; se termina si la ranura quedó vacía o solo quedaron rechazados.
    /// </summary>
    /// <param name="moneda">"MXN" o "USD": solo se aceptan billetes de esa moneda (los demás se rechazan y se devuelven). null = cualquiera.</param>
    public static void Test_DepositarBilletes(string? moneda = null)
    {
        var r = new Reporte($"Recicladora GRG 9250N - Depositar {(moneda is null ? "billetes" : moneda == "MXN" ? "pesos (MXN)" : "dólares (USD)")} (flujo GRG)");
        try
        {
            if (!AsegurarRecicladora(r)) { r.Finalizar(); return; }

            using var s = new NativeScope();
            var pStatus = s.Salida<tDevReturn>();

            Paso("INICIO del flujo de depósito de GRG");
            Paso($"Modo de validación de billetes: {Crm.ModoValidacionBilletes} ({NombreModoValidacion(Crm.ModoValidacionBilletes)}), " +
                 $"límite por conteo: {Crm.LimiteDeposito} billete(s)");
            var noAceptadas = DenominacionesDeOtrasMonedas(moneda);
            if (moneda is not null)
                Paso($"Depósito solo de {moneda}: awUnacceptableDenom excluye {noAceptadas.Count} código(s) de otras monedas");

            if (moneda is not null && !ConfigurarMonedaDeposito(r, moneda))
            { Paso("FIN: no se pudo configurar la moneda del depósito"); r.Finalizar(); return; }

            Paso("iPrepareNextTransaction: preparar la transacción");
            if (!CrmOk(r, "iPrepareNextTransaction", CRM9250N.iPrepareNextTransaction(_crm, pStatus), pStatus))
            { Paso("FIN: no se pudo preparar la transacción"); r.Finalizar(); return; }

            Paso("iPrepareDeposit: reservar el depósito");
            if (!CrmOk(r, "iPrepareDeposit", CRM9250N.iPrepareDeposit(_crm, pStatus), pStatus))
            {
                // 26301: billetes en la ranura de un intento anterior; el depósito solo se prepara con la ranura vacía.
                if (Native.Leer<tDevReturn>(pStatus).iLogicCode == 26301)
                    Consola.Aviso("        Hay billetes en la ranura de un intento anterior: ábrala con \"Abrir shutter\", retírelos y repita.");
                Paso("FIN: no se pudo preparar el depósito");
                r.Finalizar(); return;
            }

            Paso("iOpenShutter: abrir para colocar los billetes");
            if (!CrmOk(r, "iOpenShutter", CRM9250N.iOpenShutter(_crm, pStatus), pStatus))
            { Paso("FIN: el shutter no abrió"); r.Finalizar(); return; }

            if (!EsperarColocacionDeBilletes())
            {
                // Rama "Cancel" del flujo: nadie colocó billetes. NE vacío y NF vacío -> cerrar y terminar.
                Paso("¿Se colocaron billetes? NO, tiempo agotado -> Cancel");
                Consola.Aviso($"        No se colocaron billetes en {Crm.SegundosColocarBilletes} s: no hay nada que depositar.");
                CancelarDepositoSinConteo(r);
                Paso("FIN del flujo de depósito");
                r.Finalizar(); return;
            }

            Paso("iCloseShutter: cerrar para contar");
            if (!CrmOk(r, "iCloseShutter", CRM9250N.iCloseShutter(_crm, pStatus), pStatus))
            { Paso("FIN: el shutter no cerró"); r.Finalizar(); return; }

            int totalContado = 0, totalRechazado = 0;
            for (var lote = 1; lote <= Crm.MaxLotesDeposito; lote++)
            {
                var conteo = Native.Cero<tCRM9250N_CashCount>();
                conteo.iDepositLimit = Crm.LimiteDeposito;
                for (var i = 0; i < Math.Min(noAceptadas.Count, conteo.awUnacceptableDenom.Length); i++)
                    conteo.awUnacceptableDenom[i] = noAceptadas[i];
                var pConteo = s.Entrada(conteo);
                Paso($"iCashCount: contar lote {lote}");
                var rc = CRM9250N.iCashCount(_crm, pConteo, pStatus);
                var logica = Native.Leer<tDevReturn>(pStatus).iLogicCode;
                if (!CrmOk(r, $"iCashCount lote {lote}", rc, pStatus)) break;

                var res = Native.Leer<tCRM9250N_CashCount>(pConteo);
                totalContado += res.iTotalCount;
                totalRechazado += res.iRejectCount;
                Evento($"Lote {lote}: contados={res.iTotalCount} rechazados={res.iRejectCount} " +
                       $"desconocidos={res.sDenomNumInfo.iUnknownCount} (acumulado en NE: {totalContado})");
                ImprimirDenominaciones(res.sDenomNumInfo);
                if (res.iRejectCount > 0)
                {
                    Paso($"iGetNvNotesInfoEx (opcional): {res.iRejectCount} rechazo(s) en el lote {lote}");
                    ImprimirMotivosRechazo();
                }

                // "Continue count?": solo si el conteo se cortó por el límite y aún hay billetes en la ranura.
                var d = LeerEstadoDispositivo();
                var quedanEnRanura = d is { byNfHasNotes: not 0 } || d is { byShutterAreaHasNotes: not 0 };
                var cortePorLimite = logica == 25120 || res.iTotalCount >= Crm.LimiteDeposito;
                if (cortePorLimite && quedanEnRanura && lote < Crm.MaxLotesDeposito)
                {
                    Paso($"¿Continuar contando? SÍ: el lote {lote} llegó al límite ({Crm.LimiteDeposito}) y quedan billetes en la ranura");
                    continue;
                }
                Paso(quedanEnRanura
                    ? "¿Continuar contando? NO: en la ranura solo quedan billetes rechazados"
                    : "¿Continuar contando? NO: la ranura quedó vacía");
                if (cortePorLimite && quedanEnRanura)
                    Consola.Aviso($"        Se alcanzó el máximo de {Crm.MaxLotesDeposito} lote(s); lo que quede en la ranura se devuelve.");
                break;
            }

            if (totalContado > 0)
            {
                Paso($"¿Confirmar depósito? SÍ ({totalContado} billete(s) en NE) -> iStoreMoneyEx");
                var store = Native.Cero<tCRM9250N_StoreMoneyEx>();
                store.byRejectPos = Crm.DestinoRechazoDeposito;
                store.byNvMode = 0;
                // Prioridad 1..4 para RC1..RC4 (0x01-0x0F, menor = más prioridad), como en la demo de GRG.
                for (var i = 0; i < 4; i++) store.abyCassettePriority[i] = (byte)(i + 1);
                var pStore = s.Entrada(store);
                var storeOk = CrmOk(r, $"iStoreMoneyEx: depósito de {totalContado} billete(s)", CRM9250N.iStoreMoneyEx(_crm, pStore, pStatus), pStatus);
                if (storeOk) ImprimirDestinoDeposito(Native.Leer<tCRM9250N_StoreMoneyEx>(pStore));
                r.Criterio("Depósito realizado (se contó al menos 1 billete)", true,
                    $"contados={totalContado} rechazados={totalRechazado}");

                // Los rechazados regresan a la ranura: se abre el shutter para devolverlos y se recogen si nadie los toma.
                if (LeerEstadoDispositivo() is { byNfHasNotes: not 0 } or { byShutterAreaHasNotes: not 0 })
                {
                    Paso("Quedaron billetes rechazados en la ranura -> iOpenShutter para devolverlos");
                    if (CrmOk(r, "iOpenShutter (devolver rechazados)", CRM9250N.iOpenShutter(_crm, pStatus), pStatus))
                        EsperarRetiroDeBilletes(r);
                    CerrarYRecogerRemanentes(r);
                }
            }
            else
            {
                Paso("¿Confirmar depósito? NO (0 billetes aceptados) -> iCancelDepositEx + iOpenShutter para devolverlos");
                var pCancel = s.Entrada(Native.Cero<tCRM9250N_CancelDepositEx>());
                CrmOk(r, "iCancelDepositEx", CRM9250N.iCancelDepositEx(_crm, pCancel, pStatus), pStatus);
                r.Criterio("Depósito realizado (se contó al menos 1 billete)", false,
                    $"contados=0 rechazados={totalRechazado} — la máquina no aceptó ningún billete; se devuelven por el shutter");
                if (CrmOk(r, "iOpenShutter (devolver billetes)", CRM9250N.iOpenShutter(_crm, pStatus), pStatus))
                    EsperarRetiroDeBilletes(r);
                CerrarYRecogerRemanentes(r);
            }
            Paso("FIN del flujo de depósito");
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    // Códigos del NV que NO son de la moneda pedida, para iCashCount.awUnacceptableDenom (vale solo para ese conteo,
    // no requiere reiniciar la máquina). La moneda de cada código se deduce de su byte alto (FamiliaDenominacionPorMoneda).
    private static List<ushort> DenominacionesDeOtrasMonedas(string? moneda)
    {
        var excluidas = new List<ushort>();
        if (moneda is null) return excluidas;
        if (!Crm.FamiliaDenominacionPorMoneda.TryGetValue(moneda, out var familia))
        {
            Consola.Aviso($"        Moneda '{moneda}' sin familia de denominación en hardware.json: se aceptan todas.");
            return excluidas;
        }

        using var s = new NativeScope();
        var pStatus = s.Salida<tDevReturn>();
        var pTabla = s.Salida<tCRM9250N_DenominationTable>();
        if (Crm9250NExtra.iGetDenominationTable(_crm, pTabla, pStatus) != Crm.CodigoExito)
        {
            Consola.Aviso("        No se pudo leer la tabla de denominaciones: no se filtra por moneda (se aceptan todas).");
            return excluidas;
        }
        foreach (var d in Native.Leer<tCRM9250N_DenominationTable>(pTabla).asDenominationInfo)
            if (d.wDenomCode != 0 && (d.wDenomCode >> 8) != familia) excluidas.Add(d.wDenomCode);
        return excluidas;
    }

    // Sondeo de iGetDevStatusEx hasta detectar billetes en la ranura/shutter. Tras detectarlos espera unos segundos
    // más para que terminen de acomodar el fajo antes de cerrar.
    private static bool EsperarColocacionDeBilletes()
    {
        Paso($"iGetDevStatusEx (sondeo cada 500 ms): esperando que coloquen billetes, máx. {Crm.SegundosColocarBilletes} s");
        Consola.Aviso("  >> Shutter abierto: coloque los billetes");
        var inicio = DateTime.UtcNow;
        var limite = inicio.AddSeconds(Crm.SegundosColocarBilletes);
        var proximoReporte = inicio.AddSeconds(5);
        while (DateTime.UtcNow < limite)
        {
            if (LeerEstadoDispositivo() is { byNfHasNotes: not 0 } or { byShutterAreaHasNotes: not 0 })
            {
                Paso($"¿Se colocaron billetes? SÍ (a los {(DateTime.UtcNow - inicio).TotalSeconds:0} s); " +
                     $"se esperan {Crm.SegundosAcomodoBilletes} s para que terminen de acomodarlos");
                Thread.Sleep(Crm.SegundosAcomodoBilletes * 1000);
                return true;
            }
            if (DateTime.UtcNow >= proximoReporte)
            {
                Evento($"Esperando billetes... quedan {(limite - DateTime.UtcNow).TotalSeconds:0} s");
                proximoReporte = proximoReporte.AddSeconds(5);
            }
            Thread.Sleep(500);
        }
        return false;
    }

    // Rama "Cancel" sin conteo: si hay billetes en NE se cancela el depósito y se devuelven; si solo hay en NF y
    // nadie los toma, se cierran y se recogen; si no hay nada, solo se cierra el shutter.
    private static void CancelarDepositoSinConteo(Reporte r)
    {
        using var s = new NativeScope();
        var pStatus = s.Salida<tDevReturn>();
        var d = LeerEstadoDispositivo();
        if (d is { byNeHasNotes: not 0 })
        {
            Paso("¿Hay billetes en NE? SÍ -> iCloseShutter + iCancelDepositEx + iOpenShutter");
            CrmOk(r, "iCloseShutter", CRM9250N.iCloseShutter(_crm, pStatus), pStatus);
            CrmOk(r, "iCancelDepositEx", CRM9250N.iCancelDepositEx(_crm, s.Entrada(Native.Cero<tCRM9250N_CancelDepositEx>()), pStatus), pStatus);
            if (CrmOk(r, "iOpenShutter (devolver billetes)", CRM9250N.iOpenShutter(_crm, pStatus), pStatus))
                EsperarRetiroDeBilletes(r);
            CerrarYRecogerRemanentes(r);
        }
        else if (d is { byNfHasNotes: not 0 })
        {
            Paso("¿Hay billetes en NF? SÍ -> esperar retiro; si no los toman, iCloseShutter + iRetractEx");
            EsperarRetiroDeBilletes(r);
            CerrarYRecogerRemanentes(r);
        }
        else
        {
            Paso("¿Hay billetes en NE/NF? NO -> cerrar el shutter");
            CrmOk(r, "iCloseShutter", CRM9250N.iCloseShutter(_crm, pStatus), pStatus);
        }
    }

    /// <param name="pedido">
    /// Uno o varios renglones (moneda, denominación, cantidad) a dispensar JUNTOS en una sola orden atómica
    /// (iDispenseEx acepta hasta 16 RC a la vez: ver asReqDispInfo). Ej. para armar $800 MXN:
    /// <c>Test_DispensarBilletes(("MXN", 500, 1), ("MXN", 200, 1), ("MXN", 100, 1))</c>.
    /// Cada denominación debe corresponder a un único casete distinto de hardware.json (el recycler de banda
    /// azul no aparece ahí, así que nunca es una opción). El shutter se abre una sola vez al final, para que
    /// el cliente retire todo junto.
    /// </param>
    public static void Test_DispensarBilletes(params (string Moneda, int Denominacion, int Cantidad)[] pedido)
    {
        var resumen = string.Join(" + ", pedido.Select(p => $"{p.Cantidad}x{p.Moneda}{p.Denominacion}"));
        var r = new Reporte($"Recicladora GRG 9250N - Dispensar {resumen}");
        try
        {
            if (pedido.Length == 0 || pedido.Length > Crm.Casetes.Count)
            {
                r.Criterio($"Entre 1 y {Crm.Casetes.Count} renglón(es) (un casete dispensable distinto por renglón)",
                    false, $"renglones={pedido.Length}");
                r.Finalizar(); return;
            }

            if (pedido.Select(p => p.Moneda.ToUpperInvariant()).Distinct().Count() > 1)
            {
                r.Criterio("Todo el pedido es de una sola moneda (no se mezclan MXN y USD en una orden)", false,
                    string.Join(", ", pedido.Select(p => $"{p.Moneda} {p.Denominacion}")));
                r.Finalizar(); return;
            }

            var totalPedido = pedido.Sum(p => p.Cantidad);
            if (pedido.Any(p => p.Cantidad < 1) || totalPedido > Crm.MaxBilletesPrueba)
            {
                r.Criterio($"Cada renglón ≥ 1 billete y el total ≤ {Crm.MaxBilletesPrueba} (límite de seguridad de la prueba)",
                    false, $"total pedido={totalPedido}");
                r.Finalizar(); return;
            }

            var renglones = new List<(CaseteConfig Cassette, int Cantidad)>();
            foreach (var (moneda, denominacion, cantidad) in pedido)
            {
                var candidatos = Crm.Casetes
                    .Where(c => c.Moneda.Equals(moneda, StringComparison.OrdinalIgnoreCase) && c.Denominacion == denominacion)
                    .ToList();
                if (candidatos.Count != 1)
                {
                    var opciones = string.Join(", ", Crm.Casetes.Select(c => $"{c.Moneda} {c.Denominacion}"));
                    r.Criterio($"{moneda} {denominacion} corresponde a un único casete en hardware.json", false,
                        $"coincidencias={candidatos.Count}. Casetes configurados: {opciones}.");
                    r.Finalizar(); return;
                }
                renglones.Add((candidatos[0], cantidad));
            }
            if (renglones.Select(x => x.Cassette.Indice).Distinct().Count() != renglones.Count)
            {
                r.Criterio("Cada renglón apunta a un casete distinto (no se repite la misma denominación)", false);
                r.Finalizar(); return;
            }

            foreach (var (cassette, cantidad) in renglones)
                Evento($"Pedido: {cantidad}x {cassette.Nombre} ({cassette.Moneda} {cassette.Denominacion}), índice SDK {cassette.Indice}");

            if (!AsegurarRecicladora(r)) { r.Finalizar(); return; }
            if (!HayBilletesParaDispensar(renglones)) { r.Finalizar(); return; }
            Consola.Aviso($"  >> El dispositivo recibió la orden: dispensar {resumen} y abrir el shutter");

            // Flujo recomendado por GRG (PDF §5.2): iPrepareNextTransaction -> iDispenseEx -> si falla, iInitEx hasta
            // N veces y luego mantenimiento; si sale bien, iOpenShutter y sondeo de iGetDevStatusEx hasta que retiren
            // los billetes o se agote el tiempo -> iCloseShutter -> iGetDevStatusEx -> iRetractEx si quedó algo en NF/NE.
            using var s = new NativeScope();
            var pStatus = s.Salida<tDevReturn>();

            Paso("INICIO del flujo de dispensa de GRG");
            Paso($"Modo de validación de billetes: {Crm.ModoValidacionBilletes} ({NombreModoValidacion(Crm.ModoValidacionBilletes)})");
            Paso("iPrepareNextTransaction: preparar la transacción");
            if (!CrmOk(r, "iPrepareNextTransaction", CRM9250N.iPrepareNextTransaction(_crm, pStatus), pStatus))
            { Paso("FIN: no se pudo preparar la transacción"); r.Finalizar(); return; }

            var disp = Native.Cero<tCRM9250N_DispenseInfoEx>();
            disp.byDispType = Crm.TipoDispensa;
            disp.byRejectToAc = Crm.DestinoRechazoDispensa;
            for (var i = 0; i < renglones.Count; i++)
                disp.asReqDispInfo[i] = new tCRM9250N_ReqDispInfo
                { wDenomOrCassetteInfo = (ushort)renglones[i].Cassette.Indice, iNum = renglones[i].Cantidad };
            var pDisp = s.Entrada(disp);

            Paso($"iDispenseEx: dispensar {resumen} (orden única, {renglones.Count} casete(s))");
            var ok = CrmOk(r, "iDispenseEx", CRM9250N.iDispenseEx(_crm, pDisp, pStatus), pStatus);
            Paso(ok ? "¿Dispensa correcta? SÍ -> abrir shutter y esperar el retiro"
                    : "¿Dispensa correcta? NO -> no se abre el shutter; recuperación con reinicio (iInitEx)");

            var dispensadoTotal = 0;
            if (ok)
            {
                var res = Native.Leer<tCRM9250N_DispenseInfoEx>(pDisp);
                dispensadoTotal = res.aiDispensedInfo.Sum();
                var rechazadoTotal = res.aiRejectInfo.Sum();
                Evento($"iDispenseEx resultado: dispensados={dispensadoTotal} rechazados={rechazadoTotal}");
                r.Criterio($"La recicladora reporta {totalPedido} billete(s) dispensado(s) en total", dispensadoTotal == totalPedido,
                    $"dispensados={dispensadoTotal} rechazados={rechazadoTotal}");

                // El TAC exige conciliar contadores físicos por casete: una orden de dispensa no equivale a entrega exitosa.
                foreach (var (cassette, cantidad) in renglones)
                {
                    var idx = cassette.Indice - 1; // aiDispensedInfo/aiRejectInfo son 0-based, uno por RC1..RC16
                    var d = idx >= 0 && idx < res.aiDispensedInfo.Length ? res.aiDispensedInfo[idx] : -1;
                    var rj = idx >= 0 && idx < res.aiRejectInfo.Length ? res.aiRejectInfo[idx] : -1;
                    r.Criterio($"Casete {cassette.Nombre} ({cassette.Moneda} {cassette.Denominacion}): {cantidad} pedido(s), {d} dispensado(s)",
                        d == cantidad, $"rechazados={rj}");
                }
                if (rechazadoTotal > 0)
                {
                    Paso($"iGetNvNotesInfoEx (opcional): hubo {rechazadoTotal} rechazo(s), se consulta el detalle del NV");
                    ImprimirMotivosRechazo();
                }
                else Paso("iGetNvNotesInfoEx (opcional): omitido, no hubo rechazos");

                Paso("iOpenShutter: abrir para que se retiren los billetes");
                if (CrmOk(r, "iOpenShutter", CRM9250N.iOpenShutter(_crm, pStatus), pStatus))
                    EsperarRetiroDeBilletes(r);
                else Paso("El shutter no abrió: se pasa directo a cerrar y recoger");
            }
            else
            {
                // Con la dispensa fallida no se abre el shutter (antes se abría vacío): se intenta recuperar la máquina.
                if (!RecuperarTrasFalla(r))
                    Paso($"No volvió a la normalidad tras {Crm.IntentosRecuperacion} reinicio(s) -> MANTENIMIENTO FUERA DE LÍNEA " +
                         "(revisar atascos/casetes con el técnico)");
            }

            CerrarYRecogerRemanentes(r);
            Paso("FIN del flujo de dispensa");
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    /// <summary>Control manual del shutter, independiente del flujo de aceptar/dispensar. Sirve para diagnosticar
    /// si ya está abierto/cerrado antes de asumir que otro comando (ej. iDispenseEx) lo mueve por su cuenta, y
    /// también como paso final tras depositar o dispensar (el dev lo invoca cuando ya retiró/colocó los billetes).</summary>
    public static void Test_AbrirShutter()
    {
        var r = new Reporte("Recicladora GRG 9250N - Abrir shutter (control manual)");
        try
        {
            if (!AsegurarRecicladora(r)) { r.Finalizar(); return; }

            using var s = new NativeScope();
            var pStatus = s.Salida<tDevReturn>();
            Evento("iOpenShutter: solicitando (manual)");
            CrmOk(r, "iOpenShutter", CRM9250N.iOpenShutter(_crm, pStatus), pStatus);
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    /// <summary>Contraparte de <see cref="Test_AbrirShutter"/>: cierra el shutter y prepara la siguiente transacción.
    /// Úsala cuando ya hayas retirado los billetes (tras dispensar) o colocado los que vas a depositar.</summary>
    public static void Test_CerrarShutter()
    {
        var r = new Reporte("Recicladora GRG 9250N - Cerrar shutter (control manual)");
        try
        {
            if (!AsegurarRecicladora(r)) { r.Finalizar(); return; }

            using var s = new NativeScope();
            var pStatus = s.Salida<tDevReturn>();
            Evento("iCloseShutter: solicitando (manual)");
            CrmOk(r, "iCloseShutter", CRM9250N.iCloseShutter(_crm, pStatus), pStatus);
            Evento("iPrepareNextTransaction: solicitando");
            CrmOk(r, "iPrepareNextTransaction", CRM9250N.iPrepareNextTransaction(_crm, pStatus), pStatus);
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    /// <summary>
    /// Consulta de solo lectura (no mueve nada): presencia/estado por casete (iGetCassetteInfoEx) cruzado con la
    /// denominación configurada en hardware.json. No da cantidad de billetes: para eso está
    /// <see cref="Test_ConsultarConteoCasetes"/> (iGetCassetteDenomDetailsEx).
    /// </summary>
    public static void Test_ConsultarEstadoCasetes()
    {
        var r = new Reporte("Recicladora GRG 9250N - Estado de casetes (solo lectura)");
        try
        {
            if (_crm == IntPtr.Zero && !InicializarRecicladora(r)) { r.Finalizar(); return; }

            using var s = new NativeScope();
            var pStatus = s.Salida<tDevReturn>();
            var pCasetes = s.Salida<tCRM9250N_CassetteInfoEx>();
            var pEstado = s.Salida<tCRM9250N_CassetteStatusEx>();
            if (!CrmOk(r, "iGetCassetteInfoEx", Crm9250NExtra.iGetCassetteInfoEx(_crm, pCasetes, pEstado, pStatus), pStatus))
            { r.Finalizar(); return; }

            var estado = Native.Leer<tCRM9250N_CassetteStatusEx>(pEstado);
            var info = Native.Leer<tCRM9250N_CassetteInfoEx>(pCasetes);
            foreach (var cassette in Crm.Casetes.OrderBy(c => c.Indice))
            {
                var idx = cassette.Indice - 1;
                if (idx < 0 || idx >= estado.asRcStatus.Length)
                {
                    Consola.Aviso($"        {cassette.Nombre} ({cassette.Moneda} {cassette.Denominacion}): índice {cassette.Indice} fuera de rango SDK");
                    continue;
                }
                var bin = estado.asRcStatus[idx];
                // wNoteType = denominación grabada en el propio casete (PDF §3.6). Sirve para verificar el mapa de hardware.json.
                var grabado = info.asRcCassette[idx];
                Consola.Info($"        RC{cassette.Indice} {cassette.Nombre} (hardware.json: {cassette.Moneda} {cassette.Denominacion}): " +
                             $"presente={bin.byExist} status={bin.byStatus} | grabado en el casete: denominación={grabado.wNoteType} " +
                             $"(0x{grabado.wNoteType:X4}) tipo={grabado.byCassetteType} función=0x{grabado.byFunction:X2}");
            }
            for (var i = 0; i < info.sAcCassette.Length; i++)
                if (estado.sAcStatus[i].byExist != 0)
                    Consola.Info($"        AC{i + 1} (caja de rechazos): presente=1 status={estado.sAcStatus[i].byStatus} " +
                                 $"tipo={info.sAcCassette[i].byCassetteType} función=0x{info.sAcCassette[i].byFunction:X2}");
            if (estado.sAtcStatus.byExist != 0)
                Consola.Info($"        ATC: presente=1 status={estado.sAtcStatus.byStatus}");
            Consola.Info($"        LRB: presente={estado.sLrbStatus.byExist}");
            Consola.Aviso("        Esta llamada no reporta cantidad de billetes; para eso usar la opción 27 (conteo por casete).");
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    /// <summary>
    /// Consulta de solo lectura (no mueve nada): cantidad de billetes por denominación de cada casete configurado en
    /// hardware.json (iGetCassetteDenomDetailsEx, API GRG §4.28). No está confirmado si iNoteCount es un conteo físico o
    /// un contador lógico del equipo: contrastar con una carga conocida y volver a consultar tras sacar/meter el casete.
    /// </summary>
    public static void Test_ConsultarConteoCasetes()
    {
        const int CapacidadEntradas = 16;
        var r = new Reporte("Recicladora GRG 9250N - Conteo de billetes por casete (solo lectura)");
        try
        {
            if (_crm == IntPtr.Zero && !InicializarRecicladora(r)) { r.Finalizar(); return; }

            var tamEntrada = System.Runtime.InteropServices.Marshal.SizeOf<tCRM9250N_DenomDetails>();
            Consola.Info($"        Tamaño de tCRM9250N_DenomDetails con Pack=1: {tamEntrada} bytes (esperado 7; si el SDK no empaqueta serían 12)");
            foreach (var cassette in Crm.Casetes.OrderBy(c => c.Indice))
            {
                using var s = new NativeScope();
                var pStatus = s.Salida<tDevReturn>();
                var pCantidad = s.Entrada(CapacidadEntradas);
                var pDetalle = s.Bloque(tamEntrada * CapacidadEntradas);
                var etiqueta = $"RC{cassette.Indice} {cassette.Nombre} (hardware.json: {cassette.Moneda} {cassette.Denominacion})";
                if (!CrmOk(r, $"iGetCassetteDenomDetailsEx RC{cassette.Indice}",
                        Crm9250NExtra.iGetCassetteDenomDetailsEx(_crm, cassette.Indice, pCantidad, pDetalle, pStatus), pStatus))
                    continue;

                var entradas = Math.Clamp(System.Runtime.InteropServices.Marshal.ReadInt32(pCantidad), 0, CapacidadEntradas);
                var total = 0;
                if (entradas == 0) Consola.Info($"        {etiqueta}: sin denominaciones reportadas");
                for (var i = 0; i < entradas; i++)
                {
                    var d = Native.Leer<tCRM9250N_DenomDetails>(IntPtr.Add(pDetalle, i * tamEntrada));
                    total += d.iNoteCount;
                    var nombre = Crm.EtiquetaDenominacion.GetValueOrDefault(d.wDenomCode, "sin etiqueta");
                    Consola.Info($"        {etiqueta}: código={d.wDenomCode} (0x{d.wDenomCode:X4}, {nombre}) " +
                                 $"tipo={d.byNoteType} billetes={d.iNoteCount}");
                }
                if (entradas > 0)
                    Consola.Info($"        {etiqueta}: TOTAL {total} billetes = {cassette.Moneda} {(long)total * cassette.Denominacion:N0} " +
                                 "(cantidad x denominación de hardware.json)");
            }
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    // ------------------------------------------------------------------ helpers

    // byStatus de tCRM9250N_BinStatus (API GRG §3.8): 0 normal, 1 lleno, 2 casi lleno, 3 casi vacío, 4 vacío, 255 anormal.
    // Un casete vacío no es falla de la prueba: se avisa y no se manda la orden de dispensa.
    private static bool HayBilletesParaDispensar(List<(CaseteConfig Cassette, int Cantidad)> renglones)
    {
        using var s = new NativeScope();
        var pStatus = s.Salida<tDevReturn>();
        var pCasetes = s.Salida<tCRM9250N_CassetteInfoEx>();
        var pEstado = s.Salida<tCRM9250N_CassetteStatusEx>();
        var rc = Crm9250NExtra.iGetCassetteInfoEx(_crm, pCasetes, pEstado, pStatus);
        if (rc != Crm.CodigoExito)
        {
            Consola.Aviso($"        No se pudo leer el estado de los casetes (rc={rc}); se intenta dispensar de todos modos.");
            return true;
        }

        var estado = Native.Leer<tCRM9250N_CassetteStatusEx>(pEstado);
        var hay = true;
        foreach (var (cassette, _) in renglones)
        {
            var bin = estado.asRcStatus[cassette.Indice - 1];
            var etiqueta = $"{cassette.Nombre} ({cassette.Moneda} {cassette.Denominacion})";
            switch (bin.byStatus)
            {
                case 4:
                    Consola.Aviso($"        No hay billetes en {etiqueta}: el casete reporta vacío. Rellenarlo antes de dispensar.");
                    hay = false;
                    break;
                case 255:
                    Consola.Aviso($"        {etiqueta} reporta estado anormal (255): revisar el casete antes de dispensar.");
                    hay = false;
                    break;
                case 3:
                    Consola.Aviso($"        {etiqueta} reporta casi vacío: puede no alcanzar para lo pedido.");
                    break;
            }
        }
        return hay;
    }

    // Conecta sin mover y, una sola vez por sesión, hace el reinicio mecánico que recomienda el SDK antes de operar billetes.
    private static bool AsegurarRecicladora(Reporte r)
    {
        if (_crm == IntPtr.Zero && !InicializarRecicladora(r)) return false;
        return _reinicioMecanicoHecho || ReiniciarMecanicamenteRecicladora(r);
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

    /// <summary>
    /// Abre el dispositivo y lee firmware y estado SIN mover nada. iPowerOnInitEx (= iInitSettingEx + iGetCassetteInfoEx + iInitEx)
    /// hace un reinicio mecánico: por eso va aparte, en <see cref="ReiniciarMecanicamenteRecicladora"/>.
    /// </summary>
    private static bool InicializarRecicladora(Reporte r)
    {
        CerrarRecicladora(); // reconectar desde cero si ya había un handle abierto
        _reinicioMecanicoHecho = false;
        if (!DiagnosticarDllRecicladora(r)) return false;
        AplicarModoValidacionBilletes();

        _crm = CRM9250N.hOpenLogicDevice(Crm.NombreDispositivoLogico);
        if (!r.Criterio($"hOpenLogicDevice('{Crm.NombreDispositivoLogico}') devuelve handle", _crm != IntPtr.Zero,
                _crm != IntPtr.Zero ? $"handle=0x{_crm:X}"
                    : "Handle nulo: el nombre lógico no existe en la configuración del SDK, o la recicladora no está " +
                      "encendida/conectada. Revisar Recicladora.NombreDispositivoLogico (VERIFICAR con la demo de GRG)."))
            return false;

        using var s = new NativeScope();
        var pStatus = s.Salida<tDevReturn>();
        if (!CrmOk(r, "iSetCommPara", CRM9250N.iSetCommPara(_crm, pStatus), pStatus)) return false;

        // Solo informativo (no es criterio): no está verificado que iGetCassetteInfoEx responda antes de la inicialización.
        var pCasetes = s.Salida<tCRM9250N_CassetteInfoEx>();
        var pEstado = s.Salida<tCRM9250N_CassetteStatusEx>();
        var rcCasetes = Crm9250NExtra.iGetCassetteInfoEx(_crm, pCasetes, pEstado, pStatus);
        if (rcCasetes == Crm.CodigoExito) ListarCasetes(Native.Leer<tCRM9250N_CassetteStatusEx>(pEstado));
        else Consola.Aviso($"        iGetCassetteInfoEx sin lectura previa a la inicialización (rc={rcCasetes}); VERIFICAR con el fabricante.");

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
            Consola.Info($"        Casetes extraídos: RC1={d.byRC1PlugRemove} RC2={d.byRC2PlugRemove} RC3={d.byRC3PlugRemove} RC4={d.byRC4PlugRemove} " +
                         $"AC(rechazos)={d.byACPlugRemove} ATC={d.byATCPlugRemove}");
        }
        return true;
    }

    private static tCRM9250N_InitSettingEx ConfiguracionInicial(string? moneda = null)
    {
        var cfg = Native.Cero<tCRM9250N_InitSettingEx>();
        cfg.byAtcMode = Crm.ModoAtc;
        cfg.byWorkingMode = Crm.ModoTrabajo;
        cfg.byDcAccepetAllDenom = Crm.AceptarTodasDenominaciones;
        cfg.byInitCfg = Crm.ConfigInicio;
        // PDF §3.2: awAcceptableDenom "empty: all not accepted". Con la lista en ceros el NV rechaza todo
        // (visto el 2026-10-21: 18 rechazados, logic 25119). Se llena con lo que el propio NV dice soportar.
        LlenarDenominacionesAceptadas(cfg.awAcceptableDenom, moneda);
        return cfg;
    }

    /// <summary>
    /// Equivale al "Support Notes" de la herramienta de GRG: el NV valida solo las denominaciones de la última
    /// iInitSettingEx/iPowerOnInitEx y esa lista persiste en la máquina (visto el 2026-10-22: tras configurar USD en la
    /// herramienta los pesos se rechazaban, y viceversa). Se carga la de la moneda del depósito antes de abrir el shutter.
    /// No mueve nada.
    /// </summary>
    private static bool ConfigurarMonedaDeposito(Reporte r, string moneda)
    {
        Paso($"iInitSettingEx: cargar las denominaciones de {moneda} como las únicas aceptadas (Support Notes)");
        using var s = new NativeScope();
        var pStatus = s.Salida<tDevReturn>();
        var cfg = ConfiguracionInicial(moneda);
        if (cfg.awAcceptableDenom.All(d => d == 0))
        {
            r.Criterio($"Denominaciones de {moneda} cargadas", false, "El NV no reporta ninguna denominación de esa moneda (revisar FamiliaDenominacionPorMoneda).");
            return false;
        }
        return CrmOk(r, $"iInitSettingEx ({moneda})", Crm9250NExtra.iInitSettingEx(_crm, s.Entrada(cfg), pStatus), pStatus);
    }

    /// <summary>
    /// iInitSettingEx suelto (PDF §4.5): solo carga la configuración de trabajo (modo, denominaciones aceptadas) en
    /// la recicladora. NO mueve nada: el reinicio mecánico es iInitEx / iPowerOnInitEx. Conecta si no hay handle.
    /// </summary>
    public static void Test_ConfigurarRecicladora()
    {
        var r = new Reporte("Recicladora GRG 9250N - Configurar (iInitSettingEx, sin movimiento)");
        try
        {
            if (_crm == IntPtr.Zero && !InicializarRecicladora(r)) { r.Finalizar(); return; }

            using var s = new NativeScope();
            var pStatus = s.Salida<tDevReturn>();
            var cfg = ConfiguracionInicial();
            Consola.Info($"        Modo ATC={cfg.byAtcMode} trabajo={cfg.byWorkingMode} aceptar todas={cfg.byDcAccepetAllDenom} " +
                         $"init cfg={cfg.byInitCfg} | denominaciones aceptadas: {cfg.awAcceptableDenom.Count(d => d != 0)}");
            CrmOk(r, "iInitSettingEx", Crm9250NExtra.iInitSettingEx(_crm, s.Entrada(cfg), pStatus), pStatus);
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    /// <summary>
    /// iPowerOnInitEx: reinicio mecánico. Pasa el transporte a su posición inicial y manda al NF los billetes que haya en el canal
    /// (PDF §4.6). El dev ve el movimiento directo por videollamada: no se pide confirmación, solo se avisa.
    /// </summary>
    private static bool ReiniciarMecanicamenteRecicladora(Reporte r)
    {
        Consola.Aviso("  >> El dispositivo recibió la orden: reinicio mecánico (ruido de motores; el shutter y el transporte se mueven)");

        using var s = new NativeScope();
        var pStatus = s.Salida<tDevReturn>();
        var pCfg = s.Entrada(ConfiguracionInicial());
        var pCasetes = s.Salida<tCRM9250N_CassetteInfoEx>();
        var pEstado = s.Salida<tCRM9250N_CassetteStatusEx>();
        var pInit = s.Salida<tCRM9250N_InitInfoEx>();

        if (!CrmOk(r, "iPowerOnInitEx", CRM9250N.iPowerOnInitEx(_crm, pCfg, pCasetes, pEstado, pInit, pStatus), pStatus))
            return false;
        _reinicioMecanicoHecho = true;
        ListarCasetes(Native.Leer<tCRM9250N_CassetteStatusEx>(pEstado));
        return true;
    }

    /// <summary>
    /// Flujo "Power on" de GRG (PDF §5.1): hOpenLogicDevice -> iSetCommPara -> iPowerOnInitEx (rama A; equivale a la
    /// rama B iInitSettingEx + iGetCassetteInfoEx + iInitEx, son alternativas y no se corren las dos) -> si la
    /// recicladora está offline, iCloseComm + vCloseLogicDevice.
    /// "Offline" = ya no responde iGetDevStatusEx tras inicializar. Si responde pero la inicialización falló, no está
    /// offline: queda conectada para poder reintentar o diagnosticar.
    /// </summary>
    private static void EncenderRecicladora(Reporte r)
    {
        Paso("INICIO del flujo de encendido de GRG (5.1 Power on)");
        Paso("hOpenLogicDevice + iSetCommPara: abrir el dispositivo y configurar la comunicación");
        if (!InicializarRecicladora(r))
        {
            Paso("FIN: no se pudo abrir/configurar la comunicación");
            return;
        }

        Paso("iPowerOnInitEx (rama A = iInitSettingEx + iGetCassetteInfoEx + iInitEx)");
        var inicializada = ReiniciarMecanicamenteRecicladora(r);

        Paso("¿Offline? iGetDevStatusEx para comprobar que la recicladora responde");
        var offline = LeerEstadoDispositivo() is null;
        if (!offline)
        {
            Paso(inicializada
                ? "¿Offline? NO -> la recicladora queda conectada"
                : "¿Offline? NO, pero la inicialización falló -> queda conectada para reintentar o diagnosticar");
        }
        else
        {
            Paso("¿Offline? SÍ -> iCloseComm + vCloseLogicDevice");
            CerrarRecicladora();
            r.Criterio("La recicladora responde tras la inicialización", false,
                "No responde iGetDevStatusEx: se cerró la comunicación. Revisar cable USB y alimentación.");
        }
        Paso("FIN del flujo de encendido");
    }

    private static string NombreModoValidacion(int modo) =>
        modo switch { 0 => "billete real", 15 => "billete simulado", 255 => "billete de prueba", _ => "desconocido" };

    // Escribe NOTEVALIDMODE en el CRM9250NDevCfg.ini junto al .exe antes de hOpenLogicDevice (el SDK lo lee al conectar).
    // El .ini está en GBK: se edita byte a byte con Latin1 para no tocar los comentarios en chino.
    private static void AplicarModoValidacionBilletes()
    {
        var ini = Path.Combine(AppContext.BaseDirectory, "CRM9250NDevCfg.ini");
        var modo = Crm.ModoValidacionBilletes;
        try
        {
            var texto = File.ReadAllText(ini, System.Text.Encoding.Latin1);
            var actual = System.Text.RegularExpressions.Regex.Match(texto, @"(?m)^NOTEVALIDMODE=(\d+)");
            if (!actual.Success)
            {
                Consola.Aviso($"        {ini} no tiene NOTEVALIDMODE: el SDK usará su valor por defecto.");
                return;
            }
            if (actual.Groups[1].Value != modo.ToString())
            {
                texto = texto.Remove(actual.Index, actual.Length).Insert(actual.Index, $"NOTEVALIDMODE={modo}");
                File.WriteAllText(ini, texto, System.Text.Encoding.Latin1);
            }
            Consola.Info($"        Modo de validación de billetes del SDK: {modo} ({NombreModoValidacion(modo)})" +
                         (modo != 0 ? " — SOLO PRUEBAS; en producción debe ser 0" : ""));
        }
        catch (Exception ex)
        {
            Consola.Aviso($"        No se pudo aplicar NOTEVALIDMODE={modo} en {ini}: {ex.Message}");
        }
    }

    private static tCRM9250N_DevStatusEx? LeerEstadoDispositivo()
    {
        using var s = new NativeScope();
        var pStatus = s.Salida<tDevReturn>();
        var pDev = s.Salida<tCRM9250N_DevStatusEx>();
        return Crm9250NExtra.iGetDevStatusEx(_crm, pDev, pStatus) == Crm.CodigoExito
            ? Native.Leer<tCRM9250N_DevStatusEx>(pDev) : null;
    }

    // Sondeo periódico de iGetDevStatusEx hasta que el NF/shutter quede sin billetes o se agote el tiempo.
    private static void EsperarRetiroDeBilletes(Reporte r)
    {
        Paso($"iGetDevStatusEx (sondeo cada 500 ms): esperando el retiro, máx. {Crm.SegundosRetiroBilletes} s");
        Consola.Aviso("  >> Shutter abierto: retire los billetes");
        var inicio = DateTime.UtcNow;
        var limite = inicio.AddSeconds(Crm.SegundosRetiroBilletes);
        var proximoReporte = inicio.AddSeconds(5);
        var lecturasFallidas = 0;
        while (DateTime.UtcNow < limite)
        {
            var d = LeerEstadoDispositivo();
            if (d is null) lecturasFallidas++;
            else if (d.Value.byNfHasNotes == 0 && d.Value.byShutterAreaHasNotes == 0)
            {
                Paso($"¿Se retiraron los billetes? SÍ (a los {(DateTime.UtcNow - inicio).TotalSeconds:0} s) -> cerrar shutter");
                r.Criterio("Billetes retirados del shutter antes del tiempo límite", true);
                return;
            }
            if (DateTime.UtcNow >= proximoReporte)
            {
                var restante = (limite - DateTime.UtcNow).TotalSeconds;
                Evento(d is null
                    ? $"Esperando retiro... sin lectura de estado (fallidas={lecturasFallidas}), quedan {restante:0} s"
                    : $"Esperando retiro... NF={d.Value.byNfHasNotes} shutter={d.Value.byShutterAreaHasNotes}, quedan {restante:0} s");
                proximoReporte = proximoReporte.AddSeconds(5);
            }
            Thread.Sleep(500);
        }
        Paso($"¿Se retiraron los billetes? NO, tiempo agotado ({Crm.SegundosRetiroBilletes} s) -> cerrar shutter y recoger");
        r.Criterio("Billetes retirados del shutter antes del tiempo límite", false,
            $"tiempo agotado ({Crm.SegundosRetiroBilletes} s); lecturas de estado fallidas={lecturasFallidas}. " +
            "Se cierra el shutter y se recoge lo que quede.");
    }

    // Recuperación del flujo de dispensa de GRG: hasta N veces { iInitEx; ¿volvió a la normalidad? sí -> salir;
    // no -> "Offline maintenance" y contar el intento }. Solo iInitEx: la configuración y los casetes ya se cargaron
    // en el encendido (iPowerOnInitEx) de esta sesión. "Volvió a la normalidad" = iInitEx rc=0 y la máquina responde.
    private static bool RecuperarTrasFalla(Reporte r)
    {
        for (var intento = 1; intento <= Crm.IntentosRecuperacion; intento++)
        {
            Paso($"iInitEx: reinicio de recuperación {intento} de {Crm.IntentosRecuperacion}");
            using var s = new NativeScope();
            var pStatus = s.Salida<tDevReturn>();
            var pInit = s.Salida<tCRM9250N_InitInfoEx>();
            var rcOk = CrmOk(r, "iInitEx", Crm9250NExtra.iInitEx(_crm, pInit, pStatus), pStatus);
            if (rcOk && LeerEstadoDispositivo() is not null)
            {
                Paso("¿Volvió a la normalidad? SÍ -> salir del bucle y cerrar el shutter");
                return true;
            }
            Paso($"¿Volvió a la normalidad? NO -> Offline maintenance (intento {intento} de {Crm.IntentosRecuperacion})");
            Consola.Aviso("        Offline maintenance: la recicladora no se recuperó con iInitEx; " +
                          "puede requerir intervención de un técnico (atasco, casete o sensor).");
        }
        return false;
    }

    // Cierre común del flujo: iCloseShutter -> iGetDevStatusEx -> iRetractEx (NF y/o NE) si quedaron billetes.
    private static void CerrarYRecogerRemanentes(Reporte r)
    {
        using var s = new NativeScope();
        var pStatus = s.Salida<tDevReturn>();
        Paso("iCloseShutter: cerrar el shutter");
        CrmOk(r, "iCloseShutter", CRM9250N.iCloseShutter(_crm, pStatus), pStatus);

        Paso("iGetDevStatusEx: revisar si quedaron billetes en NF/NE");
        var d = LeerEstadoDispositivo();
        if (d is null)
        {
            Consola.Aviso("        No se pudo leer el estado tras cerrar el shutter (iGetDevStatusEx): no se verifica si quedaron billetes.");
            return;
        }
        Consola.Info($"        Estado tras cerrar: billetes en NF={d.Value.byNfHasNotes} en NE={d.Value.byNeHasNotes} " +
                     $"en shutter={d.Value.byShutterAreaHasNotes}");
        var quedan = d.Value.byNfHasNotes != 0 || d.Value.byNeHasNotes != 0;
        Paso(quedan ? "¿Quedan billetes en NF/NE? SÍ -> iRetractEx" : "¿Quedan billetes en NF/NE? NO -> no se requiere iRetractEx");

        foreach (var (tipo, zona, hay) in new[] { ((byte)1, "NF", d.Value.byNfHasNotes != 0), ((byte)2, "NE", d.Value.byNeHasNotes != 0) })
        {
            if (!hay) continue;
            var ret = Native.Cero<tCRM9250N_RetractInfoEx>();
            ret.byRetractType = tipo;
            ret.byRetractPos = Crm.DestinoRetract;
            for (var i = 0; i < 4; i++) ret.abyCassettePriority[i] = (byte)(i + 1);
            var pRet = s.Entrada(ret);
            Paso($"iRetractEx: recoger billetes de {zona} (destino {Crm.DestinoRetract})");
            if (CrmOk(r, $"iRetractEx {zona}", CRM9250N.iRetractEx(_crm, pRet, pStatus), pStatus))
                Consola.Info($"        Recogidos de {zona}: {Native.Leer<tCRM9250N_RetractInfoEx>(pRet).iTotalCount} billete(s) (conteo aproximado según SDK)");
        }
    }

    private static void LlenarDenominacionesAceptadas(ushort[] destino, string? moneda = null)
    {
        using var s = new NativeScope();
        var pStatus = s.Salida<tDevReturn>();
        var pTabla = s.Salida<tCRM9250N_DenominationTable>();
        var rc = Crm9250NExtra.iGetDenominationTable(_crm, pTabla, pStatus);
        if (rc != Crm.CodigoExito)
        {
            var st = Native.Leer<tDevReturn>(pStatus);
            Consola.Aviso($"        iGetDenominationTable falló (rc={rc} logic={st.iLogicCode} {CodigosGrg.Describir(st.iLogicCode)}): " +
                          "la lista de denominaciones aceptadas queda vacía y el NV puede rechazar todos los billetes.");
            return;
        }

        var soportadas = Native.Leer<tCRM9250N_DenominationTable>(pTabla).asDenominationInfo
            .Where(d => d.wDenomCode != 0)
            .Where(d => moneda is null || !Crm.FamiliaDenominacionPorMoneda.TryGetValue(moneda, out var familia) || (d.wDenomCode >> 8) == familia)
            .Take(destino.Length).ToList();
        for (var i = 0; i < soportadas.Count; i++) destino[i] = soportadas[i].wDenomCode;

        Consola.Info($"        Denominaciones que soporta el NV: {soportadas.Count}");
        foreach (var d in soportadas)
            Consola.Info($"          ID {d.wDenomCode} (0x{d.wDenomCode:X4}): {d.wLength}x{d.wWidth} mm, grosor {d.wThickness}");
        if (soportadas.Count == 0)
            Consola.Aviso("        El NV no reporta ninguna denominación: no tiene plantillas cargadas y rechazará todo billete.");
    }

    private static void ListarCasetes(tCRM9250N_CassetteStatusEx estado)
    {
        for (var i = 0; i < estado.asRcStatus.Length; i++)
            if (estado.asRcStatus[i].byExist != 0)
                Consola.Info($"        Casete RC{i + 1}: status={estado.asRcStatus[i].byStatus} presente={estado.asRcStatus[i].byExist}");
    }

    /// <summary>Línea de bitácora con timestamp para reconstruir la secuencia de eventos físicos/SDK de la dispensa.</summary>
    private static void Evento(string texto) => Consola.Info($"        {DateTime.Now:HH:mm:ss.fff} {texto}");

    /// <summary>Paso o decisión del flujo recomendado por GRG, resaltado para seguirlo en consola durante la videollamada.</summary>
    private static void Paso(string texto) => Consola.Paso($"  [Flujo] {DateTime.Now:HH:mm:ss} {texto}");

    private static bool CrmOk(Reporte r, string funcion, int rc, IntPtr pStatus)
    {
        var st = Native.Leer<tDevReturn>(pStatus);
        var significado = CodigosGrg.Describir(st.iLogicCode);
        return r.Criterio($"{funcion} (rc={rc})", rc == Crm.CodigoExito,
            $"logic={st.iLogicCode} phy={st.iPhyCode} tipo={st.iType} msg='{Native.Texto(st.acDevReturn)}'" +
            (significado.Length > 0 ? $" | {significado}" : ""));
    }

    // PDF §4.24 iGetNvNotesInfoEx: detalle por billete del último conteo. wDenomination=0 = el NV no lo reconoció;
    // abyRejectErrCode/abyInfoErrCode son los códigos crudos del NV (sin tabla pública: se pasan tal cual a GRG).
    private static void ImprimirMotivosRechazo()
    {
        const int maxNotas = 200;
        var tamDetalle = Marshal.SizeOf<tGRGNoteDetails>();
        var tamOcr = Marshal.SizeOf<tGRGNoteOCRInfo>();
        using var s = new NativeScope();
        var pStatus = s.Salida<tDevReturn>();
        var pDetalles = s.Bloque(tamDetalle * maxNotas);
        var pOcr = s.Bloque(tamOcr * maxNotas);
        var n = maxNotas;
        var fsn = new byte[260];

        var rc = CRM9250N.iGetNvNotesInfoEx(_crm, pDetalles, pOcr, ref n, 0x03, fsn, pStatus);
        if (rc != Crm.CodigoExito)
        {
            var st = Native.Leer<tDevReturn>(pStatus);
            Consola.Aviso($"        iGetNvNotesInfoEx no dio el detalle de los billetes (rc={rc} logic={st.iLogicCode} {CodigosGrg.Describir(st.iLogicCode)}).");
            return;
        }

        Consola.Info($"        Detalle del NV por billete ({n}):");
        for (var i = 0; i < Math.Min(n, maxNotas); i++)
        {
            var d = Marshal.PtrToStructure<tGRGNoteDetails>(pDetalles + i * tamDetalle);
            var o = Marshal.PtrToStructure<tGRGNoteOCRInfo>(pOcr + i * tamOcr);
            Consola.Info($"          #{d.wNoteIndex}: denom={d.wDenomination} (0x{d.wDenomination:X4}) " +
                         $"validación={d.byValidation} (0=real) cara={d.byFaceWay} destino={d.sNoteGoDest.iType} " +
                         $"errInfo={Consola.Hex(o.abyInfoErrCode)} errRechazo={Consola.Hex(o.abyRejectErrCode)}");
        }
    }

    // Destino real del depósito: iStoreMoneyEx devuelve en sInOutUnitsInfo cuántos billetes entraron a cada casete
    // (aiRcInOutNum, 0-based, RC1..RC16) y a otras unidades (NE/NF, reciclador de banda, etc.).
    private static void ImprimirDestinoDeposito(tCRM9250N_StoreMoneyEx res)
    {
        var u = res.sInOutUnitsInfo;
        Consola.Info("        Destino del depósito (billetes por casete):");
        var total = 0;
        for (var i = 0; i < u.aiRcInOutNum.Length; i++)
        {
            if (u.aiRcInOutNum[i] == 0) continue;
            total += u.aiRcInOutNum[i];
            var cfg = Crm.Casetes.FirstOrDefault(c => c.Indice == i + 1);
            var detalle = cfg is null ? "sin entrada en hardware.json" : $"{cfg.Nombre} ({cfg.Moneda} {cfg.Denominacion})";
            Consola.Info($"        -> RC{i + 1} {detalle}: {u.aiRcInOutNum[i]} billete(s)");
        }
        for (var i = 0; i < u.iAcInOutNum.Length; i++)
            if (u.iAcInOutNum[i] != 0) Consola.Info($"        -> AC{i + 1}: {u.iAcInOutNum[i]} billete(s)");
        if (u.iLrbInOutNum != 0) Consola.Info($"        -> LRB: {u.iLrbInOutNum} billete(s)");
        if (u.iAtcInOutNum != 0) Consola.Info($"        -> ATC: {u.iAtcInOutNum} billete(s)");
        if (u.iNfInOutNum != 0) Consola.Info($"        -> NF (ranura): {u.iNfInOutNum} billete(s)");
        if (u.iNeInOutNum != 0) Consola.Info($"        -> NE (escrow): {u.iNeInOutNum} billete(s)");
        if (total == 0) Consola.Aviso("        iStoreMoneyEx no reportó billetes en ningún casete RC.");
        ImprimirDenominaciones(res.sDenomNumInfo);
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

Menú `1` (inicializar), `2` (aceptar), `3` (dispensar; pide moneda/denominación/cantidad y permite agregar más renglones a la misma orden), `10` (billete inválido, espera rechazo), `20`/`21` (abrir/cerrar shutter sueltos, para diagnosticar sin pasar por depósito ni dispensa). Equivalente directo desde `Main`:

```csharp
HardwareTests.Test_InicializarRecicladora();
HardwareTests.Test_AceptarBilletes();
HardwareTests.Test_AceptarBilletes(esperaRechazo: true);
HardwareTests.Test_DispensarBilletes(("MXN", 500, 2));                              // un solo casete
HardwareTests.Test_DispensarBilletes(("USD", 20, 1));
HardwareTests.Test_DispensarBilletes(("MXN", 500, 1), ("MXN", 200, 1), ("MXN", 100, 1)); // $800 MXN en una sola orden atómica
HardwareTests.Test_AbrirShutter();
HardwareTests.Test_CerrarShutter();
```

## Escenarios de aceptación de billetes (derivados del TAC §14)

Según el demo del fabricante, el depósito real es por **lote**: el usuario mete varios billetes juntos en la misma apertura del shutter y la máquina los lee, cuenta y enruta a sus casetes uno por uno de forma interna — el software no controla ni puede verificar a qué casete fue cada billete, solo el conteo agregado (`iCashCount`, desglosado por código de denominación). `Test_AceptarBilletes` soporta esto: se declara la lista de billetes insertados juntos (`"MXN 500, MXN 200, MXN 100"`) y se compara el total contado contra el total declarado.

| Billete(s) a insertar | Resultado esperado |
| :-- | :-- |
| MXN 500 + 200 + 100 juntos, en una sola apertura | `iTotalCount == 3`, con un código de denominación distinto por cada uno en el desglose. Es el flujo "Compra de USD" (el cliente entrega MXN). |
| USD 20 | Contado. Si el hardware lo permite, puede ir al cassette 1. |
| Otra denominación USD (1, 5, 10, 50 o 100) | Contado y dirigido al recycler (no dispensable). Anotar si lo acepta o lo rechaza: es una decisión pendiente con el proveedor. |
| Billete inválido (dañado, fotocopia, moneda no soportada) | Opción `10`: `rechazados ≥ 1`, ninguno contado, y el billete se devuelve. |

Confirmar qué casete recibió cada billete requeriría abrir el gabinete en modo servicio y contar físicamente; no es algo que este harness (ni el asistente desde el shutter) pueda verificar, porque el SDK no expone un conteo en vivo por casete (solo `byStatus`/`byExist` por casete, sin cantidad).

## Criterios de aceptación

| ID | Criterio | Cómo se verifica |
| :-- | :-- | :-- |
| R-01 | `CRM9250NDev.dll` carga sin `DllNotFound` ni `BadImageFormat` | Prueba 1 no muestra el error de carga |
| R-02 | `hOpenLogicDevice` devuelve un handle distinto de cero | Criterio automático |
| R-03 | `iSetCommPara` (menú 1) e `iPowerOnInitEx` (menú 11, o la primera vez que se usa 2 o 3) devuelven el código de éxito | Criterio automático (rc y `tDevReturn` impresos) |
| R-04 | El estado de casetes lista exactamente los casetes instalados físicamente | Comparar la salida con la máquina |
| R-05 | `iGetDevVersionEx` devuelve una versión de firmware legible | Salida de consola |
| R-06 | El shutter abre y cierra físicamente y `iCashCount` cuenta ≥ 1 billete | Observación + criterio automático |
| R-07 | La denominación reportada corresponde a cada billete insertado (lote de uno o varios) | Comparación manual del código de denominación contra lo declarado |
| R-08 | `iStoreMoneyEx` guarda el billete **o** `iCancelDepositEx` lo devuelve por el shutter | Rc de éxito + observación física |
| R-09 | `iDispenseEx` reporta exactamente el total pedido dispensado y 0 rechazados, y el contador por casete coincide con lo pedido en ese casete | Criterio automático (uno por casete en la orden) |
| R-10 | La cantidad física en el shutter coincide con el total reportado, y el asistente identifica correctamente cuántos billetes ve de cada denominación pedida | El asistente cuenta el total y luego cada denominación por separado; la consola compara contra lo reportado |
| R-11 | Tres ciclos consecutivos (aceptar + dispensar) sin reiniciar el programa ni la máquina | Repetición manual |
| R-12 | Cada billete MXN 500/200/100 y USD 20 se identifica con un código de denominación distinto y estable (mismo billete → mismo código) | Tabla `EtiquetaDenominacion` completada |
| R-13 | Un billete inválido se rechaza (`rechazados ≥ 1`, 0 contados) y el programa no se bloquea | Opción `10` |
| R-14 | Se dispensa de cada casete configurado (USD 20, MXN 500, 200 y 100), incluyendo una orden que combine 2-3 casetes a la vez (ej. $800 MXN = 500+200+100), y el contador de cada casete coincide con lo entregado | Contadores por casete en consola + conteo físico por denominación |
| R-15 | *(negativa, opcional)* Pedir billetes a un casete vacío o fuera de servicio devuelve un error controlado, sin dispensado parcial silencioso | Requiere un casete vacío. El TAC exige no marcar COMPLETED ante una dispensa parcial |
| R-16 | Secuencia de eventos de la dispensa (`iPrepareDispense` → `iDispenseEx` → `iOpenShutter` → confirmación del asistente → `iCloseShutter` → `iPrepareNextTransaction`) queda con timestamp en la bitácora, para reconstruir cuándo se abrió/cerró el shutter frente a una orden con varios casetes | Líneas `Evento(...)` con `HH:mm:ss.fff` en `bitacoras\sesion_*.txt` |

**Aprobado** = R-01 a R-14 y R-16 cumplidos (R-15 es recomendable). Cualquier `rc` distinto del código de éxito se documenta con `iLogicCode`/`iPhyCode`/mensaje para consulta con el fabricante.

## Supuestos y riesgos

- **Ya confirmado con el SDK de GRG:** nombre lógico `CRM9250NDev`, éxito `rc == 0`, `byDispType = 1` (por casete), casetes RC1..RC4 = 1..4, enlace USB y DLL x86.
- **Mapa de denominaciones por casete — corregido por observación física, no por el TAC:** el TAC §2 decía Cassette 2 = MXN 500, Cassette 3 = MXN 200, Cassette 4 = MXN 100. Al jalar la gaveta física (2026-10-15) el orden real es USD 20, MXN 100, MXN 200, MXN 500 y el recycler al final — es decir, MXN 500 y MXN 100 están invertidos respecto al TAC. `hardware.json` ya quedó con el orden observado. **Pendiente:** confirmar que "posición en la gaveta" equivale a "número RC del SDK" (es la suposición más razonable para un recycler, pero no está verificada por el SDK); la forma de confirmarlo es dispensar 1 billete de cada `Indice` y que el asistente diga a simple vista qué denominación salió — ya es parte de `Test_DispensarBilletes` (pregunta "¿Cuántos billetes de X ve?").
- **Sin confirmar físicamente (primer ciclo del Día 1):** si `iDispenseEx` ya presenta los billetes en el shutter por sí solo, o si hace falta el `iOpenShutter` explícito que ahora manda `Test_DispensarBilletes` después de dispensar. El diagrama "5.2 Dispense" del PDF del SDK es una imagen que no se pudo extraer a texto ni renderizar en este entorno (falta `pdftoppm`), así que no hay confirmación documental — solo la observación en sitio decide si el llamado explícito es necesario o redundante.
- **Sigue sin confirmarse:** qué significan los `LogicCode`/`PhyCode` que devuelva (están en `doc\CRM9250N LogicalCode.xls`), los códigos de denominación (`wCode`) de MXN/USD reales (la demo usa códigos de billetes de prueba de GRG, p. ej. `0xEE37`) y el contenido de `DevBaseDll.dll` y del driver USB.
- **Discrepancia a aclarar con el proveedor:** el BEEX describe "4 casetes de **aceptación** (2,850 notas c/u)", mientras el TAC los trata como casetes de **dispensa**. El TAC §15 lista "qué denominaciones acepta y cuáles puede reciclar/dispensar" como decisión pendiente. Si los casetes resultan ser de otro tipo (`AC` en lugar de `RC` en `CRM9250N.cs`), cambia qué estructura de estado hay que leer.
- Cualquier dispensado debe verificarse físicamente: el TAC establece que una orden de dispensa no equivale a una entrega exitosa. Por eso la prueba imprime los contadores por casete y pide la confirmación humana.
- Si el SDK exige mapear códigos de denominación a moneda/valor, esta prueba solo imprime el código crudo (`wCode`).
- No se prueba `iRetractEx` (billetes olvidados en el shutter); si el usuario no retira los billetes, hacerlo manualmente o ampliar la prueba.

## Evidencia a guardar

La bitácora `bitacoras\sesion_*.txt` de cada corrida (salida completa, respuestas del asistente y valores `tDevReturn` de cualquier falla). El asistente toma una foto de los casetes y del shutter con los billetes dispensados antes de retirarlos.
