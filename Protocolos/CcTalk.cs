using System.Diagnostics;
using System.IO.Ports;

namespace IntegracionCajeroPruebas.Protocolos;

/// <summary>Respuesta ccTalk ya validada. Header 0 sin datos = ACK; Header 5 = NAK; Header 6 = BUSY.</summary>
public sealed record CcTalkRespuesta(byte Origen, byte Header, byte[] Datos)
{
    public bool EsAck => Header == 0 && Datos.Length == 0;
    public bool EsNak => Header == 5;
}

/// <summary>Cliente ccTalk mínimo (trama sin cifrado, checksum simple de 8 bits). Maestro = dirección 1.</summary>
public sealed class CcTalkBus : IDisposable
{
    public const byte Host = 1;

    // Headers del estándar ccTalk (Generic Specification) usados en las pruebas.
    public const byte HResetDevice = 1;
    public const byte HSimplePoll = 254;
    public const byte HRequestEquipmentCategory = 245;
    public const byte HRequestProductCode = 244;
    public const byte HRequestSerialNumber = 242;
    public const byte HRequestSoftwareRevision = 241;
    public const byte HTestHopper = 163;
    public const byte HEnableHopper = 164;
    public const byte HRequestHopperStatus = 166;
    public const byte HRequestHighLowStatus = 217;
    public const byte HDispenseHopperCoins = 167;

    private readonly SerialPort _puerto;

    public string UltimoError { get; private set; } = "";

    public CcTalkBus(string puerto, int baudRate)
    {
        _puerto = new SerialPort(puerto, baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 500,
            WriteTimeout = 500,
        };
        _puerto.Open();
    }

    /// <summary>[destino][nº datos][origen][header][datos...][checksum] con suma total = 0 (mod 256).</summary>
    public static byte[] ConstruirTrama(byte destino, byte header, byte[]? datos = null)
    {
        datos ??= Array.Empty<byte>();
        var trama = new byte[5 + datos.Length];
        trama[0] = destino;
        trama[1] = (byte)datos.Length;
        trama[2] = Host;
        trama[3] = header;
        datos.CopyTo(trama, 4);
        trama[^1] = (byte)((256 - trama.Take(trama.Length - 1).Sum(b => b) % 256) % 256);
        return trama;
    }

    public CcTalkRespuesta? Enviar(byte destino, byte header, byte[]? datos = null, int timeoutMs = 500)
    {
        var tx = ConstruirTrama(destino, header, datos);
        _puerto.DiscardInBuffer();
        _puerto.Write(tx, 0, tx.Length);

        var rx = LeerRespuesta(tx, timeoutMs);
        if (rx is null)
        {
            UltimoError = $"Sin respuesta a header {header} (dir {destino}) en {timeoutMs} ms";
            return null;
        }
        if (rx.Sum(b => b) % 256 != 0 || rx[0] != Host)
        {
            UltimoError = "Respuesta con checksum o destino inválido: " + BitConverter.ToString(rx);
            return null;
        }

        UltimoError = "";
        return new CcTalkRespuesta(rx[2], rx[3], rx[4..^1]);
    }

    public bool SimplePoll(byte direccion) => Enviar(direccion, HSimplePoll)?.EsAck == true;

    public string? LeerTexto(byte direccion, byte header)
    {
        var r = Enviar(direccion, header);
        return r is { EsNak: false } ? System.Text.Encoding.ASCII.GetString(r.Datos) : null;
    }

    /// <summary>
    /// Lee una trama de respuesta completa. En ccTalk (línea de un solo hilo) el maestro suele recibir
    /// primero su propio eco; si la trama enviada aparece al inicio del buffer se descarta.
    /// </summary>
    private byte[]? LeerRespuesta(byte[] tx, int timeoutMs)
    {
        var buf = new List<byte>();
        var reloj = Stopwatch.StartNew();

        while (reloj.ElapsedMilliseconds < timeoutMs)
        {
            var n = _puerto.BytesToRead;
            if (n > 0)
            {
                var tmp = new byte[n];
                _puerto.Read(tmp, 0, n);
                buf.AddRange(tmp);
            }

            var offset = buf.Count >= tx.Length && buf.Take(tx.Length).SequenceEqual(tx) ? tx.Length : 0;
            var disponibles = buf.Count - offset;
            if (disponibles >= 2 && disponibles >= 5 + buf[offset + 1])
                return buf.GetRange(offset, 5 + buf[offset + 1]).ToArray();

            Thread.Sleep(2);
        }
        return null;
    }

    public void Dispose() => _puerto.Dispose();
}
