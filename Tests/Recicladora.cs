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
