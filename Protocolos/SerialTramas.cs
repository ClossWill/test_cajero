using System.Diagnostics;
using System.IO.Ports;

namespace IntegracionCajeroPruebas.Protocolos;

public static class SerialTramas
{
    /// <summary>
    /// Lee el puerto de forma continua y entrega una "trama" cada vez que la línea queda en silencio
    /// <paramref name="silencioMs"/> milisegundos tras recibir bytes. Termina cuando continuar() devuelve false.
    /// </summary>
    public static void Escuchar(SerialPort puerto, int silencioMs, Func<bool> continuar, Action<byte[]> alRecibirTrama)
    {
        var buf = new List<byte>();
        var ultimoByte = Stopwatch.StartNew();

        while (continuar())
        {
            var n = puerto.BytesToRead;
            if (n > 0)
            {
                var tmp = new byte[n];
                puerto.Read(tmp, 0, n);
                buf.AddRange(tmp);
                ultimoByte.Restart();
            }
            else if (buf.Count > 0 && ultimoByte.ElapsedMilliseconds >= silencioMs)
            {
                alRecibirTrama(buf.ToArray());
                buf.Clear();
            }
            else
            {
                Thread.Sleep(5);
            }
        }
    }

    /// <summary>¿La trama contiene el patrón como subsecuencia contigua?</summary>
    public static bool Contiene(byte[] trama, byte[] patron)
    {
        if (patron.Length == 0 || patron.Length > trama.Length) return false;
        for (var i = 0; i <= trama.Length - patron.Length; i++)
            if (trama.AsSpan(i, patron.Length).SequenceEqual(patron)) return true;
        return false;
    }
}
