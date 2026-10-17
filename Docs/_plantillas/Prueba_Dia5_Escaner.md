# Prueba Día 5 – Escáner de pasaporte / INE MEPR500+

## Objetivo

Inicializar el SDK del escáner, detectar un documento colocado sobre la ventana, disparar la lectura y mostrar por consola **Nombre, ID, Nacionalidad, fecha de nacimiento y fecha de vencimiento** (las dos últimas, relevantes para una operación de cambio de divisas: verificar que el documento no esté caducado y registrar el dato para el expediente de identificación). Más importante que agregar campos: **garantizar que lo leído corresponde de verdad al documento**, no solo que los campos llegaron no vacíos — para eso se usa el propio dígito verificador de la MRZ que calcula el SDK (`bCheckSum`), como criterio automático independiente de la comparación visual del asistente.

## Protocolo / librería

- **SDK propietario del fabricante** (`TWRPassportAPI.dll`, OCR en segundo plano y lectura RFID/infrarrojo, ICAO 9303), recibido en `referencias_integracion/Passport scanner(MEPR500+)/MEPR500 Windows SDK 20230815` (demo WinForms en C# y en VC++, header `TWRPassportAPI.h`, manuales en `Doc/`). El código separa la prueba del SDK mediante la interfaz `IEscanerSdk`:
  - `EscanerMeprSdk`: adaptador real sobre `Protocolos/TwrPassportSdk.cs`, ya implementado (ver más abajo). Usa el **modo automático por callback** (`TW_SetAutoModel(true)` + `TW_Capture_RegisterCallbackResultImageEX`, la variante "EX" que entrega `PassportInfo` con cadenas ya parseadas, en vez de punteros crudos a buffers de imagen). **Corrección (2026-10-16, tras revisar `Example/c#/IDRead/Form1.cs` a fondo):** esta ruta EX solo se activa en el demo si se descomenta `#define API_EX` (línea 1); tal como lo distribuye el fabricante, ese define está apagado y el demo corre con la variante no-EX. Es decir, EX es código real y completo del fabricante — y la más práctica de marshalizar en .NET — pero no es la ruta que ellos mismos ejercitan por defecto. El header también declara un camino síncrono por sondeo (`TW_MonitoringDocEX` + `TW_Capture_Manual_GetInfo`) que **no se implementó**: ningún demo lo usa bajo ningún define, así que no hay ni un ejemplo funcionando de su firma real.
  - `EscanerSimulado`: devuelve datos ficticios para ejercitar el flujo. Con `Escaner.Simulado = true` la prueba **no puede aprobarse**: el criterio de coincidencia con el documento físico queda en falla a propósito.

## Precondiciones

- `TWRPassportAPI.dll`, el resto de DLLs, y la carpeta `tessdata/` de `BinTW-20230815 DEMO/` junto al `.exe` (el `.csproj` los copia solo si existe `ScannerSdkDir`; genera una advertencia de build si no). **`tessdata/` es obligatoria, no opcional:** confirmado en la máquina real (2026-10-16) que sin ella `TWRPassportAPI.dll` ni siquiera carga — usa Tesseract/OCR internamente y falla en su rutina de inicialización ("DLL initialization routine failed", Win32 `0x8007045A`) antes de que el ping pueda hacer nada. El `.csproj` original la excluía a propósito asumiendo que no hacía falta; quedó corregido.
- **Confirmado:** la DLL es de 32 bits (PE32, Intel i386) — compatible con el `PlatformTarget=x86` que ya exige la DLL de GRG, sin cambios.
- SDK instalado (drivers USB) y escáner reconocido por Windows. `Drivers/` trae `DRPassport Drivers_SCM(v2.4).exe` y un instalador de `com0com` (puerto serial virtual): sugiere que la lectura del chip RFID del documento pasa por un COM virtual, no solo USB — **sin confirmar en la máquina real**.
- Documentos de prueba: un pasaporte (con MRZ) y una INE.
- La lectura del chip RFID (ICAO 9303, `docInfo.bRFID`/`TW_Capture_GetDGData`) **no quedó implementada**: `TwrPassportSdk` solo expone el callback EX con los campos OCR/MRZ de `PassportInfo`, suficiente para el criterio E-03/E-04 (Nombre/ID/Nacionalidad) pero no para los datos del chip.

## Configuración

```json
"Escaner": {
  "Simulado": true,
  "TimeoutSegundos": 30
}
```

`Simulado` sigue en `true` por defecto: el adaptador real ya está escrito pero no se ha probado contra el lector físico. Cambiarlo a `false` el día de la prueba con el asistente.

## Código

### `Protocolos/TwrPassportSdk.cs`

Wrapper propio sobre `TWRPassportAPI.dll`, con los `[DllImport]`, estructuras (`PassportInfo`, `DocDataEX`) y el delegado de callback copiados del header y del wrapper de ejemplo del fabricante (única referencia con el marshaling ya probado).

{{CODE:Protocolos/TwrPassportSdk.cs}}

### `Tests/Escaner.cs`

{{CODE:Tests/Escaner.cs}}

## Cómo ejecutar

Menú `9` (lectura) o `17` (ping: inicializa el SDK y confirma que detecta el dispositivo, sin esperar un documento). Equivalente desde `Main`:

```csharp
HardwareTests.Test_PingEscaner();
HardwareTests.Test_EscanearDocumento();
```

## Criterios de aceptación

| ID | Criterio | Cómo se verifica |
| :-- | :-- | :-- |
| E-01 | El SDK se inicializa y detecta el dispositivo | Criterio automático |
| E-02 | El documento colocado se detecta dentro del timeout | Criterio automático |
| E-03 | Se extraen Nombre, ID, Nacionalidad, fecha de nacimiento y fecha de vencimiento no vacíos de un pasaporte | Salida de consola |
| E-04 | Se extraen los mismos campos de una INE | Salida de consola |
| E-05 | Los datos coinciden con el documento físico (comparación manual) | Confirmación por consola |
| E-06 | Cinco lecturas consecutivas sin reiniciar, incluida una con documento mal colocado (falla controlada) | Prueba repetida |
| E-07 | La prueba corrió con `Simulado = false` | Revisar la configuración |
| E-08 | El dígito verificador de la MRZ es válido (`bCheckSum` que calcula el propio SDK) | Criterio automático |

**Aprobado** = E-01 a E-08. E-08 es el criterio que de verdad garantiza que la lectura corresponde al documento (no solo que los campos no vinieron vacíos): si falla, la lectura no es confiable aunque E-03/E-04 pasen.

## Modalidad remota (desarrollador a distancia + asistente en sitio)

| Momento | Qué pasa en consola | Qué hace el asistente |
| :-- | :-- | :-- |
| Antes de esperar el documento | "Coloque el pasaporte o la INE sobre la ventana del escáner y no lo mueva" | Lo coloca y avisa cuando está listo (ver `Guia_Asistente_Sitio.md`, sección Escáner) |
| Después de leer los datos | "Le leo nombre, ID y nacionalidad: ¿coinciden con el documento?" | Compara contra el documento físico y dice "sí" o "no" |

No hay `AntesDeMover`: no hay un mecanismo motorizado, solo una ventana de lectura óptica fija.

## Supuestos y riesgos

- **Confirmado (TWRPassportAPI.h + demo del fabricante):** `TW_Initialize`/`TW_SetCapture_Start`/`TW_SetAutoModel(true)`/`TW_Capture_RegisterCallbackResultImageEX` es el camino de inicialización y captura; `PassportInfo` trae `Name_EN`, `PassportNo`, `Nationality`, `BirthDate`, `ExpiryDate`, `bCheckSum`, `chMRZ1..3`, suficiente para los criterios E-03/E-04/E-08.
- **`bCheckSum` sin documentar explícitamente:** ni el `.h` ni los manuales dicen qué valida exactamente; se asume por el nombre del campo y por ser parte de una estructura orientada a MRZ (`chMRZ1..3`) que es la validación del dígito verificador de la zona MRZ según ICAO 9303 (estándar de pasaportes). Falta confirmarlo con una lectura física: forzar una lectura con el documento mal colocado (ya cubierto por E-06) debería dar `bCheckSum = false` si la suposición es correcta.
- **`TW_InitIDCard("", new int[5])` agregado (2026-10-16) replicando la secuencia del demo** (`IDRead/Form1.cs`, `btnInit_Click`, que la llama justo después de `TW_Initialize`): no hay documentación de qué rompe si se omite, así que se agrega por si el SDK la necesita para dejar listo el subsistema completo, igual que el demo tampoco bloquea si devuelve `false`.
- **`Test_PingEscaner` confirmado en la máquina real (2026-10-16), con `Escaner.Simulado = false`:** `TW_Initialize` → `TW_InitIDCard` → registro del callback EX → `TW_SetCapture_Start` → `TW_SetAutoModel(true)` completan sin error y el SDK detecta el lector (`APROBADO 1/1`). El primer intento había fallado antes en la carga de la DLL (faltaba `tessdata/`, ver "Precondiciones"), no en el protocolo — ya corregido. Falta `Test_EscanearDocumento` con un documento físico real para validar la lectura de datos en sí (nombre, ID, nacionalidad, checksum de la MRZ).
- No se ha confirmado si el OCR cubre INE (tarjeta) igual que pasaporte (MRZ de 2-3 líneas) — el demo está orientado a documentos chinos/taiwaneses, `TWPassportType` no lista explícitamente "INE mexicana".
- La lectura del chip RFID (ICAO 9303) no está implementada (ver Precondiciones); si se necesita, falta investigar `TW_Capture_SetFunctions` (bit RFID) y `TW_Capture_GetDGData`, ninguno usado todavía.
- El modo síncrono por sondeo (`TW_MonitoringDocEX` + `TW_Capture_Manual_GetInfo`) existe en el header pero no se implementó: no hay ningún demo que lo ejercite para confiar en su firma a ciegas.
- **La ruta EX no es la que corre por defecto en el demo del fabricante** (ver "Protocolo / librería" arriba): está apagada tras `#define API_EX` comentado en `Form1.cs`. Se mantiene como elección porque es la única viable de marshalizar directo en .NET sin copiar buffers de imagen a mano, pero reduce la certeza de que esté tan probada como la variante no-EX.
- El callback nativo se registra una sola vez por proceso (campo estático en `TwrPassportSdk`); si `Test_EscanearDocumento` se corre varias veces en la misma sesión de consola, falta validar que `TW_DeInitialize` deja al SDK listo para un `TW_Initialize` siguiente sin reiniciar el `.exe`.

## Evidencia a guardar

Salida de consola por cada documento (con datos sensibles tachados en cualquier reporte compartido) y fotografía del documento usado.
