using System.Text;

namespace IntegracionCajeroPruebas.Protocolos;

/// <summary>
/// Trama serie del lector XZG004, tomada del ejemplo del proveedor (referencias_integracion\QR\xzg-example):
/// <c>02 + longitud (u16 LE) + datos + suma de los datos (u16 LE) + 03</c>. El módulo contesta a cada comando con
/// ACK 0x05 o NAK 0x15 y, por cada trama que él manda, espera un ACK 0x05 del anfitrión. Lo escaneado llega como texto
/// plano terminado en CR/LF (o tras un silencio); las líneas que empiezan con "$>:" son eco de comandos.
/// </summary>
public static class XzgTrama
{
    public const byte Stx = 0x02, Etx = 0x03, Ack = 0x05, Nak = 0x15;

    static XzgTrama() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static ushort Suma(ReadOnlySpan<byte> datos)
    {
        ushort suma = 0;
        foreach (var b in datos) suma += b;
        return suma;
    }

    public static byte[] Codificar(ReadOnlySpan<byte> datos)
    {
        var suma = Suma(datos);
        var trama = new byte[datos.Length + 6];
        trama[0] = Stx;
        trama[1] = (byte)(datos.Length & 0xFF);
        trama[2] = (byte)(datos.Length >> 8);
        datos.CopyTo(trama.AsSpan(3));
        trama[3 + datos.Length] = (byte)(suma & 0xFF);
        trama[4 + datos.Length] = (byte)(suma >> 8);
        trama[5 + datos.Length] = Etx;
        return trama;
    }

    /// <summary>UTF-8 y, si no es válido, GBK (cp936), como el ejemplo; sin espacios ni nulos en los extremos.</summary>
    public static string Decodificar(ReadOnlySpan<byte> bytes)
    {
        var ini = 0;
        var fin = bytes.Length;
        while (ini < fin && EsBlanco(bytes[ini])) ini++;
        while (fin > ini && EsBlanco(bytes[fin - 1])) fin--;
        bytes = bytes[ini..fin];
        if (bytes.IsEmpty) return "";
        try { return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes); }
        catch (DecoderFallbackException) { return Encoding.GetEncoding(936).GetString(bytes); }
    }

    /// <summary>
    /// Separa lo recibido (un tramo entre dos silencios de la línea) en acuses, tramas válidas y texto suelto (lo escaneado).
    /// No guarda estado entre llamadas: una trama partida entre dos tramos se descarta.
    /// </summary>
    public static (List<bool> Acuses, List<byte[]> Tramas, List<string> Textos) Separar(ReadOnlySpan<byte> bytes)
    {
        var acuses = new List<bool>();
        var tramas = new List<byte[]>();
        var textos = new List<string>();
        var texto = new List<byte>();
        var trama = new List<byte>();
        var enTrama = false;

        void CerrarTexto()
        {
            var t = Decodificar(texto.ToArray());
            texto.Clear();
            if (t.Length > 0 && !t.StartsWith("$>:", StringComparison.Ordinal)) textos.Add(t);
        }

        foreach (var b in bytes)
        {
            if (enTrama)
            {
                trama.Add(b);
                if (trama.Count < 2) continue;
                var largo = trama[0] | (trama[1] << 8);
                if (largo > 4097) { enTrama = false; trama.Clear(); continue; }
                if (trama.Count < largo + 5) continue;
                var datos = trama.GetRange(2, largo).ToArray();
                var suma = (ushort)(trama[2 + largo] | (trama[3 + largo] << 8));
                if (trama[4 + largo] == Etx && suma == Suma(datos)) tramas.Add(datos);
                enTrama = false;
                trama.Clear();
            }
            else if (b == Stx && texto.Count == 0) enTrama = true;
            else if ((b == Ack || b == Nak) && texto.Count == 0) acuses.Add(b == Ack);
            else if (b is (byte)'\r' or (byte)'\n') { if (texto.Count > 0) CerrarTexto(); }
            else texto.Add(b);
        }
        if (texto.Count > 0) CerrarTexto();
        return (acuses, tramas, textos);
    }

    private static bool EsBlanco(byte b) => b is 0 or (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0x0B or 0x0C;
}
