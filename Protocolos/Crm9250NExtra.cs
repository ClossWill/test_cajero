using System.Runtime.InteropServices;
using CRM9250N_WinFormDemo;

namespace IntegracionCajeroPruebas.Protocolos;

// Funciones documentadas en "CRM9250NDev API Description.pdf" que CRM9250N.cs (wrapper de GRG) no declara.
// Se declaran aquí para no modificar el archivo del fabricante.
internal static class Crm9250NExtra
{
    /// <summary>Consulta el estado de la máquina comunicándose con ella (PDF §4.10). Sin efectos físicos.</summary>
    [DllImport("CRM9250NDev.dll", EntryPoint = "iGetDevStatusEx")]
    public static extern int iGetDevStatusEx(IntPtr p_hDevHandle, IntPtr p_psDevStatus, IntPtr p_psStatus);

    /// <summary>Lee la información y el estado de los casetes (PDF §4.7). Solo consulta; no es la inicialización mecánica.</summary>
    [DllImport("CRM9250NDev.dll", EntryPoint = "iGetCassetteInfoEx")]
    public static extern int iGetCassetteInfoEx(IntPtr p_hDevHandle, IntPtr p_psCassetteInfo, IntPtr p_psCassetteStatus, IntPtr p_psStatus);

    /// <summary>Lee las monedas/denominaciones que soporta el validador NV (PDF §4.22). Sin efectos físicos.</summary>
    [DllImport("CRM9250NDev.dll", EntryPoint = "iGetDenominationTable")]
    public static extern int iGetDenominationTable(IntPtr p_hDevHandle, IntPtr p_psDenominationTable, IntPtr p_psStatus);

    /// <summary>Carga la configuración de trabajo (modo, denominaciones aceptadas, casetes deshabilitados) sin mover nada. Es el primer paso de iPowerOnInitEx.</summary>
    [DllImport("CRM9250NDev.dll", EntryPoint = "iInitSettingEx")]
    public static extern int iInitSettingEx(IntPtr p_hDevHandle, IntPtr p_psInitSetting, IntPtr p_psStatus);

    /// <summary>Reinicio mecánico solo (PDF §4.6): sin reconfigurar ni releer casetes. Es el paso de recuperación del flujo de dispensa.</summary>
    [DllImport("CRM9250NDev.dll", EntryPoint = "iInitEx")]
    public static extern int iInitEx(IntPtr p_hDevHandle, IntPtr p_psInitInfo, IntPtr p_psStatus);

    /// <summary>Cierra la comunicación (PDF §4.4).</summary>
    [DllImport("CRM9250NDev.dll", EntryPoint = "iCloseComm")]
    public static extern int iCloseComm(IntPtr p_hDevHandle, IntPtr p_psStatus);

    /// <summary>Libera el handle; es el último paso (PDF §4.2).</summary>
    [DllImport("CRM9250NDev.dll", EntryPoint = "vCloseLogicDevice")]
    public static extern void vCloseLogicDevice(IntPtr p_hDevHandle);
}

/// <summary>tCRM9250N_DenominationInfo (PDF §3.17). wDenomCode = ID que espera awAcceptableDenom.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct tCRM9250N_DenominationInfo
{
    public ushort wDenomCode;
    public ushort wLength;
    public ushort wWidth;
    public ushort wThickness;
}

/// <summary>tCRM9250N_DenominationTable (PDF §3.25).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct tCRM9250N_DenominationTable
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)] public tCRM9250N_DenominationInfo[] asDenominationInfo;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] abyReserved;
}

/// <summary>tCRM9250N_DevStatusEx de CRM9250NDevStruct.h (PDF §3.9). Pack = 1 como el resto de structs del SDK.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct tCRM9250N_DevStatusEx
{
    public byte byShutterAreaHasNotes;
    public byte byNfHasNotes;
    public byte byNfNotesWrongPosition;
    public byte byNeHasNotes;
    public byte byShutterOpen;
    public byte byNvOpen;
    public byte byLowerChannelOpen;
    public byte byUpperUnitDragOut;
    public byte byUpperUnitInPos;
    public byte byLowerUnitDragOut;
    public byte byLowerUnitInPos;
    public byte byCassettePlugRemove;
    public tCRM9250N_CassetteStatusEx sCassetteStatus;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 30)] public byte[] abyDevStatusInfo;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 45)] public byte[] abySensorStatusInfo;
    public byte byRC1PlugRemove;
    public byte byRC2PlugRemove;
    public byte byRC3PlugRemove;
    public byte byRC4PlugRemove;
    public byte byACPlugRemove;
    public byte byATCPlugRemove;
    public byte bySpeedStrategy;
    public byte byRC9PlugRemove;
    public byte byRC10PlugRemove;
    public byte byRC11PlugRemove;
    public byte byRC12PlugRemove;
    public byte byAC2PlugRemove;
    public byte byOutPosHasNotes;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 19)] public byte[] abyReserved;
}
