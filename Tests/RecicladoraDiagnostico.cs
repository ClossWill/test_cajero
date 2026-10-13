using System.Runtime.InteropServices;
using IntegracionCajeroPruebas.Helpers;

namespace IntegracionCajeroPruebas.Tests;

// Pre-vuelo de la recicladora: antes de llamar al SDK, comprobar que la DLL existe, que su
// arquitectura coincide con la del proceso y que Windows la puede cargar (con sus dependencias).
public static partial class HardwareTests
{
    private const string DllRecicladora = "CRM9250NDev.dll";

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string ruta);

    /// <returns>true si la DLL quedó cargada en el proceso (los [DllImport] posteriores la reutilizan por nombre).</returns>
    private static bool DiagnosticarDllRecicladora(Reporte r)
    {
        var ruta = LocalizarDll(DllRecicladora);
        if (!r.Criterio($"{DllRecicladora} existe junto al .exe o en el PATH", ruta is not null,
                ruta ?? $"No está en {AppContext.BaseDirectory}. Copiar la DLL del SDK de GRG (y sus dependencias) a esa carpeta."))
            return false;

        var bitsDll = LeerArquitecturaPe(ruta!);
        var bitsProceso = Environment.Is64BitProcess ? "x64" : "x86";
        r.Criterio($"Arquitectura de la DLL ({bitsDll}) coincide con la del proceso ({bitsProceso})",
            bitsDll == bitsProceso,
            bitsDll == bitsProceso ? null
                : $"Cambiar <PlatformTarget> a {bitsDll} en el .csproj y recompilar (BadImageFormatException si no).");
        if (bitsDll != bitsProceso && bitsDll is "x86" or "x64") return false;

        // hOpenLogicDevice/iSetCommPara no reciben puerto: el SDK lo lee del nodo [<nombre lógico>] de este .ini (PDF §4.1 y §4.3).
        var carpeta = Path.GetDirectoryName(ruta)!;
        var ini = Path.Combine(carpeta, "GRGDTATM_CommCfg.ini");
        var seccion = LeerSeccionIni(ini, Crm.NombreDispositivoLogico);
        r.Criterio($"GRGDTATM_CommCfg.ini junto a la DLL con el nodo [{Crm.NombreDispositivoLogico}]", seccion is not null,
            seccion is null ? $"Falta {ini} o no tiene ese nodo. El nombre lógico de hardware.json debe coincidir con el nodo del .ini."
                            : string.Join(" | ", seccion.Where(l => l.StartsWith("CommType") || l.StartsWith("ExtendStr") || l.StartsWith("ComID"))));
        if (seccion is not null) VerificarUsb(r, seccion);

        // Dependencias directas: se listan antes de cargar para nombrar exactamente lo que falta.
        var faltan = DependenciasFaltantes(ruta!, carpeta);
        r.Criterio("Todas las DLL que importa CRM9250NDev.dll están disponibles", faltan.Count == 0,
            faltan.Count == 0 ? null
                : $"Faltan: {string.Join(", ", faltan)}. Copiarlas junto al .exe (son del paquete/driver de GRG; " +
                  "devbasedll.dll no viene en el zip del SDK V2.3.7.6).");

        var h = LoadLibraryW(ruta!);
        var err = Marshal.GetLastWin32Error();
        r.Criterio("Windows carga la DLL (con sus dependencias)", h != IntPtr.Zero,
            h != IntPtr.Zero ? $"módulo=0x{h:X}" : err switch
            {
                126 => "Error 126: falta una dependencia (ver línea anterior; también puede ser una indirecta de otra DLL de GRG).",
                193 => "Error 193: DLL de otra arquitectura que el proceso.",
                _ => $"Error de Windows {err}.",
            });
        return h != IntPtr.Zero;
    }

    /// <summary>Importaciones directas de la DLL que no están en su carpeta ni en el sistema (ignora api-ms-*).</summary>
    private static List<string> DependenciasFaltantes(string ruta, string carpeta)
    {
        var faltan = new List<string>();
        foreach (var dll in LeerImportaciones(ruta))
        {
            if (dll.StartsWith("api-ms-", StringComparison.OrdinalIgnoreCase)) continue;
            var existe = File.Exists(Path.Combine(carpeta, dll))
                         || File.Exists(Path.Combine(AppContext.BaseDirectory, dll))
                         || File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), dll))
                         || File.Exists(Path.Combine(Environment.SystemDirectory, dll));
            if (!existe) faltan.Add(dll);
        }
        return faltan;
    }

    /// <summary>Nombres de DLL de la tabla de importación PE (soporta PE32 y PE32+).</summary>
    private static List<string> LeerImportaciones(string ruta)
    {
        var nombres = new List<string>();
        try
        {
            using var f = File.OpenRead(ruta);
            using var br = new BinaryReader(f);
            f.Position = 0x3C;
            var pe = br.ReadInt32();
            f.Position = pe + 6;
            var nSecciones = br.ReadUInt16();
            f.Position = pe + 20;
            var tamOpcional = br.ReadUInt16();
            var opt = pe + 24;
            f.Position = opt;
            var pe32Plus = br.ReadUInt16() == 0x20B;
            f.Position = opt + (pe32Plus ? 120 : 104); // DataDirectory[1] = tabla de importación
            var rvaImport = br.ReadUInt32();
            if (rvaImport == 0) return nombres;

            var secciones = new List<(uint Rva, uint Tam, uint Raw)>();
            f.Position = opt + tamOpcional;
            for (var i = 0; i < nSecciones; i++)
            {
                f.Position = opt + tamOpcional + i * 40 + 8;
                var virtualSize = br.ReadUInt32();
                var rva = br.ReadUInt32();
                var rawSize = br.ReadUInt32();
                var raw = br.ReadUInt32();
                secciones.Add((rva, Math.Max(virtualSize, rawSize), raw));
            }
            long ARaw(uint rva) { foreach (var s in secciones) if (rva >= s.Rva && rva < s.Rva + s.Tam) return s.Raw + (rva - s.Rva); return -1; }

            for (var d = ARaw(rvaImport); d >= 0; d += 20)
            {
                f.Position = d + 12;
                var rvaNombre = br.ReadUInt32();
                if (rvaNombre == 0) break;
                var pos = ARaw(rvaNombre);
                if (pos < 0) break;
                f.Position = pos;
                var sb = new System.Text.StringBuilder();
                for (int c; (c = f.ReadByte()) > 0;) sb.Append((char)c);
                nombres.Add(sb.ToString());
            }
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or UnauthorizedAccessException) { }
        return nombres;
    }

    /// <summary>Líneas "clave=valor" del nodo [nombre] de un .ini; null si el archivo o el nodo no existen.</summary>
    private static List<string>? LeerSeccionIni(string ruta, string nombre)
    {
        if (!File.Exists(ruta)) return null;
        var lineas = new List<string>();
        var dentro = false;
        var encontrada = false;
        foreach (var l in File.ReadLines(ruta).Select(x => x.Trim()))
        {
            if (l.StartsWith('[')) { dentro = l.Equals($"[{nombre}]", StringComparison.OrdinalIgnoreCase); encontrada |= dentro; continue; }
            if (dentro && l.Length > 0 && !l.StartsWith(';')) lineas.Add(l);
        }
        return encontrada ? lineas : null;
    }

    /// <summary>Si el nodo declara CommType=3 (USB) con "VID:xxxx, PID:yyyy", busca ese dispositivo en el registro de Windows.</summary>
    private static void VerificarUsb(Reporte r, List<string> seccion)
    {
        if (!OperatingSystem.IsWindows()) return;
        var extra = seccion.FirstOrDefault(l => l.StartsWith("ExtendStr", StringComparison.OrdinalIgnoreCase));
        var m = extra is null ? null : System.Text.RegularExpressions.Regex.Match(
            extra, @"VID:\s*([0-9A-Fa-f]{4}).*PID:\s*([0-9A-Fa-f]{4})");
        if (m is null || !m.Success) return;

        var vid = m.Groups[1].Value.ToUpperInvariant();
        var pid = m.Groups[2].Value.ToUpperInvariant();
        var clave = $@"SYSTEM\CurrentControlSet\Enum\USB\VID_{vid}&PID_{pid}";
        using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(clave);
        r.Criterio($"Windows ha enumerado el USB de la recicladora (VID_{vid}&PID_{pid})", k is not null,
            k is not null ? $"instancias={string.Join(",", k.GetSubKeyNames())}"
                          : "No aparece en el registro: la recicladora nunca se ha conectado a este equipo, el cable USB no está puesto o falta el driver de GRG. " +
                            "Revisar el Administrador de dispositivos.");
    }

    private static string? LocalizarDll(string nombre)
    {
        var local = Path.Combine(AppContext.BaseDirectory, nombre);
        if (File.Exists(local)) return local;
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidato = Path.Combine(dir.Trim(), nombre);
                if (File.Exists(candidato)) return candidato;
            }
            catch (ArgumentException) { /* entrada de PATH inválida */ }
        }
        return null;
    }

    /// <summary>Lee el campo Machine del encabezado PE: "x86", "x64", "arm64" u "desconocida".</summary>
    private static string LeerArquitecturaPe(string ruta)
    {
        try
        {
            using var f = File.OpenRead(ruta);
            using var br = new BinaryReader(f);
            f.Position = 0x3C;
            var pe = br.ReadInt32();
            f.Position = pe;
            if (br.ReadUInt32() != 0x00004550) return "desconocida"; // "PE\0\0"
            return br.ReadUInt16() switch
            {
                0x014C => "x86",
                0x8664 => "x64",
                0xAA64 => "arm64",
                var m => $"desconocida (0x{m:X})",
            };
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or UnauthorizedAccessException)
        {
            return "desconocida";
        }
    }
}
