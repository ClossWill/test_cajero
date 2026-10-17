# Guía para el asistente en sitio – Pruebas de hardware del cajero (FINSO)

El desarrollador controla la máquina desde otra ciudad. Usted es sus manos y sus ojos: él le dice qué hacer, usted lo hace y le cuenta lo que ve. No necesita tocar la computadora.

Esta guía cubre las seis pruebas que necesitan que usted esté presente: **recicladora de billetes** (Día 1), **monederos de moneda** (Día 2), **impresora de tickets** (Día 3), **escáner de pasaporte/INE** (Día 5), **LEDs indicadores** (Día 6) y **cámara** (Día 7). "Día N" es solo el nombre de cada sección, heredado de los documentos técnicos — todas se hacen en la misma visita, una tras otra, en ese mismo orden. El desarrollador le dirá cuál siguen antes de empezar cada una; lea solo esa sección además de lo común.

## Antes de empezar (todas las pruebas)

- Llamada o videollamada abierta todo el tiempo, con el altavoz encendido.
- La máquina debe estar encendida y con la puerta cerrada. Si hay algún otro programa del cajero abierto en pantalla, avísele al desarrollador.
- Lo que tenga a la mano cambia según la prueba (ver cada sección).

## Reglas de seguridad (todas las pruebas)

1. **Cuando escuche "va a moverse", aleje las manos del mecanismo (shutter, bandeja).** Diga "listo" solo cuando sus manos estén fuera. Esto solo pasa en la prueba de la recicladora; monederos e impresora no lo piden, porque no hay un mecanismo donde pueda meter la mano mientras se mueve.
2. **Nunca diga "listo" con la mano dentro del shutter.** Cuando usted dice "listo", el shutter puede cerrarse.
3. No meta los dedos ni objetos para sacar algo atorado (billete, moneda, papel). Avísele al desarrollador.
4. Si algo huele a quemado, hace un ruido raro y continuo o se atora, diga **"alto"**. El desarrollador detiene la prueba.
5. Diga siempre lo que realmente ve u oye, aunque no coincida con lo que el desarrollador espera. Una diferencia también es un resultado útil — nunca adivine ni redondee.

---

## 1. Recicladora de billetes GRG 9250N (Día 1)

### Qué tener a la mano

- Un billete de cada uno: MXN 500, MXN 200, MXN 100 y USD 20.
- Un billete inválido: roto, una fotocopia o de otro país.
- El celular para tomar fotos.
- Si es videollamada, apunte la cámara al shutter (la boca por donde entran y salen los billetes).

### Cómo hablamos

| Usted escucha | Usted hace |
| :-- | :-- |
| "Va a moverse… ¿está listo?" | Aleja las manos y dice "listo", o "no" si todavía no puede |
| "¿Qué billete(s) tiene a la mano?" | Dice cuál o cuáles son, por ejemplo "quinientos, doscientos y cien pesos" (puede ser uno solo o varios juntos) |
| "¿Ve el shutter abierto?" | Mira y dice "sí" o "no" |
| "Inserte el/los billete(s) y retire la mano" | Mete todos juntos, en la misma apertura, saca la mano completa y dice "listo" |
| "¿Cuántos billetes hay?" | Los cuenta **sin tocarlos** y dice el número. No adivine: si no ve bien, dígalo |
| "¿De qué denominación son?" | Dice la moneda y el valor de cada uno, por ejemplo "quinientos pesos" |
| "Retírelos" | Los saca todos, retira la mano y dice "listo" |

Nota: la máquina decide internamente a qué compartimento va cada billete; usted no puede verlo ni necesita saberlo, solo contar lo que entra y lo que sale.

### Fotos que se le pedirán

- El shutter con los billetes dentro, **antes** de retirarlos, cuando la máquina los entrega.
- Cualquier mensaje o luz de error en la máquina.
- Envíelas al desarrollador en cuanto las tome.

### Al terminar

- Cuente por la llamada, junto con el desarrollador, todos los billetes que la máquina le entregó, y entréguelos a la persona responsable del efectivo.
- No abra la puerta del gabinete salvo que el desarrollador se lo pida, porque eso dispara los sensores.

---

## 2. Monederos de moneda MK4 (Día 2)

Esta es la prueba más sencilla para usted: no hay nada donde meter la mano mientras se mueve (el mecanismo es un tambor cerrado), así que no se usa la regla "va a moverse / listo". Solo necesita mirar la bandeja de salida de monedas y contar.

### Qué tener a la mano

- Nada especial. Solo esté cerca de la bandeja donde caen las monedas (debajo o al lado del shutter de billetes, según el modelo — el desarrollador le indica dónde mirar la primera vez).
- El celular para tomar fotos, por si hay un atasco.

### Cómo hablamos

| Usted escucha | Usted hace |
| :-- | :-- |
| "¿Cuántas monedas de \$1 cayeron en la bandeja?" | Las cuenta **sin tocarlas** y dice el número exacto. No diga "varias" ni redondee |
| "¿Cuántas monedas de \$5 cayeron en la bandeja?" | Igual, para la otra denominación |

Puede escuchar la pregunta por cada monedero, uno a la vez (primero \$1, luego \$5), o por ambos si se pidieron juntos.

### Fotos que se le pedirán

- La bandeja con las monedas, antes de retirarlas.
- Cualquier atasco o monedas que no cayeron bien.

### Al terminar

- Retire las monedas cuando el desarrollador lo indique y entréguelas a la persona responsable del efectivo.

---

## 3. Impresora térmica KP-80A (Día 3)

Tampoco aquí hay un mecanismo donde meter la mano: solo necesita mirar el ticket que sale y confirmar dos cosas.

### Qué tener a la mano

- Nada especial. Esté donde pueda ver la salida de papel de la impresora.
- El celular para tomar fotos.

### Cómo hablamos

| Usted escucha | Usted hace |
| :-- | :-- |
| "¿Salió un ticket con 'Hola Mundo FINSO' en negritas?" | Mira el ticket y dice "sí" o "no". Si el texto salió pero sin negritas, o ilegible, dígalo así, no "sí" |
| "¿El ticket salió cortado (suelto)?" | Dice si quedó suelto o si sigue pegado al rollo |
| "¿Se ve el logo de FINSO (letras azules + figura verde) arriba del ticket?" | Mira el ticket y dice "sí" o "no". Si se ve borroso, cortado o solo una parte, dígalo así |
| "¿El ticket muestra fecha, tipo de operación, montos, denominaciones, tipo de cambio, identificación, terminal y folio en líneas separadas, como un comprobante normal?" | Lo compara contra lo que ve y dice si falta algo o se ve mal formado |

### Fotos que se le pedirán

- El ticket impreso, legible.
- Cualquier mensaje o luz de error en la impresora (sin papel, tapa abierta).

### Al terminar

- Guarde el ticket impreso; se adjunta como evidencia del reporte.

---

## 4. Escáner de pasaporte / INE MEPR500+ (Día 5)

Aquí usted sí interviene directamente: coloca el documento y después confirma si lo que el desarrollador le lee coincide con él. No hay "va a moverse" porque no hay motor, es una ventana de lectura fija.

### Qué tener a la mano

- Un pasaporte y una INE (credencial de elector) de prueba, de alguien que acepte que sus datos se usen solo para esta prueba.
- El celular para tomar fotos.

### Cómo hablamos

| Usted escucha | Usted hace |
| :-- | :-- |
| "Coloque el pasaporte (o la INE) sobre la ventana del escáner y no lo mueva" | Lo coloca boca abajo/arriba como le indique el desarrollador, lo deja quieto y dice "listo" |
| "Le leo nombre, ID y nacionalidad: ¿coinciden con el documento?" | Compara cada dato contra el documento físico y dice "sí" o, si alguno no coincide, cuál y qué dice realmente el documento. (La fecha de nacimiento y de vencimiento también se muestran en pantalla, pero esas las verifica el desarrollador, no se le leen a usted en voz) |

Puede repetirse varias veces: una con pasaporte, otra con INE, y alguna con el documento mal colocado a propósito (para ver que la prueba falla de forma controlada).

### Fotos que se le pedirán

- El documento sobre la ventana del escáner, antes de la lectura.
- Cualquier mensaje de error en pantalla si el desarrollador lo comparte, o luz de error en el escáner.

### Al terminar

- Retire el documento de la ventana.
- No comparta ni fotografíe los datos leídos por separado; la evidencia la guarda el desarrollador según las reglas de datos personales del proyecto.

---

## 5. LEDs indicadores (Día 6)

Esta tarjeta es distinta de la recicladora, los monederos, la impresora y el escáner: solo enciende, apaga y hace parpadear unas lucecitas (indicadores). No hay documento ni billete que manipular, solo mirar. El objetivo es averiguar cuál de los 5 íconos del gabinete (recibo, moneda, pasaporte, QR, efectivo) corresponde a cada canal de la tarjeta — algo que todavía no sabemos.

### Qué tener a la mano

- Nada especial. Solo párese donde pueda ver los 5 íconos del gabinete (recibo, moneda, pasaporte/INE, QR, efectivo) a la vez.

### Cómo hablamos

| Usted escucha | Usted hace |
| :-- | :-- |
| "Canal N: ¿se encendió alguna luz?" | Mira los 5 íconos y dice "sí" o "no" |
| "¿Cuál de los 5 (recibo, moneda, pasaporte, QR, efectivo)?" (solo si dijo "sí" arriba) | Dice cuál de los 5 vio prender |
| "¿Vio parpadear los indicadores durante unos segundos?" | Confirma |

Esto se repite canal por canal hasta que se identifiquen los 5 — no hace falta contar cuántos canales son en total, eso lo lleva el desarrollador.

### Fotos o video que se le pedirán

- Foto de cualquier indicador que no haya encendido cuando se esperaba o que se haya quedado prendido.

### Al terminar

- No hace falta retirar ni guardar nada; los indicadores quedan apagados al terminar la prueba.

---

## 6. Cámara (Día 7)

Aquí usted es quien aparece en la foto de prueba. No hay datos que leer ni comparar: solo pararse frente a la cámara.

### Qué tener a la mano

- Nada especial. Solo colóquese donde el desarrollador le indique que está la cámara, de frente, con buena luz (evite quedar a contraluz de una ventana).

### Cómo hablamos

| Usted escucha | Usted hace |
| :-- | :-- |
| "Párese frente a la cámara, mirando al lente, con buena luz. Avise cuando esté listo." | Se coloca de frente a la cámara y dice "listo" |

Después de que diga "listo" no le van a preguntar nada más: el desarrollador revisa la foto directamente en la máquina, no necesita que usted se la describa.

### Fotos que se le pedirán

- Ninguna adicional: la propia prueba ya genera la foto.

### Al terminar

- Nada que retirar ni guardar de su parte.
