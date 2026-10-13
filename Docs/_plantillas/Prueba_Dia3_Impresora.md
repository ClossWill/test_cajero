# Prueba Día 3 – Impresora térmica KP-80A

## Objetivo

Imprimir un voucher mínimo con el texto **"Hola Mundo FINSO"** en negritas y ejecutar el corte de papel, demostrando control directo por comandos ESC/POS.

## Protocolo / librería

- **ESC/POS** estándar. Comandos: `ESC @` (inicializar), `ESC E 1/0` (negritas), `ESC a n` (alineación), `GS V 66 0` (avanzar y corte parcial), `DLE EOT 1` (estado en línea, solo por serial).
- Dos canales, elegidos con `Impresora.Modo`:
  - `Spooler` (USB, recomendado): se envían bytes crudos a la cola de Windows con `winspool.Drv` (`datatype = RAW`), sin usar el driver gráfico (`Protocolos/EscPos.cs`, clase `ImpresoraRaw`).
  - `Serial`: escritura directa al COM con `System.IO.Ports`.

## Precondiciones

- Impresora encendida, con rollo de 80 mm y tapa cerrada.
- Modo Spooler: driver instalado y nombre exacto de la cola (`Get-Printer | Select Name`).
- Modo Serial: puerto y baudios correctos (115200 por defecto; confirmar con el autotest de la impresora).

## Configuración

```json
"Impresora": {
  "Modo": "Spooler",
  "NombreImpresora": "KP-80A",
  "Puerto": "COM4",
  "BaudRate": 115200
}
```

## Código

### `Protocolos/EscPos.cs`

{{CODE:Protocolos/EscPos.cs}}

### `Tests/Impresora.cs`

{{CODE:Tests/Impresora.cs}}

## Cómo ejecutar

Menú `6`. Equivalente desde `Main`:

```csharp
HardwareTests.Test_ImprimirVoucherPrueba();
```

## Criterios de aceptación

| ID | Criterio | Cómo se verifica |
| :-- | :-- | :-- |
| I-01 | Los bytes ESC/POS se entregan sin excepción (cola abierta o COM abierto) | Criterio automático |
| I-02 | Modo Serial: la impresora responde a `DLE EOT 1` y está en línea | Criterio automático (si no responde, se avisa; no bloquea) |
| I-03 | Se imprime "Hola Mundo FINSO" legible, centrado y **en negritas**, con el resto del texto en normal | Inspección del voucher |
| I-04 | El autocortador corta el papel al final | Inspección del voucher |
| I-05 | Dos impresiones seguidas funcionan sin reiniciar | Repetir la prueba |
| I-06 | Con la tapa abierta o sin papel la prueba falla de forma controlada (sin colgar el programa) | Prueba negativa opcional |

**Aprobado** = I-01, I-03, I-04 e I-05 (I-02 solo aplica en Serial).

## Supuestos y riesgos

- El nombre de la cola y los baudios por serial no están confirmados en el repositorio.
- El texto es ASCII; los acentos requieren seleccionar la tabla de caracteres (`ESC t n`), fuera del alcance de la prueba.
- `GS V 66 0` es el corte parcial con avance; si el modelo no lo soporta, probar `GS V 0` o `GS V 1`.

## Evidencia a guardar

El voucher físico (pegado en el reporte) y la salida de consola.
