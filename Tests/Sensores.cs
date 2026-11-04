using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using IntegracionCajeroPruebas.Protocolos;

namespace IntegracionCajeroPruebas.Tests;

// Sensores del gabinete: solo hay dos, ambos cableados a las entradas de la tarjeta de 18 canales (la misma de los LEDs,
// COM2 @ 19200; el BEEX la llama ALB02). Se consultan con CMD 0x32: 6 bytes, uno por entrada, 0/1.
//   - Vibración: pulsos de ~0.35-0.5 s en cada golpe (salida monoestable).
//   - Presencia frente a la puerta: nivel sostenido mientras haya alguien o algo delante.
// No hay contacto de puerta abierta. Entradas, nivel activo y filtros están en la sección Sensores de hardware.json.
public static partial class HardwareTests
{
    private static SensoresConfig Sensores => HardwareConfig.Actual.Sensores;

    // PARA 0x00 devuelve las 6 entradas; PARA 0x31 la tarjeta lo rechaza ('N' 05). Confirmado en la máquina real (2026-10-28).
    private const byte ParaTodasLasEntradas = 0x00;
    private const int PeriodoSondeoMs = 150;

    public static void Test_EscucharSensores()
    {
        var leds = HardwareConfig.Actual.Leds;
        Consola.Titulo($"Sensores - Presencia (E{Sensores.EntradaPresencia}) y vibración (E{Sensores.EntradaVibracion}) en {leds.Puerto} @ {leds.BaudRate}");
        Consola.Info("Acérquese al frente de la puerta y golpee el gabinete. Presione Q para terminar.");

        using var bus = new LedControlBus(leds.Puerto, leds.BaudRate);
        var indiceMax = Math.Max(Sensores.EntradaPresencia, Sensores.EntradaVibracion);

        bool? vibracionPrevia = null;
        var ultimoFlancoVibracion = DateTime.MinValue;
        var golpesAgrupados = 0;

        bool? presenciaPublicada = null;   // último estado ya informado
        bool? presenciaCandidata = null;   // estado visto en el sondeo anterior; se publica al repetirse (filtro de rebote)

        var fallas = 0;
        var fallaInformada = false;

        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q))
        {
            var r = bus.Enviar(LedControlBus.CConsultarEntradas, ParaTodasLasEntradas, 300);

            // Un timeout aislado ocurre con el ruido de los golpes (2 en ~3.5 min en la máquina): solo es falla si se repite.
            if (r is null || !r.Ok || r.Datos.Length < indiceMax)
            {
                if (++fallas >= Sensores.FallasParaAlarma && !fallaInformada)
                {
                    Consola.Error($"La tarjeta no responde ({fallas} fallos seguidos): {bus.UltimoError}");
                    fallaInformada = true;
                }
                Thread.Sleep(PeriodoSondeoMs);
                continue;
            }
            if (fallaInformada) Consola.Info("La tarjeta volvió a responder.");
            fallas = 0;
            fallaInformada = false;

            var hora = DateTime.Now.ToString("HH:mm:ss.fff");

            var vibrando = r.Datos[Sensores.EntradaVibracion - 1] == Sensores.NivelActivoVibracion;
            if (vibracionPrevia == false && vibrando)
            {
                // Un golpe puede generar varios pulsos seguidos; solo el primero de la ráfaga se anuncia.
                if ((DateTime.Now - ultimoFlancoVibracion).TotalMilliseconds > Sensores.AgruparVibracionMs)
                {
                    Consola.Error($"[{hora}] ¡VIBRACIÓN DETECTADA!" + (golpesAgrupados > 0 ? $"  (+{golpesAgrupados} pulsos en la ráfaga anterior)" : ""));
                    golpesAgrupados = 0;
                }
                else golpesAgrupados++;
            }
            if (vibrando) ultimoFlancoVibracion = DateTime.Now;
            vibracionPrevia = vibrando;

            var hayPresencia = r.Datos[Sensores.EntradaPresencia - 1] == Sensores.NivelActivoPresencia;
            if (hayPresencia == presenciaCandidata && hayPresencia != presenciaPublicada)
            {
                presenciaPublicada = hayPresencia;
                if (hayPresencia) Consola.Error($"[{hora}] ¡PRESENCIA DETECTADA!");
                else Consola.Info($"[{hora}] Zona libre");
            }
            presenciaCandidata = hayPresencia;

            Thread.Sleep(PeriodoSondeoMs);
        }
    }

    // Exploratoria: muestra la respuesta cruda de las 6 entradas y cuál cambia. Sirve para re-mapear si se cambian los sensores.
    // Comparte COM2 con los LEDs: no correrla a la vez que la prueba de LEDs.
    public static void Test_EscucharSensoresTarjeta()
    {
        var leds = HardwareConfig.Actual.Leds;
        Consola.Titulo($"Sensores por tarjeta de 18 canales - Entradas (CMD 0x32) en {leds.Puerto} @ {leds.BaudRate}");
        Consola.Info("Acérquese a la puerta y golpee el gabinete. Se imprime solo cuando la respuesta cambia. Q para terminar.");

        using var bus = new LedControlBus(leds.Puerto, leds.BaudRate);
        byte[]? anterior = null;
        var fallas = 0;

        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q))
        {
            var r = bus.Enviar(LedControlBus.CConsultarEntradas, ParaTodasLasEntradas, 300);
            if (r is null)
            {
                if (fallas++ % 25 == 0) Consola.Error($"Sin respuesta: {bus.UltimoError}");
                Thread.Sleep(200);
                continue;
            }
            fallas = 0;

            if (!r.Ok)
            {
                Consola.Error($"La tarjeta rechazó CMD 0x32 con PARA 0x{ParaTodasLasEntradas:X2} (respuesta 'N': {Consola.Hex(r.Datos)}).");
                return;
            }

            if (anterior is null || !r.Datos.AsSpan().SequenceEqual(anterior))
            {
                var estado = string.Join(' ', r.Datos.Select((v, i) => $"E{i + 1}={v}"));
                var detalle = "";
                if (anterior is not null && anterior.Length == r.Datos.Length)
                    detalle = "  << " + string.Join(", ", Enumerable.Range(0, r.Datos.Length)
                        .Where(i => r.Datos[i] != anterior[i])
                        .Select(i => $"ENTRADA {i + 1}: {anterior[i]}->{r.Datos[i]}"));
                Consola.Ok($"[{DateTime.Now:HH:mm:ss.fff}] {estado}   (RESP: {Consola.Hex(r.Datos)}){detalle}");
                anterior = r.Datos;
            }
            Thread.Sleep(PeriodoSondeoMs);
        }
    }
}
