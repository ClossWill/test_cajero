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

## Modalidad remota (desarrollador a distancia + asistente en sitio)

El desarrollador opera la consola a distancia y un asistente en sitio mete y saca los billetes. Antes de empezar, el asistente lee la sección 1 ("Recicladora de billetes") de `Guia_Asistente_Sitio.md` y los dos quedan en llamada o videollamada durante toda la prueba.

| Momento | Qué pasa en consola | Qué hace el asistente |
| :-- | :-- | :-- |
| Antes de abrir el shutter o dispensar | `!! VA A MOVERSE` + "¿El asistente está listo?" | Aleja las manos y dice "listo". Si responde que no, la prueba se detiene sin mover nada |
| Aceptar: antes de insertar | Pregunta qué billete tiene a la mano | Dice cuál es (queda en la bitácora junto al código del SDK) |
| Aceptar: shutter abierto | "¿Ve el shutter abierto?" | Lo confirma (R-06 físico) |
| Aceptar: al insertar | Bloquea hasta "listo"; **al dar ENTER el shutter se cierra** | Inserta, retira la mano por completo y solo entonces dice "listo" |
| Devolución | Aviso antes de abrir; luego "¿Cuántos billetes retiró?" | Retira y dice el número |
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

{{CODE:Tests/Recicladora.cs}}

### Pre-vuelo de la DLL (`Tests/RecicladoraDiagnostico.cs`)

{{CODE:Tests/RecicladoraDiagnostico.cs}}

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
| R-03 | `iSetCommPara` (menú 1) e `iPowerOnInitEx` (menú 11, o la primera vez que se usa 2 o 3) devuelven el código de éxito | Criterio automático (rc y `tDevReturn` impresos) |
| R-04 | El estado de casetes lista exactamente los casetes instalados físicamente | Comparar la salida con la máquina |
| R-05 | `iGetDevVersionEx` devuelve una versión de firmware legible | Salida de consola |
| R-06 | El shutter abre y cierra físicamente y `iCashCount` cuenta ≥ 1 billete | Observación + criterio automático |
| R-07 | La denominación reportada corresponde al billete insertado | Comparación manual del código de denominación |
| R-08 | `iStoreMoneyEx` guarda el billete **o** `iCancelDepositEx` lo devuelve por el shutter | Rc de éxito + observación física |
| R-09 | `iDispenseEx` reporta exactamente `cantidad` billetes dispensados y 0 rechazados | Criterio automático |
| R-10 | La cantidad física en el shutter coincide con lo reportado | El asistente cuenta los billetes; la consola compara su número con el reportado |
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

La bitácora `bitacoras\sesion_*.txt` de cada corrida (salida completa, respuestas del asistente y valores `tDevReturn` de cualquier falla). El asistente toma una foto de los casetes y del shutter con los billetes dispensados antes de retirarlos.
