using System.Collections.Concurrent;
using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using IntegracionCajeroPruebas.Helpers;

namespace IntegracionCajeroPruebas.Protocolos;

public sealed record ResultadoQr(bool Ok, string Mensaje);

/// <summary>
/// Sesión del lector XZG004. Port del ejemplo del proveedor (referencias_integracion\QR\xzg-example\Xzg004\Scanner.cs, verificado
/// en la máquina). Iniciar abre el puerto, guarda la configuración de luz (el módulo se REINICIA y el COM desaparece unos
/// segundos), lo reabre y envía SW00001 (lectura continua); un hilo propio va mostrando cada QR que llega. Detener apaga la luz,
/// envía SWFFFFF y cierra el puerto. Los comandos van dentro de una trama (ver <see cref="XzgTrama"/>).
/// </summary>
public sealed class XzgLector : IDisposable
{
    private static readonly TimeSpan Inactividad = TimeSpan.FromMilliseconds(120);
    private readonly object _operacion = new();
    private Hilo? _hilo;
    private bool _liberado;
    private int _lecturas;

    public bool Activo { get { lock (_operacion) return _hilo?.Subproceso.IsAlive == true; } }

    /// <summary>QR recibidos desde el último Iniciar.</summary>
    public int Lecturas => Volatile.Read(ref _lecturas);

    public ResultadoQr Iniciar(string? puerto, int baudRate, int luz)
    {
        try
        {
            var nombre = string.IsNullOrWhiteSpace(puerto) ? BuscarPuertoVirtual() : puerto.Trim();
            return new ResultadoQr(true, Comenzar(nombre, baudRate, luz));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or TimeoutException or UnauthorizedAccessException)
        {
            Cerrar(); // un arranque a medias no debe dejar el puerto tomado
            return new ResultadoQr(false, ex.Message);
        }
    }

    public ResultadoQr Detener()
    {
        try { return new ResultadoQr(true, Terminar()); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or TimeoutException or UnauthorizedAccessException)
        {
            return new ResultadoQr(false, ex.Message);
        }
    }

    public void Dispose()
    {
        if (_liberado) return;
        _liberado = true;
        lock (_operacion) CerrarHilo();
    }

    /// <summary>COM virtual que expone el lector en modo USB CDC (VID_26F1 PID_D001), tal como lo busca el ejemplo del proveedor.</summary>
    public static string BuscarPuertoVirtual()
    {
        using var buscador = new ManagementObjectSearcher(
            "SELECT Name, PNPDeviceID FROM Win32_PnPEntity WHERE PNPDeviceID LIKE '%VID_26F1&PID_D001%'");
        foreach (ManagementObject item in buscador.Get())
        {
            var nombre = item["Name"] as string ?? "";
            var coincide = Regex.Match(nombre, @"\((COM\d+)\)", RegexOptions.IgnoreCase);
            if (coincide.Success) return coincide.Groups[1].Value;
        }
        throw new InvalidOperationException("No se encontró el puerto COM virtual del lector (VID_26F1 PID_D001). ¿Está en USB CDC y conectado?");
    }

    private string Comenzar(string puerto, int baudRate, int luz)
    {
        if (Activo) return "El puerto ya está abierto: la lectura ya está iniciada.";
        var modo = luz switch
        {
            1 => "blink",
            2 => "on",
            3 => "scan",
            _ => throw new InvalidOperationException("La luz debe ser 1 (parpadeo), 2 (siempre encendida) o 3 (encendida solo al leer)."),
        };

        Interlocked.Exchange(ref _lecturas, 0);
        Abrir(puerto, baudRate);
        var resultadoLuz = Ejecutar("luz", modo);
        // Guardar la luz reinicia el módulo: se cierra, se espera a que el COM vuelva y se reabre antes de leer.
        Cerrar();
        EsperarPuerto(puerto);
        Abrir(puerto, baudRate);
        var lectura = Ejecutar("leer", "");
        return resultadoLuz + "\n" + lectura;
    }

    private string Terminar()
    {
        if (!Activo)
        {
            Cerrar();
            return "El puerto ya estaba cerrado.";
        }

        string luz;
        try { luz = Ejecutar("luz", "off"); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or TimeoutException)
        {
            Cerrar();
            throw;
        }

        string? detenido = null;
        try { detenido = Ejecutar("detener", ""); }
        catch (Exception ex) when (PuertoCaido(ex.Message))
        {
            // Apagar la luz también reinicia el módulo: que SWFFFFF no tenga respuesta es esperable.
        }
        catch (Exception)
        {
            Cerrar();
            throw;
        }

        Cerrar();
        var mensajes = new List<string> { luz };
        if (detenido != null) mensajes.Add(detenido);
        mensajes.Add($"Desconectado. QR leídos en la sesión: {Lecturas}.");
        return string.Join("\n", mensajes);
    }

    private void Abrir(string nombre, int baudRate)
    {
        lock (_operacion)
        {
            SerialPort puerto;
            try
            {
                puerto = new SerialPort(nombre, baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 40,
                    WriteTimeout = 1000,
                    DtrEnable = true,
                    RtsEnable = true,
                };
                puerto.Open();
                puerto.DiscardInBuffer();
                puerto.DiscardOutBuffer();
            }
            catch (Exception ex)
            {
                throw new IOException($"No se pudo abrir {nombre}: {ex.Message}");
            }
            CerrarHilo();
            var hilo = new Hilo(this, puerto);
            _hilo = hilo;
            hilo.Subproceso.Start();
        }
    }

    private void Cerrar()
    {
        lock (_operacion) CerrarHilo();
    }

    private void CerrarHilo()
    {
        var hilo = _hilo;
        _hilo = null;
        if (hilo == null) return;
        try { hilo.Trabajos.Add(new TrabajoCerrar()); } catch (InvalidOperationException) { }
        hilo.Subproceso.Join(TimeSpan.FromSeconds(20));
    }

    private static void EsperarPuerto(string puerto)
    {
        var limite = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < limite)
        {
            try
            {
                if (string.Equals(BuscarPuertoVirtual(), puerto, StringComparison.OrdinalIgnoreCase))
                {
                    Thread.Sleep(800);
                    return;
                }
            }
            catch (InvalidOperationException) { }
            Thread.Sleep(400);
        }
        throw new InvalidOperationException($"{puerto} no volvió tras el reinicio del lector.");
    }

    private string Ejecutar(string accion, string argumento)
    {
        lock (_operacion)
        {
            var hilo = _hilo ?? throw new InvalidOperationException("No conectado");
            switch (accion)
            {
                case "luz":
                {
                    // Bloque de configuración en lote (BATCHST ... BATCHET): tonos apagados + modo de luz.
                    var cuerpos = new[] { "S020029", "S010029", CodigoLuz(argumento) };
                    var carga = EnvolverLote(cuerpos.Select(c => $"$>:{c}.<$;"));
                    var respuesta = Intercambiar(hilo, Encoding.ASCII.GetBytes(carga), escuchaMs: 400);
                    return ResultadoAjustes("Luz", respuesta);
                }
                case "leer":
                {
                    Thread.Sleep(400);
                    hilo.Trabajos.Add(new TrabajoReiniciar());
                    Thread.Sleep(50);
                    Respuesta? ultima = null;
                    for (var intento = 0; intento < 3; intento++)
                    {
                        if (intento > 0) Thread.Sleep(400);
                        ultima = Intercambiar(hilo, "SW00001"u8.ToArray(), escuchaMs: 200);
                        var confirmado = ultima.Ack == true;
                        var fallo = ultima.ErrorIo != null && ultima.Ack is null;
                        if (confirmado || fallo) break;
                    }
                    return ResultadoDirecto("SW00001", ultima!, "Lectura continua iniciada");
                }
                case "detener":
                {
                    var respuesta = Intercambiar(hilo, "SWFFFFF"u8.ToArray(), escuchaMs: 400);
                    return ResultadoDirecto("SWFFFFF", respuesta, "Lectura detenida");
                }
                default:
                    throw new InvalidOperationException($"Acción desconocida: {accion}");
            }
        }
    }

    private static Respuesta Intercambiar(Hilo hilo, byte[] carga, int escuchaMs)
    {
        var listo = new TaskCompletionSource<Respuesta>(TaskCreationOptions.RunContinuationsAsynchronously);
        try { hilo.Trabajos.Add(new TrabajoIntercambio(carga, escuchaMs, listo)); }
        catch (InvalidOperationException) { throw new InvalidOperationException("Conexión cerrada"); }
        if (!listo.Task.Wait(TimeSpan.FromSeconds(15)))
            throw new TimeoutException("Sin respuesta del módulo en 15 s");
        return listo.Task.Result;
    }

    private static string ResultadoDirecto(string carga, Respuesta r, string exito)
    {
        if (r.ErrorIo != null && r.Ack != true && r.Lectura == null)
            throw new IOException($"Falló la comunicación: {r.ErrorIo}");
        if (r.Ack == false)
            throw new InvalidOperationException($"El módulo rechazó {carga} (NAK)");
        return exito;
    }

    private static string ResultadoAjustes(string resumen, Respuesta r)
    {
        if (r.ErrorIo != null)
        {
            if (r.Ack == true) return $"{resumen} enviada. El módulo se está reiniciando.";
            throw new IOException($"Falló la comunicación: {r.ErrorIo}");
        }
        return r.Ack switch
        {
            false => throw new InvalidOperationException($"El módulo rechazó la configuración de {resumen.ToLowerInvariant()} (NAK)"),
            null => throw new InvalidOperationException($"{resumen}: sin respuesta. Si el módulo se estaba reiniciando, espere e intente de nuevo."),
            _ => $"{resumen} guardada. El módulo se reinicia al cerrar la configuración.",
        };
    }

    private static bool PuertoCaido(string error) =>
        error == "No conectado"
        || error == "Conexión cerrada"
        || error.StartsWith("Falló la comunicación", StringComparison.Ordinal)
        || error.StartsWith("Sin respuesta del módulo", StringComparison.Ordinal);

    private static string CodigoLuz(string modo) => modo switch
    {
        "off" => "S0C0000",
        "scan" => "S0C0400",
        "on" => "S0C0800",
        "blink" => "S0C0C00",
        _ => throw new InvalidOperationException("La luz debe ser off, scan, on o blink"),
    };

    private static string EnvolverLote(IEnumerable<string> cuerpos)
    {
        var texto = new StringBuilder("$>:BATCHST.<$$>:S01010F.<$;");
        foreach (var cuerpo in cuerpos) texto.Append(cuerpo);
        texto.Append("$>:S01000F.<$;$>:BATCHET.<$;");
        return texto.ToString();
    }

    private void Recordar(string texto)
    {
        Interlocked.Increment(ref _lecturas);
        Consola.Ok($"[{DateTime.Now:HH:mm:ss.fff}] QR: {texto}  ({texto.Length} caracteres)");
    }

    private sealed class Respuesta
    {
        public bool? Ack { get; set; }
        public string? Lectura { get; set; }
        public string? ErrorIo { get; set; }
    }

    private abstract class Trabajo;
    private sealed class TrabajoCerrar : Trabajo;
    private sealed class TrabajoReiniciar : Trabajo;
    private sealed class TrabajoIntercambio(byte[] carga, int escuchaMs, TaskCompletionSource<Respuesta> listo) : Trabajo
    {
        public byte[] Carga { get; } = carga;
        public int EscuchaMs { get; } = escuchaMs;
        public TaskCompletionSource<Respuesta> Listo { get; } = listo;
    }

    /// <summary>Hilo dueño del puerto: atiende los comandos pendientes y, mientras no hay ninguno, lee lo que mande el lector.</summary>
    private sealed class Hilo
    {
        private readonly SerialPort _puerto;
        private readonly XzgLector _dueño;
        public BlockingCollection<Trabajo> Trabajos { get; } = new();
        public Thread Subproceso { get; }

        public Hilo(XzgLector dueño, SerialPort puerto)
        {
            _dueño = dueño;
            _puerto = puerto;
            Subproceso = new Thread(Ciclo) { IsBackground = true, Name = "xzg-io" };
        }

        private void Ciclo()
        {
            var analizador = new XzgParser();
            try
            {
                while (true)
                {
                    if (!Trabajos.TryTake(out var trabajo, 20))
                    {
                        if (!Bombear(analizador, null, new Respuesta())) break;
                        continue;
                    }
                    if (trabajo is TrabajoCerrar) break;
                    if (trabajo is TrabajoReiniciar)
                    {
                        analizador.Reiniciar();
                        continue;
                    }
                    if (trabajo is TrabajoIntercambio intercambio)
                    {
                        var respuesta = Intercambio(analizador, intercambio.Carga, intercambio.EscuchaMs);
                        var fallo = respuesta.ErrorIo != null && respuesta.Ack is null && respuesta.Lectura == null;
                        intercambio.Listo.TrySetResult(respuesta);
                        if (fallo) break;
                    }
                }
            }
            finally
            {
                try { if (_puerto.IsOpen) _puerto.Close(); } catch { /* al cerrar, el puerto puede haber desaparecido */ }
                _puerto.Dispose();
                Trabajos.CompleteAdding();
            }
        }

        // Envía un comando en su trama y espera hasta 1.2 s su ACK/NAK; luego sigue escuchando escuchaMs más.
        private Respuesta Intercambio(XzgParser analizador, byte[] carga, int escuchaMs)
        {
            var respuesta = new Respuesta();
            try
            {
                var trama = XzgTrama.Codificar(carga);
                _puerto.Write(trama, 0, trama.Length);
                Consola.Paso($"[{DateTime.Now:HH:mm:ss.fff}] TX: {Texto(carga)}  ({trama.Length} bytes)");
            }
            catch (Exception ex)
            {
                respuesta.ErrorIo = ex.Message;
                return respuesta;
            }

            var limiteAck = DateTime.UtcNow + TimeSpan.FromMilliseconds(1200);
            while (DateTime.UtcNow < limiteAck && respuesta.Ack is null)
            {
                if (!Bombear(analizador, carga, respuesta)) return respuesta;
                respuesta.Ack ??= analizador.TomarAcuse();
            }
            var cola = DateTime.UtcNow + TimeSpan.FromMilliseconds(escuchaMs);
            while (DateTime.UtcNow < cola)
            {
                if (!Bombear(analizador, carga, respuesta)) break;
                respuesta.Ack ??= analizador.TomarAcuse();
            }
            Consola.Info($"      RX: {(respuesta.Ack is null ? "(sin ACK ni NAK)" : respuesta.Ack == true ? "ACK (05)" : "NAK (15)")}");
            return respuesta;
        }

        private bool Bombear(XzgParser analizador, byte[]? enviado, Respuesta respuesta)
        {
            var buffer = new byte[512];
            try
            {
                var n = _puerto.Read(buffer, 0, buffer.Length);
                if (n > 0) analizador.Agregar(buffer.AsSpan(0, n));
            }
            catch (TimeoutException)
            {
                analizador.CerrarPorInactividad(Inactividad);
            }
            catch (Exception ex)
            {
                respuesta.ErrorIo = ex.Message;
                return false;
            }
            return Vaciar(analizador, enviado, respuesta);
        }

        private bool Vaciar(XzgParser analizador, byte[]? enviado, Respuesta respuesta)
        {
            while (analizador.TomarTrama() is { } trama)
            {
                // Por cada trama que manda el módulo espera un ACK del anfitrión.
                try { _puerto.Write(new[] { XzgTrama.Ack }, 0, 1); } catch { /* si falla el acuse, lo ya leído sigue valiendo */ }
                if (enviado != null && trama.AsSpan().SequenceEqual(enviado)) continue;
                var texto = XzgTrama.Decodificar(trama);
                if (EsEco(trama)) continue;
                if (texto.Length == 0) continue;
                Registrar(texto, respuesta);
            }
            while (analizador.TomarLectura() is { } texto)
                Registrar(texto, respuesta);
            return true;
        }

        private void Registrar(string texto, Respuesta respuesta)
        {
            respuesta.Lectura ??= texto;
            _dueño.Recordar(texto);
        }

        private static bool EsEco(byte[] trama) =>
            trama.AsSpan().StartsWith("$>:"u8)
            || trama.AsSpan().StartsWith("SW"u8)
            || trama.AsSpan().StartsWith("TM"u8)
            || trama.AsSpan().StartsWith("SV"u8);

        private static string Texto(byte[] carga) => Encoding.ASCII.GetString(carga);
    }
}
