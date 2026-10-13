namespace IntegracionCajeroPruebas.Helpers;

public static class Consola
{
    public static void Titulo(string texto)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(new string('=', 70));
        Console.WriteLine(texto);
        Console.WriteLine(new string('=', 70));
        Console.ResetColor();
    }

    public static void Info(string texto) => Escribir(texto, ConsoleColor.Gray);
    public static void Ok(string texto) => Escribir(texto, ConsoleColor.Green);
    public static void Error(string texto) => Escribir(texto, ConsoleColor.Red);
    public static void Aviso(string texto) => Escribir(texto, ConsoleColor.Yellow);

    public static void Esperar(string mensaje)
    {
        Aviso(mensaje);
        Console.ReadLine();
    }

    public static bool Confirmar(string pregunta)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{pregunta} [s/N]: ");
        Console.ResetColor();
        var r = Console.ReadLine()?.Trim().ToLowerInvariant();
        return r is "s" or "si" or "sí" or "y";
    }

    public static string Hex(IEnumerable<byte> bytes) => string.Join(' ', bytes.Select(b => b.ToString("X2")));

    /// <summary>Convierte "AA 01 0x02" / "AA0102" a bytes. Cadena vacía devuelve null.</summary>
    public static byte[]? ParseHex(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpio = texto.Replace("0x", "", StringComparison.OrdinalIgnoreCase)
                          .Replace(" ", "").Replace("-", "");
        return Convert.FromHexString(limpio);
    }

    private static void Escribir(string texto, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(texto);
        Console.ResetColor();
    }
}
