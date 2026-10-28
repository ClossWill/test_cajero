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

## 2. Sensores de puerta y vibración — identificar entradas

**Hallazgo:** COM5 (el puerto configurado para el ALB02) no entrega ningún byte en reposo. El BEEX llama "Multifunctional serial control module (ALB02)" a la tarjeta de 18 canales de la carpeta de referencia, la misma de los LEDs en COM2 @ 19200. Esa tarjeta tiene 6 entradas de sensor de 5 V que se consultan con CMD 0x32.

**Confirmado en la máquina real (2026-10-28):** menú 33 en COM2. `PARA 0x00` responde 6 bytes, uno por entrada; `PARA 0x31` lo rechaza. Estado inicial observado: `01 00 01 01 01 01` (entrada 2 en 0, el resto en 1).

**Pendiente:**

1. Con el menú 33, abrir la puerta, cerrarla, golpear el gabinete y (si hay) acercarse al sensor de proximidad; guardar la salida para ver qué entrada cambia con cada evento y en qué sentido.
2. Con eso, fijar en `hardware.json` el mapa entrada → evento (hoy no existe; la prueba solo imprime `ENTRADA n: a->b`) y mostrar `¡PUERTA ABIERTA!` / `¡VIBRACIÓN DETECTADA!` desde esa tarjeta.
3. Decidir qué hacer con COM5 y `Test_CalibrarSensores` / `Test_EscucharSensores` (menú 7, 8, 16), que asumen un ALB02 aparte: eliminarlos o dejarlos solo si resulta que existe otro módulo.
4. El formato de la respuesta de CMD 0x32 no se pudo leer del PDF del fabricante (fuentes codificadas); validar con la hoja de protocolo que "un byte por entrada, 0/1" es la interpretación correcta y qué nivel significa "activado".
5. La opción 33 usa COM2, el mismo puerto de los LEDs: no correrla a la vez que la prueba de LEDs.
