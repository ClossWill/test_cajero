namespace IntegracionCajeroPruebas.Helpers;

/// <summary>
/// Validación propia de los dígitos verificadores de la MRZ (ICAO 9303, pesos 7-3-1). Existe porque el
/// <c>bCheckSum</c> del SDK del MEPR500+ da falsos negativos: el 29-oct-2026 marcó FALLA en pasaportes e INE cuya
/// MRZ, leída bien, cumple todos los dígitos verificadores.
/// </summary>
public static class Mrz
{
    public sealed record Resultado(bool Valido, string Formato, string Detalle);

    /// <summary>
    /// Tipo de documento según el código de la primera línea de la MRZ (ICAO 9303): P = pasaporte, V = visa,
    /// I/A/C = tarjeta de identidad (la INE usa "IDMEX"). Se deduce de la MRZ y no del <c>iType</c> del SDK porque
    /// de ese código no hay referencia para la INE mexicana.
    /// </summary>
    public static string TipoDocumento(string? mrz)
    {
        var linea1 = (mrz ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "";
        if (linea1.Length < 2) return "No determinado (sin MRZ)";
        if (linea1.StartsWith("IDMEX")) return "INE (credencial para votar)";
        return linea1[0] switch
        {
            'P' => "Pasaporte",
            'V' => "Visa",
            'I' or 'A' or 'C' => "Tarjeta de identidad (ID)",
            _ => $"No determinado (MRZ empieza con '{linea1[0]}')",
        };
    }

    /// <summary>Acepta las líneas separadas por '\n' (como las entrega EscanerMeprSdk). Pasaporte = 2x44, INE/TD1 = 3x30.</summary>
    public static Resultado Validar(string? mrz)
    {
        var lineas = (mrz ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lineas.Length == 2 && lineas[1].Length == 44) return ValidarTd3(lineas[1]);
        if (lineas.Length >= 2 && lineas[0].Length == 30 && lineas[1].Length == 30) return ValidarTd1(lineas[0], lineas[1]);
        return new(false, "desconocido",
            $"MRZ con formato no reconocido ({lineas.Length} líneas; longitudes {string.Join('/', lineas.Select(l => l.Length))})");
    }

    // Pasaporte (TD3), línea 2: nº doc 0-8 (dv 9), nacimiento 13-18 (dv 19), vencimiento 21-26 (dv 27),
    // dato opcional 28-41 (dv 42), compuesto = 0-9 + 13-19 + 21-42 (dv 43).
    private static Resultado ValidarTd3(string l2) => Resumir("pasaporte (TD3)", new (string, string, char)[]
    {
        ("número de documento", l2[..9], l2[9]),
        ("fecha de nacimiento", l2.Substring(13, 6), l2[19]),
        ("fecha de vencimiento", l2.Substring(21, 6), l2[27]),
        ("dato opcional", l2.Substring(28, 14), l2[42]),
        ("compuesto", l2[..10] + l2.Substring(13, 7) + l2.Substring(21, 22), l2[43]),
    });

    // INE / tarjeta (TD1), con la numeración de la propia credencial: CIC 5-13 (dv 14); línea 2: nacimiento 0-5
    // (dv 6), vencimiento 8-13 (dv 14), compuesto = línea1[5..30] + línea2[0..7] + [8..15] + [18..29] (dv 29).
    // El sexo viene como H/M (no M/F), por eso no se valida.
    private static Resultado ValidarTd1(string l1, string l2) => Resumir("INE (TD1)", new (string, string, char)[]
    {
        ("CIC", l1.Substring(5, 9), l1[14]),
        ("fecha de nacimiento", l2[..6], l2[6]),
        ("fecha de vencimiento", l2.Substring(8, 6), l2[14]),
        ("compuesto", l1[5..] + l2[..7] + l2.Substring(8, 7) + l2.Substring(18, 11), l2[29]),
    });

    private static Resultado Resumir(string formato, (string Campo, string Datos, char Dv)[] campos)
    {
        var fallos = campos.Where(c => Digito(c.Datos) != c.Dv).Select(c => c.Campo).ToList();
        return fallos.Count == 0
            ? new(true, formato, $"{campos.Length} dígitos verificadores correctos")
            : new(false, formato, $"dígito verificador incorrecto en: {string.Join(", ", fallos)}");
    }

    private static char Digito(string datos)
    {
        int[] pesos = { 7, 3, 1 };
        var suma = 0;
        for (var i = 0; i < datos.Length; i++)
        {
            var c = datos[i];
            var valor = c == '<' ? 0 : char.IsDigit(c) ? c - '0' : char.ToUpperInvariant(c) - 'A' + 10;
            suma += valor * pesos[i % 3];
        }
        return (char)('0' + suma % 10);
    }
}
