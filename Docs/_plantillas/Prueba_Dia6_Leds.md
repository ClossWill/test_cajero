# Prueba Día 6 – Control de LEDs indicadores (tarjeta 24002-2)

> Esta prueba **no estaba en el alcance original** (no es uno de los 5 periféricos del documento de arranque del proyecto). Se agregó porque llegó la documentación del fabricante y porque los LEDs de esta tarjeta son los indicadores de función actual del cajero (qué operación está en curso, error, etc.) que la app oficial necesitará encender.

## Objetivo

Leer la versión de firmware, identificar cuáles de los 18 canales corresponden a los 5 íconos del gabinete (recibo, moneda, pasaporte, QR, efectivo) probándolos uno por uno con el asistente, y validar el parpadeo general.

## Protocolo / librería

- **RS232 propio de la tarjeta** (no ccTalk, no ESC/POS, no SDK de fabricante): `DLE STX [LEN(2, low-first) 'C' CMD PARA] DLE ETX BCC`, 19200 baudios 8N1, checksum BCC = XOR de los bytes entre STX y el DLE final. Documentado en `referencias_integracion/Multifunctional serial control module/` (hoja de protocolo + especificación de la tarjeta, modelo **24002-2**, Shenzhen Hongzhou Smart Technology).
- **No es el módulo ALB02** del punto 5 del documento de arranque: es una tarjeta RS232 aparte, en otro puerto COM, dedicada solo a las 18 salidas de 12V. El ALB02 (sensores de puerta/vibración) sigue siendo el de `Tests/Sensores.cs`, sin relación con esta tarjeta.
- Implementado en `Protocolos/LedControlSerial.cs` (`LedControlBus`): construcción de trama, envío y lectura de respuesta con validación de BCC.

## Precondiciones

- Tarjeta conectada por RS232 (DB9) o USB (puerto serie virtual) y alimentada con 12V/10A según su propia fuente — **no** se alimenta desde el puerto de datos.
- Puerto COM identificado en *Administrador de dispositivos* y anotado en `hardware.json` (`Leds.Puerto`).
- Al menos un indicador (LED) cableado a un canal de salida para poder confirmar visualmente; idealmente varios canales para el barrido.
- **Confirmado (hoja de protocolo del fabricante):** 19200 baudios, 8N1, checksum BCC. Verificado recalculando el BCC a mano contra ~40 ejemplos hexadecimales de la hoja de especificación (firmware, consulta de 6 entradas, encender/apagar/parpadear los 18 canales): todos coinciden.
- **Sin confirmar:** el protocolo menciona de pasada una sección de "caracteres de escape" (para cuando 0x10 aparece dentro de los datos) que no estaba en el PDF recibido. Para los comandos de esta prueba (LEN y CMD fijos, PARA ≤ 18) no hay colisión conocida con 0x10, pero no se implementó manejo de escape.

## Configuración

```json
"Leds": {
  "Puerto": "COM2",
  "BaudRate": 19200,
  "Canales": 18,
  "PerifericosEsperados": 5
}
```

`PerifericosEsperados` es cuántos íconos identificables tiene el gabinete (recibo, moneda, pasaporte, QR, efectivo); la prueba prueba canales uno por uno y se detiene apenas los identifica, sin necesidad de agotar los 18.

**Confirmado en la máquina real (2026-10-16) con `Test_PingLeds` (menú `18`):** el puerto es `COM2`, no `COM6` (valor original nunca verificado). Firmware reportado: `ADK LEDBOX V2.0-20240613`. Hizo falta activar `DtrEnable`/`RtsEnable` al abrir el puerto (mismo hallazgo que en monederos, ver `Prueba_Dia2_Monederos.md`) — sin eso, el puerto abría sin error pero el firmware nunca respondía, en ningún COM.

## Código

### `Protocolos/LedControlSerial.cs`

{{CODE:Protocolos/LedControlSerial.cs}}

### `Tests/Leds.cs`

{{CODE:Tests/Leds.cs}}

## Cómo ejecutar

Menú `12` (prueba completa) o `18` (ping: solo consulta el firmware). Equivalente desde `Main`:

```csharp
HardwareTests.Test_PingLeds();
HardwareTests.Test_ControlarLeds();
```

## Modalidad remota (desarrollador a distancia + asistente en sitio)

| Momento | Qué pasa en consola | Qué hace el asistente |
| :-- | :-- | :-- |
| Antes de empezar | "Párese donde pueda ver los 5 íconos del gabinete (recibo, moneda, pasaporte, QR, efectivo). Voy a encender los canales uno por uno." | Se coloca frente al gabinete |
| Por cada canal probado | "Canal N: ¿se encendió alguna luz?" | Dice "sí" o "no". Si dice "sí", sigue: "¿Cuál de los 5 (recibo, moneda, pasaporte, QR, efectivo)?" → el asistente lo nombra |
| Después de "parpadear todos" | "¿Vio parpadear los indicadores durante unos segundos?" | Confirma |

No hay `AntesDeMover`: son salidas eléctricas de 12V hacia luces, no hay ningún mecanismo donde el asistente pueda tener la mano dentro.

## Criterios de aceptación

| ID | Criterio | Cómo se verifica |
| :-- | :-- | :-- |
| L-01 | La tarjeta responde a la consulta de firmware | Criterio automático (BCC válido) |
| L-02 | Se identifican los 5 periféricos esperados (canal → ícono) | Confirmación por consola, canal por canal |
| L-03 | Los canales probados aceptan encender/apagar (ACK) | Criterio automático |
| L-04 | El comando "parpadear todos" es aceptado (ACK) | Criterio automático |
| L-05 | Se observa el parpadeo | Confirmación por consola |

**Aprobado** = L-01 a L-05. Si se agotan los 18 canales sin llegar a 5 identificados, L-02 falla y queda registrado cuántos y cuáles sí se identificaron.

## Supuestos y riesgos

- Sin probar aún en la tarjeta real (2026-10-15): el protocolo está confirmado por los ejemplos del fabricante (recálculo manual de BCC), pero `LedControlBus` no se ha ejecutado contra hardware.
- **Puerto COM confirmado en la máquina real (2026-10-16):** `COM2`, no `COM6` (el valor original era un supuesto nunca verificado, igual que pasó con los monederos). Sigue sin confirmarse si llega por RS232 o por el USB "de repuesto" que menciona la especificación.
- **`DtrEnable`/`RtsEnable` confirmados como necesarios (2026-10-16), mismo hallazgo que en monederos (`Protocolos/CcTalk.cs`):** con `COM6` (el puerto equivocado) el ping no obtenía respuesta; al corregir el puerto a `COM2` **y** tener ya `DtrEnable`/`RtsEnable` activados, el firmware respondió (`ADK LEDBOX V2.0-20240613`). No se aisló cuál de los dos cambios era el necesario (pudo ser solo el puerto), pero dejar DTR/RTS activados no tiene costo.
- No se investigó el comando de consulta de estado de entradas (`CMD 0x32`, 6 sensores de 5V) porque esta tarjeta es distinta del ALB02 y el alcance de esta prueba es solo el control de salidas; si en algún momento se confirma que es la misma tarjeta física que el ALB02, ese comando ya está documentado en la hoja de protocolo y se podría reutilizar para `Tests/Sensores.cs`.
- **El mapeo de qué canal (1-18) corresponde a qué ícono físico del gabinete (RECEIPT, COINS OUT, PASSPORT/ID CARD, QR CODE, CASH IN/OUT) no está documentado en ningún lado** — confirmado revisando la hoja de especificación del fabricante de la tarjeta (`24002-2 Specification.pdf`): solo define `OUT1`-`OUT18` genéricos, sin relación con el gabinete del cajero, porque esa relación la define el arnés de cables que armó el integrador, no el fabricante de la tarjeta. Por eso la prueba lo obtiene probando canal por canal con el asistente (ver "Modalidad remota") y se detiene en cuanto identifica los 5; no hay otra forma de obtenerlo.
- Se asume que, de los 18 canales, exactamente 5 están conectados a un ícono del gabinete y el resto no hace nada visible; si algún canal "de más" también enciende algo (indicador no listado en la foto de referencia), la prueba lo registraría igual al preguntar "¿cuál de los 5?" — el asistente podría nombrar algo fuera de la lista y habría que ajustar `PerifericosEsperados` o la lista de íconos.

## Evidencia a guardar

Salida de consola completa (bitácora de la sesión), con el mapeo canal → ícono que imprime la prueba al final.
