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

    /// <summary>
    /// Depósito según el flujo recomendado por GRG (PDF §5.3), sin preguntas (el dev ve todo por videollamada):
    /// iPrepareNextTransaction -> iPrepareDeposit -> iOpenShutter -> sondeo de iGetDevStatusEx hasta que coloquen
    /// billetes (o "Cancel" por tiempo agotado) -> iCloseShutter -> iCashCount por lotes -> iStoreMoneyEx si se contó
    /// algo ("Confirm deposit = Y"); si no, iCancelDepositEx + iOpenShutter para devolverlos.
    /// "Continue count?" se decide solo: otro lote si el conteo se cortó por LimiteDeposito (25120) y quedan billetes
    /// en la ranura; se termina si la ranura quedó vacía o solo quedaron rechazados.
    /// </summary>
    public static void Test_DepositarBilletes()
    {
        var r = new Reporte("Recicladora GRG 9250N - Depositar billetes (flujo GRG)");
        try
        {
            if (!AsegurarRecicladora(r)) { r.Finalizar(); return; }

            using var s = new NativeScope();
            var pStatus = s.Salida<tDevReturn>();

            Paso("INICIO del flujo de depósito de GRG");
            Paso($"Modo de validación de billetes: {Crm.ModoValidacionBilletes} ({NombreModoValidacion(Crm.ModoValidacionBilletes)}), " +
                 $"límite por conteo: {Crm.LimiteDeposito} billete(s)");
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
                CrmOk(r, $"iStoreMoneyEx: depósito de {totalContado} billete(s)", CRM9250N.iStoreMoneyEx(_crm, pStore, pStatus), pStatus);
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
    /// denominación configurada en hardware.json. El SDK no expone un conteo de billetes en vivo por casete (solo
    /// presencia/estado); para el inventario real hay que conciliar contra los contadores de depósito/dispensa
    /// (ver Test_AceptarBilletes/Test_DispensarBilletes) y el conteo físico del asistente.
    /// </summary>
    public static void Test_ConsultarInventario()
    {
        var r = new Reporte("Recicladora GRG 9250N - Consultar inventario (solo lectura)");
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
            foreach (var cassette in Crm.Casetes.OrderBy(c => c.Indice))
            {
                var idx = cassette.Indice - 1;
                if (idx < 0 || idx >= estado.asRcStatus.Length)
                {
                    Consola.Aviso($"        {cassette.Nombre} ({cassette.Moneda} {cassette.Denominacion}): índice {cassette.Indice} fuera de rango SDK");
                    continue;
                }
                var bin = estado.asRcStatus[idx];
                Consola.Info($"        RC{cassette.Indice} {cassette.Nombre} ({cassette.Moneda} {cassette.Denominacion}): " +
                             $"presente={bin.byExist} status={bin.byStatus}");
            }
            Consola.Aviso("        El SDK no reporta cantidad de billetes por casete en esta llamada; " +
                          "confirmar conteo físico visualmente si se requiere inventario exacto.");
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

    /// <summary>
    /// iPowerOnInitEx: reinicio mecánico. Pasa el transporte a su posición inicial y manda al NF los billetes que haya en el canal
    /// (PDF §4.6). El dev ve el movimiento directo por videollamada: no se pide confirmación, solo se avisa.
    /// </summary>
    private static bool ReiniciarMecanicamenteRecicladora(Reporte r)
    {
        Consola.Aviso("  >> El dispositivo recibió la orden: reinicio mecánico (ruido de motores; el shutter y el transporte se mueven)");

        using var s = new NativeScope();
        var pStatus = s.Salida<tDevReturn>();
        var cfg = Native.Cero<tCRM9250N_InitSettingEx>();
        cfg.byAtcMode = Crm.ModoAtc;
        cfg.byWorkingMode = Crm.ModoTrabajo;
        cfg.byDcAccepetAllDenom = Crm.AceptarTodasDenominaciones;
        cfg.byInitCfg = Crm.ConfigInicio;
        // PDF §3.2: awAcceptableDenom "empty: all not accepted". Con la lista en ceros el NV rechaza todo
        // (visto el 2026-10-21: 18 rechazados, logic 25119). Se llena con lo que el propio NV dice soportar.
        LlenarDenominacionesAceptadas(cfg.awAcceptableDenom);
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

    // "iInitEx hasta N veces": iPowerOnInitEx ya incluye iInitEx (PDF §4.6) y además rearma la lista de denominaciones.
    private static bool RecuperarTrasFalla(Reporte r)
    {
        for (var intento = 1; intento <= Crm.IntentosRecuperacion; intento++)
        {
            Paso($"iInitEx (vía iPowerOnInitEx): reinicio de recuperación {intento} de {Crm.IntentosRecuperacion}");
            if (ReiniciarMecanicamenteRecicladora(r))
            {
                Paso("¿Volvió a la normalidad? SÍ -> cerrar shutter");
                return true;
            }
            Paso(intento < Crm.IntentosRecuperacion
                ? "¿Volvió a la normalidad? NO -> se reintenta"
                : "¿Volvió a la normalidad? NO -> sin más reintentos");
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

    private static void LlenarDenominacionesAceptadas(ushort[] destino)
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
            .Where(d => d.wDenomCode != 0).Take(destino.Length).ToList();
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
