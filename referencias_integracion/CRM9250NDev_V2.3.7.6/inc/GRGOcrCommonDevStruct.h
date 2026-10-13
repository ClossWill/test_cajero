#ifndef _GRG_CIM_COMMON_DEV_STRUCT_H
#define _GRG_CIM_COMMON_DEV_STRUCT_H

#define GRG_DB_NOTEDETAILS_TABLENAME		("GRGNoteDetails")		//数据库钞票序列号表名

//钞票序列号数据库表项
#define GRG_NOTEINFO_DBTABLE_ID				("ID")					//数据库记录的ID(索引)
#define GRG_NOTEINFO_DBTABLE_JOURNALNO		("acJournalNo")			//交易流水号
#define GRG_NOTEINFO_DBTABLE_ACCOUNTNO		("acAccountNo")			//银行账户(卡号或存折号)
#define GRG_NOTEINFO_DBTABLE_SYSTIME		("sSysTime")			//交易进行时的系统时间
#define GRG_NOTEINFO_DBTABLE_DATE			("iDate")				//交易日期
#define GRG_NOTEINFO_DBTABLE_TIMES			("byTimes")				//交易流水号的第几次存款/取款
#define GRG_NOTEINFO_DBTABLE_NOTEINDEX		("uiNoteIndex")			//钞票序号
#define GRG_NOTEINFO_DBTABLE_VALIDATION		("byValidation")		//钞票有效性
#define GRG_NOTEINFO_DBTABLE_FACEWAY		("byFaceWay")			//钞票面向
#define GRG_NOTEINFO_DBTABLE_NOTETYPE		("byNoteType")			//钞票种类(参考GRG面额代码表
#define GRG_NOTEINFO_DBTABLE_NOTEVERSION	("byNoteVersion")		//钞票版本(参考GRG面额代码表)
#define GRG_NOTEINFO_DBTABLE_NOTEDENOMCODE	("byNoteDenomCode")		//钞票面额代号(参考GRG面额代码表)
#define GRG_NOTEINFO_DBTABLE_GODESTTYPE		("byGoDestType")		//钞票去向类型
#define GRG_NOTEINFO_DBTABLE_GODESTDESC		("acGoDestDesc")		//钞票去向目的地描述(如iType==1，则为钱箱ID)
#define GRG_NOTEINFO_DBTABLE_IMAGEFILENAME	("acImageFileName")		//钞票序列号图像文件名
#define GRG_NOTEINFO_DBTABLE_SERIALNO		("acSerialNo")			//序列号图片识别后的钞票序列号
#define GRG_NOTEINFO_DBTABLE_MAC			("acMac")				//图像校验码

//取钞票信息错误码
#define E_GRGNOTE_ERROR_BASE			(1)
#define E_GRGNOTE_NV_COM				(E_GRGNOTE_ERROR_BASE+0)	//与NV通信失败
#define E_GRGNOTE_SAVE_IMAGE			(E_GRGNOTE_ERROR_BASE+1)	//图像数据保存为文件失败
#define E_GRGNOTE_BACKUP_FILES			(E_GRGNOTE_ERROR_BASE+2)	//备份失败
#define E_GRGNOTE_RECOGNITION			(E_GRGNOTE_ERROR_BASE+3)	//图像识别失败
#define E_GRGNOTE_QUERY					(E_GRGNOTE_ERROR_BASE+4)	//数据检索失败
#define E_GRGNOTE_OPERATE_DATABASE		(E_GRGNOTE_ERROR_BASE+5)	//操作数据库失败
#define E_GRGNOTE_MEMORY_ALLOCATE		(E_GRGNOTE_ERROR_BASE+6)	//分配内存失败
#define E_GRGNOTE_DELETE_IMAGE			(E_GRGNOTE_ERROR_BASE+7)	//删除图像文件失败
#define E_GRGNOTE_LOAD_OCRDLL			(E_GRGNOTE_ERROR_BASE+8)	//加载OCR动态库失败
#define E_GRGNOTE_OPERATE_SQLFILE		(E_GRGNOTE_ERROR_BASE+9)	//操作SQL文件失败
#define E_GRGNOTE_INVALID_PARA			(E_GRGNOTE_ERROR_BASE+10)	//无效参数
#define E_GRGNOTE_BACKUPING_FILES		(E_GRGNOTE_ERROR_BASE+11)	//数据库备份还原中，请稍后重试
#define E_GRGNOTE_BLACKLIST_SN_LEN		(E_GRGNOTE_ERROR_BASE+12)	//黑名单更新失败

#define GRG_NOTEINFO_WIDTHBYTES(bits)	(((bits *8 ) + 31) / 32 * 4)//计算图像每行象素所占的字节数目
#define GRG_RGB24_WIDTHBYTES(bits)	(((bits *8*3 ) + 31) / 32 * 4)//计算24BITES图像每行象素所占的字节数目
#define IMAGE_WIDTH						136							//序列号图像宽度
#define IMAGE_HEIGHT					40							//序列号图像高度
#define	IMAGE_SIZE						(IMAGE_WIDTH * IMAGE_HEIGHT)//序列号图像大小

#pragma pack(push, 1)

//日期
typedef struct tGRGDate
{
	WORD			wYear;				//年(2009)
	WORD			wMonth;				//月(1—12)
	WORD			wDay;				//日(1—31)
}tGRGDate;

//日期时间
typedef struct tGRGDateTime
{
	WORD			wYear;				//年(2009)
	WORD			wMonth;				//月(1—12)
	WORD			wDay;				//日(1—31)
	WORD			wHour;				//时(0-23)
	WORD			wMinute;			//分(0-59)
	WORD			wSecond;			//秒(0-59)
}tGRGDateTime;

//钞票去向
typedef struct tGRGNoteGoDest
{
	BYTE			iType;				//钞票去向类型(1:钱箱；2:钞口；3:暂存；4:URJB)
	char			acDest[8];			//钞票去向描述(如iType==1，则为钱箱ID)
}tGRGNoteGoDest;

//交易信息参数
typedef struct tGRGTransInfoParas
{
	char			acJournalNo[32];	//交易流水号
	char			acAccountNo[48];	//银行账户(卡号或存折号)
	BYTE			byTimes;			//第几次存款/取款
}tGRGTransInfoParas;

//检索类型
typedef enum eGRGQueryType
{
	GRG_QUERY_TYPE_BACK = 0,			//向后检索
	GRG_QUERY_TYPE_FRONT = 1			//向前检索
}eGRGQueryType;

//检索方式(上层可以根据该参数实现上下翻页查询功能)
typedef struct tGRGQueryType
{
	eGRGQueryType	eType;				//检索类型(GRG_QUERY_TYPE_BACK：数据库检索ID后检索；GRG_QUERY_TYPE_FRONT：数据库检索ID前检索)
	UINT			uiID;				//数据库记录的ID索引(初始查询，该值设为0)
}tGRGQueryType;

//钞票检索参数
typedef struct tGRGNoteQueryParas	
{
	tGRGQueryType	sQueryType;			//检索类型
	char			acJournalNo[32];	//交易流水号
	char			acAccountNo[48];	//银行账户(卡号或存折号)
	BYTE			byTimes;			//第几次存款/取款
	WORD			wDenomination;		//面额代号(参考GRG面额代码表)
	tGRGNoteGoDest	sNoteGoDest;		//钞票去向
	char			acSerialNo[32];		//指定的序列号
	tGRGDate		sDateBegin;			//日期开始
	tGRGDate		sDateEnd;			//日期结束
	
}tGRGNoteQueryParas;

//钞票检索参数（扩展）
typedef struct tGRGNoteQueryParasEx	
{
	tGRGQueryType	sQueryType;			//检索类型
	char			acJournalNo[32];	//交易流水号
	char			acAccountNo[48];	//银行账户(卡号或存折号)
	BYTE			byTimes;			//第几次存款/取款
	WORD			wDenomination;		//面额代号(参考GRG面额代码表)
	tGRGNoteGoDest	sNoteGoDest;		//钞票去向
	char			acSerialNo[32];		//指定的序列号
	tGRGDateTime	sDateTimeBegin;		//日期时间开始
	tGRGDateTime	sDateTimeEnd;		//日期时间结束
	
}tGRGNoteQueryParasEx;

//按钞票序列号检索参数
typedef struct tGRGNoteSnQueryParas	
{
	WORD			wDenomination;		//面额代号(参考GRG面额代码表)
	char			acSerialNo[32];		//指定的序列号
	int				iMatchLevelBegin;	//匹配个数开始
	int				iMatchLevelEnd;		//匹配个数结束	
	tGRGDate		sDateBegin;			//日期开始
	tGRGDate		sDateEnd;			//日期结束
}tGRGNoteSnQueryParas;

//按钞票序列号检索参数（扩展）
typedef struct tGRGNoteSnQueryParasEx	
{
	WORD			wDenomination;		//面额代号(参考GRG面额代码表)
	char			acSerialNo[32];		//指定的序列号
	int				iMatchLevelBegin;	//匹配个数开始
	int				iMatchLevelEnd;		//匹配个数结束	
	tGRGDateTime	sDateTimeBegin;		//日期时间开始
	tGRGDateTime	sDateTimeEnd;		//日期时间结束
}tGRGNoteSnQueryParasEx;

//钞票信息结构
typedef struct tGRGNoteDetails 
{
	UINT			uiID;				//数据库记录的ID索引
	SYSTEMTIME		sSysTime;			//交易进行时的系统时间
	char			acJournalNo[32];	//交易流水号
	char			acAccountNo[48];	//银行账户(卡号或存折号)
	BYTE			byTimes;			//交易流水号的第几次存款/取款	
	WORD			wNoteIndex;			//钞票序号
	BYTE			byValidation;		//钞票有效性(0x00:真钞；其他:非真钞)
	BYTE			byFaceWay;			//钞票面向
										//0x00：正面向前、顶端在上
										//0x01：正面向前、顶端在下
										//0x02：背面向前、顶端在上
										//0x03：背面向前、顶端在下
	WORD			wDenomination;		//面额代号(参考GRG面额代码表)
	tGRGNoteGoDest	sNoteGoDest;		//钞票去向	
	char			acImageFileName[64];//钞票序列号图像文件名
	char			acSerialNo[32];		//序列号图片识别后的钞票序列号
	char			acMac[8];			//图像校验码
	BYTE			byMatchCount;		//按序列号检索时，匹配个数
}tGRGNoteDetails;
typedef struct tGRGSegSnData  
{
	DWORD	adwData[32];		// 图像冠字号码点阵数据
}tGRGSegSnData;
typedef struct tGRGSegOneSnInfo
{
	WORD		wNum;			// 字符数，与wCharNum一样。
	WORD		wHeight;		// 每个图像字符的高度，最大32
	WORD		wWidth;			// 每个图像字符的宽度，最大32
	tGRGSegSnData	asSnData[12];	// 最多12个字符的点阵数据。
}tGRGSegOneSnInfo;
typedef struct tGRGNoteOCRInfo
{
	WORD				wNoteIndex;			//钞票序号
	tGRGSegOneSnInfo	asSnInfo;					//点阵数据。
	BYTE				abyInfoErrCode[6];			//钞票鉴别错误码
	BYTE				abyRejectErrCode[6];		//拒钞错误码
	BYTE				byQuality;					//成色
	char				acImageNameFront[260];	//全图文件名_正面
	char				acImageNameBack[260];	//全图文件名_反面
	BYTE				abyReserved[32];
}tGRGNoteOCRInfo;

//OCR序列号黑名单信息
typedef struct tGRGOcrSnBlackList
{
	int				iTotal;					//黑名单序列号个数
	char			acSerialNoInfo[256][32];//最多256个黑名单序列号信息，每个序列号最多32个字符
											//目前硬件只支持每个序列号20个字符
}tGRGOcrBlackListInfo;

//OCR相同序列号信息
typedef struct tGRGOcrSnSameInfo
{
	int				iCount;				//相同序列号个数
	char			acSerialNo[32];		//相同序列号
}tGRGOcrSnSameInfo;

//OCR相同序列号列表信息
typedef struct tGRGOcrSnSameList
{
	int						iTotal;					//记录条数
	tGRGOcrSnSameInfo		asOcrSnSameInfo[64];	//最多保存64个相同序列号信息
}tGRGOcrSnSameList;

// modify by oyquan 2016-03-24
//2016-3-7 add by pslin
//OCR序列号黑名单信息
typedef struct tGRGOcrSnBlack
{
	BYTE			byOCRType; 				/* OCR类型:
	                                       0x01:精确的10位类型,如:MA15151515 
	                                       0x02:带*的模糊匹配类型，如:*HD90* 
	                                       0x03:区间类型，如:HD90102200-HD90102299 */
	BYTE			byBlackType;			// 黑名单类型:1:黑名单 2:可疑钞 
	int				iDenomCode; 			// 钞票面额代号:不区分ID时值为0
	char			acSerialNoInfo[32];		// 冠字号字符串:初始化全部为'\0'
}tGRGOcrSnBlack;

//OCR序列号黑名单信息列表
typedef struct tGRGOcrSnBlackListEx
{

	int             iOPType;				/* 黑名单操作方式：
											1:以添加方式更新黑名单(往黑名单后面添加，数据不保存至Flash) 
											2:以覆盖方式更新黑名单(用当前黑名单覆盖，数据不保存至Flash) 
											3:以添加方式更新黑名单(往黑名单后面添加，数据保存至Flash) 
											4:以覆盖方式更新黑名单(用当前黑名单覆盖，数据保存至Flash) */
	int				iProtocolVersion;		// 黑名单协议版本:第一版为1,后续可递增。0为版本不可用
	int             iBlackCount;			// 黑名单序列号个数:上层传入acSerialNoInfo有效个数
	tGRGOcrSnBlack	asSerialNoInfo[1000];	// 目前定义1000,目前硬件支持1000条 （后续如果硬件支持大于1000条，上层需要更新大于1000条黑名单则分包下发即可）
	BYTE            abyReserved[16];			// 16个保留字节:初始化全部为'\0'
}tGRGOcrSnBlackListEx;

// OCR序列号黑名单容量(NV支持的黑名单最大数目)
typedef struct tGRGOcrSnBlackListCapacity 
{ 
	int				iProtocolVersion;		// [IN][OUT]	协议版本
	int				iCapacity;				// [OUT]		容量
	BYTE			abyReserved[16];		//				保留 
} tGRGOcrSnBlackListCapacity; 
//2016-3-7 end add by pslin

// add by oyquan 2016-07-20
typedef struct tGRGOcrCfgInfo
{
	char acName[32];
	char acValue[128];
	char acReserved[32];
} tGRGOcrCfgInfo;
// end add by oyquan 2016-07-20

typedef struct tGrgFsnFileName
{
	char			acFsnFileName[260];	// FSN文件名
	BYTE			abyReserve[8];		// 保留
} tGrgFsnFileName;

// 金标认证信息结构体
typedef struct tGrgCertificationInfo
{
	char			acDevType[32];				//型号规格
	char			acSoftVersion[32];			//软件版本号
	char			acHwVersion[32];			//硬件版本号
	char			acCertificationDate[16];	//签发日期	如：2022-06-02
	char			acExpirationDate[16];		//失效日期  如：2024-06-02
	char 			acReserved[64];				//保留
	
} tGrgCertificationInfo;
//end add by cbsheng 2022-06-02

#pragma pack(pop)

#endif // _GRG_CIM_COMMON_DEV_STRUCT_H