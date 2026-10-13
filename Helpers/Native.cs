using System.Runtime.InteropServices;
using System.Text;

namespace IntegracionCajeroPruebas.Helpers;

/// <summary>Utilidades para pasar structs [StructLayout] con ByValArray (como los de CRM9250N.cs) a una DLL nativa.</summary>
public static class Native
{
    /// <summary>Struct con todos sus arrays ya instanciados al tamaño de SizeConst (evita nulls al hacer marshal).</summary>
    public static T Cero<T>() where T : struct
    {
        var p = AsignarCero(Marshal.SizeOf<T>());
        try { return Marshal.PtrToStructure<T>(p)!; }
        finally { Marshal.FreeHGlobal(p); }
    }

    public static T Leer<T>(IntPtr p) where T : struct => Marshal.PtrToStructure<T>(p)!;

    public static IntPtr AsignarCero(int bytes)
    {
        var p = Marshal.AllocHGlobal(bytes);
        Marshal.Copy(new byte[bytes], 0, p, bytes);
        return p;
    }

    /// <summary>Texto ASCII hasta el primer byte nulo.</summary>
    public static string Texto(byte[]? bytes)
    {
        if (bytes is null) return "";
        var fin = Array.IndexOf(bytes, (byte)0);
        return Encoding.ASCII.GetString(bytes, 0, fin < 0 ? bytes.Length : fin).Trim();
    }
}

/// <summary>Memoria no administrada que se libera toda junta al terminar la prueba.</summary>
public sealed class NativeScope : IDisposable
{
    private readonly List<IntPtr> _punteros = new();

    // La demo de GRG reserva 8 veces el tamaño de cada struct (y avisa de que el tamaño exacto "falla"):
    // se copia esa holgura para que el SDK nunca escriba fuera del buffer.
    private const int Holgura = 8;

    /// <summary>Buffer en cero para parámetros de salida.</summary>
    public IntPtr Salida<T>() where T : struct
    {
        var p = Native.AsignarCero(Marshal.SizeOf<T>() * Holgura);
        _punteros.Add(p);
        return p;
    }

    /// <summary>Copia el struct a memoria nativa (parámetro de entrada o entrada/salida).</summary>
    public IntPtr Entrada<T>(T valor) where T : struct
    {
        var p = Native.AsignarCero(Marshal.SizeOf<T>() * Holgura);
        Marshal.StructureToPtr(valor, p, false);
        _punteros.Add(p);
        return p;
    }

    public void Dispose()
    {
        foreach (var p in _punteros) Marshal.FreeHGlobal(p);
        _punteros.Clear();
    }
}
