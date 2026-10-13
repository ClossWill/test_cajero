namespace IntegracionCajeroPruebas.Helpers;

/// <summary>Acumula los criterios de aceptación de una prueba y muestra el veredicto final.</summary>
public sealed class Reporte
{
    private readonly string _titulo;
    private readonly List<(string Descripcion, bool Cumple)> _criterios = new();

    public Reporte(string titulo)
    {
        _titulo = titulo;
        Consola.Titulo(titulo);
    }

    public bool Criterio(string descripcion, bool cumple, string? detalle = null)
    {
        _criterios.Add((descripcion, cumple));
        if (cumple) Consola.Ok($"[OK]    {descripcion}");
        else Consola.Error($"[FALLA] {descripcion}");
        if (!string.IsNullOrEmpty(detalle)) Consola.Info($"        {detalle}");
        return cumple;
    }

    public bool Finalizar()
    {
        var aprobados = _criterios.Count(c => c.Cumple);
        var ok = aprobados == _criterios.Count && _criterios.Count > 0;
        Console.WriteLine();
        var texto = $"RESULTADO {_titulo}: {(ok ? "APROBADO" : "RECHAZADO")} ({aprobados}/{_criterios.Count} criterios)";
        if (ok) Consola.Ok(texto); else Consola.Error(texto);
        return ok;
    }
}
