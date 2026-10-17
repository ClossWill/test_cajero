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
            if (InicializarRecicladora(r) && reinicioMecanico) ReiniciarMecanicamenteRecicladora(r);
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

            // Se pregunta antes de insertar: el asistente prepara los billetes y queda registrado cuáles eran.
            // Según el demo del fabricante, el depósito real es por lote: se meten varios billetes juntos en
            // la misma apertura y la máquina los lee, cuenta y enruta a sus casetes uno por uno internamente.
            var billeteTexto = Consola.PreguntarTextoAsistente(esperaRechazo
                ? "Tenga a la mano UN billete inválido (roto, fotocopia o de otra moneda). ¿Cuál es?"
                : "Tenga a la mano los billetes que va a depositar juntos (uno o varios, pueden ser de distinta denominación). ¿Cuáles son? (separe por coma, ej. MXN 500, MXN 200, MXN 100)");
            var billetes = billeteTexto.Split(',').Select(b => b.Trim()).Where(b => b.Length > 0).ToList();
            var esperados = esperaRechazo ? 1 : Math.Max(billetes.Count, 1);
            if (!r.Criterio("Asistente listo antes de abrir el shutter",
                    Consola.AntesDeMover("se abrirá el shutter para recibir el/los billete(s)")))
            { r.Finalizar(); return; }

            using var s = new NativeScope();
            var pStatus = s.Salida<tDevReturn>();

            var listo = CrmOk(r, "iPrepareDeposit", CRM9250N.iPrepareDeposit(_crm, pStatus), pStatus)
                     && CrmOk(r, "iOpenShutter", CRM9250N.iOpenShutter(_crm, pStatus), pStatus);
            if (!listo) { r.Finalizar(); return; }

            // R-06: rc=0 no prueba que el shutter se haya movido.
            if (!r.Criterio("El asistente ve el shutter abierto", Consola.PreguntarAsistente("¿Ve el shutter abierto?")))
            {
                CrmOk(r, "iCloseShutter", CRM9250N.iCloseShutter(_crm, pStatus), pStatus);
                CrmOk(r, "iPrepareNextTransaction", CRM9250N.iPrepareNextTransaction(_crm, pStatus), pStatus);
                r.Finalizar(); return;
            }

            Consola.Asistente($"Inserte junto(s), en la misma apertura, el/los billete(s) ({string.Join(" + ", billetes)}) en el shutter " +
                              "y retire la mano por completo. Al decir \"listo\" el shutter SE CIERRA.");

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
                // La máquina enruta cada billete a su casete internamente; aquí solo se puede verificar
                // que contó tantos billetes como se insertaron (no a cuál casete fue cada uno).
                r.Criterio($"Se contaron los {esperados} billete(s) esperados (según lo declarado por el asistente)",
                    res.iTotalCount == esperados, detalle);
            ImprimirDenominaciones(res.sDenomNumInfo);
            Consola.Info($"        Billete(s) insertado(s) (según el asistente): {(billetes.Count > 0 ? string.Join(" + ", billetes) : "sin dato")}");
            if (!esperaRechazo && res.iTotalCount > 0 && billetes.Count > 0)
                Consola.Aviso("        EVIDENCIA: comparar el desglose por código de arriba contra los billetes declarados y " +
                              "asociar cada código a su denominación en Recicladora.EtiquetaDenominacion (hardware.json)");

            // Con billetes contados: guardarlos (confirma el depósito) o devolverlos al usuario.
            if (res.iTotalCount > 0 && Consola.Confirmar("¿Guardar los billetes en la recicladora (iStoreMoneyEx)? n = devolver"))
            {
                var store = Native.Cero<tCRM9250N_StoreMoneyEx>();
                store.byRejectPos = Crm.DestinoRechazoDeposito;
                store.byNvMode = 0;
                // Prioridad 1..4 para RC1..RC4 (0x01-0x0F, menor = más prioridad), como en la demo de GRG.
                for (var i = 0; i < 4; i++) store.abyCassettePriority[i] = (byte)(i + 1);
                var pStore = s.Entrada(store);
                if (CrmOk(r, "iStoreMoneyEx", CRM9250N.iStoreMoneyEx(_crm, pStore, pStatus), pStatus))
                    r.Criterio("El asistente confirma que el/los billete(s) entraron (shutter vacío)",
                        Consola.PreguntarAsistente("¿El shutter quedó vacío (el/los billete(s) entraron a la máquina)?"));
            }
            else
            {
                Consola.Asistente("El shutter se abrirá para devolver el/los billete(s). Mantenga las manos fuera y diga \"listo\".");
                var cancel = Native.Cero<tCRM9250N_CancelDepositEx>();
                var pCancel = s.Entrada(cancel);
                CrmOk(r, "iCancelDepositEx", CRM9250N.iCancelDepositEx(_crm, pCancel, pStatus), pStatus);
                // Se insertaron "esperados" billetes: deben volver todos, contados o rechazados.
                var devueltos = Consola.PreguntarNumeroAsistente("Retire lo que haya en el shutter. ¿Cuántos billetes retiró?");
                r.Criterio($"El asistente recuperó los {esperados} billete(s) insertados", devueltos == esperados,
                    $"devueltos según el asistente={devueltos?.ToString() ?? "sin respuesta"}");
            }

            Consola.Asistente("Manos fuera del shutter: puede cerrarse. Diga \"listo\".");
            CrmOk(r, "iPrepareNextTransaction", CRM9250N.iPrepareNextTransaction(_crm, pStatus), pStatus);
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
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
            if (!r.Criterio("Asistente listo antes de dispensar", Consola.AntesDeMover(
                    $"se dispensarán {resumen} y se abrirá el shutter")))
            { r.Finalizar(); return; }

            using var s = new NativeScope();
            var pStatus = s.Salida<tDevReturn>();

            Evento("iPrepareDispense: solicitando");
            if (!CrmOk(r, "iPrepareDispense", CRM9250N.iPrepareDispense(_crm, pStatus), pStatus))
            { r.Finalizar(); return; }

            var disp = Native.Cero<tCRM9250N_DispenseInfoEx>();
            disp.byDispType = Crm.TipoDispensa;
            disp.byRejectToAc = Crm.DestinoRechazoDispensa;
            for (var i = 0; i < renglones.Count; i++)
                disp.asReqDispInfo[i] = new tCRM9250N_ReqDispInfo
                { wDenomOrCassetteInfo = (ushort)renglones[i].Cassette.Indice, iNum = renglones[i].Cantidad };
            var pDisp = s.Entrada(disp);

            Evento($"iDispenseEx: solicitando {resumen} (orden única, {renglones.Count} casete(s))");
            var ok = CrmOk(r, "iDispenseEx", CRM9250N.iDispenseEx(_crm, pDisp, pStatus), pStatus);

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
            }

            // No se asume que iDispenseEx abra el shutter por sí solo: se ordena explícitamente (VERIFICAR en el
            // primer ciclo físico si ya estaba abierto o si esta llamada es la que lo mueve).
            Evento("iOpenShutter: solicitando (para que el cliente retire todo junto)");
            CrmOk(r, "iOpenShutter", CRM9250N.iOpenShutter(_crm, pStatus), pStatus);

            // Se pregunta también si iDispenseEx falló, para detectar una dispensa parcial (R-15).
            // Se pide el número, no un sí/no sobre el esperado, para no inducir la respuesta.
            var esperados = ok ? totalPedido : 0;
            var vistos = Consola.PreguntarNumeroAsistente("Sin tocarlos todavía: ¿cuántos billetes hay en el shutter en total?");
            Evento($"Asistente reporta {vistos?.ToString() ?? "sin respuesta"} billete(s) en el shutter (esperados={esperados})");
            r.Criterio($"El asistente cuenta {esperados} billete(s) en el shutter", vistos == esperados,
                $"asistente={vistos?.ToString() ?? "sin respuesta"} total reportado por la recicladora={dispensadoTotal}");

            if (vistos > 0)
            {
                foreach (var (cassette, cantidad) in renglones)
                {
                    var vistosDenom = Consola.PreguntarNumeroAsistente($"¿Cuántos billetes de {cassette.Moneda} {cassette.Denominacion} ve?");
                    r.Criterio($"El asistente ve {cantidad} billete(s) de {cassette.Moneda} {cassette.Denominacion}",
                        vistosDenom == cantidad, $"asistente={vistosDenom?.ToString() ?? "sin respuesta"}");
                }
                Consola.Asistente("Retire todos los billetes y la mano del shutter. Al decir \"listo\" el shutter puede cerrarse.");
            }

            Evento("iCloseShutter: solicitando");
            CrmOk(r, "iCloseShutter", CRM9250N.iCloseShutter(_crm, pStatus), pStatus);
            Evento("iPrepareNextTransaction: solicitando");
            CrmOk(r, "iPrepareNextTransaction", CRM9250N.iPrepareNextTransaction(_crm, pStatus), pStatus);
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    /// <summary>Control manual del shutter, independiente del flujo de aceptar/dispensar. Sirve para diagnosticar
    /// si ya está abierto/cerrado antes de asumir que otro comando (ej. iDispenseEx) lo mueve por su cuenta.</summary>
    public static void Test_AbrirShutter()
    {
        var r = new Reporte("Recicladora GRG 9250N - Abrir shutter (control manual)");
        try
        {
            if (!AsegurarRecicladora(r)) { r.Finalizar(); return; }
            if (!r.Criterio("Asistente listo antes de abrir el shutter", Consola.AntesDeMover("se abrirá el shutter")))
            { r.Finalizar(); return; }

            using var s = new NativeScope();
            var pStatus = s.Salida<tDevReturn>();
            Evento("iOpenShutter: solicitando (manual)");
            CrmOk(r, "iOpenShutter", CRM9250N.iOpenShutter(_crm, pStatus), pStatus);
            r.Criterio("El asistente ve el shutter abierto", Consola.PreguntarAsistente("¿Ve el shutter abierto?"));
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    /// <summary>Contraparte de <see cref="Test_AbrirShutter"/>: cierra el shutter sin pasar por depósito ni dispensa.</summary>
    public static void Test_CerrarShutter()
    {
        var r = new Reporte("Recicladora GRG 9250N - Cerrar shutter (control manual)");
        try
        {
            if (!AsegurarRecicladora(r)) { r.Finalizar(); return; }
            if (!r.Criterio("Asistente listo antes de cerrar el shutter",
                    Consola.AntesDeMover("se cerrará el shutter: manos y billetes fuera")))
            { r.Finalizar(); return; }

            using var s = new NativeScope();
            var pStatus = s.Salida<tDevReturn>();
            Evento("iCloseShutter: solicitando (manual)");
            CrmOk(r, "iCloseShutter", CRM9250N.iCloseShutter(_crm, pStatus), pStatus);
            r.Criterio("El asistente ve el shutter cerrado", Consola.PreguntarAsistente("¿Ve el shutter cerrado?"));
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    // ------------------------------------------------------------------ helpers

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
            Consola.Info($"        Casetes extraídos: RC1={d.byRC1PlugRemove} RC2={d.byRC2PlugRemove} RC3={d.byRC3PlugRemove} RC4={d.byRC4PlugRemove}");
        }
        return true;
    }

    /// <summary>
    /// iPowerOnInitEx: reinicio mecánico. Pasa el transporte a su posición inicial y manda al NF los billetes que haya en el canal
    /// (PDF §4.6). Pide confirmación al asistente antes de ejecutarlo.
    /// </summary>
    private static bool ReiniciarMecanicamenteRecicladora(Reporte r)
    {
        if (!r.Criterio("Asistente listo antes del reinicio mecánico",
                Consola.AntesDeMover("la recicladora se reinicia (ruido de motores; el shutter y el transporte se mueven)")))
            return false;

        using var s = new NativeScope();
        var pStatus = s.Salida<tDevReturn>();
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
        _reinicioMecanicoHecho = true;
        ListarCasetes(Native.Leer<tCRM9250N_CassetteStatusEx>(pEstado));
        return true;
    }

    private static void ListarCasetes(tCRM9250N_CassetteStatusEx estado)
    {
        for (var i = 0; i < estado.asRcStatus.Length; i++)
            if (estado.asRcStatus[i].byExist != 0)
                Consola.Info($"        Casete RC{i + 1}: status={estado.asRcStatus[i].byStatus} presente={estado.asRcStatus[i].byExist}");
    }

    /// <summary>Línea de bitácora con timestamp para reconstruir la secuencia de eventos físicos/SDK de la dispensa.</summary>
    private static void Evento(string texto) => Consola.Info($"        {DateTime.Now:HH:mm:ss.fff} {texto}");

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
