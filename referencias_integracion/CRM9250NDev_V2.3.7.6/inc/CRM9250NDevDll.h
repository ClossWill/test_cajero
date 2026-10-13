#ifndef _CRM9250NDEVDLL_H_
#define _CRM9250NDEVDLL_H_

#include "CRM9250NDevDef.h"
#include "CRM9250NDevStruct.h"
#include "GRGOcrCommonDevStruct.h"

#ifdef __cplusplus
extern "C" {
#endif  //__cplusplus
	
	// 打开和关闭逻辑设备
	HANDLE WINAPI hOpenLogicDevice(const char *p_pcLogicDevName);								
	void WINAPI vCloseLogicDevice(HANDLE p_hDevHandle);
	
	// 获取和释放通信口
	int WINAPI iSetCommPara(HANDLE p_hDevHandle, tDevReturn *p_psStatus);
	int WINAPI iCloseComm(HANDLE p_hDevHandle, tDevReturn *p_psStatus);

	// 获取和设置整数类型配置项的值
	int WINAPI iGetIntValue(HANDLE p_hDevHandle, const char *p_pcSection, const char *p_pcKeyName, int *p_piKeyValue);
	int WINAPI iSetIntValue(HANDLE p_hDevHandle, const char *p_pcSection, const char *p_pcKeyName, int p_iKeyValue);

	// 获取和设置字符串类型配置项的值
	int WINAPI iGetStringValue(HANDLE p_hDevHandle, 
								const char *p_pcSection, 
								const char *p_pcKeyName, 
								char *p_pcKeyValue, 
								unsigned *p_puiValueLen);
	int WINAPI iSetStringValue(HANDLE p_hDevHandle, 
								const char *p_pcSection, 
								const char *p_pcKeyName, 
								const char *p_pcKeyValue);

	// 获取和设置硬件错误标识
	tDevReturn WINAPI tGetErrorFlag(HANDLE p_hDevHandle);
	void WINAPI vSetErrorFlag(HANDLE p_hDevHandle, tDevReturn p_sStatus);

	// 初始化设置
	int WINAPI iInitSetting(HANDLE p_hDevHandle, tCRM9250N_InitSetting *p_psInitSetting, tDevReturn *p_psStatus);

	// 获取和设置钱箱信息
	int WINAPI iGetCassetteInfo(HANDLE p_hDevHandle, 
								tCRM9250N_CassetteInfo *p_psCassetteInfo, 
								tCRM9250N_CassetteStatus *p_psCassetteStatus, 
								tDevReturn *p_psStatus);
	int WINAPI iSetCassetteInfo(HANDLE p_hDevHandle, const tCRM9250N_CassetteInfo *p_psCassetteInfo, tDevReturn *p_psStatus);
	
	// 初始化
	int WINAPI iInit(HANDLE p_hDevHandle, tCRM9250N_InitInfo *p_psInitInfo, tDevReturn *p_psStatus);
	
	// 上电初始化: iInitSetting + iGetCassetteInfo + iInit
	int WINAPI iPowerOnInit(HANDLE p_hDevHandle,
							tCRM9250N_InitSetting *p_psInitSetting,
							tCRM9250N_CassetteInfo *p_psCassetteInfo,
							tCRM9250N_CassetteStatus *p_psCassetteStatus, 
							tCRM9250N_InitInfo *p_psInitInfo,
							tDevReturn *p_psStatus);

	// 获取状态信息、获取最后一次状态信息
	int WINAPI iGetDevStatus(HANDLE p_hDevHandle, tCRM9250N_DevStatus *p_psDevStatus, tDevReturn *p_psStatus);
	int WINAPI iGetLastDevStatus(HANDLE p_hDevHandle, tCRM9250N_DevStatus *p_psDevStatus);

	// 获取动态库、固件和硬件版本信息
	int WINAPI iGetVersion(HANDLE p_hDevHandle, char *p_pcVersion);
	int WINAPI iGetDevVersion(HANDLE p_hDevHandle, tCRM9250N_DevVersionInfo *p_psDevVersion, tDevReturn *p_psStatus);
	int WINAPI iGetHardwareVersion(HANDLE p_hDevHandle, tCRM9250N_HwVersionInfo *p_psHwVersion, tDevReturn *p_psStatus);

	// 准备下一笔交易、准备存款、准备取款(保留)
	int WINAPI iPrepareNextTransaction(HANDLE p_hDevHandle, tDevReturn *p_psStatus);
	int WINAPI iPrepareDeposit(HANDLE p_hDevHandle, tDevReturn *p_psStatus);
	int WINAPI iPrepareDispense(HANDLE p_hDevHandle, tDevReturn *p_psStatus);

	// 打开、关闭和自检闸门
	int WINAPI iOpenShutter(HANDLE p_hDevHandle, tDevReturn *p_psStatus);
	int WINAPI iCloseShutter(HANDLE p_hDevHandle, tDevReturn *p_psStatus);
	int WINAPI iSelfCheckDev(HANDLE p_hDevHandle, tDevReturn *p_psStatus);

	// 点钞、存款、取消存款
	int WINAPI iCashCount(HANDLE p_hDevHandle, tCRM9250N_CashCount *p_psCashCount, tDevReturn *p_psStatus);
	int WINAPI iStoreMoney(HANDLE p_hDevHandle, tCRM9250N_StoreMoney *p_psStoreMoney, tDevReturn *p_psStatus);
	int WINAPI iCancelDeposit(HANDLE p_hDevHandle, tCRM9250N_CancelDeposit *p_psCancelDeposit, tDevReturn *p_psStatus);

	// 取款、送钞(保留)、回收NF或NE
	int WINAPI iDispense(HANDLE p_hDevHandle, tCRM9250N_DispenseInfo *p_psDispenseInfo, tDevReturn *p_psStatus);
	int WINAPI iPresent(HANDLE p_hDevHandle, tDevReturn *p_psStatus);
	int WINAPI iRetract(HANDLE p_hDevHandle, tCRM9250N_RetractInfo *p_psRetractInfo, tDevReturn *p_psStatus);

	// 获取NV支持币种
	int WINAPI iGetDenominationTable(HANDLE p_hDevHandle,
										tCRM9250N_DenominationTable *p_psDenominationTable, 
										tDevReturn *p_psStatus);

	// 开始审计、获取审计状态、获取审计结果
	int WINAPI iStartAudit(HANDLE p_hDevHandle, const tCRM9250N_StartAudit *p_psStartAudit, tDevReturn *p_psStatus);
	int WINAPI iGetAuditStatus(HANDLE p_hDevHandle, tCRM9250N_AuditStatus *p_psAuditStatus, tDevReturn *p_psStatus);
	int WINAPI iGetAuditResult(HANDLE p_hDevHandle, tCRM9250N_AuditResult *p_psAuditResult, tDevReturn *p_psStatus);

	// 获取OCR序列号黑名单、更新OCR序列号黑名单、清空OCR序列号黑名单
	int WINAPI iGetOcrBlackList(HANDLE p_hDevHandle, tGRGOcrSnBlackList *p_psOcrBlackListInfo, tDevReturn *p_psStatus);
	int WINAPI iUpdateOcrBlackList(HANDLE p_hDevHandle,
									BYTE p_byType, 
									const tGRGOcrSnBlackList *p_psOcrBlackListInfo, 
									tDevReturn *p_psStatus);
	int WINAPI iClearOcrBlackList(HANDLE p_hDevHandle, tDevReturn *p_psStatus);
	
	// 设置交易信息、获取相同序列号信息
	int WINAPI iSetTransInfo(HANDLE p_hDevHandle, const tGRGTransInfoParas *p_psTransInfo, tDevReturn *p_psStatus);
	int WINAPI iGetOcrSnSameInfo(HANDLE p_hDevHandle, 
									const tGRGTransInfoParas *p_psTransInfoParas, 
									tGRGOcrSnSameList *p_psOcrSnSameList, 
									tDevReturn *p_psStatus);

	// 测试NV是否已连接上、获取经过NV鉴别的钞票信息
	int WINAPI iGetNvConnected(HANDLE p_hDevHandle, tDevReturn *p_psStatus);
	int WINAPI iGetNvNotesInfo(HANDLE p_hDevHandle, 
								tGRGNoteDetails *p_psNoteDetails, 
								UINT *p_puiNumOfItems, 
								BYTE p_byNoteType, 
								char p_pcFsnFileName[260], 
								tDevReturn *p_psStatus);
	int WINAPI iGetNvNotesInfoEx(HANDLE p_hDevHandle, 
								tGRGNoteDetails *p_psNoteDetails, 
								tGRGNoteOCRInfo *p_psNoteOCRInfo,
								UINT *p_puiNumOfItems, 
								BYTE p_byNoteType, 
								char p_pcFsnFileName[260], 
								tDevReturn *p_psStatus);	
	// 按照指定模式删除指定日期前的数据库表内容和/或对应图片文件、FSN文件，p_byMode参数说明见eCRM9250N_DelMode
	int WINAPI iDeleteSerialRecords(HANDLE p_hDevHandle, BYTE p_byMode, tGRGDate p_sDate, tDevReturn *p_psStatus);

	// 按交易流水号、银行账号、日期、钞票面额等检索钞票信息
	int WINAPI iQueryCashInfo(HANDLE p_hDevHandle,
								const tGRGNoteQueryParasEx *p_psNoteQueryParas, 
								tGRGNoteDetails *p_psNoteDetails,
								UINT *p_puiNumOfItems, 
								BYTE *p_pbyHasItems,
								tDevReturn *p_psStatus);
	
	// 按序号检索钞票信息
	int WINAPI iQueryCashInfoSn(HANDLE p_hDevHandle,
								const tGRGNoteSnQueryParasEx *p_psNoteSnQueryParas, 
								tGRGNoteDetails *p_psNoteDetails,
								UINT *p_puiNumOfItems, 
								tDevReturn *p_psStatus);

	// VDA功能接口，见VDA接口专用说明文件
	int WINAPI iVDAExecute(HANDLE p_hDevHandle, int p_iCmd, const void *p_poInput, void *p_poOutput, tDevReturn *p_psStatus);
	
	// 黑名单扩展接口：更新黑名单扩展接口、获取黑名单扩展接口、获取黑名单容量
	int WINAPI iUpdateOcrBlackListEx(HANDLE p_hDevHandle, tGRGOcrSnBlackListEx *p_psOcrBlackListInfo, tDevReturn *p_psStatus);
	int WINAPI iGetOcrBlackListEx(HANDLE p_hDevHandle, tGRGOcrSnBlackListEx *p_psOcrBlackListInfo, tDevReturn *p_psStatus);
	int WINAPI iGetOcrBlackListCapacity(HANDLE p_hDevHandle, tGRGOcrSnBlackListCapacity *p_psOcrBlackListCapacity, tDevReturn *p_psStatus);
	// 发送取审计OCR完成消息
	int WINAPI iSetAuditInfoDone(HANDLE p_hDevHandle, tDevReturn *p_psStatus);
	// 取机芯序列号
	int WINAPI iGetDevSerailNo(HANDLE p_hDevHandle, tCRM9250N_DevSerialNo *p_psDevSerialNo,tDevReturn *p_psStatus);
	int WINAPI iGetDevSerailNoEx(HANDLE p_hDevHandle, tCRM9250N_DevSerialNoEx *p_psDevSerialNo,tDevReturn *p_psStatus);
	BOOL WINAPI bIsSendCmdFinish(HANDLE p_hDevHandle);
	//执行SQL语句
//	int WINAPI iExecuteSql(HANDLE p_hDevHandle,const char *p_pcSql, tGRGNoteDetails *p_psNoteDetails, UINT *p_puiNumOfItems, BYTE *p_pbyHasItems,tDevReturn *p_psStatus);
	int WINAPI iGetCassetteDenomDetails(HANDLE p_hDevHandle, int p_iCassetteNo, int *p_piDenomCount, 
										tCRM9250N_DenomDetails *p_psDenomDetails, tDevReturn *p_psStatus);
// 混存功能接口：获取DC钱箱面额信息、设置DC钱箱面额信息、获取钱箱币种明细信息(0表示AC、1~4表示RC1~RC4、5表示LRB)
	int WINAPI iGetDCDenomAttribute(HANDLE p_hDevHandle, int p_iCassetteNo, 
									tCRM9250N_DCDenomAttribute *p_psDCDenomAttribute, tDevReturn *p_psStatus);
	int WINAPI iSetDCDenomAttribute(HANDLE p_hDevHandle, int p_iCassetteNo, 
									const tCRM9250N_DCDenomAttribute *p_psDCDenomAttribute, tDevReturn *p_psStatus);

	// 在线下载主控介质或NV介质
	int WINAPI iFirmwareDownload(HANDLE p_hDevHandle, const char *p_pcDownloadPath, BYTE p_byMode, tDevReturn *p_psStatus);
	
	// 强制下载主控板介质
	// 若主控板介质已丢失，则必须先完成强制下载主控板介质，然后才能在线下载其它主控介质(上控板和钱箱)
	int WINAPI iFirmwareDownloadEx(HANDLE p_hDevHandle, const char *p_pcDownloadPath, tDevReturn *p_psStatus);
	
	// 获取当前文件下载进度
	int	WINAPI iGetCurFileProgress(HANDLE p_hDevHandle, tCRM9250N_PDLProgress *p_psPdlProgress);
	// 打开闸门
	int WINAPI iCloseShutterEx(HANDLE p_hDevHandle, BYTE p_byMode,tDevReturn *p_psStatus);

	//DRC箱机芯接口

	// 初始化设置
	int WINAPI iInitSettingEx(HANDLE p_hDevHandle, tCRM9250N_InitSettingEx *p_psInitSetting, tDevReturn *p_psStatus);
	// 上电初始化: iInitSetting + iGetCassetteInfo + iInit
	int WINAPI iPowerOnInitEx(HANDLE p_hDevHandle,
		tCRM9250N_InitSettingEx *p_psInitSetting,
		tCRM9250N_CassetteInfoEx *p_psCassetteInfo,
		tCRM9250N_CassetteStatusEx *p_psCassetteStatus, 
		tCRM9250N_InitInfoEx *p_psInitInfo,
							tDevReturn *p_psStatus);
	// 初始化
	int WINAPI iInitEx(HANDLE p_hDevHandle, tCRM9250N_InitInfoEx *p_psInitInfo, tDevReturn *p_psStatus);
	// 获取状态信息、获取最后一次状态信息
	int WINAPI iGetDevStatusEx(HANDLE p_hDevHandle, tCRM9250N_DevStatusEx *p_psDevStatus, tDevReturn *p_psStatus);
	int WINAPI iGetLastDevStatusEx(HANDLE p_hDevHandle, tCRM9250N_DevStatusEx *p_psDevStatus);
	// 存款
	int WINAPI iStoreMoneyEx(HANDLE p_hDevHandle, tCRM9250N_StoreMoneyEx *p_psStoreMoney, tDevReturn *p_psStatus);
	int WINAPI iCancelDepositEx(HANDLE p_hDevHandle, tCRM9250N_CancelDepositEx *p_psCancelDeposit, tDevReturn *p_psStatus);

	// 取款、回收NF或NE
	int WINAPI iDispenseEx(HANDLE p_hDevHandle, tCRM9250N_DispenseInfoEx *p_psDispenseInfo, tDevReturn *p_psStatus);
	int WINAPI iRetractEx(HANDLE p_hDevHandle, tCRM9250N_RetractInfoEx *p_psRetractInfo, tDevReturn *p_psStatus);
	
	// 开始审计、获取审计状态、获取审计结果
	int WINAPI iStartAuditEx(HANDLE p_hDevHandle, const tCRM9250N_StartAuditEx *p_psStartAudit, tDevReturn *p_psStatus);
	int WINAPI iGetAuditStatusEx(HANDLE p_hDevHandle, tCRM9250N_AuditStatusEx *p_psAuditStatus, tDevReturn *p_psStatus);
	int WINAPI iGetAuditResultEx(HANDLE p_hDevHandle, tCRM9250N_AuditResultEx *p_psAuditResult, tDevReturn *p_psStatus);
	// 获取和设置钱箱信息
	int WINAPI iGetCassetteInfoEx(HANDLE p_hDevHandle, 
								tCRM9250N_CassetteInfoEx *p_psCassetteInfo, 
								tCRM9250N_CassetteStatusEx *p_psCassetteStatus, 
								tDevReturn *p_psStatus);
	int WINAPI iSetCassetteInfoEx(HANDLE p_hDevHandle, const tCRM9250N_CassetteInfoEx *p_psCassetteInfo, tDevReturn *p_psStatus);
	// 获取钱箱币种明细信息(0表示AC1、1~10表示RC1~RC10、11表示LRB、12表示AC2)
	int WINAPI iGetCassetteDenomDetailsEx(HANDLE p_hDevHandle, int p_iCassetteNo, int *p_piDenomCount, 
		tCRM9250N_DenomDetails *p_psDenomDetails, tDevReturn *p_psStatus);
	int WINAPI iGetDevVersionEx(HANDLE p_hDevHandle, tCRM9250N_DevVersionInfoEx *p_psDevVersion, tDevReturn *p_psStatus);
	// 取机芯序列号
	int WINAPI iGetDevSerailNoEx(HANDLE p_hDevHandle, tCRM9250N_DevSerialNoEx *p_psDevSerialNo,tDevReturn *p_psStatus);
	//获取上一笔交易进出各部件的张数
	int WINAPI iGetLastTranDetail(HANDLE p_hDevHandle, tCRM9250N_LastInOutUnitsInfo *p_psTranData,BYTE *p_byOperator,tDevReturn *p_psStatus);
	//读取机芯log数据并写入文件
	int WINAPI iReadDevLogToFile(HANDLE p_hDevHandle, tCRM9250N_ReadDevLog *p_psReadDevLog, tDevReturn* p_psStatus);
	//加钞张数设置接口
	int WINAPI iSetAddNotesNumLimit(HANDLE p_hDevHandle, tCRM9250N_ADDNOTES p_sNumLimit,tDevReturn* p_psStatus);
	//设置FSN信息
	int WINAPI iSetFsnInfo(HANDLE p_hDevHandle, tCRM9250N_SetFsnInfoIn p_sInfoIn,tDevReturn* p_psStatus);
	HANDLE WINAPI poCreateTPMDevInfo(const char *p_pszLogName);
	int WINAPI iDirectStoreMoney(HANDLE p_hDevHandle, tCRM9250N_DirectStoreMoney *p_psStoreMoney, tDevReturn *p_psStatus);
	//更新认证时间
	int WINAPI iUpdateCertificationDate(HANDLE p_hDevHandle, tCRM9250N_CertificationUpdate p_sDataIn,tDevReturn* p_psStatus);
	//获取部件认证信息
	int WINAPI iGetCertificationSerialNo(HANDLE p_hDevHandle, tCRM9250N_DevSerialNoEx* p_psSerialNo,tDevReturn* p_psStatus);
	//部件认证
	int WINAPI iModelsCertification(HANDLE p_hDevHandle, tCRM9250N_Certification p_sDataIn, tDevReturn* p_psStatus);
	//设置FSN配置项
	int WINAPI iSetFSNConfigParam(HANDLE p_hDevHandle, tCRM9250N_FsnCfgInfo p_sCfgInfo,tDevReturn* p_psStatus);
	
	int WINAPI iDirectStoreMoneyEx(HANDLE p_hDevHandle, tCRM9250N_DirectStoreMoney *p_psStoreMoney, tDevReturn *p_psStatus);

	int WINAPI iInitSettingEx2(HANDLE p_hDevHandle, tCRM9250N_InitSettingEx2 *p_psInitSetting, tDevReturn *p_psStatus);
	int WINAPI iCashCountEx(HANDLE p_hDevHandle, tCRM9250N_CashCountEx *p_psCashCount, tDevReturn *p_psStatus);

	//获取金标认证信息
	int WINAPI iGetCerificationInfo(HANDLE p_hDevHandle, tGrgCertificationInfo* p_sCertificationInfo,char* p_bsupp,tDevReturn* p_psStatus);

	int WINAPI iSetDuniteCfg(HANDLE p_hDevHandle, const char* CfgFileDir,const char* loggerDir, const char* UsbSetDeviceFd, tDevReturn *p_psStatus);

#ifdef __cplusplus
}
#endif //__cplusplus

#endif // _CRM9250NDEVDLL_H_
