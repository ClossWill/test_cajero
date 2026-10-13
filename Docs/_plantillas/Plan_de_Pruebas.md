# Plan de Pruebas de Hardware – Agente ATM FINSO

> **Alcance:** aplicación de consola (.NET 8) que valida la comunicación directa con cada periférico. **No** es la aplicación ni un middleware: sin lógica de negocio, sin UI, sin base de datos. Cada prueba es un método `Test_*` que se lanza desde un menú.
> **Referencia de hardware:** `especificaciones_de_hardware_finso.md` (HZ20260125).

## 1. Cronograma

| Día | Periférico | Protocolo / librería | Métodos `Test_*` | Documento |
| :-- | :-- | :-- | :-- | :-- |
| 0 | Entorno y estructura | .NET 8, `System.IO.Ports` | – (menú) | este documento |
| 1 | Recicladora GRG 9250N | `CRM9250NDev.dll` vía `CRM9250N.cs` | `Test_InicializarRecicladora`, `Test_AceptarBilletes`, `Test_DispensarBilletes` | [Prueba_Dia1_Recicladora.md](Prueba_Dia1_Recicladora.md) |
| 2 | Monederos MK4 (x2) | ccTalk sobre COM | `Test_PingMonederos`, `Test_DispensarMonedas` | [Prueba_Dia2_Monederos.md](Prueba_Dia2_Monederos.md) |
| 3 | Impresora KP-80A | ESC/POS (spooler USB o COM) | `Test_ImprimirVoucherPrueba` | [Prueba_Dia3_Impresora.md](Prueba_Dia3_Impresora.md) |
| 4 | Módulo ALB02 (sensores) | Serial reactivo (COM) | `Test_CalibrarSensores`, `Test_EscucharSensores` | [Prueba_Dia4_Sensores.md](Prueba_Dia4_Sensores.md) |
| 5 | Escáner MEPR500+ | SDK propietario | `Test_EscanearDocumento` | [Prueba_Dia5_Escaner.md](Prueba_Dia5_Escaner.md) |
| 6 | Cierre y regresión | – | Todos, en secuencia | sección 5 |

Orden por dependencias: la recicladora es el componente más crítico y el de más riesgo, por eso va primero. La impresora va antes que sensores y escáner porque sirve para imprimir la evidencia del resto. El escáner queda al final porque depende de recibir el SDK del fabricante.

## 2. Día 0 – Preparación del entorno

1. Instalar .NET 8 SDK en la máquina objetivo (Windows 11 IoT Enterprise LTSC 2024) o publicar con `dotnet publish -c Release -r win-x64 --self-contained`.
2. Identificar los puertos COM en *Administrador de dispositivos → Puertos (COM y LPT)* y anotarlos en `hardware.json` (recicladora no usa COM desde nuestro código; lo gestiona la DLL).
3. Copiar la carpeta `dll` del SDK de GRG junto al `.exe` (el `.csproj` lo hace solo desde `referencias_integracion\CRM9250NDev_V2.3.7.6\dll` del repo) **más `DevBaseDll.dll` y el driver USB de GRG**, que no vienen en el zip. La DLL es de 32 bits: el proyecto ya compila como x86 y la máquina IoT necesita el runtime x86 de .NET 8 (o publicar con `-r win-x86 --self-contained`).
4. Instalar el driver de la KP-80A y anotar el nombre exacto de la cola (`Get-Printer | Select Name`).
5. Verificar que ningún otro software (la app oficial, utilerías del fabricante) tenga abiertos los puertos.
6. Compilar y ejecutar: `dotnet run`. Debe aparecer el menú; las pruebas sin hardware conectado deben fallar de forma controlada.

### Estructura del proyecto

```
IntegracionCajeroPruebas.csproj
Program.cs                 menú de consola
hardware.json              puertos COM, direcciones y supuestos (se edita en la máquina)
CRM9250N.cs                fuente nativa del fabricante (sin modificar)
Config/HardwareConfig.cs   carga de hardware.json
Helpers/                   Consola (salida), Reporte (criterios), Native (marshaling)
Protocolos/                CcTalk, EscPos + ImpresoraRaw, SerialTramas
Tests/                     una clase parcial HardwareTests por periférico
Docs/                      estos documentos
```

### Program.cs (menú)

{{CODE:Program.cs}}

### Helpers compartidos

`Reporte` acumula los criterios de aceptación de cada prueba y emite `APROBADO`/`RECHAZADO` al final. Las confirmaciones físicas (¿cayeron las monedas?, ¿cortó el papel?) se piden por consola con `Consola.Confirmar`.

{{CODE:Helpers/Reporte.cs}}

{{CODE:Helpers/Native.cs}}

### Configuración (`hardware.json`)

{{CODE:hardware.json}}

## 3. Qué aportan los PDFs y qué sigue siendo supuesto

**Confirmado por `Finso_TAC_Logical_Tecnica.pdf` y `Finso_Guia_Integracion_Hardware BEEX.pdf`:**

- Mapa físico: Cassette 1 = USD 20, Cassette 2 = MXN 500, Cassette 3 = MXN 200, Cassette 4 = MXN 100; Monedero 1 = MXN 1, Monedero 2 = MXN 5; el resto de billetes válidos va al recycler. Está en `hardware.json` (`Casetes`, `Hoppers`).
- Escenarios de prueba que el TAC exige: billete inválido rechazado, billete válido no dispensable al recycler, monedero $1 agotado y dispensa parcial. Quedaron incorporados como criterios en los días 1 y 2.
- Regla del TAC: una orden de dispensa no equivale a entrega física. Por eso cada prueba imprime contadores y pide confirmación humana.
- Para el ALB02, el BEEX confirma que envía cadenas de bytes por COM y que importa el **cambio de estado**, que es lo que hace la escucha (ignora repeticiones idénticas).

**Los PDFs no incluyen bytes, funciones ni parámetros.** Para cerrar los puntos de la tabla faltan estos materiales, que el BEEX cita pero no se han recibido:

| Material | Lo que resuelve |
| :-- | :-- |
| Carpeta de GRG (demo WinForm y guía del SDK `CRM9250N`) | Puntos 1 y 2 |
| Tablas de Excel de ccTalk de la carpeta de integración | Puntos 3 y 4 |
| Documentación del ALB02 | Punto 5 |
| SDK y manual del MEPR500+ | Punto 6 |

**Discrepancia por aclarar:** el BEEX llama a los 4 casetes "de aceptación"; el TAC los usa como casetes de dispensa. El TAC §15 deja como decisión pendiente con el proveedor qué denominaciones se aceptan y cuáles se reciclan o dispensan.

**Supuestos aún abiertos** (marcados `VERIFICAR` en el código o en `hardware.json`):

| # | Supuesto | Dónde | Cómo validarlo |
| :-- | :-- | :-- | :-- |
| 1 | ~~Nombre lógico y código de éxito~~ **Confirmado:** `CRM9250NDev`, `rc == 0` (demo y PDF de GRG) | `Recicladora.*` | – |
| 2 | ~~`byDispType` e `Indice` de casete~~ **Confirmado:** `1` = por casete; RC1..RC4 = 1..4 | `TipoDispensa`, `Casetes[].Indice` | Probar con 1 billete por casete |
| 3 | Dispense de hopper = `[serie x3][cantidad]` sin cifrado | `Monederos.cs` | Manual MK4; si responde NAK, implementar Pump RNG (161) / cipher (160) |
| 4 | Direcciones ccTalk de los hoppers (3 y 4) | `Monederos.Hoppers` | Si no responden, hacer barrido de direcciones 1–10 |
| 5 | Formato de tramas del ALB02 | `Sensores.Tramas` | `Test_CalibrarSensores` |
| 6 | API del SDK del MEPR500+ | `EscanerMeprSdk` | Documentación y DLL del fabricante |
| 7 | ~~Bits de la DLL~~ **Confirmado:** x86. Falta `DevBaseDll.dll` en el zip del SDK | `.csproj` | El pre-vuelo del menú 1 lo detecta |

## 4. Convenciones de evidencia

Para cada prueba guardar en `Evidencia/DiaN/`: captura de la consola con el resultado `APROBADO`/`RECHAZADO`, foto o video corto del comportamiento físico y, en la impresora, el voucher físico. Las tramas crudas (ccTalk, ALB02) se pegan tal como aparecen en consola.

## 5. Día 6 – Cierre y regresión

1. Ejecutar las 9 opciones del menú en secuencia, sin reiniciar el programa; confirmar que no quedan puertos bloqueados entre pruebas.
2. Repetir 3 ciclos de aceptar/dispensar billetes y 3 de dispensar monedas.
3. Confirmar `hardware.json` final con los valores reales de la máquina.
4. Consolidar la lista de **métodos validados** para extraerlos a la aplicación oficial y la lista de **supuestos que resultaron falsos**.
5. Firmar el cierre solo si todos los criterios de los documentos de los días 1–5 están en `APROBADO`; el escáner no puede cerrarse mientras use el modo simulado.
