# Prueba Día 2 – Dispensadores de monedas MK4 (x2)

## Objetivo

Confirmar que el PC se comunica por puerto serial con los dos hoppers MK4 (ping y datos de identificación) y que se puede ordenar la entrega de N monedas de $1 y de $5, verificando el conteo reportado por el hopper y la caída física.

## Protocolo / librería

- **ccTalk** sobre puerto COM (8N1, normalmente 9600 baudios). Implementación mínima propia en `Protocolos/CcTalk.cs` (sin dependencias): trama `[destino][nº datos][origen=1][header][datos…][checksum]`, con suma total ≡ 0 mod 256. El código descarta el eco de la propia trama, habitual en la línea ccTalk de un solo hilo.
- Headers usados: `254` Simple poll, `245` categoría de equipo, `244` código de producto, `241` revisión de software, `242` número de serie, `163` Test hopper, `164` Enable hopper, `167` Dispense hopper coins, `166` Request hopper status, `217` Request high/low status (nivel de monedas).
- Fuente de los comandos: `referencias_integracion/CoinDispenserMK4` (Shanghai Elan: "MK2 & MK4 Coin Dispensor Commands", "Programming Flow", "cctalk address"). Cubre poll, reset, enable, dispense, estado, test hopper y nivel de monedas.
- Puerto: `System.IO.Ports` (paquete NuGet `System.IO.Ports` 8.0.0).

## Precondiciones

- Hoppers alimentados (fuente Mean Well) y cableados al convertidor ccTalk/USB-serial; puerto identificado en el Administrador de dispositivos.
- Cada hopper con **dirección distinta** (se asumen 3 y 4) y con monedas cargadas (≥ 10 de $1 y de $5).
- Ninguna otra aplicación usando el COM.

## Configuración

```json
"Monederos": {
  "BaudRate": 9600,
  "MaxMonedasPrueba": 10,
  "Hoppers": [
    { "Nombre": "MK4 #1 ($1 MXN)", "Denominacion": 1, "Puerto": "COM1", "Direccion": 3 },
    { "Nombre": "MK4 #2 ($5 MXN)", "Denominacion": 5, "Puerto": "COM1", "Direccion": 4 }
  ]
}
```

Si cada hopper está en un COM distinto, basta cambiar `Puerto`. El código abre cada puerto una sola vez aunque lo compartan.

**Confirmado en la máquina real (2026-10-16):** el puerto es `COM1`, no `COM3` (el valor original era un supuesto nunca verificado). `COM1` es uno de los puertos de la tarjeta serial multi-puerto interna (`ACPI\PNP0501` en Device Manager), no del adaptador USB CP210x (ese expone `COM13`/`COM14`, de otro periférico — se probaron por error antes de encontrar el correcto). Se encontró comparando contra el software oficial del fabricante del hopper (**SCH2**, en `D:\SCH2`): Setup → puerto COM → `Hopper Address` 3 → Poll/Reset/Enable, todos `OK`. Direcciones 3 y 4 confirmadas con `Test_PingMonederos` (menú `4`): responden `Payout / SUH1-NOENCRYPT / SUH1-U3.0`.

## Código

### `Protocolos/CcTalk.cs`

{{CODE:Protocolos/CcTalk.cs}}

### `Tests/Monederos.cs`

{{CODE:Tests/Monederos.cs}}

## Modalidad remota (desarrollador a distancia + asistente en sitio)

`Test_PingMonederos` no requiere al asistente: es puramente eléctrico/serial. `Test_DispensarMonedas` sí lo necesita, una vez por hopper, solo para contar lo que cayó:

| Momento | Qué pasa en consola | Qué hace el asistente |
| :-- | :-- | :-- |
| Al terminar de dispensar cada hopper | "¿Cuántas monedas de \$N cayeron en la bandeja?" | Las cuenta sin tocarlas y dice el número (ver `Guia_Asistente_Sitio.md`, sección Monederos) |

No hay `AntesDeMover`: el mecanismo del hopper es un tambor cerrado, no hay un shutter donde el asistente pueda tener la mano dentro.

## Cómo ejecutar

Menú `4` (ping) y `5` (dispensar; pide cantidades). Equivalente desde `Main`:

```csharp
HardwareTests.Test_PingMonederos();
HardwareTests.Test_DispensarMonedas(2, 1); // dos monedas de $1, una de $5
```

## Criterios de aceptación

| ID | Criterio | Cómo se verifica |
| :-- | :-- | :-- |
| M-01 | El puerto COM abre sin excepción | Criterio automático |
| M-02 | Cada hopper responde ACK a Simple Poll en su dirección | Criterio automático |
| M-03 | Se lee categoría, código de producto, revisión de software y número de serie | Salida de consola (la categoría suele ser `Payout`) |
| M-04 | `Enable Hopper` y `Dispense` responden ACK (no NAK) | Criterio automático |
| M-05 | Al terminar, `Request hopper status` reporta `cantidad` monedas pagadas, 0 sin pagar y 0 restantes | Criterio automático |
| M-06 | Las monedas físicas caídas coinciden con lo pedido, y cada hopper entrega su denominación | Confirmación por consola |
| M-07 | Dispensar solo $1 (5, 0) y solo $5 (0, 2) por separado funciona; ambos hoppers a la vez también | Tres ejecuciones |
| M-08 | Velocidad coherente con ~3 monedas/s | Observación |
| M-09 | *(negativa)* Con el hopper de $1 vacío o casi vacío, pedir más monedas de las que tiene reporta `sin pagar > 0` y falla de forma controlada, sin colgarse ni marcar éxito | Requiere dejar pocas monedas. El TAC §14 exige probar "monedero $1 agotado" |

**Aprobado** = M-01 a M-08 (M-09 es recomendable: el TAC exige que una entrega parcial nunca se dé por completa). Guardar la trama hexadecimal de cualquier NAK o timeout.

## Supuestos y riesgos

- **Confirmado por el TAC §2:** Monedero 1 entrega MXN 1 y Monedero 2 entrega MXN 5; la configuración de `Hoppers` coincide.
- **Dispense (167), confirmado por el fabricante:** 9 bytes de datos = 8 en `00` + cantidad (1–255), sin cifrado (modo por defecto). El MK4 responde con 1 byte de datos (contador de eventos), por eso no se exige ACK vacío. Sondear `166` cada 200 ms con reintentos (~10 seguidos antes de declarar falla). El BEEX recomienda usar una librería ccTalk de código abierto; este código usa una implementación mínima propia, ya validada contra el manual del fabricante.
- Las direcciones ccTalk se fijan por pines del hopper (tabla en "MK2 cctalk address.pdf": 3 = por defecto, 4, 5...). Cada hopper debe tener una distinta; si nadie responde, barrer 1–10 con Simple Poll.
- Conviene que el primer intento use cantidades pequeñas (1 moneda) y vigilar atascos.
- **`DtrEnable`/`RtsEnable` necesarios, confirmado en la máquina real (2026-10-16):** sin activarlos al abrir el puerto, Simple Poll no obtenía respuesta en ningún COM probado, aunque el puerto abría sin error — el adaptador RS232↔ccTalk de esta máquina alimenta o habilita su transceptor desde esas líneas, que .NET deja en `false` por defecto. Ya corregido en `CcTalk.cs`.
- **`Test_PingMonederos` confirmado en la máquina real (2026-10-16):** los dos hoppers responden en `COM1`, direcciones 3 y 4 (`APROBADO 2/2`). Falta ejecutar `Test_DispensarMonedas` frente al hardware.
- Mientras se buscaba el puerto correcto, un programa externo (SCH2 u otro) puede dejar el COM abierto en exclusiva: si `Test_PingMonederos` falla con "Apertura de COMx" (no con "sin respuesta"), revisar que no haya otra herramienta de prueba serial abierta al mismo tiempo.

## Evidencia a guardar

Salida de consola de ping y dispensado, video corto de la bandeja y las tramas crudas en caso de falla.
