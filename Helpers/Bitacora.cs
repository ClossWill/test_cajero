using System.Text;

namespace IntegracionCajeroPruebas.Helpers;

/// <summary>
/// Copia todo lo que sale y entra por consola a bitacoras\sesion_*.txt (junto al .exe), para revisar o compartir
/// la corrida completa en remoto, incluidas las respuestas del asistente que teclea el desarrollador.
/// </summary>
public static class Bitacora
{
    public static string? Ruta { get; private set; }

    public static void Iniciar()
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "bitacoras");
            Directory.CreateDirectory(dir);
            Ruta = Path.Combine(dir, $"sesion_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            var archivo = new StreamWriter(Ruta, append: false, new UTF8Encoding(false)) { AutoFlush = true };
            Console.SetOut(new Duplicador(Console.Out, archivo));
            Console.SetIn(new EcoEntrada(Console.In, archivo));
        }
        catch (Exception ex)
        {
            Ruta = null;
            Consola.Aviso($"Sin bitácora: {ex.Message}");
        }
    }

    private sealed class Duplicador(TextWriter consola, TextWriter archivo) : TextWriter
    {
        public override Encoding Encoding => consola.Encoding;
        public override void Write(char value) { consola.Write(value); archivo.Write(value); }
        public override void Write(string? value) { consola.Write(value); archivo.Write(value); }
        public override void WriteLine(string? value) { consola.WriteLine(value); archivo.WriteLine(value); }
        public override void Flush() { consola.Flush(); archivo.Flush(); }
    }

    private sealed class EcoEntrada(TextReader consola, TextWriter archivo) : TextReader
    {
        public override string? ReadLine()
        {
            var linea = consola.ReadLine();
            archivo.WriteLine(linea ?? "<fin de entrada>");
            return linea;
        }
        public override int Read() => consola.Read();
        public override int Peek() => consola.Peek();
    }
}
