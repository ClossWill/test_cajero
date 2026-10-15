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

            // Se pregunta antes de insertar: el asistente prepara el billete y queda registrado qué era.
            var billete = Consola.PreguntarTextoAsistente(esperaRechazo
                ? "Tenga a la mano UN billete inválido (roto, fotocopia o de otra moneda). ¿Cuál es?"
                : "Tenga a la mano UN billete. ¿Cuál es? (ej. MXN 100, USD 20)");
            if (!r.Criterio("Asistente listo antes de abrir el shutter",
                    Consola.AntesDeMover("se abrirá el shutter para recibir el billete")))
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

            Consola.Asistente($"Inserte el billete ({billete}) en el shutter y retire la mano por completo. " +
                              "Al decir \"listo\" el shutter SE CIERRA.");

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
            Consola.Info($"        Billete insertado (según el asistente): {(billete.Length > 0 ? billete : "sin dato")}");
            if (!esperaRechazo && res.iTotalCount > 0 && billete.Length > 0)
                Consola.Aviso($"        EVIDENCIA: asociar el código de arriba a \"{billete}\" en Recicladora.EtiquetaDenominacion (hardware.json)");

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
                    r.Criterio("El asistente confirma que el billete entró (shutter vacío)",
                        Consola.PreguntarAsistente("¿El shutter quedó vacío (el billete entró a la máquina)?"));
            }
            else
            {
                Consola.Asistente("El shutter se abrirá para devolver el billete. Mantenga las manos fuera y diga \"listo\".");
                var cancel = Native.Cero<tCRM9250N_CancelDepositEx>();
                var pCancel = s.Entrada(cancel);
                CrmOk(r, "iCancelDepositEx", CRM9250N.iCancelDepositEx(_crm, pCancel, pStatus), pStatus);
                // Se insertó un solo billete: debe volver ese mismo, contado o rechazado.
                var devueltos = Consola.PreguntarNumeroAsistente("Retire lo que haya en el shutter. ¿Cuántos billetes retiró?");
                r.Criterio("El asistente recuperó el billete insertado", devueltos == 1,
                    $"devueltos según el asistente={devueltos?.ToString() ?? "sin respuesta"}");
            }

            Consola.Asistente("Manos fuera del shutter: puede cerrarse. Diga \"listo\".");
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
            if (!r.Criterio("Asistente listo antes de dispensar", Consola.AntesDeMover(
                    $"se dispensarán {cantidad} billete(s) {cassette.Moneda} {cassette.Denominacion} y se abrirá el shutter")))
            { r.Finalizar(); return; }

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
            var dispensados = 0;
            if (ok)
            {
                var res = Native.Leer<tCRM9250N_DispenseInfoEx>(pDisp);
                dispensados = res.aiDispensedInfo.Sum();
                var rechazados = res.aiRejectInfo.Sum();
                r.Criterio($"La recicladora reporta {cantidad} billete(s) dispensado(s)", dispensados == cantidad,
                    $"dispensados={dispensados} rechazados={rechazados}");
                // El TAC exige conciliar contadores físicos: una orden de dispensa no equivale a entrega exitosa.
                for (var i = 0; i < res.aiDispensedInfo.Length; i++)
                    if (res.aiDispensedInfo[i] != 0 || res.aiRejectInfo[i] != 0)
                        Consola.Info($"        Contador RC{i + 1}: dispensados={res.aiDispensedInfo[i]} rechazados={res.aiRejectInfo[i]}");
            }

            // Se pregunta también si iDispenseEx falló, para detectar una dispensa parcial (R-15).
            // Se pide el número, no un sí/no sobre el esperado, para no inducir la respuesta.
            var esperados = ok ? cantidad : 0;
            var vistos = Consola.PreguntarNumeroAsistente("Sin tocarlos todavía: ¿cuántos billetes hay en el shutter?");
            r.Criterio($"El asistente cuenta {esperados} billete(s) en el shutter", vistos == esperados,
                $"asistente={vistos?.ToString() ?? "sin respuesta"} reportados por la recicladora={dispensados}");
            if (vistos > 0)
            {
                r.Criterio($"Los billetes entregados son {cassette.Moneda} {cassette.Denominacion}",
                    Consola.PreguntarAsistente($"¿Todos son de {cassette.Moneda} {cassette.Denominacion}?"));
                Consola.Asistente("Retire los billetes y la mano del shutter. Al decir \"listo\" el shutter puede cerrarse.");
            }

            CrmOk(r, "iPrepareNextTransaction", CRM9250N.iPrepareNextTransaction(_crm, pStatus), pStatus);
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
