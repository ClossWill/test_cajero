using System.Diagnostics;
using System.IO.Ports;
using IntegracionCajeroPruebas.Helpers;

namespace IntegracionCajeroPruebas.Protocolos;

/// <summary>Respuesta ya separada de su framing. <c>Ok</c> = el tablero devolvió 'P' (comando correcto).</summary>
public sealed record LedRespuesta(bool Ok, byte Cmd, byte Para, byte[] Datos);

/// <summary>
/// Cliente para la tarjeta "18-Channel LED Light Bar Control Board" (modelo 24002-2, Shenzhen Hongzhou,
/// doc. <c>referencias_integracion/Multifunctional serial control module/</c>). No es el módulo ALB02: es
/// una tarjeta RS232 aparte que controla 18 salidas de 12V (indicadores de función).
///
/// Trama: <c>DLE STX [LEN(2, low-first) 'C' CMD PARA] DLE ETX BCC</c>, con BCC = XOR de todos los bytes
/// entre STX y el DLE final (LEN + 'C' + CMD + PARA). Para nuestros comandos (PARA de 1 byte) LEN siempre
/// vale <c>03 00</c> (longitud de 'C'+CMD+PARA), confirmado byte a byte contra las ~40 tramas de ejemplo
/// de la hoja de especificación del fabricante. La respuesta usa el mismo framing con 'P' (éxito, 0x50) o
/// 'N' (falla, 0x4E) en el lugar de 'C'. El protocolo no documenta caracteres de escape para cuando 0x10
/// aparece dentro de los datos (sí lo menciona de pasada, pero esa sección no estaba en el PDF recibido);
/// para los comandos usados aquí (LEN, CMD y PARA siempre &lt; 0x10 o fijos) no hay colisión conocida.
/// </summary>
public sealed class LedControlBus : IDisposable
{
    public const byte CConsultarFirmware = 0x31;
    public const byte CConsultarEntradas = 0x32; // estado de las 6 entradas de sensor de 5 V (hoja de protocolo, §consulta de entradas)
    public const byte CApagar = 0x33;
    public const byte CParpadear = 0x34;
    public const byte CEncender = 0x35;

    /// <summary>PARA = 0 controla los 18 canales a la vez; 1..18 direcciona un canal individual.</summary>
    public const byte ParaTodos = 0x00;

    private readonly SerialPort _puerto;

    public string UltimoError { get; private set; } = "";

    public LedControlBus(string puerto, int baudRate)
    {
        _puerto = new SerialPort(puerto, baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 1000,
            WriteTimeout = 500,
            // Mismo hallazgo que en CcTalk.cs (2026-10-16): el puerto puede abrir sin error y aun así no
            // recibir nada si el adaptador RS232 necesita DTR/RTS para alimentar o habilitar su transceptor.
            // .NET los deja en false por defecto; se activan aquí también por la misma razón.
            DtrEnable = true,
            RtsEnable = true,
        };
        _puerto.Open();
    }

    public static byte[] ConstruirComando(byte cmd, byte para)
    {
        byte[] mensaje = { 0x03, 0x00, 0x43, cmd, para }; // LEN(2) 'C' CMD PARA
        byte bcc = 0;
        foreach (var b in mensaje) bcc ^= b;

        var trama = new byte[2 + mensaje.Length + 3];
        trama[0] = 0x10; trama[1] = 0x02; // DLE STX
        mensaje.CopyTo(trama, 2);
        trama[^3] = 0x10; trama[^2] = 0x03; trama[^1] = bcc; // DLE ETX BCC
        return trama;
    }

    public LedRespuesta? Enviar(byte cmd, byte para, int timeoutMs = 1000)
    {
        _puerto.DiscardInBuffer();
        var tx = ConstruirComando(cmd, para);
        _puerto.Write(tx, 0, tx.Length);

        var rx = LeerRespuesta(timeoutMs);
        if (rx is null) UltimoError = $"Sin respuesta válida a CMD 0x{cmd:X2} PARA 0x{para:X2} en {timeoutMs} ms";
        return rx;
    }

    public bool Apagar(byte canal) => Enviar(CApagar, canal)?.Ok == true;
    public bool Encender(byte canal) => Enviar(CEncender, canal)?.Ok == true;
    public bool Parpadear(byte canal) => Enviar(CParpadear, canal)?.Ok == true;

    /// <summary>RESP trae la versión como texto, p. ej. "POWER BOX Vx.xx" (15 bytes, según la hoja de protocolo).</summary>
    public string? ConsultarFirmware()
    {
        var r = Enviar(CConsultarFirmware, 0x31); // PARA fijo = 0x31 en todos los ejemplos del fabricante
        return r is { Ok: true } ? System.Text.Encoding.ASCII.GetString(r.Datos).TrimEnd('\0', ' ') : null;
    }

    /// <summary>Busca DLE STX ('P'|'N') LEN(2) y, si ya llegó la trama completa, valida el BCC y la separa.</summary>
    private LedRespuesta? LeerRespuesta(int timeoutMs)
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

            var i = -1;
            for (var k = 0; k + 2 < buf.Count; k++)
            {
                if (buf[k] == 0x10 && buf[k + 1] == 0x02 && (buf[k + 2] == 0x50 || buf[k + 2] == 0x4E)) { i = k; break; }
            }

            if (i >= 0 && buf.Count >= i + 5)
            {
                var len = buf[i + 3] | (buf[i + 4] << 8);
                var total = i + 5 + len + 3; // DLE STX 'P/N' LEN(2) + payload(len) + DLE ETX BCC
                if (buf.Count >= total)
                {
                    var mensaje = buf.GetRange(i + 3, 2 + len).ToArray(); // LEN(2) + CMD + PARA + RESP/ERROR
                    byte bcc = 0;
                    foreach (var b in mensaje) bcc ^= b;
                    if (bcc != buf[total - 1])
                    {
                        UltimoError = "BCC inválido en la respuesta: " + Consola.Hex(buf.GetRange(i, total - i));
                        return null;
                    }

                    var payload = buf.GetRange(i + 5, len).ToArray(); // CMD PARA RESP/ERROR
                    var ok = buf[i + 2] == 0x50;
                    return new LedRespuesta(ok, payload[0], payload[1], payload[2..]);
                }
            }

            Thread.Sleep(2);
        }
        return null;
    }

    public void Dispose() => _puerto.Dispose();
}
