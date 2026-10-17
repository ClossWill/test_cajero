# Prueba Día 3 – Impresora térmica KP-80A

## Objetivo

Imprimir un voucher mínimo con el texto **"Hola Mundo FINSO"** en negritas y ejecutar el corte de papel, demostrando control directo por comandos ESC/POS **y** validando con certeza el canal que se usará en el despliegue productivo (ver "Spooler vs. SDK" más abajo).

Además, `Test_ImprimirVoucherOperacion` prueba el formato real que necesitará el voucher de producción: logo de la empresa arriba y, debajo, los datos de una operación de cambio con el formato de un comprobante tradicional (fecha, tipo de operación, montos entregado/recibido, denominaciones, tipo de cambio, comisión, nivel de identificación, terminal y folio). **Funciona en cualquier modo** (`Spooler`, `Serial` o `UsbSdk`): el logo se arma con `GS v 0` (raster bit image) y el tamaño de fuente con `GS !` — ambos comandos ESC/POS estándar, implementados en `EscPos.cs` (`ImagenDesdeArchivo`/`ImagenRaster`/`EscalaTexto`). Originalmente esto dependía del SDK del fabricante (`CP_Pos_PrintRasterImageFromFile`/`CP_Pos_SetTextScale`), pero el SDK no se pudo destrabar en esta máquina (ver "Supuestos y riesgos") y se reimplementó por ESC/POS crudo para no bloquear la prueba.

## Protocolo / librería

- **ESC/POS** estándar. Comandos: `ESC @` (inicializar), `ESC E 1/0` (negritas), `ESC a n` (alineación), `GS V 66 0` (avanzar y corte parcial), `DLE EOT 1` (estado en línea, solo por serial).
- Tres canales, elegidos con `Impresora.Modo`:
  - `Spooler` (USB): se envían bytes crudos a la cola de Windows con `winspool.Drv` (`datatype = RAW`), sin usar el driver gráfico (`Protocolos/EscPos.cs`, clase `ImpresoraRaw`).
  - `Serial`: escritura directa al COM con `System.IO.Ports`.
  - **`UsbSdk` (recomendado para producción):** usa el SDK nativo del fabricante (`autoreplyprint.dll`, modo "autoreply" por USB) en vez del spooler de Windows. Permite consultar el estado real (papel/tapa/cortador) antes de imprimir y confirmar con `QueryPrintResult` que el ticket salió, no solo que se envió el comando (`Protocolos/AutoReplyPrintSdk.cs`).

### Spooler vs. SDK: por qué importa para producción

El modo `Spooler` basta para esta prueba de hardware, pero **no** es el canal recomendado para la app oficial:

1. **No confirma impresión real.** `WritePrinter` confirma que el spooler de Windows recibió los bytes, no que la impresora los imprimió. El servicio de cola puede encolar, reintentar o colgarse sin que la app se entere — en un flujo de dispensación de efectivo, no saber si el comprobante salió es un problema de conciliación.
2. **Sin estado antes de imprimir.** Hay que saber si hay papel, si la tapa está cerrada y si el cortador está libre *antes* de imprimir, no después de que el cliente ya espera el ticket. El SDK, con `autoreplymode=1`, hace que la impresora reporte su estado solo y permite consultarlo con `CP_Pos_QueryRTStatus`.
3. **Dependencia del servicio Spooler de Windows**, que se puede detener, bloquear por otro trabajo o fallar en frío tras un reinicio — un punto de falla fuera del control de la app en un kiosco.

Por eso esta prueba implementa también el modo `UsbSdk`: valida en la máquina real el mismo canal (estado + confirmación de impresión) que se extraerá a la app oficial, en vez de validar solo el camino corto del spooler.

**Actualización (2026-10-17): el modo `UsbSdk` no se pudo destrabar en esta máquina** — ver el detalle completo en "Supuestos y riesgos". `Impresora.Modo` quedó de vuelta en `Spooler` (el único que de verdad imprime hoy) y el logo/tamaños de fuente se reimplementaron por ESC/POS crudo (ver arriba) para no depender del SDK. El razonamiento de esta sección sigue siendo válido como objetivo a futuro para la app oficial — solo que en esta máquina concreta hace falta resolver el acceso del SDK primero (posiblemente con ayuda del proveedor/BEEX).

## Precondiciones

- Impresora encendida, con rollo de 80 mm y tapa cerrada.
- Modo Spooler: driver instalado y nombre exacto de la cola (`Get-Printer | Select Name`).
- Modo Serial: puerto y baudios correctos (115200 por defecto; confirmar con el autotest de la impresora).
- Modo UsbSdk: `autoreplyprint.dll` (win32) junto al `.exe` — la compila el `.csproj` desde `referencias_integracion/Receipt Printer(KP-80A)/autoreplyprint_dll_win32/`. Solo una impresora USB conectada (si hay varias, hace falta implementar `CP_Port_EnumUsb` para elegir).

## Configuración

```json
"Impresora": {
  "Modo": "Spooler",
  "NombreImpresora": "POS80 Printer",
  "Puerto": "COM4",
  "BaudRate": 115200,
  "NombreUsb": "POS80",
  "RutaLogo": "logo.png"
}
```

`RutaLogo` es relativa a la carpeta del `.exe`; el `.csproj` copia `logo.png` (raíz del repo) junto al build. Solo la usa `Test_ImprimirVoucherOperacion`.

## Código

### `Protocolos/EscPos.cs`

{{CODE:Protocolos/EscPos.cs}}

### `Protocolos/AutoReplyPrintSdk.cs` (modo `UsbSdk`)

Wrapper propio por P/Invoke directo a `autoreplyprint.dll` nativa (cdecl, cadenas ANSI) en vez del wrapper administrado `autoreplyprint.cs.dll` del SDK (es una DLL .NET Framework antigua; se evita esa dependencia, igual que `EscPos.cs`/`CcTalk.cs` implementan el protocolo propio).

{{CODE:Protocolos/AutoReplyPrintSdk.cs}}

### `Tests/Impresora.cs`

{{CODE:Tests/Impresora.cs}}

## Modalidad remota (desarrollador a distancia + asistente en sitio)

En los tres modos, la prueba solo necesita al asistente al final, para confirmar lo que no se puede leer por software:

| Momento | Qué pasa en consola | Qué hace el asistente |
| :-- | :-- | :-- |
| Después de imprimir | "¿Salió un ticket con 'Hola Mundo FINSO' en negritas?" | Mira el ticket y dice "sí" o "no" (ver `Guia_Asistente_Sitio.md`, sección Impresora) |
| Después de imprimir | "¿El ticket salió cortado (suelto)?" | Lo confirma |
| `Test_ImprimirVoucherOperacion`, después de imprimir | "¿Se ve el logo de FINSO arriba del ticket?" | Mira el ticket y dice "sí" o "no" |
| `Test_ImprimirVoucherOperacion`, después de imprimir | "¿El ticket muestra fecha, operación, montos, denominaciones, tipo de cambio, identificación, terminal y folio en líneas separadas, como un comprobante normal?" | Lo confirma o dice qué falta/se ve mal |

No hay `AntesDeMover`: no hay un mecanismo donde el asistente pueda tener la mano dentro.

## Cómo ejecutar

Menú `6` (voucher de prueba), `14` (voucher de operación, logo + formato tradicional) o `15` (ping: abre/consulta estado según el modo, sin imprimir nada). Equivalente desde `Main`:

```csharp
HardwareTests.Test_PingImpresora();
HardwareTests.Test_ImprimirVoucherPrueba();
HardwareTests.Test_ImprimirVoucherOperacion();
```

## Criterios de aceptación

| ID | Criterio | Cómo se verifica |
| :-- | :-- | :-- |
| I-01 | Los bytes/comandos se entregan sin excepción (cola abierta, COM abierto o handle USB-SDK válido) | Criterio automático |
| I-02 | Modo Serial: la impresora responde a `DLE EOT 1` y está en línea | Criterio automático (si no responde, se avisa; no bloquea) |
| I-03 | Se imprime "Hola Mundo FINSO" legible, centrado y **en negritas**, con el resto del texto en normal | Inspección del voucher |
| I-04 | El autocortador corta el papel al final | Inspección del voucher |
| I-05 | Dos impresiones seguidas funcionan sin reiniciar | Repetir la prueba |
| I-06 | Con la tapa abierta o sin papel la prueba falla de forma controlada (sin colgar el programa) | Prueba negativa opcional |
| I-07 | Modo UsbSdk: antes de imprimir no reporta sin papel / tapa abierta / error de cortador | Criterio automático (`CP_Pos_QueryRTStatus`) |
| I-08 | Modo UsbSdk: el SDK confirma que el ticket se imprimió, no solo que se envió | Criterio automático (`CP_Pos_QueryPrintResult`) |
| I-10 | El logo (`RutaLogo`) se encuentra en disco y se arma el comando de imagen sin excepción (`CP_Pos_PrintRasterImageFromFile` en UsbSdk, `GS v 0`/`ImagenDesdeArchivo` en Spooler/Serial) | Criterio automático |
| I-11 | El asistente ve el logo de FINSO legible arriba del ticket | Inspección del voucher |
| I-12 | El asistente ve los datos de la operación con formato de voucher tradicional (fecha, operación, montos, denominaciones, tipo de cambio, comisión, identificación, terminal, folio) | Inspección del voucher |

**Aprobado** = I-01, I-03, I-04 e I-05 (I-02 solo aplica en Serial; I-07/I-08 solo aplican en UsbSdk, y son los que dan certeza para el despliegue productivo). Para `Test_ImprimirVoucherOperacion`: I-10 a I-12 (ya no exige un modo específico; corre en el que esté configurado).

## Supuestos y riesgos

- **Nombre de la cola confirmado en la máquina (2026-10-14):** `POS80 Printer` (driver `POS80`, puerto `CP001`, tipo `USBPort` — confirmado en Propiedades > Puertos, 2026-10-17), no "KP-80A". Los baudios por serial siguen sin confirmar.
- **Modo `UsbSdk` probado contra hardware real (2026-10-16/17) y no se pudo destrabar — queda documentado para no repetir el mismo camino:**
  - `CP_Port_OpenUsb` detecta un dispositivo USB (log propio de la DLL: VID 0x0FE6/PID 0x811E, `szProduct="Virtual PRN"`) pero el `Open` siempre devuelve handle inválido. Se descartaron, uno por uno: el servicio Spooler de Windows corriendo (se detuvo y persistió el fallo), falta de privilegios (se corrió como administrador y persistió), y la cola `POS80 Printer` reteniendo el puerto (se intentó quitar la cola desde Windows, pero se reinstala sola por Plug & Play mientras el cable siga conectado — prueba no concluyente, no se pudo aislar del todo).
  - Se probó la alternativa `CP_Port_OpenCom` (mismo SDK, puerto serie en vez de USB): la herramienta oficial del fabricante (**Print Test Tool**, `referencias_integracion/Receipt Printer(KP-80A)/Windows SDK + Testing & Configuration Tools/Print Test Tool/`) confirmó manualmente que `COM14` abre con el mismo SDK (`115200/8/NOPARITY/ONESTOPBITS/No FlowControl`). Se corrigió un bug real encontrado en el camino — `CreateFile` (lo que usa `CP_Port_OpenCom` internamente) exige el prefijo `\\.\` para puertos COM10 en adelante, si no falla en silencio aunque el puerto exista y esté libre — pero aun con eso corregido, nuestro código seguía sin poder abrir `COM13` ni `COM14`.
  - Conclusión práctica: Windows sí reconoce la impresora como "USB Printing Support" genuino (`CP001`/`USBPort`, "This device is working properly"), así que el hardware y el cableado están bien; el bloqueo es específico de cómo esta máquina deja (o no) que `autoreplyprint.dll` tome el dispositivo. Pendiente: contactar al proveedor/BEEX con estos logs, o probar en otra máquina para aislar si es algo de esta instalación de Windows en particular.
  - **Mientras tanto, `Impresora.Modo` quedó en `Spooler`** y el logo/tamaños de fuente se reimplementaron por ESC/POS crudo (ver Objetivo) para no bloquear el avance.
- **`QueryRTStatus` puede devolver 0 por dos motivos indistinguibles:** falla de lectura o, según el propio manual del SDK, "no hay verificación, no se garantiza que el resultado sea correcto". Por eso el criterio I-07 solo se evalúa cuando hubo respuesta; si no la hay, se avisa y se continúa (igual que `DLE EOT 1` en Serial).
- El texto es ASCII; los acentos requieren seleccionar la tabla de caracteres (`ESC t n` en ESC/POS, o `CP_Pos_PrintTextInUTF8` en el SDK), fuera del alcance de la prueba.
- `GS V 66 0` es el corte parcial con avance; si el modelo no lo soporta, probar `GS V 0` o `GS V 1`. En modo UsbSdk se usa `CP_Pos_FullCutPaper` (corte completo); si el hardware no lo soporta, usar `CP_Pos_HalfCutPaper` o `CP_Pos_FeedAndHalfCutPaper`.
- **Tamaño del logo (576×147 dots) sin confirmar con una impresión física todavía (pendiente, criterios I-10 a I-12):** 576 es el ancho imprimible estándar de una impresora de 80mm a 203dpi (72mm; el resto son márgenes del cabezal — lo confirma además `EasyLabel-203dpi-...exe`, incluido en el mismo paquete del fabricante), y 147 es el alto proporcional al tamaño real de `logo.png` (2560×655 px) para no deformarlo. `EscPos.ImagenDesdeArchivo` usa un umbral simple (128) para convertir a blanco/negro — si el logo sale muy "quemado" o muy tenue, ese umbral es lo primero a ajustar. `EscPos.EscalaTexto` usa 0-7 (x1 a x8) para `GS !`, el rango estándar documentado de ESC/POS (a diferencia del equivalente del SDK, `CP_Pos_SetTextScale`, cuyo rango nunca se confirmó porque no se llegó a usar).
- Los datos de la operación en `Test_ImprimirVoucherOperacion` son de muestra fija (no vienen de una transacción real ni de los demás tests): sirven para probar el formato de impresión, no para validar aritmética de cambio de divisas.

## Evidencia a guardar

El voucher físico (pegado en el reporte) y la salida de consola.
