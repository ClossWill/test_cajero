# Prueba Día 5 – Escáner de pasaporte / INE MEPR500+

## Objetivo

Inicializar el SDK del escáner, detectar un documento colocado sobre la ventana, disparar la lectura y mostrar por consola **Nombre, ID y Nacionalidad**.

## Protocolo / librería

- **SDK propietario del fabricante** (OCR en segundo plano y lectura RFID/infrarrojo, ICAO 9303). El SDK no está en el repositorio, así que el código separa la prueba del SDK mediante la interfaz `IEscanerSdk`:
  - `EscanerMeprSdk`: adaptador real, **pendiente de completar** cuando se reciba la DLL y su documentación (lanza `NotImplementedException` con instrucciones).
  - `EscanerSimulado`: devuelve datos ficticios para ejercitar el flujo. Con `Escaner.Simulado = true` la prueba **no puede aprobarse**: el criterio de coincidencia con el documento físico queda en falla a propósito.

## Precondiciones

- SDK instalado (drivers USB, DLL, licencias) y escáner reconocido por Windows.
- Documentos de prueba: un pasaporte (con MRZ) y una INE.
- Definir en la primera sesión: qué DLL expone el SDK, su arquitectura (x86/x64) y si trae un wrapper .NET.

## Configuración

```json
"Escaner": {
  "Simulado": false,
  "TimeoutSegundos": 30
}
```

## Código (`Tests/Escaner.cs`)

{{CODE:Tests/Escaner.cs}}

## Cómo completar el adaptador real

1. Copiar la DLL del SDK junto al `.exe` y declarar los `[DllImport]` (o referenciar el wrapper .NET) dentro de `EscanerMeprSdk`.
2. Implementar `Inicializar` (abrir dispositivo), `EsperarDocumento` (sondeo o evento de presencia con el timeout dado), `Leer` (disparar la lectura y mapear campos a `DocumentoLeido`) y `Dispose` (liberar el dispositivo).
3. Poner `"Simulado": false` y ejecutar.

## Cómo ejecutar

Menú `9`. Equivalente desde `Main`:

```csharp
HardwareTests.Test_EscanearDocumento();
```

## Criterios de aceptación

| ID | Criterio | Cómo se verifica |
| :-- | :-- | :-- |
| E-01 | El SDK se inicializa y detecta el dispositivo | Criterio automático |
| E-02 | El documento colocado se detecta dentro del timeout | Criterio automático |
| E-03 | Se extraen Nombre, ID y Nacionalidad no vacíos de un pasaporte | Salida de consola |
| E-04 | Se extraen los mismos campos de una INE | Salida de consola |
| E-05 | Los datos coinciden con el documento físico (comparación manual) | Confirmación por consola |
| E-06 | Cinco lecturas consecutivas sin reiniciar, incluida una con documento mal colocado (falla controlada) | Prueba repetida |
| E-07 | La prueba corrió con `Simulado = false` | Revisar la configuración |

**Aprobado** = E-01 a E-07.

## Supuestos y riesgos

- Todo el adaptador real está pendiente del SDK; este es el único día cuyo código no está completo.
- No se han confirmado los campos que entrega el SDK ni si el OCR cubre INE además de pasaportes.
- La lectura RFID puede requerir tiempo adicional y la contraseña/BAC (número de documento, fecha de nacimiento y vencimiento).

## Evidencia a guardar

Salida de consola por cada documento (con datos sensibles tachados en cualquier reporte compartido) y fotografía del documento usado.
