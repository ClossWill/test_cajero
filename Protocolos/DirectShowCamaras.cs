using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

// Todo el proyecto es Windows-only (COM de DirectShow, SDKs nativos): evita avisos CA1416.
[assembly: SupportedOSPlatform("windows")]

namespace IntegracionCajeroPruebas.Protocolos;

/// <summary>
/// Nombres de las cámaras que ve DirectShow, en el mismo orden que usa OpenCV (VideoCaptureAPIs.DSHOW) para
/// numerar los índices. OpenCV no expone nombres de dispositivo; esto permite elegir la cámara por nombre en
/// vez de depender de un índice que cambia entre la laptop y el ATM.
/// </summary>
public static class DirectShowCamaras
{
    private static readonly Guid ClsidSystemDeviceEnum = new("62BE5D10-60EB-11D0-BD3B-00A0C911CE86");
    private static readonly Guid ClsidVideoInputDeviceCategory = new("860BB310-5D01-11D0-BD3B-00A0C911CE86");
    private static readonly Guid IidPropertyBag = new("55272A00-42CB-11CE-8135-00AA004BB851");

    [ComImport, Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICreateDevEnum
    {
        [PreserveSig]
        int CreateClassEnumerator(ref Guid clsidDeviceClass, out IEnumMoniker? enumMoniker, int flags);
    }

    [ComImport, Guid("55272A00-42CB-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyBag
    {
        [PreserveSig]
        int Read([MarshalAs(UnmanagedType.LPWStr)] string propertyName, [MarshalAs(UnmanagedType.Struct)] out object value, IntPtr errorLog);
    }

    /// <summary>Nombres amigables; la posición en la lista es el índice DSHOW de OpenCV.</summary>
    public static List<string> Listar()
    {
        var nombres = new List<string>();
        var tipo = Type.GetTypeFromCLSID(ClsidSystemDeviceEnum)
                   ?? throw new InvalidOperationException("DirectShow no está disponible (SystemDeviceEnum).");
        var enumerador = (ICreateDevEnum)Activator.CreateInstance(tipo)!;
        try
        {
            var categoria = ClsidVideoInputDeviceCategory;
            // S_FALSE (1) con enumMoniker nulo = no hay ninguna cámara.
            if (enumerador.CreateClassEnumerator(ref categoria, out var monikers, 0) != 0 || monikers is null) return nombres;
            try
            {
                var uno = new IMoniker[1];
                while (monikers.Next(1, uno, IntPtr.Zero) == 0)
                {
                    nombres.Add(LeerNombre(uno[0]));
                    Marshal.ReleaseComObject(uno[0]);
                }
            }
            finally { Marshal.ReleaseComObject(monikers); }
        }
        finally { Marshal.ReleaseComObject(enumerador); }
        return nombres;
    }

    private static string LeerNombre(IMoniker moniker)
    {
        var iid = IidPropertyBag;
        moniker.BindToStorage(null!, null!, ref iid, out var objeto);
        try
        {
            var bolsa = (IPropertyBag)objeto;
            return bolsa.Read("FriendlyName", out var valor, IntPtr.Zero) == 0 && valor is string s ? s : "(sin nombre)";
        }
        finally { Marshal.ReleaseComObject(objeto); }
    }
}
