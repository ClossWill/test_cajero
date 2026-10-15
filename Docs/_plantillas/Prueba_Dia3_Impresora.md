# Prueba Día 3 – Impresora térmica KP-80A

## Objetivo

Imprimir un voucher mínimo con el texto **"Hola Mundo FINSO"** en negritas y ejecutar el corte de papel, demostrando control directo por comandos ESC/POS **y** validando con certeza el canal que se usará en el despliegue productivo (ver "Spooler vs. SDK" más abajo).

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

## Precondiciones

- Impresora encendida, con rollo de 80 mm y tapa cerrada.
- Modo Spooler: driver instalado y nombre exacto de la cola (`Get-Printer | Select Name`).
- Modo Serial: puerto y baudios correctos (115200 por defecto; confirmar con el autotest de la impresora).
- Modo UsbSdk: `autoreplyprint.dll` (win32) junto al `.exe` — la compila el `.csproj` desde `referencias_integracion/Receipt Printer(KP-80A)/autoreplyprint_dll_win32/`. Solo una impresora USB conectada (si hay varias, hace falta implementar `CP_Port_EnumUsb` para elegir).

## Configuración

```json
"Impresora": {
  "Modo": "UsbSdk",
  "NombreImpresora": "POS80 Printer",
  "Puerto": "COM4",
  "BaudRate": 115200,
  "NombreUsb": "POS80"
}
```

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

No hay `AntesDeMover`: no hay un mecanismo donde el asistente pueda tener la mano dentro.

## Cómo ejecutar

Menú `6`. Equivalente desde `Main`:

```csharp
HardwareTests.Test_ImprimirVoucherPrueba();
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

**Aprobado** = I-01, I-03, I-04 e I-05 (I-02 solo aplica en Serial; I-07/I-08 solo aplican en UsbSdk, y son los que dan certeza para el despliegue productivo).

## Supuestos y riesgos

- **Nombre de la cola confirmado en la máquina (2026-10-14):** `POS80 Printer` (driver `POS80`, puerto `CP001`), no "KP-80A". Los baudios por serial siguen sin confirmar (la impresora también expone un puente CP210x en COM13/COM14; no se ha determinado cuál es de la impresora). Esto solo afecta a los modos Spooler/Serial.
- **Modo UsbSdk sin probar aún en la máquina real** (2026-10-14): el código compila y la DLL se copia al `.exe`, pero falta ejecutarlo frente al hardware. Si `CP_Port_OpenUsb` no encuentra la impresora, hace falta implementar `CP_Port_EnumUsb` (el SDK usa un buffer multi-string, no un `String[]` directo como el wrapper C#) para listar los nombres válidos.
- **`QueryRTStatus` puede devolver 0 por dos motivos indistinguibles:** falla de lectura o, según el propio manual del SDK, "no hay verificación, no se garantiza que el resultado sea correcto". Por eso el criterio I-07 solo se evalúa cuando hubo respuesta; si no la hay, se avisa y se continúa (igual que `DLE EOT 1` en Serial).
- El texto es ASCII; los acentos requieren seleccionar la tabla de caracteres (`ESC t n` en ESC/POS, o `CP_Pos_PrintTextInUTF8` en el SDK), fuera del alcance de la prueba.
- `GS V 66 0` es el corte parcial con avance; si el modelo no lo soporta, probar `GS V 0` o `GS V 1`. En modo UsbSdk se usa `CP_Pos_FullCutPaper` (corte completo); si el hardware no lo soporta, usar `CP_Pos_HalfCutPaper` o `CP_Pos_FeedAndHalfCutPaper`.

## Evidencia a guardar

El voucher físico (pegado en el reporte) y la salida de consola.
