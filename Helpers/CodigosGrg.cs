namespace IntegracionCajeroPruebas.Helpers;

/// <summary>
/// Traduce el LogicCode que devuelve el SDK de GRG a su descripción y acción recomendada.
/// Tabla: Config/CodigosLogicosGrg.tsv (extraída de "CRM9250N LogicalCode.xls" del SDK, en inglés, sin traducir).
/// </summary>
internal static class CodigosGrg
{
    private static readonly Lazy<Dictionary<int, (string Descripcion, string Solucion)>> Tabla = new(Cargar);

    public static string Describir(int logicCode)
    {
        if (logicCode == 0) return "";
        if (!Tabla.Value.TryGetValue(logicCode, out var e))
            return Tabla.Value.Count == 0 ? "(tabla de códigos no encontrada junto al .exe)" : "(código ausente en la tabla del SDK)";
        return string.IsNullOrWhiteSpace(e.Solucion) ? e.Descripcion : $"{e.Descripcion} -> {e.Solucion}";
    }

    private static Dictionary<int, (string, string)> Cargar()
    {
        var tabla = new Dictionary<int, (string, string)>();
        var ruta = Path.Combine(AppContext.BaseDirectory, "Config", "CodigosLogicosGrg.tsv");
        if (!File.Exists(ruta)) return tabla;

        foreach (var linea in File.ReadLines(ruta))
        {
            if (linea.StartsWith('#')) continue;
            var c = linea.Split('\t');
            if (c.Length >= 2 && int.TryParse(c[0], out var codigo))
                tabla.TryAdd(codigo, (c[1], c.Length > 2 ? c[2] : ""));
        }
        return tabla;
    }
}
