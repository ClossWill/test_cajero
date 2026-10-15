# Contexto de traslado a la máquina Win 11 IoT

> Resumen para retomar el trabajo en la máquina ATM (Windows 11 IoT Enterprise) sin perder lo aprendido en la laptop de desarrollo.
> Estado al **2026-10-09**. Léelo junto con `CLAUDE.md` (directriz: esto es un *test harness*, no una app ni un middleware).

## 1. Situación

- El proyecto es una consola C# .NET 8 (`FinsoHardwareTests`) con un menú; cada opción llama a un `HardwareTests.Test_*`.
- **Foco actual por indicación del usuario: validar primero la comunicación con la recicladora GRG 9250N.** Los demás periféricos (monederos MK4, impresora KP-80A, sensores ALB02, escáner MEPR500+) están codificados pero **no se validan hasta cerrar la recicladora**.
- Todo se desarrolló en una **laptop sin hardware**. Nada se ha probado contra un periférico real. Lo único ejecutado es el pre-vuelo de la DLL y el arranque del menú.
- Siguiente paso en la IoT: ejecutar la opción `1` del menú y analizar la salida (sección 6).

### Actualización 2026-10-13 (ya en la IoT)

- **Inicialización de la recicladora: APROBADO (11/11).** Firmware `v6.07B174`, RC1..RC4 presentes (`status=4`), shutter cerrado, sin billetes en NF/NE. Con la DLL del repo (v2.3.7.6).
- Entorno de la IoT:
  - `DevBaseDll.dll` ya estaba en `C:\Windows\SysWOW64` (v3.3.3.96). No hay que copiarla.
  - Driver GRG del fabricante en `C:\Program Files (x86)\GrgBanking\GRGDevDriver` (`CRM9250NDev.dll` v2.3.2.4). Sirve de control si la del repo falla.
  - USB: `PID_8016` (CRM9250N) y `PID_8017` (validador SNV-003). COM13/COM14 = puente CP210x doble (¿monederos/ALB02?, por verificar).
  - Sin administrador ni `winget`. .NET 8 se instaló por usuario con `dotnet-install.ps1`: SDK 8.0.426 x64 en `%LOCALAPPDATA%\Microsoft\dotnet`, runtime x86 8.0.32 en `%LOCALAPPDATA%\Microsoft\dotnet-x86`. Se fijó la variable de usuario `DOTNET_ROOT(x86)` a esta última carpeta.
  - El `dotnet` del PATH es el de la máquina (solo runtime 10, sin SDK). Para compilar hay que usar la ruta completa: `& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" build`.
  - Desde bash: `printf '1\n0\n' | env "DOTNET_ROOT(x86)=$LOCALAPPDATA\\Microsoft\\dotnet-x86" ./FinsoHardwareTests.exe` (`export` no admite paréntesis).

## 2. Qué hay en la carpeta

```
IntegracionCajeroPruebas.csproj   net8.0, PlatformTarget=x86, RollForward=Major, copia el SDK de GRG a la salida
Program.cs                        menú; fija el directorio de trabajo al del .exe
hardware.json                     configuración editable en la máquina (con comentarios)
CRM9250N.cs                       wrapper P/Invoke de GRG. NO modificar (idéntico al de la demo del SDK)
Config/HardwareConfig.cs          carga de hardware.json
Helpers/                          Consola, Reporte (criterios + APROBADO/RECHAZADO), Native (marshaling)
Protocolos/                       CcTalk, EscPos, SerialTramas, Crm9250NExtra (funciones del SDK que el wrapper no declara)
Tests/Recicladora*.cs             FOCO: Inicializar, Aceptar, Dispensar + pre-vuelo de la DLL
Tests/Monederos|Impresora|Sensores|Escaner.cs   fases posteriores
Docs/                             Plan_de_Pruebas.md y Prueba_Dia1..5_*.md (generados desde Docs/_plantillas)
Docs/Referencia/                  texto extraído de los PDF (API de GRG, guía BEEX, TAC)
```

Menú: `1` Inicializar · `2` Aceptar billetes · `3` Dispensar · `10` Billete inválido (espera rechazo) · `4`-`9` otros periféricos · `0` salir.

## 3. Hechos confirmados sobre la recicladora

Fuente: SDK `CRM9250NDev_V2.3.7.6` (demo WinForms, `doc\CRM9250NDev API Description.pdf`, `GRGDTATM_CommCfg.ini`).

| Hecho | Valor |
| :-- | :-- |
| Nombre lógico | **`CRM9250NDev`** (nodo del `GRGDTATM_CommCfg.ini`; la demo lo usa igual) |
| Éxito | `rc == 0` |
| Enlace | **USB**, `CommType=3`, `VID:04B4, PID:8016`. No usa COM |
| Arquitectura de `CRM9250NDev.dll` | **x86 (32 bits)**. El proceso debe ser x86 |
| `byDispType` | `1` = dispensar por casete RC |
| Índice de casete | **Base 1**: `wDenomOrCassetteInfo = 2` es RC2 (RC1..RC4 = 1..4) |
| Mapa de casetes (TAC §2) | RC1 = USD 20, RC2 = MXN 500, RC3 = MXN 200, RC4 = MXN 100 |
| Parámetros de la demo ya aplicados | `byAtcMode=1`, `byDcAccepetAllDenom=1`, `byRejectToAc=1`, `byRejectPos=1`, `byNvMode=0`, prioridad de casetes 1..4, buffers con holgura 8x |
| Funciones extra usadas | `iGetDevStatusEx` (estado en vivo, sin movimiento), `iCloseComm`, `vCloseLogicDevice` |

Secuencias: Inicializar = `hOpenLogicDevice` → `iSetCommPara` → `iPowerOnInitEx` → `iGetDevVersionEx` → `iGetDevStatusEx`. Aceptar = `iPrepareDeposit` → `iOpenShutter` → `iCloseShutter` → `iCashCount` → `iStoreMoneyEx` o `iCancelDepositEx` → `iPrepareNextTransaction`. Dispensar = `iPrepareDispense` → `iDispenseEx` → `iPrepareNextTransaction`.

## 4. Bloqueos conocidos (lo primero a resolver en la IoT)

1. **Falta `DevBaseDll.dll`.** `CRM9250NDev.dll` (y `OcrManager.dll`) la importan y **no está en el zip del SDK**. El pre-vuelo la nombra. Hay que obtenerla del instalador/driver de GRG; en la IoT puede que ya esté instalada con el software actual de la máquina (buscar `DevBaseDll.dll` en `C:\Windows\SysWOW64`, `C:\Program Files*`, carpetas de la app oficial).
2. **Driver USB de GRG** (VID_04B4&PID_8016). En la laptop Windows no lo enumera, como era de esperar. En la IoT debe aparecer en el Administrador de dispositivos.
3. **Runtime x86 de .NET 8**: la laptop solo tiene el x86 10.0.3. Por eso el `.csproj` lleva `RollForward=Major`. En la IoT: instalar el runtime x86 de .NET 8 o publicar autocontenido (sección 5).
4. **Conflicto de acceso:** si la aplicación oficial u otra utilería del fabricante tiene abierta la recicladora (USB), el SDK no podrá abrirla. Cerrarla antes de probar.

## 5. Cómo compilar y ejecutar en la IoT

El SDK de GRG vive dentro del repo (`referencias_integracion\CRM9250NDev_V2.3.7.6\`) y es la ruta por defecto del `.csproj`; esa carpeta se excluye de la compilación (trae su propio `CRM9250N.cs`, `Program.cs`, etc.). Para usar otro SDK, indicar la carpeta:

```powershell
dotnet build -p:GrgSdkDir="C:\ruta\al\sdk\dll\"
```

Si la IoT no tiene SDK de .NET, compilar en la laptop y copiar la carpeta publicada:

```powershell
dotnet publish -c Release -r win-x86 --self-contained -o publish
```

Después copiar `DevBaseDll.dll` junto a `FinsoHardwareTests.exe` y ejecutar **desde esa carpeta**. `hardware.json` se edita ahí mismo, sin recompilar.

## 6. Qué hacer y qué observar en la IoT (primera sesión)

1. Cerrar cualquier software del fabricante / app oficial que use la recicladora.
2. Ejecutar `FinsoHardwareTests.exe` → opción `1`.
3. Leer la salida en este orden. El pre-vuelo se detiene en el primer fallo y dice la causa:

| Paso | Si falla |
| :-- | :-- |
| DLL presente, arquitectura x86, `.ini` con nodo `[CRM9250NDev]` | Copiar la carpeta `dll` completa del SDK |
| USB `VID_04B4&PID_8016` enumerado | Cable, alimentación de la recicladora o driver |
| Importaciones de la DLL disponibles | Copiar lo que nombre (p. ej. `DevBaseDll.dll`) |
| `hOpenLogicDevice` ≠ 0 | Nombre lógico / `.ini` no encontrado |
| `iSetCommPara`, `iPowerOnInitEx` = 0 | Mirar `logic`/`phy`/`msg`; códigos en `doc\CRM9250N LogicalCode.xls` del SDK |
| `iGetDevVersionEx` y `iGetDevStatusEx` | Firmware legible y estado coherente = **enlace validado** |

4. **Meta de esta fase:** `RESULTADO Recicladora GRG 9250N - Inicialización: APROBADO`, con firmware legible y los casetes presentes iguales a los instalados.
5. Solo entonces: opción `2` (aceptar **1** billete), opción `10` (billete inválido) y opción `3` con **1** billete por casete.
6. Pegar la salida completa de cada corrida (incluidos `logic`/`phy`/`msg`) para decidir el siguiente ajuste.

Criterios de aceptación completos (R-01..R-15): `Docs/Prueba_Dia1_Recicladora.md`.

## 7. Lo que aún se desconoce

- Significado de los `PhyCode`: no hay tabla en el SDK. Los `LogicCode` ya se traducen: `Config/CodigosLogicosGrg.tsv` (1229 códigos, extraídos de `doc\CRM9250N LogicalCode.xls`, en inglés) y `Helpers/CodigosGrg.cs` añaden descripción y acción recomendada a cada línea `logic=... phy=...`.
- `DevBaseDll.dll` **sigue sin estar** en `referencias_integracion` (ni en el zip); hay que traerla de la IoT o del instalador de GRG.
- **Códigos de denominación (`wCode`) de MXN y USD reales.** La demo usa códigos de billetes de prueba de GRG (p. ej. `0xEE37`). `Test_AceptarBilletes` imprime el código y pregunta qué billete era, para llenar `Recicladora.EtiquetaDenominacion` en `hardware.json`.
- Discrepancia BEEX/TAC: el BEEX llama a los 4 casetes "de aceptación"; el TAC los usa como casetes de dispensa. Aclarar con el proveedor (TAC §15). Si fueran de otro tipo cambiaría la estructura de estado a leer.
- Por qué la demo reserva 8x el tamaño de cada struct: se copió esa holgura (`NativeScope.Holgura`) sin entender el motivo exacto.

### Nota: demo WinForms del SDK vs. este proyecto (solo recicladora)

- **Este proyecto basta** para validar la conexión con la recicladora. Llama a la misma `CRM9250NDev.dll` con el mismo `CRM9250N.cs` (idéntico al de la demo) y las mismas secuencias y parámetros. La versión de .NET no influye: la DLL es nativa x86 y solo exige un proceso x86 (aquí .NET 8; la demo es .NET Framework 4.6).
- **La demo solo sirve como control** si este proyecto falla: si la demo conecta y esta no, el fallo está en nuestro código; si ninguna conecta, es el entorno (`DevBaseDll.dll`, driver USB, cable, alimentación, otra app con la recicladora abierta). No trae ejecutable: hay que compilarla (`bin\Debug` y `bin\Release` vacíos). Con la demo los errores salen como códigos crudos (sin descripción), sin pre-vuelo y sin veredicto.
- **Lo que la demo hace y este proyecto no:** `RetractNE`/`RetractNF` (recoger billetes), `iGetNvNotesInfoEx` e `iFirmwareDownload`. Están declaradas en `CRM9250N.cs`, pero ningún menú las llama. No son necesarias para validar la conexión; añadir si hacen falta.
- Para los demás periféricos la demo no aplica (es solo de la recicladora).

## 8. Fases posteriores (no tocar hasta cerrar la recicladora)

| Periférico | Estado | Falta |
| :-- | :-- | :-- |
| Monederos MK4 (ccTalk) | Implementación mínima propia en `Protocolos/CcTalk.cs` | Tablas de Excel de ccTalk de la carpeta de integración (formato de Dispense, direcciones); el BEEX recomienda una librería ccTalk de código abierto |
| Impresora KP-80A | `Test_ImprimirVoucherPrueba` por spooler o COM | Nombre de la cola en la IoT |
| Sensores ALB02 | `Test_CalibrarSensores` para descubrir tramas | Documentación del ALB02 y puerto COM |
| Escáner MEPR500+ | `IEscanerSdk` + `EscanerSimulado`; `EscanerMeprSdk` lanza `NotImplementedException` | SDK y manual del MEPR500+ |

## 9. Notas de trabajo

- **No inventar** bytes ni funciones de SDK: lo no confirmado queda marcado `VERIFICAR` en código o `hardware.json`.
- Del TAC se dejó fuera cotización, compliance, ledger y máquina de estados: es lógica de negocio y `CLAUDE.md` la excluye.
- Una orden de dispensa no equivale a entrega física (TAC): las pruebas imprimen contadores por casete y piden confirmación humana.
- Para probar el menú sin teclado desde PowerShell, el BOM de la tubería rompe la primera opción; usar `printf '1\n0\n' | ./FinsoHardwareTests.exe` desde bash.
- `Docs/*.md` se generan desde las plantillas de `Docs/_plantillas/` (marcadores `{{CODE:ruta}}`). Si cambia el código, editar la plantilla y regenerar con `powershell -NoProfile -File Docs\_plantillas\regenerar.ps1`. No editar los `.md` generados a mano: se pisan.
- Ingresar billetes reales o dispensar: empezar siempre con 1 unidad; `MaxBilletesPrueba` (5) y `MaxMonedasPrueba` (10) limitan por seguridad.
