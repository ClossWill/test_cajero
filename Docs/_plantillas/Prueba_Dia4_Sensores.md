# Prueba Día 4 – Módulo de sensores y seguridad ALB02

> **Pendiente (2026-10-28):** en COM5 la calibración no recibe ningún byte; los sensores parecen estar en las 6 entradas de la tarjeta de 18 canales (COM2, CMD 0x32, menú 33). Ver [Pendientes.md](Pendientes.md) §2.

## Objetivo

Leer el puerto COM del ALB02 en un bucle reactivo y mostrar `¡PUERTA ABIERTA!` o `¡VIBRACIÓN DETECTADA!` cuando se manipule físicamente el gabinete.

## Protocolo / librería

- **Escucha serial reactiva**: el módulo envía tramas de forma autónoma por el COM; el programa no envía comandos, solo lee.
- `System.IO.Ports` + `Protocolos/SerialTramas.cs`: agrupa los bytes en "tramas" cuando la línea queda en silencio (`SilencioTramaMs`, 30 ms por defecto) y detecta cada evento buscando un patrón de bytes dentro de la trama.
- **El formato de tramas del ALB02 no está en el repositorio.** Por eso la prueba tiene dos pasos: `Test_CalibrarSensores` captura lo que llega en reposo y ante cada evento; las tramas propias de cada evento se copian a `hardware.json`; `Test_EscucharSensores` las usa para disparar los mensajes.

## Precondiciones

- ALB02 alimentado y conectado al COM; puerto y baudios (9600 por defecto) confirmados con el manual del módulo.
- **Puerto confirmado en la máquina real (2026-10-16) con `Test_PingSensores`:** `COM5` (el valor por defecto) abre correctamente — a diferencia de monederos y LEDs, aquí el supuesto original resultó ser el correcto.
- Acceso al gabinete para abrir/cerrar la puerta y golpearlo con cuidado.
- Cerradura S&G operable (para abrir sin forzar).

## Configuración

```json
"Sensores": {
  "Puerto": "COM5",
  "BaudRate": 9600,
  "SilencioTramaMs": 30,
  "Tramas": {
    "PUERTA_ABIERTA": "AA 01 01 55",
    "PUERTA_CERRADA": "AA 01 00 55",
    "VIBRACION": "AA 02 01 55",
    "PROXIMIDAD": ""
  }
}
```

Los valores de `Tramas` del ejemplo son **ilustrativos**; deben sustituirse con lo capturado en la calibración.

## Código

### `Protocolos/SerialTramas.cs`

{{CODE:Protocolos/SerialTramas.cs}}

### `Tests/Sensores.cs`

{{CODE:Tests/Sensores.cs}}

## Cómo ejecutar

1. Menú `7` (calibración): seguir las instrucciones de reposo, puerta abierta, puerta cerrada y vibración; copiar las tramas a `hardware.json`.
2. Menú `8` (escucha): manipular el gabinete; `Q` termina.
3. Menú `16` (ping: solo abre el puerto COM; el ALB02 es reactivo, no hay comando de consulta que responder). Equivalente desde `Main`:

```csharp
HardwareTests.Test_PingSensores();
HardwareTests.Test_CalibrarSensores();
HardwareTests.Test_EscucharSensores();
```

## Criterios de aceptación

| ID | Criterio | Cómo se verifica |
| :-- | :-- | :-- |
| S-01 | El puerto COM abre y se reciben bytes en reposo o ante eventos | Calibración |
| S-02 | Cada evento (puerta abierta, cerrada, vibración) produce una trama distinta de la de reposo | Calibración |
| S-03 | Al abrir la puerta aparece `¡PUERTA ABIERTA!` en menos de 1 s | Observación en la escucha |
| S-04 | Al golpear el gabinete aparece `¡VIBRACIÓN DETECTADA!` | Observación en la escucha |
| S-05 | Cerrar la puerta produce "Puerta cerrada" y no repite alarmas falsas | Observación |
| S-06 | 10 aperturas/cierres consecutivos se detectan las 10 veces (sin pérdidas) | Prueba repetida |
| S-07 | Sin manipulación durante 5 min no hay falsas alarmas | Prueba de estabilidad |

**Aprobado** = S-01 a S-07 para los eventos de puerta y vibración (proximidad es opcional).

## Supuestos y riesgos

- Si el módulo envía un latido periódico, las repeticiones idénticas se ignoran para no inundar la consola; los cambios sí se muestran.
- Si una trama contiene varios estados a la vez (p. ej. un byte de bits), el patrón por subsecuencia será insuficiente y habrá que decodificar el byte; se decide con los datos de la calibración.
- Baudios o paridad incorrectos producen basura; probar 9600/19200/115200 si la calibración sale ilegible.

## Evidencia a guardar

Salida de la calibración, `hardware.json` con las tramas finales y un video corto de la puerta abierta junto a la consola.
