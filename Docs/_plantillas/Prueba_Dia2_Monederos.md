# Prueba Día 2 – Dispensadores de monedas MK4 (x2)

## Objetivo

Confirmar que el PC se comunica por puerto serial con los dos hoppers MK4 (ping y datos de identificación) y que se puede ordenar la entrega de N monedas de $1 y de $5, verificando el conteo reportado por el hopper y la caída física.

## Protocolo / librería

- **ccTalk** sobre puerto COM (8N1, normalmente 9600 baudios). Implementación mínima propia en `Protocolos/CcTalk.cs` (sin dependencias): trama `[destino][nº datos][origen=1][header][datos…][checksum]`, con suma total ≡ 0 mod 256. El código descarta el eco de la propia trama, habitual en la línea ccTalk de un solo hilo.
- Headers usados: `254` Simple poll, `245` categoría de equipo, `244` código de producto, `241` revisión de software, `242` número de serie, `164` Enable hopper, `167` Dispense hopper coins, `166` Request hopper status.
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
    { "Nombre": "MK4 #1 ($1 MXN)", "Denominacion": 1, "Puerto": "COM3", "Direccion": 3 },
    { "Nombre": "MK4 #2 ($5 MXN)", "Denominacion": 5, "Puerto": "COM3", "Direccion": 4 }
  ]
}
```

Si cada hopper está en un COM distinto, basta cambiar `Puerto`. El código abre cada puerto una sola vez aunque lo compartan.

## Código

### `Protocolos/CcTalk.cs`

{{CODE:Protocolos/CcTalk.cs}}

### `Tests/Monederos.cs`

{{CODE:Tests/Monederos.cs}}

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
- **Falta la fuente de los comandos:** el BEEX indica que los bytes específicos están en "las tablas de Excel de la carpeta de integración", que no se han recibido. Esas tablas deben contrastarse con `CcTalk.cs` antes de dar por buenos los puntos siguientes. Además el BEEX recomienda usar una librería ccTalk de código abierto; este código usa una implementación mínima propia.
- **Formato del comando Dispense (167):** se asume `[serie x3][cantidad]` sin cifrado. Si el MK4 responde NAK, requiere modo seguro (Pump RNG `161`, Request cipher key `160`); sería un trabajo adicional del día 2.
- Direcciones 3 y 4 son una suposición; si nadie responde, barrer las direcciones 1–10 con Simple Poll.
- Conviene que el primer intento use cantidades pequeñas (1 moneda) y vigilar atascos.

## Evidencia a guardar

Salida de consola de ping y dispensado, video corto de la bandeja y las tramas crudas en caso de falla.
