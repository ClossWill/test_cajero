using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace CRM9250N_WinFormDemo
{

	#region  Struct
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tDevReturn
	{
		public int iLogicCode;         
		public int iPhyCode;             
		public int iHandle;               
		public int iType;      
		
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
		public byte[] acDevReturn;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
		public byte[] acReserve;       
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tCRM9250N_InitSettingEx
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] abyCassetteDisable;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public ushort[] awAcceptableDenom;
		public sbyte cSubType;
		public byte byAtcMode;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] abyDRCCassetteCfg;
		public byte byAcMode;
        public byte byWorkingMode;     // [in] Set 0-CRM Machine Mode
        public byte byDcAccepetAllDenom;    // [in] Set 1-Receive all denominations in the "awAcceptableDenom" 
        public byte byInitCfg; // [in] Set 0-Normal mode. During initialization, check the AC box.
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
		public byte[] abyReserved;         
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tCRM9250N_BinStatus
	{
		public byte byStatus;
		public byte byExist;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tCRM9250N_CassetteStatusEx
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public tCRM9250N_BinStatus[] asRcStatus;

		public tCRM9250N_BinStatus sAtcStatus;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
		public tCRM9250N_BinStatus[] sAcStatus;

		public tCRM9250N_BinStatus sLrbStatus;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tCRM9250N_BinInfo
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
		public byte[] abyLogID;

		public byte byCountryCode;
		public byte byCassetteType;
		public ushort wNoteType;
		public byte byFunction;
		public byte byStoreType;
		public ushort wRCAsDC;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 30)]
		public byte[] abyReserved;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tCRM9250N_CassetteInfoEx
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] abyCassetteDisable;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
		public tCRM9250N_BinInfo[] sAcCassette;

		public tCRM9250N_BinInfo sAtcCassette;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public tCRM9250N_BinInfo[] asRcCassette;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] abyReserved;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tCRM9250N_InOutUnitsInfo
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
		public int[] aiRcInOutNum;

		public int iAcInOutNum;
		public int iLrbInOutNum;
		public int iAtcInOutNum;
		public int iNeInOutNum;
		public int iNfInOutNum;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] abyReserved;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tCRM9250N_InOutUnitsInfoEx
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public int[] aiRcInOutNum;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
		public int[] iAcInOutNum;

		public int iLrbInOutNum;
		public int iAtcInOutNum;
		public int iNeInOutNum;
		public int iNfInOutNum;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] abyReserved;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tCRM9250N_InitInfoEx
	{
		public tCRM9250N_InOutUnitsInfoEx sInOutUnitsInfo;
		public byte byMode;
        public byte byNEMode;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 30)]
		public byte[] abyReserved;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tCRM9250N_DenomAndNum
	{
		public ushort wCode;
		public int iNum;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tCRM9250N_DenomNumInfo
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public tCRM9250N_DenomAndNum[] asDenomInfoL4A;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public tCRM9250N_DenomAndNum[] asDenomInfoL4B;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public tCRM9250N_DenomAndNum[] asDenomInfoL3;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public tCRM9250N_DenomAndNum[] asDenomInfoL2;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public tCRM9250N_DenomAndNum[] asDenomInfoReserved1;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public tCRM9250N_DenomAndNum[] asDenomInfoReserved2;
		public int iUnknownCount;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tCRM9250N_CashCount
	{
		public int iDepositLimit;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public ushort[] awUnacceptableDenom;

		public int iTotalCount;
		public int iRejectCount;
		public tCRM9250N_DenomNumInfo sDenomNumInfo;
		public uint dwAmountLimit;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
		public byte[] abyReserved;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tCRM9250N_CancelDepositEx
	{
		public tCRM9250N_InOutUnitsInfoEx sInOutUnitsInfo;
		public tCRM9250N_DenomNumInfo sDenomNumInfo;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] abyReserved;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tCRM9250N_StoreMoneyEx
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] abyCassetteDisable;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] abyCassettePriority;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public ushort[] awUnacceptableDenom;

		public tCRM9250N_InOutUnitsInfoEx sInOutUnitsInfo;
		public tCRM9250N_DenomNumInfo sDenomNumInfo;
		public byte byRejectPos;
        public byte byNvMode;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 30)]
		public byte[] abyReserved;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tCRM9250N_ReqDispInfo
	{
		public ushort wDenomOrCassetteInfo;
		public int iNum;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tCRM9250N_DispenseInfoEx
	{
		public byte byDispType;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public tCRM9250N_ReqDispInfo[] asReqDispInfo;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] abyCassetteDisable;

		public int iTotalCount;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public tCRM9250N_DenomAndNum[] asDenomNum;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public int[] aiDispensedInfo;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public int[] aiRejectInfo;

		public tCRM9250N_InOutUnitsInfoEx sInOutUnitsInfo;
		public byte byNFNotesPolicy;
		public byte byRejectToAc;
		public byte byCountEnable;
		public byte byCountError;
		public byte byCountNVPolicy;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
		public byte[] abyReserved;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tCRM9250N_RetractInfoEx
	{
		public byte byRetractType;
		public byte byRetractPos;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] abyCassetteDisable;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] abyCassettePriority;

		public int iTotalCount;          
		public tCRM9250N_DenomNumInfo sDenomNumInfo;
		public tCRM9250N_InOutUnitsInfoEx sInOutUnitsInfo;
        public byte abyRetractFlag;                // [in] set 0
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 31)]
		public byte[] abyReserved;      
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tCRM9250N_DevVersionInfoEx
	{
		public sbyte cSubType;                      
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acFwMainBoard;            
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acFwUpperBoard;            
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
		public byte[] acFwAc;                
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
		public byte[] acFwLrb;                      
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
		public byte[] acFwRc;                
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acFwAtc;           
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acHwNvInfo;                 
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acFwDSP;                 
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acFwGraphicsFPGA;    
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acFwArithmetic;             
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acFwMagneticArithmetic;       
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acFwMagneticFPGA;          
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acFwFullFormatThickness;  
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acFwUltravioletArithmetic; 
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acFwDSPGraphicsArithmetic; 
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acFwFSNArithmetic;      
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acFwECBSoftware;          
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acHwInfo;                
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acHwNv;                       
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acHwUnits;                 

		public sbyte bySecFlag;                  

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acFwDriverBoard;         

		public sbyte byFsnCertification;
		public sbyte byCassetteLogicNums;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 253)]
		public byte[] acReserved;               
	}

	#endregion

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tGRGNoteGoDest
	{
		public byte iType;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
		public byte[] acDest;    
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct SYSTEMTIME
	{
		public ushort wYear;
		public ushort wMonth;
		public ushort wDayOfWeek;
		public ushort wDay;
		public ushort wHour;
		public ushort wMinute;
		public ushort wSecond;
		public ushort wMilliseconds;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tGRGSegSnData
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public uint[] adwData;      
	};

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tGRGSegOneSnInfo
	{
		public ushort wNum;
		public ushort wHeight;
		public ushort wWidth;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
		public tGRGSegSnData[] asSnData;
	};

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tGRGNoteDetails
	{
		public uint uiID;              
		public SYSTEMTIME sSysTime;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acJournalNo;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
		public byte[] acAccountNo;
		public byte byTimes;
		public ushort wNoteIndex;
		public byte byValidation;
		public byte byFaceWay;
		public ushort wDenomination;
		public tGRGNoteGoDest sNoteGoDest;     
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
		public byte[] acImageFileName;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] acSerialNo;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
		public byte[] acMac;
		public byte byMatchCount;     
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct tGRGNoteOCRInfo
	{
		public ushort wNoteIndex;
		public tGRGSegOneSnInfo asSnInfo;               
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
		public byte[] abyInfoErrCode;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
		public byte[] abyRejectErrCode;
		public byte byQuality;                 
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 260)]
		public byte[] acImageNameFront;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 260)]
		public byte[] acImageNameBack;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] abyReserved;
	};

	class CRM9250N
    {

		[DllImport("CRM9250NDev.dll", EntryPoint = "iGetNvNotesInfoEx")]
		public static extern int iGetNvNotesInfoEx(IntPtr p_hDevHandle, IntPtr p_psNoteDetails, IntPtr p_psNoteOCRInfo, ref int p_puiNumOfItems, byte p_byNoteType, byte[] p_pcFsnFileName, IntPtr p_psStatus);

		[DllImport("CRM9250NDev.dll",  EntryPoint = "hOpenLogicDevice")]
		public static extern IntPtr hOpenLogicDevice(string p_pcLogicDevName);

		[DllImport("CRM9250NDev.dll", EntryPoint = "iSetCommPara" )]
		public static extern int iSetCommPara(IntPtr p_hDevHandle, IntPtr p_psStatus);

		[DllImport("CRM9250NDev.dll", EntryPoint = "iPowerOnInitEx")]
		public static extern int iPowerOnInitEx(IntPtr p_hDevHandle, IntPtr p_psInitSetting, IntPtr p_psCassetteInfo, IntPtr p_psCassetteStatus, IntPtr p_psInitInfo, IntPtr p_psStatus);
		
		[DllImport("CRM9250NDev.dll", EntryPoint = "iGetDevVersionEx")]
		public static extern int iGetDevVersionEx(IntPtr p_hDevHandle, IntPtr p_psDevVersion, IntPtr p_psStatus);

		[DllImport("CRM9250NDev.dll", EntryPoint = "iFirmwareDownload")]
		public static extern int iFirmwareDownload(IntPtr p_hDevHandle, string p_pcDownloadPath, byte p_byMode, IntPtr p_psStatus);

		[DllImport("CRM9250NDev.dll", EntryPoint = "iPrepareDeposit")]
		public static extern int iPrepareDeposit(IntPtr p_hDevHandle, IntPtr p_psStatus);

		[DllImport("CRM9250NDev.dll",  EntryPoint = "iOpenShutter")]
		public static extern int iOpenShutter(IntPtr p_hDevHandle, IntPtr p_psStatus);

		[DllImport("CRM9250NDev.dll",  EntryPoint = "iCloseShutter")]
		public static extern int iCloseShutter(IntPtr p_hDevHandle, IntPtr p_psStatus);

		[DllImport("CRM9250NDev.dll", EntryPoint = "iCashCount")]
		public static extern int iCashCount(IntPtr p_hDevHandle, IntPtr p_psCashCountInfo, IntPtr p_psStatus);

		[DllImport("CRM9250NDev.dll", EntryPoint = "iCancelDepositEx")]
		public static extern int iCancelDepositEx(IntPtr p_hDevHandle, IntPtr p_psCancelDeposit, IntPtr p_psStatus);

		[DllImport("CRM9250NDev.dll", EntryPoint = "iStoreMoneyEx")]
		public static extern int iStoreMoneyEx(IntPtr p_hDevHandle, IntPtr p_psStoreMoney, IntPtr p_psStatus);

		[DllImport("CRM9250NDev.dll", EntryPoint = "iPrepareNextTransaction")]
		public static extern int iPrepareNextTransaction(IntPtr p_hDevHandle, IntPtr p_psStatus);

		[DllImport("CRM9250NDev.dll",  EntryPoint = "iPrepareDispense")]
		public static extern int iPrepareDispense(IntPtr p_hDevHandle, IntPtr p_psStatus);

		[DllImport("CRM9250NDev.dll", EntryPoint = "iDispenseEx")]
		public static extern int iDispenseEx(IntPtr p_hDevHandle, IntPtr p_psDispenseInfo, IntPtr p_psStatus);

		[DllImport("CRM9250NDev.dll",  EntryPoint = "iRetractEx")]
		public static extern int iRetractEx(IntPtr p_hDevHandle, IntPtr p_psRetractInfo, IntPtr p_psStatus);
	}
}
