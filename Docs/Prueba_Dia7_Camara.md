# Prueba Día 7 – Cámara USB (reconocimiento facial, item 12 BEEX)

> Esta prueba **no estaba en el alcance original** (no es uno de los 5 periféricos del documento de arranque del proyecto). Se agregó porque `Finso_Guia_Integracion_Hardware BEEX.pdf`, página 2, items 11 y 12, dice explícitamente que el item 12 **sí requiere integración** por parte del Agente (a diferencia del item 11, ver más abajo).

## Qué dice el BEEX de los items 11 y 12

| Item | Componente | Dictamen técnico del BEEX | ¿Se programa? |
| :-- | :-- | :-- | :-- |
| 11 | Camera+NVR for face & cash acceptor (Hikvision: NVR DS-7604NI-Q1 + cámaras pin-hole IP840 + disco 1TB) | "Ecosistema Autónomo Cerrado... No requiere que el Agente gestione el almacenamiento. Únicamente si requieres visualización en vivo dentro de la app, se consumirá el API de red local de Hikvision." | **No**, salvo que se pida video en vivo dentro de la app (API ISAPI de Hikvision) — fuera de alcance por ahora, sin código ni prueba para esto |
| 12 | Camera (face recognition) GDZS-DC004-MG3021, USB nativa | "Dispositivo de Video Estándar: Es detectada nativamente por el Kernel de Windows 11 IoT como una cámara web genérica USB. Se integra de manera directa utilizando librerías estándar de captura multimedia de .NET o wrappers de OpenCV." | **Sí** — esta prueba |

## Objetivo

Confirmar que Windows detecta la cámara USB del item 12 como webcam genérica y que se puede abrir y capturar una imagen fija desde el Agente, sin reconocimiento facial todavía (eso queda para cuando se integre el SDK/servicio de reconocimiento que use la app oficial).

## Protocolo / librería

- **OpenCvSharp** (`VideoCapture`/`Mat`/`Cv2.ImWrite`), el wrapper de OpenCV que el propio BEEX sugiere para este item. A diferencia del resto de `Protocolos/` (ccTalk, ESC/POS, SDKs nativos por `[DllImport]`), aquí no hay un protocolo propietario que envolver: la cámara ya se expone como dispositivo de captura estándar de Windows, así que el código vive directo en `Tests/Camara.cs`.
- Paquetes NuGet agregados: `OpenCvSharp4` y `OpenCvSharp4.runtime.win` (trae los binarios nativos para win-x86 y win-x64; el proyecto compila x86 por la DLL de GRG, y el runtime de .NET resuelve el nativo x86 automáticamente).

## Precondiciones

- Cámara GDZS-DC004-MG3021 conectada por USB y reconocida por Windows como cámara (verificar en *Administrador de dispositivos* o la app Cámara de Windows).
- Si hay más de una cámara USB en la máquina (por ejemplo, alguna integrada en un monitor), confirmar el índice correcto en `Camara.IndiceDispositivo` — por defecto es `0` (la primera que detecta OpenCV), sin garantía de que coincida con el orden físico.
- Buena iluminación frente a la cámara para la captura de prueba.

## Configuración

```json
"Camara": {
  "IndiceDispositivo": 0,
  "CarpetaCapturas": "Capturas"
}
```

## Código (`Tests/Camara.cs`)

```csharp
using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using OpenCvSharp;

namespace IntegracionCajeroPruebas.Tests;

// Item 12 del BEEX, página 2: "Camera (face recognition)" GDZS-DC004-MG3021. El propio dictamen técnico
// dice que es una "cámara USB nativa... detectada por el Kernel de Windows 11 IoT como webcam genérica" y
// que se integra "utilizando librerías estándar de captura multimedia de .NET o wrappers de OpenCV" — por
// eso se usa OpenCvSharp (VideoCapture) en vez de un protocolo propio como en el resto de Protocolos/.
// No incluye reconocimiento facial: solo valida que se puede abrir la cámara y capturar una imagen fija.
public static partial class HardwareTests
{
    public static void Test_PingCamara()
    {
        var cfg = HardwareConfig.Actual.Camara;
        var r = new Reporte($"Cámara USB - Ping (dispositivo {cfg.IndiceDispositivo})");
        try
        {
            using var captura = new VideoCapture(cfg.IndiceDispositivo);
            r.Criterio("La cámara se abre (VideoCapture.IsOpened)", captura.IsOpened());
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    public static void Test_CapturarRostro()
    {
        var cfg = HardwareConfig.Actual.Camara;
        var r = new Reporte($"Cámara USB (item 12, reconocimiento facial) - dispositivo {cfg.IndiceDispositivo}");
        try
        {
            using var captura = new VideoCapture(cfg.IndiceDispositivo);
            if (!r.Criterio("La cámara se abre (VideoCapture.IsOpened)", captura.IsOpened())) { r.Finalizar(); return; }

            Consola.Asistente("Párese frente a la cámara, mirando al lente, con buena luz. Avise cuando esté listo.");

            using var frame = new Mat();
            var leido = false;
            // Los primeros frames de muchas webcams llegan vacíos o negros mientras el sensor ajusta
            // exposición/balance de blancos; se reintenta en vez de fallar con el primer Read().
            for (var intento = 0; intento < 10 && !leido; intento++)
            {
                leido = captura.Read(frame) && !frame.Empty();
                if (!leido) Thread.Sleep(100);
            }
            if (!r.Criterio("Se capturó un frame no vacío", leido)) { r.Finalizar(); return; }

            Directory.CreateDirectory(cfg.CarpetaCapturas);
            var ruta = Path.Combine(cfg.CarpetaCapturas, $"captura_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
            Cv2.ImWrite(ruta, frame);
            // Sin confirmación oral del asistente: la consola corre en la misma IoT, así que el desarrollador
            // revisa el archivo directamente en Capturas/ en vez de preguntarle a alguien que no puede verlo.
            r.Criterio($"Imagen guardada en {ruta} ({frame.Width}x{frame.Height}) — revisar visualmente", File.Exists(ruta));
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }
}
```

## Cómo ejecutar

Menú `13` (captura) o `19` (ping: solo abre el dispositivo, sin guardar nada). Equivalente desde `Main`:

```csharp
HardwareTests.Test_CapturarRostro();
HardwareTests.Test_PingCamara();
```

## Modalidad remota (desarrollador a distancia + asistente en sitio)

Esta prueba corre en la propia IoT, y el desarrollador tiene acceso directo al directorio de capturas (sesión local, carpeta compartida o similar) — no depende de que el asistente le describa lo que ve. Por eso el único rol del asistente aquí es físico: pararse frente al lente. No hay confirmación oral que pedirle después, porque el desarrollador revisa el `.jpg` resultante directamente.

| Momento | Qué pasa en consola | Qué hace el asistente |
| :-- | :-- | :-- |
| Antes de capturar | "Párese frente a la cámara, mirando al lente, con buena luz. Avise cuando esté listo." | Se posiciona y dice "listo" |
| Después de capturar | (nada — no se le pregunta nada) | El desarrollador abre `Capturas/` y revisa el archivo directamente |

No hay `AntesDeMover`: no hay ningún mecanismo, solo pararse frente a un lente.

## Criterios de aceptación

| ID | Criterio | Cómo se verifica |
| :-- | :-- | :-- |
| C-01 | `VideoCapture.IsOpened()` devuelve verdadero | Criterio automático |
| C-02 | Se captura un frame no vacío en 10 intentos (1s) | Criterio automático |
| C-03 | La imagen se guarda como `.jpg` en `Camara.CarpetaCapturas` | Criterio automático (`File.Exists`) |
| C-04 | La imagen guardada muestra un rostro reconocible, bien iluminado y enfocado | Inspección manual del archivo por el desarrollador, directo en la IoT (no automatizable desde la consola) |

**Aprobado** = C-01 a C-04. C-04 requiere que el desarrollador abra el `.jpg` en `Capturas/` y lo confirme visualmente — ya no se depende de una confirmación oral del asistente para esto.

## Supuestos y riesgos

- Sin probar aún contra la cámara real (2026-10-15).
- No se confirmó qué backend de captura usa OpenCvSharp por defecto en esta máquina (DirectShow vs. Media Foundation) ni si hace falta fijarlo explícitamente si el dispositivo no se detecta con el backend automático.
- `IndiceDispositivo = 0` es un supuesto: si hay otra cámara o dispositivo de captura en el sistema, el índice correcto puede ser otro. Falta validarlo en la máquina real (conectar solo esta cámara en la primera prueba, o enumerar dispositivos).
- Esta prueba no hace reconocimiento facial ni usa el sensor infrarrojo (300W luz visible / 200W IR) de la cámara: solo valida la ruta de captura de imagen. El reconocimiento facial en sí es responsabilidad de un SDK/servicio que todavía no se ha definido, fuera del alcance de este harness.
- El item 11 (NVR Hikvision) queda fuera de esta prueba por completo: no se escribió código ni se planeó prueba para él, según el propio dictamen del BEEX. Si en el futuro se pide video en vivo dentro de la app, ahí sí habría que investigar el API ISAPI de Hikvision — no investigado todavía.

## Evidencia a guardar

El archivo `.jpg` capturado (carpeta `Capturas/`) y la bitácora de la sesión con el resultado `APROBADO`/`RECHAZADO`.
