#ifndef _CRM9250NDEVSTRUCT_H_
#define _CRM9250NDEVSTRUCT_H_

#pragma pack(push, 1)
typedef struct
{
	int		iLogicCode;	//Logical code
	int		iPhyCode;	//Physical code
	int		iHandle;	    //Handling methods: 0-No 1-Initializing 2 -resend
	int		iType;		//Status code type: 0-Warning 1-Error
	char	acDevReturn[128];	//Reserve
	char	acReserve[128];	//Reserve
} tDevReturn;
// CRM9250N机芯子型号枚举，见tCRM9250N_InitSetting.cSubType和tCRM9250N_DevVersionInfo.cSubType
typedef enum 
{
	CRM9250N_SUBTYPE_UNKNOWN = 0,							// 未知机型
	CRM9250N_SUBTYPE_A = 'A',								// 机型A，有审计箱(ATC，可复用为循环箱)，2个循环箱(RC)
	CRM9250N_SUBTYPE_B = 'B',								// 机型B，无审计箱，3个循环箱
	CRM9250N_SUBTYPE_C = 'C',								// 机型C，有审计箱(可复用为循环箱)，3个循环箱
	CRM9250N_SUBTYPE_D = 'D',								// 机型D，DRC箱
	CRM9250N_SUBTYPE_E = 'E'								// 机型E，
} eCRM9250N_SubType;

// 初始化设置结构体，见iInitSetting接口的p_psInitSetting参数
typedef struct tCRM9250N_InitSetting  
{
	BYTE						abyCassetteDisable[8];		// [in] 禁用各RC钱箱(第1~4项对应RC1~RC4)	0-启用	1-禁用
	WORD						awAcceptableDenom[256];		// [in] 可接收的钞票面额信息(不填表示全部不接收)
	char						cSubType;					// [out] 机芯子型号，见eCRM9250N_SubType
	BYTE						byAtcMode;					// [in] ATC模式		0-仅作为审计箱	1-复用为循环箱和审计箱
	BYTE						byAcMode;					// [in] AC模式		0-作为回收箱使用，1-作为循环箱使用（暂不支持）
	BYTE						byWorkingMode;				// [in] 设备工作模式 0-循环机模式, 1-兑换机模式
	BYTE						byDcAccepetAllDenom;		// [in] 是否接收所有面额 0-接收DC钱箱设置的面额；1-接收awAcceptableDenom中的所有面额
	BYTE						byInitCfg;					// [in] 0-正常模式初始化检查AC箱 1-维护模式初始化不检查AC箱
	BYTE						abyReserved[27];			// 保留
} tCRM9250N_InitSetting;
typedef struct tCRM9250N_InitSettingEx  
{
	BYTE						abyCassetteDisable[16];		// [in] 禁用各RC钱箱(第1~4项对应RC1~RC4)	0-启用	1-禁用
	WORD						awAcceptableDenom[256];		// [in] 可接收的钞票面额信息(不填表示全部不接收)
	char						cSubType;					// [out] 机芯子型号，见eCRM9250N_SubType
	BYTE						byAtcMode;					// [in] ATC模式		0-仅作为审计箱	1-复用为循环箱和审计箱
	BYTE						abyDRCCassetteCfg[16];		// [in] DRC槽位配置
	BYTE						byAcMode;					// [in] AC模式		0-作为回收箱使用，1-作为循环箱使用（D机型支持）
	BYTE						byWorkingMode;				// [in] 设备工作模式 0-循环机模式, 1-兑换机模式
	BYTE						byDcAccepetAllDenom;		// [in] 是否接收所有面额 0-接收DC钱箱设置的面额；1-接收awAcceptableDenom中的所有面额
	BYTE						byInitCfg;					// [in] 0-正常模式初始化检查AC箱 1-维护模式初始化不检查AC箱
	BYTE						abyReserved[17];			// 保留
} tCRM9250N_InitSettingEx;
typedef struct tCRM9250N_InitSettingEx2  
{
	BYTE						abyCassetteDisable[16];		// [in] 禁用各RC钱箱(第1~4项对应RC1~RC4)	0-启用	1-禁用
	WORD						awAcceptableDenom[1024];		// [in] 可接收的钞票面额信息(不填表示全部不接收)
	char						cSubType;					// [out] 机芯子型号，见eCRM9250N_SubType
	BYTE						byAtcMode;					// [in] ATC模式		0-仅作为审计箱	1-复用为循环箱和审计箱
	BYTE						abyDRCCassetteCfg[16];		// [in] DRC槽位配置
	BYTE						byAcMode;					// [in] AC模式		0-作为回收箱使用，1-作为循环箱使用（D机型支持）
	BYTE						byWorkingMode;				// [in] 设备工作模式 0-循环机模式, 1-兑换机模式
	BYTE						byDcAccepetAllDenom;		// [in] 是否接收所有面额 0-接收DC钱箱设置的面额；1-接收awAcceptableDenom中的所有面额
	BYTE						byInitCfg;					// [in] 0-正常模式初始化检查AC箱 1-维护模式初始化不检查AC箱
	BYTE						abyReserved[17];			// 保留
} tCRM9250N_InitSettingEx2;

// 进出各部件的钞票数量结构体，正数表示进入数量，负数表示出去数量
typedef struct tCRM9250N_InOutUnitsInfo
{
	int							aiRcInOutNum[8];			// [out] RC进出钞票数量(第1~4项对应RC1~RC4)
	int							iAcInOutNum;				// [out] AC进出钞票数量
	int							iLrbInOutNum;				// [out] LRB进出钞票数量
	int							iAtcInOutNum;				// [out] ATC进出钞票数量
	int							iNeInOutNum;				// [out] NE进出钞票数量
	int							iNfInOutNum;				// [out] NF进出钞票数量
	BYTE						abyReserved[32];			// 保留
} tCRM9250N_InOutUnitsInfo;

// 初始化信息结构体，见iInit接口的p_psInitInfo参数
typedef struct tCRM9250N_InitInfo 
{
	tCRM9250N_InOutUnitsInfo	sInOutUnitsInfo;			// [out] 进出各部件钞票数量，进入钱箱和NF的钞票数量可能不准确
	BYTE						byMode;						// 0 正常初始化，1快速初始化
	BYTE						byNEMode;					// 0 初始化NE退钞，1初始化NE不退钞（initsetting workingmodel）
	BYTE						abyReserved[30];			// 保留
} tCRM9250N_InitInfo;
// 进出各部件的钞票数量结构体，正数表示进入数量，负数表示出去数量
typedef struct tCRM9250N_InOutUnitsInfoEx
{
	int							aiRcInOutNum[16];			// [out] RC进出钞票数量(第1~4项对应RC1~RC4)
	int							iAcInOutNum[2];				// [out] AC进出钞票数量
	int							iLrbInOutNum;				// [out] LRB进出钞票数量
	int							iAtcInOutNum;				// [out] ATC进出钞票数量
	int							iNeInOutNum;				// [out] NE进出钞票数量
	int							iNfInOutNum;				// [out] NF进出钞票数量
	BYTE						abyReserved[32];				// 保留
} tCRM9250N_InOutUnitsInfoEx;

// 初始化信息结构体，见iInit接口的p_psInitInfo参数
typedef struct tCRM9250N_InitInfoEx 
{
	tCRM9250N_InOutUnitsInfoEx	sInOutUnitsInfo;			// [out] 进出各部件钞票数量，进入钱箱和NF的钞票数量可能不准确
	BYTE						byMode;						// 0 正常初始化，1快速初始化
	BYTE						byNEMode;					// 0 初始化NE退钞，1初始化NE不退钞（initsetting workingmodel）
	BYTE						abyReserved[30];			// 保留
} tCRM9250N_InitInfoEx;
// RC或AC或LRB钱箱状态结构体，见tCRM9250N_CassetteStatus.sAcStatus、sAcStatus和asRcStatus
typedef struct  tCRM9250N_BinStatus
{
	BYTE						byStatus;					// [out] 钱箱状态		0-正常		1-满	2-将满	3-将空	4-空	255-异常
	BYTE						byExist;					// [out] 钱箱存在		0-不存在	1-存在
} tCRM9250N_BinStatus;

// 钱箱状态结构体，见接口的p_psCassetteStatus参数和tCRM9250N_DevStatus.sCassetteStatus
typedef struct tCRM9250N_CassetteStatus
{
	tCRM9250N_BinStatus			asRcStatus[8];				// [out] RC状态，第1~4项对应RC1~RC4
	tCRM9250N_BinStatus			sAtcStatus;					// [out] ATC状态
	tCRM9250N_BinStatus			sAcStatus;					// [out] AC状态
	tCRM9250N_BinStatus			sLrbStatus;					// [out] LRB状态，byExist有效，byStatus保留
} tCRM9250N_CassetteStatus;
typedef struct tCRM9250N_CassetteStatusEx
{
	tCRM9250N_BinStatus			asRcStatus[16];				// [out] RC状态，第1~4项对应RC1~RC4
	tCRM9250N_BinStatus			sAtcStatus;				// [out] ATC状态
	tCRM9250N_BinStatus			sAcStatus[2];					// [out] AC状态
	tCRM9250N_BinStatus			sLrbStatus;					// [out] LRB状态，byExist有效，byStatus保留
} tCRM9250N_CassetteStatusEx;
// 机芯状态结构体，见iGetDevStatus和iGetLastDevStatus接口的p_psDevStatus参数
typedef struct tCRM9250N_DevStatus
{
	BYTE						byShutterAreaHasNotes;		// [out] 闸门的活动区域内有钞票		0-无钞票	1-有钞票
	BYTE						byNfHasNotes;				// [out] 钞票口有钞票				0-无钞票	1-有钞票
	BYTE						byNfNotesWrongPosition;		// [out] 钞票放在钞票口错误的位置	0-位置正确	1-位置错误
	BYTE						byNeHasNotes;				// [out] NE有钞票(保留)				0-无钞票	1-有钞票
	
	BYTE						byShutterOpen;				// [out] 闸门已打开					0-关闭		1-打开
	BYTE						byNvOpen;					// [out] NV已打开					0-关闭		1-打开
	BYTE						byLowerChannelOpen;			// [out] 下部盖式通道已打开			0-关闭		1-打开
	
	BYTE						byUpperUnitDragOut;			// [out] 上部机芯被拖出/推入		0-推入		1-拖出
	BYTE						byUpperUnitInPos;			// [out] 上部机芯已到位				0-未到位	1-已到位
	BYTE						byLowerUnitDragOut;			// [out] 下部机芯被拖出/推入		0-推入		1-拖出
	BYTE						byLowerUnitInPos;			// [out] 下部机芯已到位				0-未到位	1-已到位
	
	BYTE						byCassettePlugRemove;		// [out] 有钱箱被拔插				0-无插拔	1-有插拔
															//	若有钱箱被插拔，则需要读取钱箱信息，之后该位变为0
	
	tCRM9250N_CassetteStatus	sCassetteStatus;			// [out] 钱箱状态
	
	BYTE						abyDevStatusInfo[16];		// [out] 机芯状态信息(目前设备只用10字节，预留6字节)
	BYTE						abySensorStatusInfo[32];	// [out] 传感器状态信息(目前设备只用25字节，预留7字节)
	
	BYTE						byRC1PlugRemove;			// RC1钱箱被拔插				0-无插拔	1-有插拔
	BYTE						byRC2PlugRemove;			// RC2钱箱被拔插				0-无插拔	1-有插拔
	BYTE						byRC3PlugRemove;			// RC3钱箱被拔插				0-无插拔	1-有插拔
	BYTE						byRC4PlugRemove;			// RC4钱箱被拔插				0-无插拔	1-有插拔
	BYTE						byACPlugRemove;				// AC钱箱被拔插					0-无插拔	1-有插拔
	BYTE						byATCPlugRemove;			// ATC钱箱被拔插				0-无插拔	1-有插拔
	BYTE						bySpeedStrategy;			// 0-正常速度	1-提速
	BYTE						byOutPosHasNotes;			// 出钞口有钞
	BYTE						abyReserved[24];			// 保留
} tCRM9250N_DevStatus;

typedef struct tCRM9250N_DevStatusEx
{
	BYTE						byShutterAreaHasNotes;		// [out] 闸门的活动区域内有钞票		0-无钞票	1-有钞票
	BYTE						byNfHasNotes;				// [out] 钞票口有钞票				0-无钞票	1-有钞票
	BYTE						byNfNotesWrongPosition;		// [out] 钞票放在钞票口错误的位置	0-位置正确	1-位置错误
	BYTE						byNeHasNotes;				// [out] NE有钞票(保留)				0-无钞票	1-有钞票
	
	BYTE						byShutterOpen;				// [out] 闸门已打开					0-关闭		1-打开
	BYTE						byNvOpen;					// [out] NV已打开					0-关闭		1-打开
	BYTE						byLowerChannelOpen;			// [out] 下部盖式通道已打开			0-关闭		1-打开
	
	BYTE						byUpperUnitDragOut;			// [out] 上部机芯被拖出/推入		0-推入		1-拖出
	BYTE						byUpperUnitInPos;			// [out] 上部机芯已到位				0-未到位	1-已到位
	BYTE						byLowerUnitDragOut;			// [out] 下部机芯被拖出/推入		0-推入		1-拖出
	BYTE						byLowerUnitInPos;			// [out] 下部机芯已到位				0-未到位	1-已到位
	
	BYTE						byCassettePlugRemove;		// [out] 有钱箱被拔插				0-无插拔	1-有插拔
	//	若有钱箱被插拔，则需要读取钱箱信息，之后该位变为0
	
	tCRM9250N_CassetteStatusEx	sCassetteStatus;			// [out] 钱箱状态
	
	BYTE						abyDevStatusInfo[30];		// [out] 机芯状态信息 7字节保留
	BYTE						abySensorStatusInfo[45];	// [out] 传感器状态信息
	
	BYTE						byRC1PlugRemove;			// RC1钱箱被拔插				0-无插拔	1-有插拔
	BYTE						byRC2PlugRemove;			// RC2钱箱被拔插				0-无插拔	1-有插拔
	BYTE						byRC3PlugRemove;			// RC3钱箱被拔插				0-无插拔	1-有插拔
	BYTE						byRC4PlugRemove;			// RC4钱箱被拔插				0-无插拔	1-有插拔
	BYTE						byACPlugRemove;				// AC钱箱被拔插					0-无插拔	1-有插拔
	BYTE						byATCPlugRemove;			// ATC钱箱被拔插				0-无插拔	1-有插拔
	BYTE						bySpeedStrategy;			// 0-正常速度	1-提速

	BYTE						byRC9PlugRemove;			// RC9钱箱被拔插				0-无插拔	1-有插拔
	BYTE						byRC10PlugRemove;			// RC10钱箱被拔插				0-无插拔	1-有插拔
	BYTE						byRC11PlugRemove;			// RC11钱箱被拔插				0-无插拔	1-有插拔
	BYTE						byRC12PlugRemove;			// RC12钱箱被拔插				0-无插拔	1-有插拔
	BYTE						byAC2PlugRemove;			// AC2钱箱被拔插					0-无插拔	1-有插拔
	BYTE						byOutPosHasNotes;			// 出钞口有钞
	BYTE						abyReserved[19];			// 保留
} tCRM9250N_DevStatusEx;

// 固件版本信息结构体，见iGetDevVersion接口的p_psDevVersion参数
typedef struct tCRM9250N_DevVersionInfo
{
	char						cSubType;						// [out] 机芯子型号，见eCRM9250N_SubType
	char						acFwMainBoard[32];				// [out] 主控板固件版本
	char						acFwUpperBoard[32];				// [out] 上部控制板固件版本
	char						acFwAc[32];						// [out] AC固件版本
	char						acFwLrb[8];						// [out] LRB固件版本(保留)
	char						acFwRc[8][32];					// [out] RC固件版本(第1~4项对应RC1~RC4)
	char						acFwAtc[32];					// [out] ATC固件版本

	char						acHwNvInfo[32];					// [out] NV硬件信息
	char						acFwDSP[32];					// [out] DSP固件版本
	char						acFwGraphicsFPGA[32];			// [out] 图像FPGA版本
	char						acFwArithmetic[32];				// [out] 算法版本
	char						acFwMagneticArithmetic[32];		// [out] 磁算法版本
	char						acFwMagneticFPGA[32];			// [out] 磁FPGA版本
	char						acFwFullFormatThickness[32];	// [out] 全幅面厚度版本
	char						acFwUltravioletArithmetic[32];	// [out] 紫外算法版本
	char						acFwDSPGraphicsArithmetic[32];	// [out] DSP图像算法版本
	char						acFwFSNArithmetic[32];			// [out] 冠字号算法版本
	char						acFwECBSoftware[32];			// [out] ECB软件版本
	char						acHwInfo[32];					// [out] 硬件版本信息

	char						acHwNv[32];						// [out] NV硬件版本
	char						acHwUnits[32];					// [out] 机芯硬件版本
	char						bySecFlag;						// [out] 机芯安全标识，0 不支持，2 机芯安全2.0
	char						byFsnCertification;				// [out] 2018金标硬件标识，0 不支持，1支持
    char                        byCassetteLogicNums;            // [out] 支持设置钱箱逻辑张数 iSetAddNotesNumLimit 接口 byCfgType 参数支持传2
	char						acReserved[253];				// 保留
} tCRM9250N_DevVersionInfo;
typedef struct tCRM9250N_DevVersionInfoEx
{
	char						cSubType;						// [out] 机芯子型号，见eCRM9250N_SubType
	char						acFwMainBoard[32];				// [out] 主控板固件版本
	char						acFwUpperBoard[32];				// [out] 上部控制板固件版本
	char						acFwAc[2][32];					// [out] AC固件版本
	char						acFwLrb[8];						// [out] LRB固件版本(保留)
	char						acFwRc[16][32];					// [out] RC固件版本(第1~4项对应RC1~RC4)
	char						acFwAtc[32];					// [out] ATC固件版本
	
	char						acHwNvInfo[32];					// [out] NV硬件信息
	char						acFwDSP[32];					// [out] DSP固件版本
	char						acFwGraphicsFPGA[32];			// [out] 图像FPGA版本
	char						acFwArithmetic[32];				// [out] 算法版本
	char						acFwMagneticArithmetic[32];		// [out] 磁算法版本
	char						acFwMagneticFPGA[32];			// [out] 磁FPGA版本
	char						acFwFullFormatThickness[32];	// [out] 全幅面厚度版本
	char						acFwUltravioletArithmetic[32];	// [out] 紫外算法版本
	char						acFwDSPGraphicsArithmetic[32];	// [out] DSP图像算法版本
	char						acFwFSNArithmetic[32];			// [out] 冠字号算法版本
	char						acFwECBSoftware[32];			// [out] ECB软件版本
	char						acHwInfo[32];					// [out] 硬件版本信息
	
	char						acHwNv[32];						// [out] NV硬件版本
	char						acHwUnits[32];					// [out] 机芯硬件版本
	char						bySecFlag;						// [out] 机芯安全标识，0 不支持，2 机芯安全2.0
	char						acFwDriverBoard[32];			// [out] 驱动板版本
	char						byFsnCertification;				// [out] 2018金标硬件标识，0 不支持，1支持
    char                        byCassetteLogicNums;            // [out] 支持设置钱箱逻辑张数 iSetAddNotesNumLimit 接口 byCfgType 参数支持传2
	char						acReserved[253];				// 保留
} tCRM9250N_DevVersionInfoEx;
// 硬件版本信息结构体，见iGetHardwareVersion接口的p_psHwVersion 参数
typedef struct tCRM9250N_HwVersionInfo
{
	char						acMainBoard[16];			// [out] 主控板硬件版本
	char						acNv[16];					// [out] NV硬件版本
	char						acUpperBoard[16];			// [out] 上部控制板硬件版本
	char						acReserved[32];				// 保留
} tCRM9250N_HwVersionInfo;

// 钞票面额和数量结构体
typedef struct tCRM9250N_DenomAndNum  
{
	WORD						wCode;						// [out] 钞票面额代号
	int							iNum;						// [out] 钞票数量
} tCRM9250N_DenomAndNum;

// 各类钞票面额和数量信息结构体
typedef struct tCRM9250N_DenomNumInfo 
{
	tCRM9250N_DenomAndNum		asDenomNumL4A[256];			// [out] 执行ECB时表示4A钞票面额和数量信息，否则表示所有钞票面额和数量信息
	tCRM9250N_DenomAndNum		asDenomNumL4B[256];			// [out] 执行ECB时表示4B钞票面额和数量信息，否则保留
	tCRM9250N_DenomAndNum		asDenomNumL3[256];			// [out] 执行ECB时表示L3钞票面额和数量信息，否则保留
	tCRM9250N_DenomAndNum		asDenomNumL2[256];			// [out] 执行ECB时表示L2钞票面额和数量信息，否则保留
	tCRM9250N_DenomAndNum		asDenomNumReserved1[256];	// [out] 保留类1钞票面额和数量信息
	tCRM9250N_DenomAndNum		asDenomNumReserved2[256];	// [out] 保留类2钞票面额和数量信息
	int							iUnknownCount;				// [out] 未识别钞票数量
} tCRM9250N_DenomNumInfo;

// 点钞信息结构体，见iCashCount接口的p_psCashCount参数
typedef struct tCRM9250N_CashCount 
{
	int							iDepositLimit;				// [in] 存款限制，若不大于零则以配置文件为准，否则以该字段为准
	WORD						awUnacceptableDenom[256];	// [in] 不可接收的钞票面额信息(不填表示全部接收，仅此次点钞有效)
	int							iTotalCount;				// [out] 此次点钞进入NE钞票数量，准确
	int							iRejectCount;				// [out] 此次点钞拒收钞票数量，可能不准确
	tCRM9250N_DenomNumInfo		sDenomNumInfo;				// [out] 进入NE的钞票面额和数量信息
	DWORD						dwAmountLimit;				// [in] 存款金额限制
	BYTE						abyReserved[28];			// 保留
} tCRM9250N_CashCount;

// 点钞信息结构体，见iCashCount接口的p_psCashCount参数
typedef struct tCRM9250N_CashCountEx 
{
	int							iDepositLimit;				// [in] 存款限制，若不大于零则以配置文件为准，否则以该字段为准
	WORD						awUnacceptableDenom[1024];	// [in] 不可接收的钞票面额信息(不填表示全部接收，仅此次点钞有效)
	int							iTotalCount;				// [out] 此次点钞进入NE钞票数量，准确
	int							iRejectCount;				// [out] 此次点钞拒收钞票数量，可能不准确
	tCRM9250N_DenomNumInfo		sDenomNumInfo;				// [out] 进入NE的钞票面额和数量信息
	DWORD						dwAmountLimit;				// [in] 存款金额限制
	BYTE						abyReserved[28];			// 保留
} tCRM9250N_CashCountEx;

// 存款信息结构体，见iStoreMoney接口的p_psStoreMoney参数
typedef struct tCRM9250N_StoreMoney 
{
	BYTE						abyCassetteDisable[8];		// [in] 禁用各RC钱箱(第1~4项对应RC1~RC4)	0-启用	1-禁用
	BYTE						abyCassettePriority[8];		// [in] RC钱箱优先级(第1~4项对应RC1~RC4)	范围0x01~0x04数值越低优先级越高
	WORD						awUnacceptableDenom[256];	// [in] 不可接收的钞票面额信息(不填表示全部接收，仅此次存款有效)
	tCRM9250N_InOutUnitsInfo	sInOutUnitsInfo;			// [out] 进出各部件钞票数量，出NE和进钱箱钞票数量可能不准确
	tCRM9250N_DenomNumInfo		sDenomNumInfo;				// [out] 存入钱箱的钞票面额和数量信息
	BYTE						byRejectPos;				// [in]	存款拒钞配置 1: AC1,2:AC2,3:LRB
	BYTE						byNvMode;					// [in]	0:NV 验钞(默认)  1: NV 不验钞
	BYTE						abyReserved[30];			// 保留
} tCRM9250N_StoreMoney;
// 存款信息结构体，见iStoreMoney接口的p_psStoreMoney参数
typedef struct tCRM9250N_StoreMoneyEx 
{
	BYTE						abyCassetteDisable[16];		// [in] 禁用各RC钱箱(第1~4项对应RC1~RC4)	0-启用	1-禁用
	BYTE						abyCassettePriority[16];		// [in] RC钱箱优先级(第1~4项对应RC1~RC4)	范围0x01~0x04数值越低优先级越高
	WORD						awUnacceptableDenom[256];	// [in] 不可接收的钞票面额信息(不填表示全部接收，仅此次存款有效)
	tCRM9250N_InOutUnitsInfoEx	sInOutUnitsInfo;			// [out] 进出各部件钞票数量，出NE和进钱箱钞票数量可能不准确
	tCRM9250N_DenomNumInfo		sDenomNumInfo;				// [out] 存入钱箱的钞票面额和数量信息
	BYTE						byRejectPos;				// [in]	存款拒钞配置 1: AC1,2:AC2,3:LRB
	BYTE						byNvMode;					// [in]	0:NV 验钞(默认)  1: NV 不验钞
	BYTE						abyReserved[30];			// 保留
} tCRM9250N_StoreMoneyEx;

// 取消存款信息结构体，见iCancelDeposit接口的p_psCancelDeposit参数
typedef struct tCRM9250N_CancelDeposit 
{
	tCRM9250N_InOutUnitsInfo	sInOutUnitsInfo;			// [out] 进出各部件钞票数量，iNeInOutNum和iNfInOutNum可能不准确
	tCRM9250N_DenomNumInfo		sDenomNumInfo;				// [out] 进入钱箱的钞票面额和数量信息(仅在机芯执行ECB时有意义)
	BYTE						abyReserved[32];			// 保留
} tCRM9250N_CancelDeposit;
// 取消存款信息结构体，见iCancelDeposit接口的p_psCancelDeposit参数
typedef struct tCRM9250N_CancelDepositEx 
{
	tCRM9250N_InOutUnitsInfoEx	sInOutUnitsInfo;			// [out] 进出各部件钞票数量，iNeInOutNum和iNfInOutNum可能不准确
	tCRM9250N_DenomNumInfo		sDenomNumInfo;				// [out] 进入钱箱的钞票面额和数量信息(仅在机芯执行ECB时有意义)
	BYTE						abyReserved[32];				// 保留
} tCRM9250N_CancelDepositEx;

// 请求取款信息结构体，见tCRM9250N_DispenseInfo.asReqDispInfo
typedef struct tCRM9250N_ReqDispInfo
{	
	WORD						wDenomOrCassetteInfo;		// [in] 钞票面额代号或RC钱箱编号信息	byDispType == 0时，表示钞票面额代号
															//										byDispType == 1时，表示RC钱箱编号(1表示RC1，...，4表示RC4)
	int							iNum;						// [in] 张数
} tCRM9250N_ReqDispInfo;

// 取款信息结构体，见iDispense接口的p_psDispenseInfo参数
typedef struct tCRM9250N_DispenseInfo
{
	BYTE						byDispType;					// [in] 取款类型	0-按面额出钞(保留)	1-按RC钱箱出钞
	tCRM9250N_ReqDispInfo		asReqDispInfo[8];			// [in] 请求取款信息
	BYTE						abyCassetteDisable[8];		// [in] 禁用各RC钱箱(保留，第1~4项对应RC1~RC4)	0-启用	1-禁用
	int							iTotalCount;				// [out] 岀钞至NF的钞票数量，准确
	tCRM9250N_DenomAndNum		asDenomNum[256];			// [out] 岀钞至NF的钞票面额和数量信息
	int							aiDispensedInfo[8];			// [out] 各RC钱箱岀钞至NF的钞票数量，可能不准确(第1~4项对应RC1~RC4)
	int							aiRejectInfo[8];			// [out] 各RC钱箱的拒钞数量，可能不准确(第1~4项对应RC1~RC4)
	tCRM9250N_InOutUnitsInfo	sInOutUnitsInfo;			// [out] 进出各部件钞票数量，iNfInOutNum准确，aiRcInOutNum可能不准确
	BYTE						byNFNotesPolicy;			// [in]	钞口有钞票时是否允许取款。0表示不允许，1表示允许
	BYTE						abyReserved[31];			// 保留
} tCRM9250N_DispenseInfo;
typedef struct tCRM9250N_DispenseInfoEx
{
	BYTE						byDispType;					// [in] 取款类型	0-按面额出钞(保留)	1-按RC钱箱出钞
	tCRM9250N_ReqDispInfo		asReqDispInfo[16];			// [in] 请求取款信息
	BYTE						abyCassetteDisable[16];		// [in] 禁用各RC钱箱(保留，第1~4项对应RC1~RC4)	0-启用	1-禁用
	int							iTotalCount;				// [out] 岀钞至NF的钞票数量，准确
	tCRM9250N_DenomAndNum		asDenomNum[256];			// [out] 岀钞至NF的钞票面额和数量信息
	int							aiDispensedInfo[16];		// [out] 各RC钱箱岀钞至NF的钞票数量，可能不准确(第1~4项对应RC1~RC4)
	int							aiRejectInfo[16];			// [out] 各RC钱箱的拒钞数量，可能不准确(第1~4项对应RC1~RC4)
	tCRM9250N_InOutUnitsInfoEx	sInOutUnitsInfo;			// [out] 进出各部件钞票数量，iNfInOutNum准确，aiRcInOutNum可能不准确
	BYTE						byNFNotesPolicy;			// [in]	钞口有钞票时是否允许取款。0表示不允许，1表示允许
	BYTE						byRejectToAc;				// [in] 1: AC1,2:AC2,3:LRB 该参数仅当"初始化设置"命令中，"取款拒钞配置"值为1时生效，用于选择取款拒钞的去向
	BYTE						byCountEnable;				// [in] 0:取款功能，1：清机功能
	BYTE						byCountError;				// [in] 出钞不足时是否报错。0：表示报故障；1：表示报警告；
	BYTE						byCountNVPolicy;			// [in] 0:表示需要NV识别；1:表示不做NV识别
	BYTE						abyReserved[28];			// 保留
} tCRM9250N_DispenseInfoEx;

// 回收类型枚举，见tCRM9250N_RetractInfo.byRetractType
typedef enum
{
	RETRACT_TYPE_NF	= 1,									// 回收NF
	RETRACT_TYPE_NE	= 2										// 回收NE
} eCRM9250N_RetractType;									

// 回收区域枚举，见tCRM9250N_RetractInfo.byRetractPos
typedef enum
{
	RETRACT_POS_LRB			= 1,							// 所有钞票回收至LRB
	RETRACT_POS_AC			= 2,							// 所有钞票回收至AC
	RETRACT_POS_NE			= 3,							// 所有钞票回收至NE
	RETRACT_POS_AC2			= 4,							// 所有钞票回收至AC2
	RETRACT_POS_RC			= 5,							// 所有钞票回收至RC,不合格钞票进AC
	RETRACT_POS_ACEX		= 6,							// 所有钞票回收到扩展AC
	RETRACT_POS_DC			= 7,							// 所有钞票回收到DC
	RETRACT_POS_RCLRB		= 8								// 所有钞票回收至RC,不合格钞票进LRB

} eCRM9250N_RetractPos;									


// 回收信息结构体，见iRetract接口的p_psRetractInfo参数
typedef struct tCRM9250N_RetractInfo 
{
	BYTE						byRetractType;				// [in] 回收类型，见eCRM9250N_RetractType
	BYTE						byRetractPos;				// [in] 回收区域，见eCRM9250N_RetractPos
	BYTE						abyCassetteDisable[8];		// [in] 禁用各RC钱箱(第1~4项对应RC1~RC4)	0-启用	1-禁用
	BYTE						abyCassettePriority[8];		// [in] RC钱箱优先级(第1~4项对应RC1~RC4)	范围0x01-0x04数值越低优先级越高
	int							iTotalCount;				// [out] 回收钞票数量，可能不准确
	tCRM9250N_DenomNumInfo		sDenomNumInfo;				// [out] 回收钞票的面额和数量信息
	tCRM9250N_InOutUnitsInfo	sInOutUnitsInfo;			// [out] 进出各部件钞票数量，可能不准确
	BYTE						abyRetractFlag;				// [in]1-应用回收  其他-sp回收
	BYTE						abyReserved[31];			// 保留
} tCRM9250N_RetractInfo;					
typedef struct tCRM9250N_RetractInfoEx 
{
	BYTE						byRetractType;				// [in] 回收类型，见eCRM9250N_RetractType
	BYTE						byRetractPos;				// [in] 回收区域，见eCRM9250N_RetractPos
	BYTE						abyCassetteDisable[16];		// [in] 禁用各RC钱箱(第1~4项对应RC1~RC4)	0-启用	1-禁用
	BYTE						abyCassettePriority[16];	// [in] RC钱箱优先级(第1~4项对应RC1~RC4)	范围0x01-0x04数值越低优先级越高
	int							iTotalCount;				// [out] 回收钞票数量，可能不准确
	tCRM9250N_DenomNumInfo		sDenomNumInfo;				// [out] 回收钞票的面额和数量信息
	tCRM9250N_InOutUnitsInfoEx	sInOutUnitsInfo;			// [out] 进出各部件钞票数量，可能不准确
	BYTE						abyRetractFlag;				// [in] 1-应用回收  其他-sp回收
	BYTE						abyReserved[31];			// 保留
} tCRM9250N_RetractInfoEx;					

// RC或ATC或AC或DC钱箱信息结构体
typedef struct tCRM9250N_BinInfo 
{
	BYTE						abyLogID[8];				// 逻辑ID
	BYTE						byCountryCode;				// 国家码(保留)
	BYTE						byCassetteType;				// 钱箱类型		1-RC和ATC,2-AC,3-DC,4-NRC,5-NDC,6-DRC,9-DAC									
	WORD						wNoteType;					// 钱箱面额。对于RC为面额代号；对AC无意义；对于DC必须为0xFFF0
	BYTE						byFunction;					// 钱箱功能		对于RC		0x00-循环功能	0xF0只取功能	0xFF-只存功能
															//				对于ATC必须为0xF3
															//				对于AC必须为0xF2
															//				对于DC必须为0xF1
	BYTE						byStoreType;				// 存款设置，仅对RC有意义					0-按照面额存款	其它-保留
	WORD						wRCAsDC;					// 0x8000：RC当DC使用(byFunction 必须为FF)，0默认
	BYTE						abyReserved[30];			// 保留
} tCRM9250N_BinInfo;
									

// 钱箱信息结构体，见iGetCassetteInfo和iSetCassetteInfo接口的p_psCassetteInfo参数
typedef struct  tCRM9250N_CassetteInfo
{
	BYTE						abyCassetteDisable[8];		// [in] 禁用各RC钱箱(第1~4项对应RC1~RC4，仅在设置钱箱信息时有效)	0-启用	1-禁用
	tCRM9250N_BinInfo			sAcCassette;				// AC钱箱信息
	tCRM9250N_BinInfo			sAtcCassette;				// ATC钱箱信息
	tCRM9250N_BinInfo			asRcCassette[8];			// RC和DC钱箱信息
	BYTE						abyReserved[32];			// 保留
} tCRM9250N_CassetteInfo;
typedef struct  tCRM9250N_CassetteInfoEx
{
	BYTE						abyCassetteDisable[16];		// [in] 禁用各RC钱箱(第1~4项对应RC1~RC4，仅在设置钱箱信息时有效)	0-启用	1-禁用
	tCRM9250N_BinInfo			sAcCassette[2];				// AC钱箱信息
	tCRM9250N_BinInfo			sAtcCassette;				// ATC钱箱信息
	tCRM9250N_BinInfo			asRcCassette[16];			// RC和DC钱箱信息
	BYTE						abyReserved[32];			// 保留
} tCRM9250N_CassetteInfoEx;

// 币种信息结构体，见tCRM9250N_DenominationTable.asDenominationInfo
typedef struct tCRM9250N_DenominationInfo
{
	WORD						wDenomCode;					// [out] 钞票面额代号
	WORD						wLength;					// [out] 钞票长度
	WORD						wWidth;						// [out] 钞票宽度
	WORD						wThickness;					// [out] 钞票厚度
} tCRM9250N_DenominationInfo;				

// 钞票面额表信息结构体，见iGetDenominationTable接口的p_psDenominationTable参数
typedef struct  tCRM9250N_DenominationTable
{
	tCRM9250N_DenominationInfo	asDenominationInfo[512];	// [out] 机芯支持币种信息
	BYTE						abyReserved[32];			// 保留
} tCRM9250N_DenominationTable;

// 审计方向枚举，见tCRM9250N_AuditInfo.byCassetteNo、tCRM9250N_AuditStatus.byDirection和tCRM9250N_AuditResult.byDirection
typedef enum
{
	DIRECTION_RC_TO_ATC		= 0,							// 由指定RC钱箱进入审计钱箱
	DIRECTION_ATC_TO_RC		= 1,							// 由审计钱箱进入指定RC钱箱
} eCRM9250N_AuditDirection;

// 开始审计信息结构体，见iStartAudit接口的p_psStartAudit参数
typedef struct tCRM9250N_StartAudit 
{
	BYTE						byDirection;				// [in] 走钞方向，见eCRM9250N_AuditDirection
	int							aiRequestRcNum[8];			// [in] RC钱箱请求出钞或进钞数量(第1~3项对应RC1~RC3)
															//	byDirection为0时，表示从各RC钱箱出钞至审计钱箱的钞票数量，0xFFFF表示对应RC钱箱一直出钞至空。支持0~0xFFFF
															//	byDirection为1时，表示从审计钱箱出钞至各RC钱箱的钞票数量，0xFFFF表示审计钱箱一直出钞至空。目前只支持0和0xFFFF，其它值保留
	BYTE						abyCassettePriority[8];		// [in] 仅在byDirection为1时有意义，RC钱箱优先级(第1~3项对应RC1~RC3)	范围0x01-0x03数值越低优先级越高			
	BYTE						byEventContinue;			// 面额不一致是否继续；0 停止，1 继续
	BYTE						byATCError;					// ATC有钞时,是否报错；0 不报错，1 报错
	BYTE						abyReserved[30];			// 保留留
} tCRM9250N_StartAudit;

typedef struct tCRM9250N_StartAuditEx 
{
	BYTE						byDirection;				// [in] 走钞方向，见eCRM9250N_AuditDirection
	int							aiRequestRcNum[16];			// [in] RC钱箱请求出钞或进钞数量(第1~3项对应RC1~RC3)
	//	byDirection为0时，表示从各RC钱箱出钞至审计钱箱的钞票数量，0xFFFF表示对应RC钱箱一直出钞至空。支持0~0xFFFF
	//	byDirection为1时，表示从审计钱箱出钞至各RC钱箱的钞票数量，0xFFFF表示审计钱箱一直出钞至空。目前只支持0和0xFFFF，其它值保留
	BYTE						abyCassettePriority[16];		// [in] 仅在byDirection为1时有意义，RC钱箱优先级(第1~3项对应RC1~RC3)	范围0x01-0x03数值越低优先级越高			
	BYTE						byEventContinue;			// 面额不一致是否继续；0 停止，1 继续
	BYTE						byATCError		;			// ATC有钞时,是否报错；0 不报错，1 报错
	BYTE						abyReserved[30];			// 保留
} tCRM9250N_StartAuditEx;

// 审计状态枚举
typedef enum
{
	AUDIT_NONE = 0,											// 未进行审计
	AUDIT_IN_PROGRESS = 1,									// 正在审计中
	AUDIT_FINISH = 2,										// 审计已完成
	AUDIT_OCR_READY = 17									// OCR信息就绪
} eCRM9250N_AuditProgress;


// 审计状态结构体，iGetAuditStatus接口的p_psAuditStatus参数
typedef struct tCRM9250N_AuditStatus 
{
	BYTE						byDirection;				// [out] 审计方向，见eCRM9250N_AuditDirection
	BYTE						byStatus;					// [out] 审计状态，见tCRM9250N_AuditProgress
															// [out] 审计已完成且上层读取审计结果后，审计状态转换成未进行审计
	tCRM9250N_InOutUnitsInfo	sInUnitsInfo;				// [out] 进各部件钞票数量，数值为正数或零
	tCRM9250N_InOutUnitsInfo	sOutUnitsInfo;				// [out] 出各部件钞票数量，数值为负数或零
	BYTE						abyReserved[32];			// 保留
} tCRM9250N_AuditStatus;
typedef struct tCRM9250N_AuditStatusEx 
{
	BYTE						byDirection;				// [out] 审计方向，见eCRM9250N_AuditDirection
	BYTE						byStatus;					// [out] 审计状态，见tCRM9250N_AuditProgress
	// [out] 审计已完成且上层读取审计结果后，审计状态转换成未进行审计
	tCRM9250N_InOutUnitsInfoEx	sInUnitsInfo;				// [out] 进各部件钞票数量，数值为正数或零
	tCRM9250N_InOutUnitsInfoEx	sOutUnitsInfo;				// [out] 出各部件钞票数量，数值为负数或零
	BYTE						abyReserved[32];			// 保留
} tCRM9250N_AuditStatusEx;

// 审计结果结构体，见iGetAuditResult接口的p_psAuditResult参数
typedef struct tCRM9250N_AuditResult 
{
	BYTE						byDirection;				// [out] 审计方向，见eCRM9250N_AuditDirection
	int							iUnknownCount;				// [out] 未识别钞票数量
	tCRM9250N_DenomAndNum		asDenomNum[256];			// [out] 审计钞票面额信息
	tCRM9250N_InOutUnitsInfo	sInOutUnitsInfo;			// [out] 进出各部件钞票数量，出钱箱和进入NE的钞票数量可能不准确，进入钱箱的钞票数量准确
	int							aiRcRejectNum[8];			// [out] 各RC钱箱的拒钞数量(第1~3项对应RC1~RC3)
	int							iAtcRejectNum;				// [out] ATC钱箱的拒钞数量
	BYTE						abyReserved[32];			// 保留
} tCRM9250N_AuditResult;
typedef struct tCRM9250N_AuditResultEx 
{
	BYTE						byDirection;				// [out] 审计方向，见eCRM9250N_AuditDirection
	int							iUnknownCount;				// [out] 未识别钞票数量
	tCRM9250N_DenomAndNum		asDenomNum[256];			// [out] 审计钞票面额信息
	tCRM9250N_InOutUnitsInfoEx	sInOutUnitsInfo;			// [out] 进出各部件钞票数量，出钱箱和进入NE的钞票数量可能不准确，进入钱箱的钞票数量准确
	int							aiRcRejectNum[16];			// [out] 各RC钱箱的拒钞数量(第1~3项对应RC1~RC3)
	int							iAtcRejectNum;				// [out] ATC钱箱的拒钞数量
	BYTE						abyReserved[32];			// 保留
} tCRM9250N_AuditResultEx;

// 钞票类型，见iGetNvNotesInfo接口的p_byNoteType参数
typedef enum
{
	NOTE_TYPE_GENUINE		= 0x01,							// 真钞
	NOTE_TYPE_NOTGENUINE	= 0x02,							// 非真钞
	NOTE_TYPE_ALL			= 0x03,							// 所有钞票
	NOTE_TYPE_BLACKLIST		= 0x04,							// 黑名单钞票
	NOTE_TYPE_ECBL2			= 0x08,							// 36条款假钞(第2类钞票)，可组合
	NOTE_TYPE_ECBL3			= 0x10,							// 36条款疑似假钞(第3类钞票)，可组合
	NOTE_TYPE_ECBL4A		= 0x20,							// 36条款适合流通真钞(第4A类钞票)，可组合
	NOTE_TYPE_ECBL4B		= 0x40,							// 36条款不适合流通真钞(第4B类钞票)，可组合
	NOTE_TYPE_SUSPICIOUS	= 0x80							// 可疑钞票
} eCRM9250N_NoteType;

// 黑名单更新方式，见iUpdateOcrBlackList接口的p_byType参数
typedef enum
{
	UPDATE_APPEND		= 1,								// 添加
	UPDATE_OVERWRITE	= 2									// 覆盖
} eCRM9250N_UpdateType; 

// 删除模式，见iDeleteSerialRecords接口的p_byMode参数
typedef enum
{
	DEL_ALL			= 0,									// 都删除(数据库、图片文件和FSN文件)
	DEL_DATABASE	= 1,									// 只删除数据库
	DEL_PICTURE		= 2,									// 只删除图片文件
	DEL_FSN			= 3										// 只删除FSN文件
} eCRM9250N_DelMode;
// 机芯序列号结构体，见iGetDevSerialNo接口的参数
typedef struct tCRM9250N_DevSerialNo
{
	char						acMainBoardNo[16];			// [out] 主板序列号
	char						acUpBoardNo[16];			// [out] 上部版序列号
	char						acACNo[16];					// [out] AC序列号
	char						acRC1No[16];				// [out] RC1序列号
	char						acRC2No[16];				// [out] RC2序列号
	char						acRC3No[16];				// [out] RC3序列号
	char						acRC4No[16];				// [out] RC4序列号
	char						acATCNo[16];				// [out] ATC序列号
	char						acChipNo[16];				// [out] 芯片ID
	char						acNVNo[16];					// [out] NV
	char						acReserved[32];				// 保留
} tCRM9250N_DevSerialNo;
typedef struct tCRM9250N_DevSerialNoEx
{
	char						acMainBoardNo[16];			// [out] 主板序列号
	char						acUpBoardNo[16];			// [out] 上部版序列号
	char						acACNo[2][16];					// [out] AC序列号
	char						acRCNo[16][16];				// [out] RC1序列号
	char						acATCNo[16];				// [out] ATC序列号
	char						acChipNo[16];				// [out] 芯片ID
	char						acNVNo[16];					// [out] NV
	char						acReserved[32];				// 保留
} tCRM9250N_DevSerialNoEx;

// 钱箱币种明细信息
typedef struct tCRM9250N_DenomDetails
{
    WORD						wDenomCode;					// 钞票面额代号。0表示不可用面额，其它表示具体面额
    int							iNoteCount;					// 对应币种的计数值
    BYTE						byNoteType;					// 钞票类型
															// 2: 真钞(FIT)
															// 3: 第2类钞票，确认假钞
															// 4: 第3类钞票，疑似假钞
															// 5: 真钞(UNFIT)，36条款的4B类钞票
} tCRM9250N_DenomDetails;

// DC钱箱面额信息
typedef struct tCRM9250N_DCDenomAttribute
{
	WORD						awDenomCode[256];			// 钞票面额代号，目前最多支持64个面额。0表示不可用面额，其它表示具体面额
	BYTE						abyDenomAttribute[256];		// 钞票面额属性
															// 0xE0: 接收不适合流通真钞(UNFIT)
															// 0x0E: 接收适合流通(可循环)真钞(FIT)
															// 0xEE: 既可接收不适合流通真钞，也接收适合流通真钞
															// 0xE1: 接收不适合流通的真钞和第三类钞票
															// 其它: 不接收
	BYTE						abyReserved[32];			// 保留
} tCRM9250N_DCDenomAttribute;

// 介质下载进度结构体
typedef struct tCRM9250N_PDLProgress
{
	char		acFileName[260];						// 当前下载文件名
	int			iTotalSize;									// 文件大小
	int			iCurPos;									// 当前下载位置
    BYTE        abyReserved[32];							// 保留
} tCRM9250N_PDLProgress;

typedef struct tCRM9250N_LastInOutUnitsInfo
{
	int							aiRcInOutNum[16];			// [out] RC进出钞票数量(第1~4项对应RC1~RC4)
	int							iAcInOutNum;				// [out] AC进出钞票数量
	int							iLrbInOutNum;				// [out] LRB进出钞票数量
	int							iAtcInOutNum;				// [out] ATC进出钞票数量
	int							iNeInOutNum;				// [out] NE进出钞票数量
	int							iNfOutNum;					// [out] NF出 钞票数量
	int							iNfInNum;					// [out] NF进 钞票数量
	BYTE						byPowerOff;					// [out] 是否断电标识 0,未断电。0xFF断电
	BYTE						abyReserved[31];			// 保留
} tCRM9250N_LastInOutUnitsInfo;

//CRM9250N读取机芯日志LOG
typedef struct tCRM9250N_ReadDevLog
{
	char	acFilePath[260];		//[in]文件路径（不包括文件名）
	int		iType;						//[in]读取类型，0x00:读取全部日志；0x01:按笔，不支持，保留,0x02:按天读取日志
	int		iPara;						//[in]读取参数，iType=0x00：该参数无用，表示最后N天交易日志
							
}tCRM9250N_ReadDevLog;

typedef struct tCRM9250N_ADDNOTES
{
	WORD		awNumRC[16];						// RC1~16的加钞张数限制
    BYTE        abyReserved[31];					// 保留
	BYTE		byCfgType;							// 0，存款钱箱限额张数；1:钱箱加钞张数;2:设置钱箱逻辑张数
} tCRM9250N_ADDNOTES;

typedef struct tCRM9250N_SetFsnInfoIn
{//下面字符数组，abyString[0]==0x00表示维持旧值，不设置。abyString[0]==0x20表示清空旧值			
	BYTE                 byFitCode;				// 未识别冠字号码字符的替换符。默认使用下划线‘_’。	
	char                 abyFsnFilesPath[260];	// FSN文件存放目录。	
	char                 abyFinanIns[32];		// 金融机构缩写。	
	char                 abyEnableTime[32];		// 设备启用时间,两位年+两位月，共计4位,2008年01月01日，设备启用时间为："0801"	
	char                 abyFinanInst[32];		// 报送银行的金融机构编码。	
	char                 abyFinanInstOutlet[32];// 报送网点的金融机构编码。	
	char                 abyMachineModel[64];	//机具型号	
	char                 abyMachineSNo[64];		//机具编号	
	char                 abyOperator[32];		//操作人员	
	BYTE				 byFsnVer;				//1:2015，2:2018.其他忽略维持原状	
	unsigned char		 abyReserved[256];			
} tCRM9250N_SetFsnInfoIn;


// 直接存款信息结构体，见iDirectStoreMoney接口的p_psStoreMoney参数
typedef struct tCRM9250N_DirectStoreMoney 
{
	int							iDepositLimit;				// [in] 存款限制，若不大于零则以配置文件为准，否则以该字段为准
	DWORD						dwAmountLimit;				// [in] 存款金额限制
	WORD						awUnacceptableDenom[256];	// [in] 不可接收的钞票面额信息(不填表示全部接收，仅此次点钞有效)
	BYTE						abyCassettePriority[16];	// [in] RC钱箱优先级(第1~4项对应RC1~RC4)	范围0x01~0x04数值越低优先级越高
	BYTE						abyCassetteDisable[16];		// [in] 禁用各RC钱箱(第1~4项对应RC1~RC4)	0-启用	1-禁用

	int							iTotalCount;				// [out] 此次点钞数量
	int							iRejectCount;				// [out] 此次点钞拒收钞票数量，
	tCRM9250N_InOutUnitsInfoEx	sInOutUnitsInfo;			// [out] 进出各部件钞票数量(不包括NF\NE)
	tCRM9250N_DenomNumInfo		sDenomNumInfo;				// [out] 存入钱箱的钞票面额和数量信息
	BYTE						abyReserved[32];			// 保留
} tCRM9250N_DirectStoreMoney;

//更新认证时间
typedef struct tCRM9250N_CertificationUpdate
{
	SYSTEMTIME	sUpdateTime;								// [in] 认证时间
	char		abyVerifyCode[16];							// [in] 更新认证时间的授权码
	char		abyVerifyData1[16];							// [in] 授权码生成因子1~3 ，授权更新功能保留，现在是无条件更新。
	char		abyVerifyData2[16];
	char		abyVerifyData3[16];
	char		abyReserved[512];
}tCRM9250N_CertificationUpdate;

typedef struct tCRM9250N_Certification
{
	SYSTEMTIME	sDate;										// [in] 认证时间
	char		abyVerifyCode[16];							// [in] 认证校验码，由部件序列号信息服务端生成
	char		abyReserved[128];
}tCRM9250N_Certification;

typedef struct tCRM9250N_FsnCfgInfo
{
	char acName[32];
	char acValue[128];
	char acReserved[32];
}tCRM9250N_FsnCfgInfo;

#pragma pack(pop)

#endif // _CRM9250NDEVSTRUCT_H_