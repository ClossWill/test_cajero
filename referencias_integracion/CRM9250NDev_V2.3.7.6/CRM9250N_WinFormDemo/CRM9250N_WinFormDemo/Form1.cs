using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace CRM9250N_WinFormDemo
{
    public partial class Form1 : Form
    {
        private IntPtr m_hDevHandle;
        private Struct[] IntPtrToStructarray<Struct>(System.IntPtr intPtr)
        {
            Struct[] StructReturn = new Struct[8];
            for (int i = 0; i < StructReturn.Length; i++)
            {
                IntPtr ptr = (IntPtr)((UInt32)intPtr + i * Marshal.SizeOf(typeof(Struct)));
                StructReturn[i] = (Struct)Marshal.PtrToStructure(ptr, typeof(Struct));
            }
            return StructReturn;
        }
        public Form1()
        {
            InitializeComponent();
        }

        private void iOpenShutter_Click(object sender, EventArgs e)
        {
            System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);
            int ret = CRM9250N.iOpenShutter(m_hDevHandle, I_DevReturn);
            tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
            text1.Text = "iOpenShutter = " + (ret == 0 ? "success" : "fail") + "，LogicCode = " + DevReturn[0].iLogicCode;
            Marshal.FreeHGlobal(I_DevReturn);
        }

        private void iCloseShutter_Click(object sender, EventArgs e)
        {
            System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);
            int ret = CRM9250N.iCloseShutter(m_hDevHandle, I_DevReturn);
            tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
            text1.Text = "iCloseShutter = " + (ret == 0 ? "success" : "fail") + "，LogicCode = " + DevReturn[0].iLogicCode;
            Marshal.FreeHGlobal(I_DevReturn);
        }

        private void iPrepareNextTransaction_Click(object sender, EventArgs e)
        {
            System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);
            int ret = CRM9250N.iPrepareNextTransaction(m_hDevHandle, I_DevReturn);
            tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
            text1.Text = "iPrepareNextTransaction=" + (ret == 0 ? "success" : "fail") + "，LogicCode = " + DevReturn[0].iLogicCode;
            Marshal.FreeHGlobal(I_DevReturn);
        }

        private void iPrepareDispense_Click(object sender, EventArgs e)
        {
            System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);
            int ret = CRM9250N.iPrepareDispense(m_hDevHandle, I_DevReturn);
            tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);

            text1.Text = "iPrepareDeposit = " + (ret == 0 ? "success" : "fail") + "，LogicCode=" + DevReturn[0].iLogicCode.ToString();
            Marshal.FreeHGlobal(I_DevReturn);
        }

        private void iPrepareDeposit_Click(object sender, EventArgs e)
        {
            System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);
            int ret = CRM9250N.iPrepareDeposit(m_hDevHandle, I_DevReturn);
            tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
            text1.Text = "iPrepareDeposit = " + (ret == 0 ? "success" : "fail") + "，LogicCode = " + DevReturn[0].iLogicCode;
            Marshal.FreeHGlobal(I_DevReturn);
        }

        private void hOpenLogicDevice_Click(object sender, EventArgs e)
        {
            m_hDevHandle = CRM9250N.hOpenLogicDevice("CRM9250NDev");
            if (m_hDevHandle != null)
                text1.Text = "OpenLogicDevice Success";
            else
                text1.Text = "OpenLogicDevice Fail";
        }

        private void iSetCommPara_Click(object sender, EventArgs e)
        {
            System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);
            int ret = CRM9250N.iSetCommPara(m_hDevHandle, I_DevReturn);
            tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
            Marshal.FreeHGlobal(I_DevReturn);

            text1.Text = "iSetCommPara = " + (ret == 0 ? "success" : "fail") + "，LogicCode = " + DevReturn[0].iLogicCode;
        }

        [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize", CallingConvention = CallingConvention.StdCall)]
        private static extern int SetProcessWorkingSetSize(IntPtr process, int minSize, int maxSize);
        private void clear_Click(object sender, EventArgs e)
        {
            text1.Text = "";
            GC.Collect();
            GC.WaitForPendingFinalizers();
            SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1);



        }

        private void iCashCount_Click(object sender, EventArgs e)
        {
            tCRM9250N_CashCount CashCount = new tCRM9250N_CashCount();
            CashCount.iDepositLimit = 200;
            CashCount.awUnacceptableDenom = new ushort[256];
            CashCount.awUnacceptableDenom[0] = 0x116; CashCount.awUnacceptableDenom[1] = 0x117;
            System.IntPtr I_CashCount = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_CashCount)));
            Marshal.StructureToPtr(CashCount, I_CashCount, true);
            System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);
            int ret = CRM9250N.iCashCount(m_hDevHandle, I_CashCount, I_DevReturn);

            tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
            IntPtr ptr = (IntPtr)((UInt32)I_CashCount);
            tCRM9250N_CashCount StructReturn = (tCRM9250N_CashCount)Marshal.PtrToStructure(ptr, typeof(tCRM9250N_CashCount));

            Marshal.FreeHGlobal(I_CashCount); Marshal.FreeHGlobal(I_DevReturn);
            text1.Text = "iCashCount = " + (ret == 0 ? "success" : "fail") + "，LogicCode = " + DevReturn[0].iLogicCode
            + "\r\nTotalCount：" + StructReturn.iTotalCount + ",RejectCount：" + StructReturn.iRejectCount;
        }


        private void iCancelDeposit_Click(object sender, EventArgs e)
        {
            System.IntPtr I_CancelDeposit = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_CancelDepositEx)) * 8);
            System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);
            int ret = CRM9250N.iCancelDepositEx(m_hDevHandle, I_CancelDeposit, I_DevReturn);
            tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
            tCRM9250N_CancelDepositEx[] CancelDeposit = IntPtrToStructarray<tCRM9250N_CancelDepositEx>(I_CancelDeposit);
            Marshal.FreeHGlobal(I_CancelDeposit); Marshal.FreeHGlobal(I_DevReturn);

            text1.Text = "iCancelDeposit = " + (ret == 0 ? "success" : "fail") + "，LogicCode = " + DevReturn[0].iLogicCode + "\r\n" +
            "RC1:" + CancelDeposit[0].sInOutUnitsInfo.aiRcInOutNum[0] + ",RC2:" + CancelDeposit[0].sInOutUnitsInfo.aiRcInOutNum[1] + ",RC3:" + CancelDeposit[0].sInOutUnitsInfo.aiRcInOutNum[2] + ",RC4:" + CancelDeposit[0].sInOutUnitsInfo.aiRcInOutNum[3] +
            ",AC:" + CancelDeposit[0].sInOutUnitsInfo.iAcInOutNum[0] + ",NE:" + CancelDeposit[0].sInOutUnitsInfo.iNeInOutNum + ",NF:" + CancelDeposit[0].sInOutUnitsInfo.iNfInOutNum;
        }


        private void iPowerOnInitEx_Click(object sender, EventArgs e)
        {
            tCRM9250N_InitSettingEx InitSetting = new tCRM9250N_InitSettingEx();
            tCRM9250N_CassetteInfoEx CassetteInfo = new tCRM9250N_CassetteInfoEx();
            InitSetting.byAtcMode = 1;
            InitSetting.byAcMode = 0;
            InitSetting.byWorkingMode = 0;
            InitSetting.byDcAccepetAllDenom = 1;
            InitSetting.byInitCfg = 0;
            InitSetting.abyCassetteDisable = new byte[16];
            InitSetting.awAcceptableDenom = new ushort[256];
            CassetteInfo.abyCassetteDisable = new byte[16];
            for (int box = 0; box < 4; box++) 
            {
                InitSetting.abyCassetteDisable[box] = 0;    //0-enable
                CassetteInfo.abyCassetteDisable[box] = 0;
            }
            //GRG test note denomination ID
            InitSetting.awAcceptableDenom[0] = 0xEE37; InitSetting.awAcceptableDenom[1] = 0xEE36; InitSetting.awAcceptableDenom[2] = 0xEE35;
            InitSetting.awAcceptableDenom[3] = 0xEE34; InitSetting.awAcceptableDenom[4] = 0xEE33; InitSetting.awAcceptableDenom[5] = 0xEF13;
            InitSetting.awAcceptableDenom[6] = 0xEF14; InitSetting.awAcceptableDenom[7] = 0xEF15; InitSetting.awAcceptableDenom[8] = 0xEF16;
            InitSetting.awAcceptableDenom[9] = 0xEF17; InitSetting.awAcceptableDenom[10] = 0xEF18; InitSetting.awAcceptableDenom[11] = 0xF113;
            InitSetting.awAcceptableDenom[12] = 0xF114; InitSetting.awAcceptableDenom[13] = 0xF115; InitSetting.awAcceptableDenom[14] = 0xF116;
            InitSetting.awAcceptableDenom[15] = 0xF117; InitSetting.awAcceptableDenom[16] = 0xF118; InitSetting.awAcceptableDenom[17] = 0xF119;
            InitSetting.awAcceptableDenom[18] = 0xF123; InitSetting.awAcceptableDenom[19] = 0xF124; InitSetting.awAcceptableDenom[20] = 0xF125;
            InitSetting.awAcceptableDenom[21] = 0xF126; InitSetting.awAcceptableDenom[22] = 0xF127; InitSetting.awAcceptableDenom[23] = 0xF128;
            InitSetting.awAcceptableDenom[24] = 0xF129; InitSetting.awAcceptableDenom[25] = 0xFA14; InitSetting.awAcceptableDenom[26] = 0xD16;
            InitSetting.awAcceptableDenom[27] = 0xD17; InitSetting.awAcceptableDenom[28] = 0xD19; InitSetting.awAcceptableDenom[29] = 0xD1A;
            InitSetting.awAcceptableDenom[30] = 0xD1C;

            System.IntPtr I_InitSetting = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_InitSettingEx)));
            Marshal.StructureToPtr(InitSetting, I_InitSetting, true);
            System.IntPtr I_CassetteInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_CassetteInfoEx)));
            Marshal.StructureToPtr(CassetteInfo, I_CassetteInfo, true);
            System.IntPtr I_psCassetteStatus = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_CassetteStatusEx)) * 8);
            System.IntPtr I_InitInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_InitInfoEx)) * 8);
            System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);

            int ret = CRM9250N.iPowerOnInitEx(m_hDevHandle, I_InitSetting, I_CassetteInfo, I_psCassetteStatus, I_InitInfo, I_DevReturn);
            tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
            tCRM9250N_InitInfoEx[] InitInfo = IntPtrToStructarray<tCRM9250N_InitInfoEx>(I_InitInfo);

            text1.Text = "iPowerOnInitEx = " + (ret == 0 ? "success" : "fail") + "，LogicCode = " + DevReturn[0].iLogicCode + "\r\n" +
            "AC:" + InitInfo[0].sInOutUnitsInfo.iAcInOutNum[0] + ",NE:" + InitInfo[0].sInOutUnitsInfo.iNeInOutNum + ",NF:" + InitInfo[0].sInOutUnitsInfo.iNfInOutNum;
            Marshal.FreeHGlobal(I_InitSetting); Marshal.FreeHGlobal(I_CassetteInfo);
            Marshal.FreeHGlobal(I_psCassetteStatus); Marshal.FreeHGlobal(I_InitInfo); Marshal.FreeHGlobal(I_DevReturn);
        }

        private void iDispenseEx_Click(object sender, EventArgs e)
        {
            tCRM9250N_DispenseInfoEx DispenseInfo = new tCRM9250N_DispenseInfoEx();
            DispenseInfo.byDispType = 1;
            DispenseInfo.byRejectToAc = 1;
            DispenseInfo.asReqDispInfo = new tCRM9250N_ReqDispInfo[16];
            DispenseInfo.asReqDispInfo[0].wDenomOrCassetteInfo = 2;     // RC2
            DispenseInfo.asReqDispInfo[0].iNum = 2;
            DispenseInfo.abyCassetteDisable = new byte[16];
            for (int box = 0; box < 4; box++)
            {
                DispenseInfo.abyCassetteDisable[box] = 0;
            }
            
            System.IntPtr I_DispenseInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_DispenseInfoEx)) * 8);
            Marshal.StructureToPtr(DispenseInfo, I_DispenseInfo, true);
            System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);
            int ret = CRM9250N.iDispenseEx(m_hDevHandle, I_DispenseInfo, I_DevReturn);
            tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
            tCRM9250N_DispenseInfoEx[] DispenseInfoRe = IntPtrToStructarray<tCRM9250N_DispenseInfoEx>(I_DispenseInfo);

            text1.Text = "iDispenseEx=" + (ret == 0 ? "success" : "fail") + "，LogicCode = "  + DevReturn[0].iLogicCode + " ,TotalCount：" + DispenseInfoRe[0].iTotalCount + "\r\n" +
            "RC1:" + DispenseInfoRe[0].sInOutUnitsInfo.aiRcInOutNum[0] + ",RC2:" + DispenseInfoRe[0].sInOutUnitsInfo.aiRcInOutNum[1] + ",RC3:" + DispenseInfoRe[0].sInOutUnitsInfo.aiRcInOutNum[2] + ",RC4:" + DispenseInfoRe[0].sInOutUnitsInfo.aiRcInOutNum[3] +
            ",AC:" + DispenseInfoRe[0].sInOutUnitsInfo.iAcInOutNum[0] + ",LRB:" + DispenseInfoRe[0].sInOutUnitsInfo.iLrbInOutNum + ",NE:" + DispenseInfoRe[0].sInOutUnitsInfo.iNeInOutNum + ",NF:" + DispenseInfoRe[0].sInOutUnitsInfo.iNfInOutNum;
            Marshal.FreeHGlobal(I_DispenseInfo); Marshal.FreeHGlobal(I_DevReturn);
        }

        private void RetractNE_Click(object sender, EventArgs e)
        {
            tCRM9250N_RetractInfoEx RetractInfo = new tCRM9250N_RetractInfoEx();
            RetractInfo.byRetractType = 2;
            RetractInfo.byRetractPos = 2;  //AC
            RetractInfo.abyCassetteDisable = new byte[16];
            for (int box = 0; box < 4; box++)
                RetractInfo.abyCassetteDisable[box] = 0;
            RetractInfo.abyCassettePriority = new byte[16];
            RetractInfo.abyCassettePriority[0] = 1; RetractInfo.abyCassettePriority[1] = 2;
            RetractInfo.abyCassettePriority[2] = 3; RetractInfo.abyCassettePriority[3] = 4;

            System.IntPtr I_RetractInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_RetractInfoEx)) * 8);
            Marshal.StructureToPtr(RetractInfo, I_RetractInfo, true);
            System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);

            int ret = CRM9250N.iRetractEx(m_hDevHandle, I_RetractInfo, I_DevReturn);

            tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
            tCRM9250N_RetractInfoEx[] RetractInfoRe = IntPtrToStructarray<tCRM9250N_RetractInfoEx>(I_RetractInfo);

            text1.Text = "RetractNE=" + (ret == 0 ? "success" : "fail") + "，LogicCode=" + DevReturn[0].iLogicCode + ",TotalCount：" + RetractInfoRe[0].iTotalCount + "\r\n" +
            "RC1:" + RetractInfoRe[0].sInOutUnitsInfo.aiRcInOutNum[0] + ",RC2:" + RetractInfoRe[0].sInOutUnitsInfo.aiRcInOutNum[1] + ",RC3:" + RetractInfoRe[0].sInOutUnitsInfo.aiRcInOutNum[2] + ",RC4:" + RetractInfoRe[0].sInOutUnitsInfo.aiRcInOutNum[3] +
            ",AC:" + RetractInfoRe[0].sInOutUnitsInfo.iAcInOutNum + ",LRB:" + RetractInfoRe[0].sInOutUnitsInfo.iLrbInOutNum +  ",NE:" + RetractInfoRe[0].sInOutUnitsInfo.iNeInOutNum + ",NF:" + RetractInfoRe[0].sInOutUnitsInfo.iNfInOutNum;
            Marshal.FreeHGlobal(I_RetractInfo); Marshal.FreeHGlobal(I_DevReturn);
        }

        private void RetractNF_Click(object sender, EventArgs e)
        {
            tCRM9250N_RetractInfoEx RetractInfo = new tCRM9250N_RetractInfoEx();
            RetractInfo.byRetractType = 1;
            RetractInfo.byRetractPos = 2;  
            RetractInfo.abyCassetteDisable = new byte[16];
            for (int box = 0; box < 4; box++)
                RetractInfo.abyCassetteDisable[box] = 0;
            RetractInfo.abyCassettePriority = new byte[16];
            RetractInfo.abyCassettePriority[0] = 1; RetractInfo.abyCassettePriority[1] = 2;
            RetractInfo.abyCassettePriority[2] = 3; RetractInfo.abyCassettePriority[3] = 4;

            System.IntPtr I_RetractInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_RetractInfoEx)) * 8);
            Marshal.StructureToPtr(RetractInfo, I_RetractInfo, true);
            System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);

            int ret = CRM9250N.iRetractEx(m_hDevHandle, I_RetractInfo, I_DevReturn);

            tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
            tCRM9250N_RetractInfoEx[] RetractInfoRe = IntPtrToStructarray<tCRM9250N_RetractInfoEx>(I_RetractInfo);

            text1.Text = "RetractNF=" + (ret == 0 ? "success" : "fail") + "，LogicCode=" + DevReturn[0].iLogicCode + ",TotalCount：" + RetractInfoRe[0].iTotalCount + "\r\n" +
            "RC1:" + RetractInfoRe[0].sInOutUnitsInfo.aiRcInOutNum[0] + ",RC2:" + RetractInfoRe[0].sInOutUnitsInfo.aiRcInOutNum[1] + ",RC3:" + RetractInfoRe[0].sInOutUnitsInfo.aiRcInOutNum[2] + ",RC4:" + RetractInfoRe[0].sInOutUnitsInfo.aiRcInOutNum[3] +
            ",AC:" + RetractInfoRe[0].sInOutUnitsInfo.iAcInOutNum + ",LRB:" + RetractInfoRe[0].sInOutUnitsInfo.iLrbInOutNum  + ",NE:" + RetractInfoRe[0].sInOutUnitsInfo.iNeInOutNum + ",NF:" + RetractInfoRe[0].sInOutUnitsInfo.iNfInOutNum;
            Marshal.FreeHGlobal(I_RetractInfo); Marshal.FreeHGlobal(I_DevReturn);
        }

        #region
        //private void iInitSetting_Click(object sender, EventArgs e)
        //{
        //    tCRM9250N_InitSetting InitSetting = new tCRM9250N_InitSetting();
        //    InitSetting.abyCassetteDisable = new byte[8];
        //    InitSetting.awAcceptableDenom = new ushort[256];
        //    InitSetting.byAtcMode = 1;
        //    for (int i = 0; i < 3; i++)
        //        InitSetting.abyCassetteDisable[i] = 0;
        //    //仿真钞
        //    InitSetting.awAcceptableDenom[0] = 0xEE37; InitSetting.awAcceptableDenom[1] = 0xEE36; InitSetting.awAcceptableDenom[2] = 0xEE35;
        //    InitSetting.awAcceptableDenom[3] = 0xEE34; InitSetting.awAcceptableDenom[4] = 0xEE33; InitSetting.awAcceptableDenom[5] = 0xEF13;
        //    InitSetting.awAcceptableDenom[6] = 0xEF14; InitSetting.awAcceptableDenom[7] = 0xEF15; InitSetting.awAcceptableDenom[8] = 0xEF16;
        //    InitSetting.awAcceptableDenom[9] = 0xEF17; InitSetting.awAcceptableDenom[10] = 0xEF18; InitSetting.awAcceptableDenom[11] = 0xF113;
        //    InitSetting.awAcceptableDenom[12] = 0xF114; InitSetting.awAcceptableDenom[13] = 0xF115; InitSetting.awAcceptableDenom[14] = 0xF116;
        //    InitSetting.awAcceptableDenom[15] = 0xF117; InitSetting.awAcceptableDenom[16] = 0xF118; InitSetting.awAcceptableDenom[17] = 0xF119;
        //    InitSetting.awAcceptableDenom[18] = 0xF123; InitSetting.awAcceptableDenom[19] = 0xF124; InitSetting.awAcceptableDenom[20] = 0xF125;
        //    InitSetting.awAcceptableDenom[21] = 0xF126; InitSetting.awAcceptableDenom[22] = 0xF127; InitSetting.awAcceptableDenom[23] = 0xF128;
        //    InitSetting.awAcceptableDenom[24] = 0xF129; InitSetting.awAcceptableDenom[25] = 0xFA14; InitSetting.awAcceptableDenom[26] = 0xD16;
        //    InitSetting.awAcceptableDenom[27] = 0xD17; InitSetting.awAcceptableDenom[28] = 0xD19; InitSetting.awAcceptableDenom[29] = 0xD1A;
        //    InitSetting.awAcceptableDenom[30] = 0xD1C;

        //    System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);
        //    System.IntPtr I_InitSetting = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_InitSetting)) * 8);
        //    Marshal.StructureToPtr(InitSetting, I_InitSetting, true);

        //    int ret = CRM9250N.iInitSetting(m_hDevHandle, I_InitSetting, I_DevReturn);
        //    tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);

        //    text1.Text = "iInitSetting()=" + ret + ",LogicCode =" + DevReturn[0].iLogicCode;
        //    Marshal.FreeHGlobal(I_DevReturn); Marshal.FreeHGlobal(I_InitSetting);
        //}

        //private void iGetCassetteInfo_Click(object sender, EventArgs e)
        //{
        //    System.IntPtr I_CassetteInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_CassetteInfo)) * 8);
        //    System.IntPtr I_psCassetteStatus = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_CassetteStatus)) * 8);
        //    System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);

        //    int ret = CRM9250N.iGetCassetteInfo(m_hDevHandle, I_CassetteInfo, I_psCassetteStatus, I_DevReturn);

        //    tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
        //    tCRM9250N_CassetteStatus[] CassetteStatus = IntPtrToStructarray<tCRM9250N_CassetteStatus>(I_psCassetteStatus);
        //    tCRM9250N_CassetteInfo[] CassetteInfoRE = IntPtrToStructarray<tCRM9250N_CassetteInfo>(I_CassetteInfo);
        //    Marshal.FreeHGlobal(I_CassetteInfo); Marshal.FreeHGlobal(I_psCassetteStatus); Marshal.FreeHGlobal(I_DevReturn);

        //    text1.Text = "iGetCassetteInfo()=" + ret + ",LogicCode = " + DevReturn[0].iLogicCode +
        //    "\r\nAC,Status:" + CassetteStatus[0].sAcStatus.byStatus + ",Exist: " + CassetteStatus[0].sAcStatus.byExist
        //    + "\r\nRC1,Status:" + CassetteStatus[0].asRcStatus[0].byStatus + ",Exist: " + CassetteStatus[0].asRcStatus[0].byExist
        //    + "\r\nRC2,Status:" + CassetteStatus[0].asRcStatus[1].byStatus + ",Exist: " + CassetteStatus[0].asRcStatus[1].byExist
        //    + "\r\nRC3,Status:" + CassetteStatus[0].asRcStatus[2].byStatus + ",Exist: " + CassetteStatus[0].asRcStatus[2].byExist
        //    + "\r\nRC4,Status:" + CassetteStatus[0].asRcStatus[3].byStatus + ",Exist: " + CassetteStatus[0].asRcStatus[3].byExist;

        //    text1.Text += "\r\nRC1:ID:" + BitConverter.ToString(CassetteInfoRE[0].asRcCassette[0].abyLogID) + ",CassType:" +
        //    CassetteInfoRE[0].asRcCassette[0].byCassetteType + ",NoteType:" + CassetteInfoRE[0].asRcCassette[0].wNoteType
        //    + "\r\nRC2:ID:" + BitConverter.ToString(CassetteInfoRE[0].asRcCassette[1].abyLogID) + ",CassType:" +
        //    CassetteInfoRE[0].asRcCassette[1].byCassetteType + ",NoteType:" + CassetteInfoRE[0].asRcCassette[1].wNoteType
        //    + "\r\nRC3:ID:" + BitConverter.ToString(CassetteInfoRE[0].asRcCassette[2].abyLogID) + ",CassType:" +
        //    CassetteInfoRE[0].asRcCassette[2].byCassetteType + ",NoteType:" + CassetteInfoRE[0].asRcCassette[2].wNoteType
        //    + "\r\nRC4:ID:" + BitConverter.ToString(CassetteInfoRE[0].asRcCassette[3].abyLogID) + ",CassType:" +
        //    CassetteInfoRE[0].asRcCassette[3].byCassetteType + ",NoteType:" + CassetteInfoRE[0].asRcCassette[3].wNoteType;
        //}

        //private void iInit_Click(object sender, EventArgs e)
        //{
        //    System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);
        //    System.IntPtr I_InitInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_InitInfo)) * 8);
        //    int ret = CRM9250N.iInit(m_hDevHandle, I_InitInfo, I_DevReturn);
        //    tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
        //    tCRM9250N_InitInfo[] InitInfo = IntPtrToStructarray<tCRM9250N_InitInfo>(I_InitInfo);
        //    text1.Text = "iInit=" + ret + "，LogicCode=" + DevReturn[0].iLogicCode + "\r\n" +
        //    "RC1:" + InitInfo[0].sInOutUnitsInfo.aiRcInOutNum[0] + ",RC2:" + InitInfo[0].sInOutUnitsInfo.aiRcInOutNum[1] + ",RC3:" + InitInfo[0].sInOutUnitsInfo.aiRcInOutNum[2] + ",RC4:" + InitInfo[0].sInOutUnitsInfo.aiRcInOutNum[3] +
        //    ",AC:" + InitInfo[0].sInOutUnitsInfo.iAcInOutNum + ",LRB:" + InitInfo[0].sInOutUnitsInfo.iLrbInOutNum + ",ATC:" + InitInfo[0].sInOutUnitsInfo.iAtcInOutNum + ",NE:" + InitInfo[0].sInOutUnitsInfo.iNeInOutNum + ",NF:" + InitInfo[0].sInOutUnitsInfo.iNfInOutNum;
        //    Marshal.FreeHGlobal(I_DevReturn); Marshal.FreeHGlobal(I_InitInfo);
        //}
        //private void iPowerOnInit_Click(object sender, EventArgs e)
        //{
        //    tCRM9250N_InitSetting InitSetting = new tCRM9250N_InitSetting();
        //    InitSetting.abyCassetteDisable = new byte[8];
        //    InitSetting.awAcceptableDenom = new ushort[256];
        //    InitSetting.byAtcMode = 1;
        //    for (int i = 0; i < 4; i++)
        //        InitSetting.abyCassetteDisable[i] = 0;
        //    InitSetting.awAcceptableDenom[0] = 295;
        //    InitSetting.awAcceptableDenom[1] = 311; InitSetting.awAcceptableDenom[2] = 327; InitSetting.awAcceptableDenom[3] = 343;
        //    InitSetting.awAcceptableDenom[4] = 326; InitSetting.awAcceptableDenom[5] = 342;

        //    tCRM9250N_CassetteInfo CassetteInfo = new tCRM9250N_CassetteInfo();
        //    CassetteInfo.abyCassetteDisable = new byte[8];
        //    for (int box = 0; box < 4; box++)
        //        CassetteInfo.abyCassetteDisable[box] = 0;

        //    System.IntPtr I_InitSetting = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_InitSetting)));
        //    Marshal.StructureToPtr(InitSetting, I_InitSetting, true);
        //    System.IntPtr I_CassetteInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_CassetteInfo)));
        //    Marshal.StructureToPtr(CassetteInfo, I_CassetteInfo, true);
        //    System.IntPtr I_psCassetteStatus = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_CassetteStatus)) * 8);
        //    System.IntPtr I_InitInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_InitInfo)) * 8);
        //    System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);

        //    int ret = CRM9250N.iPowerOnInit(m_hDevHandle, I_InitSetting, I_CassetteInfo, I_psCassetteStatus, I_InitInfo, I_DevReturn);
        //    tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
        //    tCRM9250N_InitInfo[] InitInfo = IntPtrToStructarray<tCRM9250N_InitInfo>(I_InitInfo);

        //    text1.Text = "iPowerOnInit=" + ret + "，LogicCode=" + DevReturn[0].iLogicCode + "\r\n" +
        //    "RC1:" + InitInfo[0].sInOutUnitsInfo.aiRcInOutNum[0] + ",RC2:" + InitInfo[0].sInOutUnitsInfo.aiRcInOutNum[1] + ",RC3:" + InitInfo[0].sInOutUnitsInfo.aiRcInOutNum[2] + ",RC4:" + InitInfo[0].sInOutUnitsInfo.aiRcInOutNum[3] +
        //    ",AC:" + InitInfo[0].sInOutUnitsInfo.iAcInOutNum + ",LRB:" + InitInfo[0].sInOutUnitsInfo.iLrbInOutNum + ",ATC:" + InitInfo[0].sInOutUnitsInfo.iAtcInOutNum + ",NE:" + InitInfo[0].sInOutUnitsInfo.iNeInOutNum + ",NF:" + InitInfo[0].sInOutUnitsInfo.iNfInOutNum;
        //    Marshal.FreeHGlobal(I_InitSetting); Marshal.FreeHGlobal(I_CassetteInfo);
        //    Marshal.FreeHGlobal(I_psCassetteStatus); Marshal.FreeHGlobal(I_InitInfo); Marshal.FreeHGlobal(I_DevReturn);
        //}

        //private void iDispense_Click(object sender, EventArgs e)
        //{
        //    tCRM9250N_DispenseInfo DispenseInfo = new tCRM9250N_DispenseInfo();
        //    DispenseInfo.byDispType = 1;   
        //    DispenseInfo.asReqDispInfo = new tCRM9250N_ReqDispInfo[8];
        //    DispenseInfo.abyCassetteDisable = new byte[8];

        //    DispenseInfo.asReqDispInfo[0].wDenomOrCassetteInfo = 2; // 钱箱 1 
        //    DispenseInfo.asReqDispInfo[0].iNum = 2; //出钞2张
        //    DispenseInfo.abyCassetteDisable[0] = 0;

        //    System.IntPtr I_DispenseInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_DispenseInfo)));
        //    Marshal.StructureToPtr(DispenseInfo, I_DispenseInfo, true);
        //    System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);
        //    int ret = CRM9250N.iDispense(m_hDevHandle,I_DispenseInfo, I_DevReturn);
        //    tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
        //    tCRM9250N_DispenseInfo[] DispenseInfoRe = IntPtrToStructarray<tCRM9250N_DispenseInfo>(I_DispenseInfo);

        //    text1.Text = "iDispense=" + ret + "，LogicCode=" + DevReturn[0].iLogicCode + "总张数：" + DispenseInfoRe[0].iTotalCount +"\r\n" +
        //    "RC1:" + DispenseInfoRe[0].sInOutUnitsInfo.aiRcInOutNum[0] + ",RC2:" + DispenseInfoRe[0].sInOutUnitsInfo.aiRcInOutNum[1] + ",RC3:" + DispenseInfoRe[0].sInOutUnitsInfo.aiRcInOutNum[2] + ",RC4:" + DispenseInfoRe[0].sInOutUnitsInfo.aiRcInOutNum[3] +
        //    ",AC:" + DispenseInfoRe[0].sInOutUnitsInfo.iAcInOutNum + ",LRB:" + DispenseInfoRe[0].sInOutUnitsInfo.iLrbInOutNum + ",ATC:" + DispenseInfoRe[0].sInOutUnitsInfo.iAtcInOutNum + ",NE:" + DispenseInfoRe[0].sInOutUnitsInfo.iNeInOutNum + ",NF:" + DispenseInfoRe[0].sInOutUnitsInfo.iNfInOutNum;
        //    Marshal.FreeHGlobal(I_DispenseInfo); Marshal.FreeHGlobal(I_DevReturn);
        //}

        #endregion

        private void StoreMoneyEx_Click(object sender, EventArgs e)
        {
            tCRM9250N_StoreMoneyEx StoreMoney = new tCRM9250N_StoreMoneyEx();
            StoreMoney.byRejectPos = 1;
            StoreMoney.byNvMode = 0;
            StoreMoney.abyCassetteDisable = new byte[16]; StoreMoney.abyCassettePriority = new byte[16];
            for (int i = 0; i < 4; i++)
                StoreMoney.abyCassetteDisable[i] = 0;
            StoreMoney.abyCassettePriority[0] = 1; StoreMoney.abyCassettePriority[1] = 2;
            StoreMoney.abyCassettePriority[2] = 3; StoreMoney.abyCassettePriority[3] = 4;
            // 这里使用 (Marshal.SizeOf(typeof(tCRM9250N_StoreMoney)))会失败
            System.IntPtr I_StoreMoney = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_StoreMoneyEx)) * 8);
            Marshal.StructureToPtr(StoreMoney, I_StoreMoney, true);
            System.IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);
            int ret = CRM9250N.iStoreMoneyEx(m_hDevHandle, I_StoreMoney, I_DevReturn);

            tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
            tCRM9250N_StoreMoneyEx[] StoreMoneyRe = IntPtrToStructarray<tCRM9250N_StoreMoneyEx>(I_StoreMoney);

            text1.Text = "iStoreMoneyEx = " + (ret == 0 ? "success" : "fail") + "，LogicCode = " + DevReturn[0].iLogicCode + "，NF：" + StoreMoneyRe[0].sInOutUnitsInfo.iNfInOutNum + ",NE：" + StoreMoneyRe[0].sInOutUnitsInfo.iNeInOutNum
            + "\r\nRC 1-4:";
            for (int i = 0; i < 4; i++)
                text1.Text += StoreMoneyRe[0].sInOutUnitsInfo.aiRcInOutNum[i] + ",";
            Marshal.FreeHGlobal(I_DevReturn); Marshal.FreeHGlobal(I_StoreMoney);
        }

        private void iGetDevVersionEx_Click(object sender, EventArgs e)
        {
            IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);
            IntPtr I_tCRM9250N_DevVersionInfoEx = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tCRM9250N_DevVersionInfoEx)) * 8);

            int ret = CRM9250N.iGetDevVersionEx(m_hDevHandle, I_tCRM9250N_DevVersionInfoEx, I_DevReturn);

            tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
            tCRM9250N_DevVersionInfoEx[] DevVersionInfoEx = IntPtrToStructarray<tCRM9250N_DevVersionInfoEx>(I_tCRM9250N_DevVersionInfoEx);
            Marshal.FreeHGlobal(I_DevReturn); Marshal.FreeHGlobal(I_tCRM9250N_DevVersionInfoEx);
            string acFwMainBoard = Encoding.ASCII.GetString(DevVersionInfoEx[0].acFwMainBoard); string acHwNv = Encoding.ASCII.GetString(DevVersionInfoEx[0].acHwNv);

            text1.Text = "iGetDevVersionEx = " + (ret == 0 ? "success" : "fail") + ",LogicCode = " + DevReturn[0].iLogicCode;
            text1.Text += "\r\nacFwMainBoard：" + acFwMainBoard;
            text1.Text += "\r\nacHwNv：" + acHwNv;
        }

        private void iFirmwareDownload_Click(object sender, EventArgs e)
        {
            string path = string.Empty;
            System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog();
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                path = fbd.SelectedPath;
            else
                return;

            IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);
            byte CE = 1; byte NV = 2;

            int ret = CRM9250N.iFirmwareDownload(m_hDevHandle, path, CE, I_DevReturn);

            tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
            Marshal.FreeHGlobal(I_DevReturn);

            text1.Text = "iFirmwareDownload = " + (ret == 0 ? "success" : "fail") + ",LogicCode = " + DevReturn[0].iLogicCode;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            IntPtr I_DevReturn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tDevReturn)) * 8);
            IntPtr I_tGRGNoteDetails = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tGRGNoteDetails)) * 200);
            IntPtr I_tGRGNoteOCRInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(tGRGNoteOCRInfo)) * 200);
            int numOfItems = 200;
            byte NOTE_TYPE_ALL = 3;
            byte[] p_pcFsnFileName = new byte[260];

            int ret = CRM9250N.iGetNvNotesInfoEx(m_hDevHandle, I_tGRGNoteDetails, I_tGRGNoteOCRInfo, ref numOfItems, NOTE_TYPE_ALL, p_pcFsnFileName, I_DevReturn);

            tDevReturn[] DevReturn = IntPtrToStructarray<tDevReturn>(I_DevReturn);
            tGRGNoteDetails[] GRGNoteDetails = new tGRGNoteDetails[numOfItems];
            tGRGNoteOCRInfo[] GRGNoteOCRInfo = new tGRGNoteOCRInfo[numOfItems];
            for (int i = 0; i < GRGNoteDetails.Length; i++)
            {
                IntPtr ptr = (IntPtr)((UInt32)I_tGRGNoteDetails + i * Marshal.SizeOf(typeof(tGRGNoteDetails)));
                GRGNoteDetails[i] = (tGRGNoteDetails)Marshal.PtrToStructure(ptr, typeof(tGRGNoteDetails));
            }

            Marshal.FreeHGlobal(I_tGRGNoteDetails); Marshal.FreeHGlobal(I_tGRGNoteOCRInfo); Marshal.FreeHGlobal(I_DevReturn);
            text1.Text = "iGetNvNotesInfoEx = " + (ret == 0 ? "success" : "fail") + ",LogicCode = " + DevReturn[0].iLogicCode;
            if (ret == 0)
            {
                for (int i = 0; i < GRGNoteDetails.Length; i++)
                {
                    // byValidation (0:L4A; 1:L4B; 2:L3; 3:L2)
                    text1.Text += "\r\n byValidation = " + GRGNoteDetails[i].byValidation + ", Denomination = " + GRGNoteDetails[i].wDenomination 
                    + ", SerialNo = " + Encoding.ASCII.GetString(GRGNoteDetails[i].acSerialNo) + ", ImageFileName = " + Encoding.ASCII.GetString(GRGNoteDetails[i].acImageFileName);
                }
            }
        }
    }
}
