# Pendientes abiertos (actualizado 2026-10-28)

## 1. Lector QR XZG004 — disparo bajo demanda

**Objetivo:** que el lector escanee solo cuando el software lo pida. Hoy se queda en modo de escaneo activo.

**Estado:** código listo (`Tests/Qr.cs`, sección `Qr` de `hardware.json`, menú 30–32), sin probar con el lector.

**Bloqueo:** el manual `XZG004 EN Barcode User Manual V1.0.docx` documenta los códigos de programación pero no el comando serial de disparo. En modo teclado (HID-KBW, como llega de fábrica) el host no puede enviarle comandos.

**Pasos pendientes:**

1. Reprogramar el lector escaneando, en orden, estos códigos del manual (comparar siempre el texto impreso bajo cada QR):
   - `$>:S01010F.<$` (Enter Setup)
   - `$>: S03001A.<$` (Manual Mode: dispara por botón o comando)
   - `$>: S030000.<$` (Single Mode)
   - `$>: S0F0216.<$` (USB CDC, para tener canal de comandos)
   - `$>:S01000F.<$` (Exit Setup)
   - Para volver a fábrica: `$>:S010186.<$`.
2. Ver qué COM nuevo aparece (Administrador de dispositivos > Puertos) y ponerlo en `Qr.Puerto`, con `Qr.Modo = "Serial"`. No hace falta desconectar nada; si el COM no aparece, reconectar el USB del lector una vez.
3. Menú 32 (Probar comandos de disparo): el candidato que encienda el haz va a `Qr.ComandoActivar` (y su pareja a `Qr.ComandoDesactivar`). Los candidatos de `hardware.json` son supuestos, no vienen del manual.
4. Si ninguno funciona: pedir al fabricante del XZG004 el comando serial de disparo.
5. Validar con el menú 30 que el lector solo escanea durante la prueba y queda inactivo al terminar.

## 2. Sensores de presencia y vibración — validar el menú 8

**Alcance:** solo existen dos sensores (confirmado por el equipo, 2026-10-29): presencia frente a la puerta y vibración. No hay contacto de puerta abierta, así que `¡PUERTA ABIERTA!` no se puede generar.

**Hallazgo:** el "ALB02" del BEEX es la tarjeta de 18 canales de los LEDs (COM2 @ 19200), cuyas 6 entradas se consultan con CMD 0x32 (`PARA 0x00` → 6 bytes, uno por entrada; `PARA 0x31` lo rechaza). COM5 no entrega nada; el código de COM5 (calibrar tramas, ping) se eliminó.

**Mapa observado (log del 2026-10-29, menú 33):**

- **E1 = vibración.** Reposo en 1; cada golpe baja a 0 por ~0.35–0.5 s.
- **E2 = presencia** (probable). Cambia al acercarse o alejarse y mantiene el valor. En 1 con alguien delante, en 0 con la máquina vacía.
- E3 siempre 1; E4–E6 cambian poco y sin patrón: sin sensor, se ignoran.
- 2 timeouts de 300 ms en ~3.5 min: se toleran; el menú 8 avisa solo tras 3 fallos seguidos (`FallasParaAlarma`).

**Pendiente:**

1. Confirmar la polaridad de E2 corregida: con la máquina vacía el menú 8 mostró `¡PRESENCIA DETECTADA!` con `NivelActivoPresencia = 0`, así que ahora es 1 (E2 en 1 = alguien delante). Validar acercándose y alejándose.
2. Correr el menú 8: debe mostrar `¡PRESENCIA DETECTADA!` / `Zona libre` y un solo `¡VIBRACIÓN DETECTADA!` por golpe (ráfagas agrupadas en `AgruparVibracionMs`).
3. Confirmar con el instalador que E4–E6 realmente no tienen sensor.
4. El formato de la respuesta de CMD 0x32 no se pudo leer del PDF del fabricante (fuentes codificadas); la interpretación "un byte por entrada, 0/1" es la observada en la máquina.
5. Los menús 8 y 33 usan COM2, el mismo puerto de los LEDs: no correrlos a la vez que la prueba de LEDs.
