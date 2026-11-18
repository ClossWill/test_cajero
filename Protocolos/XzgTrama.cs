using System.Text;

namespace IntegracionCajeroPruebas.Protocolos;

/// <summary>
/// Trama serie del lector XZG004, copiada del ejemplo del proveedor (referencias_integracion\QR\xzg-example\Xzg004\Protocol.cs,
/// verificado en la máquina): <c>02 + longitud (u16 LE) + datos + suma de los datos (u16 LE) + 03</c>. El módulo contesta a cada
/// comando con ACK 0x05 o NAK 0x15 y, por cada trama que él manda, espera un ACK 0x05 del anfitrión. Lo escaneado llega como
/// texto plano terminado en CR/LF (o tras un silencio); las líneas que empiezan con "$>:" son eco de comandos.
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
        var largo = (ushort)datos.Length;
        var suma = Suma(datos);
        var trama = new byte[datos.Length + 6];
        trama[0] = Stx;
        trama[1] = (byte)(largo & 0xFF);
        trama[2] = (byte)(largo >> 8);
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

    private static bool EsBlanco(byte b) => b is 0 or (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0x0B or 0x0C;
}

/// <summary>
/// Separa lo que llega del lector en acuses (ACK/NAK), tramas válidas y texto suelto (lo escaneado). Guarda estado entre
/// lecturas, así una trama partida entre dos lecturas se completa. Copia de <c>Parser</c> del ejemplo del proveedor.
/// </summary>
public sealed class XzgParser
{
    private enum Modo { Suelto, Trama }

    private Modo _modo = Modo.Suelto;
    private readonly List<byte> _trama = new();
    private readonly List<byte> _texto = new();
    private DateTime? _ultimoTexto;
    private readonly Queue<bool> _acuses = new();
    private readonly Queue<byte[]> _tramas = new();
    private readonly Queue<string> _lecturas = new();

    public void Agregar(ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes)
        {
            if (_modo == Modo.Suelto) EnSuelto(b);
            else EnTrama(b);
        }
    }

    /// <summary>Cierra como lectura el texto pendiente si la línea lleva <paramref name="inactivo"/> sin recibir nada (el lector puede no mandar CR/LF).</summary>
    public void CerrarPorInactividad(TimeSpan inactivo)
    {
        if (_ultimoTexto is { } desde && _texto.Count > 0 && DateTime.UtcNow - desde >= inactivo)
            EmitirTexto();
    }

    public bool? TomarAcuse() => _acuses.Count == 0 ? null : _acuses.Dequeue();

    public byte[]? TomarTrama() => _tramas.Count == 0 ? null : _tramas.Dequeue();

    public string? TomarLectura() => _lecturas.Count == 0 ? null : _lecturas.Dequeue();

    public void Reiniciar()
    {
        _modo = Modo.Suelto;
        _trama.Clear();
        _texto.Clear();
        _ultimoTexto = null;
        _acuses.Clear();
        _tramas.Clear();
        _lecturas.Clear();
    }

    private void EnSuelto(byte b)
    {
        if (b == XzgTrama.Stx && _texto.Count == 0)
        {
            _modo = Modo.Trama;
            _trama.Clear();
            return;
        }
        if (b == XzgTrama.Ack && _texto.Count == 0) { _acuses.Enqueue(true); return; }
        if (b == XzgTrama.Nak && _texto.Count == 0) { _acuses.Enqueue(false); return; }
        if (b is (byte)'\r' or (byte)'\n')
        {
            if (_texto.Count > 0) EmitirTexto();
            return;
        }
        _texto.Add(b);
        _ultimoTexto = DateTime.UtcNow;
        if (_texto.Count > 8192) EmitirTexto();
    }

    private void EnTrama(byte b)
    {
        _trama.Add(b);
        if (_trama.Count < 2) return;
        var largo = _trama[0] | (_trama[1] << 8);
        if (largo > 4097)
        {
            _modo = Modo.Suelto;
            _trama.Clear();
            return;
        }
        var total = 2 + largo + 2 + 1;
        if (_trama.Count < total) return;
        var datos = _trama.GetRange(2, largo).ToArray();
        var suma = (ushort)(_trama[2 + largo] | (_trama[3 + largo] << 8));
        var etx = _trama[4 + largo];
        var resto = _trama.Count > total ? _trama.GetRange(total, _trama.Count - total).ToArray() : Array.Empty<byte>();
        _modo = Modo.Suelto;
        _trama.Clear();
        if (etx == XzgTrama.Etx && suma == XzgTrama.Suma(datos)) _tramas.Enqueue(datos);
        if (resto.Length > 0) Agregar(resto);
    }

    private void EmitirTexto()
    {
        var crudo = _texto.ToArray();
        _texto.Clear();
        _ultimoTexto = null;
        var texto = XzgTrama.Decodificar(crudo);
        if (texto.Length == 0 || texto.StartsWith("$>:", StringComparison.Ordinal)) return;
        _lecturas.Enqueue(texto);
    }
}
