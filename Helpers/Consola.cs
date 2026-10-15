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

    // Pruebas en remoto: el desarrollador opera la consola y un asistente en sitio manipula la máquina.
    // Las líneas ">> ASISTENTE" se le leen tal cual (teléfono o videollamada); el desarrollador teclea lo que responde.

    /// <summary>Aviso para el asistente que no espera respuesta.</summary>
    public static void AvisarAsistente(string texto) => Escribir($"  >> ASISTENTE: {texto}", ConsoleColor.Magenta);

    /// <summary>Instrucción física para el asistente. Bloquea hasta que diga "listo".</summary>
    public static void Asistente(string instruccion)
    {
        AvisarAsistente(instruccion);
        Esperar("     ENTER cuando el asistente diga \"listo\"...");
    }

    /// <summary>Pregunta de sí/no sobre lo que el asistente ve u oye.</summary>
    public static bool PreguntarAsistente(string pregunta)
    {
        AvisarAsistente(pregunta);
        return Confirmar("     Respuesta del asistente");
    }

    /// <summary>Pide un número (p. ej. cuántos billetes ve). Preguntar el número y no confirmar el esperado evita respuestas inducidas.</summary>
    public static int? PreguntarNumeroAsistente(string pregunta)
    {
        AvisarAsistente(pregunta);
        Console.Write("     Número que dice el asistente: ");
        return int.TryParse(Console.ReadLine()?.Trim(), out var n) ? n : null;
    }

    public static string PreguntarTextoAsistente(string pregunta)
    {
        AvisarAsistente(pregunta);
        Console.Write("     Respuesta del asistente: ");
        return Console.ReadLine()?.Trim() ?? "";
    }

    /// <summary>Aviso previo a un movimiento mecánico. false = el asistente no está listo y la prueba no debe moverse.</summary>
    public static bool AntesDeMover(string movimiento)
    {
        Escribir($"  !! VA A MOVERSE: {movimiento}", ConsoleColor.Red);
        AvisarAsistente("manos fuera del shutter y de las bandejas; avise cuando esté listo.");
        return Confirmar("     ¿El asistente está listo?");
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
