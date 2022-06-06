using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Xml;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

using Common;
using Logger;
using CommonFunctions;
using BaseDB;
using BaseSMTP;

namespace OutReceiver
{
    class Program
    {
        private static string version = "Version 4.17 от 2016.09.13 © ЗАО «Розан Файнэнс»";
        private static int IsDebug = 0;
        private static int IsDeepDebug = 0;
        private static int IsFTPTrace = 0;

        public static string PackageName = "";
        public static int Qty = 0;
        public static string Host = "";
        public static string BaseFolder = "";
        public static string ArchiveFolder = "";

        public static bool 
            isNoRegfch = false;

        private static bool 
            isTest = false,
            isLiteTest = false,
            isVersion = false,
            isHelp = false,
            isNoCheckProcess = false,
            isNoSendError = false,
            isNoDB = false,
            isNoMap = false,
            isNoCrypto = false,
            isNoPGP = false,
            isNoMail = false,
            isSecure = false,
            isDBOK = false;

        private static string 
            sServer = "", 
            sBase = "", 
            sUser = "", 
            sPassword = "", 
            sAlert = "",
            sEmergency = "",
            sLogDir = "",
            sReportDir = "",
            sProjectName = "";

        private static string
            sIniFile = "",
            sRepMode = "";

        private static string sFtpBufferSize = "0";

        private static string SmtpHost;
        private static int SmtpPort;
        private static string SmtpLogin = "";
        private static string SmtpPassword = "";

        private static List<string> attrsConfig = new List<string>
        {
            "SMTP", "DBServer", "DBBase", "DBUser", "DBPass", "ReportDir", "Alert", "Emergency", "FtpBufferSize"
        };

        private static Dictionary<string, string> attrsJob = new Dictionary<string, string>
        {
            {"CHECKMAP",      "SourceDir:MapDir:Login:Password"},
            {"COPY",          "[Input]:[Output]:[Mode]:[InputDir]:[OutputDir]:[Mask]:[TargetMask]:[InputLogin]:[InputPassword]:[OutputLogin]:[OutputPassword]:[CountCards]"},
            {"CPEXPLORE",     "[Certificate]:[CryptoModule]:[Input]:[Output]:[Mode]:[InputDir]:[OutputDir]:[Mask]:[TargetMask]:[RosanCert]:[Password]:[ClientCert]"},
            {"CREATEDIR",     "InputDir"},
            {"DADOWNLOAD",    "[FTP]:[Input]:[Output]:[Mode]:[Response]:[FTPAddress]:[Login]:[Password]:[InputDir]:[Mask]:[Timeout]:[BufferSize]"},
            {"DAUPLOAD",      "[FTP]:[Input]:[Output]:[Mode]:[Response]:[FTPAddress]:[Login]:[Password]:[InputDir]:[Mask]:[Timeout]:[BufferSize]"},
            {"DECRYPT",       "[Certificate]:[CryptoModule]:[Input]:[Output]:[Mode]:[InputDir]:[OutputDir]:[Mask]:[TargetMask]:[RosanCert]:[Password]"},
            {"DELETE",        "[Mode]:InputDir:Mask:[InputLogin]:[InputPassword]"},
            {"DOWNLOAD",      "[FTP]:[Input]:[Output]:[Mode]:[Response]:[FTPAddress]:[Login]:[Password]:[InputDir]:[OutputDir]:[Mask]:[Timeout]:[BufferSize]"},
            {"EMAIL",         "[Address]:[Response]:[Input]:[Output]:[From]:[FromName]:[To]:Subject:Body:[Attachment]"},
            {"ENCRYPT",       "[Certificate]:[CryptoModule]:[Input]:[Output]:[Mode]:[InputDir]:[OutputDir]:[Mask]:[TargetMask]:[RosanCert]:[Password]:[ClientCert]"},
            {"EXEC",          "[Command]:[Module]:[Options]:[Work]:[Timeout]:[SuccessCode]"},
            {"GETFTP",        "[FTP]:[Input]:[Output]:[Mode]:[Response]:[FTPAddress]:[Login]:[Password]:[InputDir]:[OutputDir]:[Mask]:[Timeout]:[BufferSize]"},
            {"MOVE",          "[Input]:[Output]:[Mode]:[InputDir]:[OutputDir]:[Mask]:[TargetMask]:[InputLogin]:[InputPassword]:[OutputLogin]:[OutputPassword]:[CountCards]"},
            {"PGPDECRYPT",    "[Certificate]:[CryptoModule]:[Input]:[Output]:[Mode]:[InputDir]:[OutputDir]:[Mask]:[TargetMask]:[Password]:[ClientCert]"},
            {"PGPENCRYPT",    "[Certificate]:[CryptoModule]:[Input]:[Output]:[Mode]:[InputDir]:[OutputDir]:[Mask]:[TargetMask]:[Password]:[ClientCert]"},
            {"PGPSIGN",       "[Certificate]:[CryptoModule]:[Input]:[Output]:[Mode]:[InputDir]:[OutputDir]:[Mask]:[TargetMask]:[Password]:[ClientCert]"},
            {"PGPVERIFY",     "[Certificate]:[CryptoModule]:[Input]:[Output]:[Mode]:[InputDir]:[OutputDir]:[Mask]:[TargetMask]:[Password]:[ClientCert]"},
            {"PUTFTP",        "[FTP]:[Input]:[Output]:[Mode]:[Response]:[FTPAddress]:[Login]:[Password]:[InputDir]:[OutputDir]:[Mask]:[Timeout]:[BufferSize]"},
            {"RAR",           "[Input]:[Output]:[Mode]:[InputDir]:[OutputDir]:[Mask]:Password:FileMode:ArchName:[Password]:[Command]:[Options]"},
            {"REMOVEDIR",     "[Mode]:InputDir:[Mask]"},
            {"RENAME",        "[Mode]:InputDir:Mask:TargetMask:[CountCards]"},
            {"SIGN",          "[Certificate]:[CryptoModule]:[Input]:[Output]:[Mode]:[InputDir]:[OutputDir]:[Mask]:[TargetMask]:[RosanCert]:[Password]"},
            {"SFTPDOWNLOAD",  "[SFTP]:[Input]:[Output]:[Mode]:[Response]:[SFTPAddress]:[Login]:[Password]:[InputDir]:[OutputDir]:[Mask]:[Timeout]:[BufferSize]"},
            {"SFTPUPLOAD",    "[SFTP]:[Input]:[Output]:[Mode]:[Response]:[SFTPAddress]:[Login]:[Password]:[InputDir]:[OutputDir]:[Mask]:[Timeout]:[BufferSize]"},
            {"SFTPGET",       "[SFTP]:[Input]:[Output]:[Mode]:[Response]:[SFTPAddress]:[Login]:[Password]:[InputDir]:[OutputDir]:[Mask]:[Timeout]:[BufferSize]"},
            {"SFTPPUT",       "[SFTP]:[Input]:[Output]:[Mode]:[Response]:[SFTPAddress]:[Login]:[Password]:[InputDir]:[OutputDir]:[Mask]:[Timeout]:[BufferSize]"},
            {"UNRAR",         "[Input]:[Output]:[Mode]:[InputDir]:[OutputDir]:[Mask]:[Password]:[Command]:[Options]"},
            {"UNZIP",         "[Input]:[Output]:[Mode]:[InputDir]:[OutputDir]:[Mask]:[Password]:[Command]:[Options]"},
            {"UPLOAD",        "[FTP]:[Input]:[Output]:[Mode]:[Response]:[FTPAddress]:[Login]:[Password]:[InputDir]:[OutputDir]:[Mask]:[Timeout]:[BufferSize]"},
            {"VERIFY",        "[Certificate]:[CryptoModule]:[Input]:[Output]:[Mode]:[InputDir]:[OutputDir]:[Mask]:[TargetMask]:[RosanCert]:[Password]:[ClientCert]"},
            {"ZIP",           "[Input]:[Output]:[Mode]:[InputDir]:[OutputDir]:[Mask]:Password:FileMode:ArchName:[Password]:[Command]:[Options]"},
        };

        private static Dictionary<string, string> actions = new Dictionary<string, string>
        {
            {"CHECKMAP",      "DoJobCheckMap"},
            {"COPY",          "DoJobCopy"},
            {"CPEXPLORE",     "DoJobCryptoProExplore"},
            {"CREATEDIR",     "DoJobCreateFolder"},
            {"DADOWNLOAD",    "DoJobDeleteAfterDownload"},
            {"DAUPLOAD",      "DoJobDeleteAfterUpload"},
            {"DECRYPT",       "DoJobDecrypt"},
            {"DELETE",        "DoJobDelete"},
            {"DOWNLOAD",      "DoJobDownload"},
            {"ENCRYPT",       "DoJobEncrypt"},
            {"EXEC",          "DoJobExec"},
            {"GETFTP",        "DoJobGet"},
            {"EMAIL",         "DoJobEmail"},
            {"MOVE",          "DoJobMove"},
            {"PGPDECRYPT",    "DoJobDecrypt"},
            {"PGPENCRYPT",    "DoJobEncrypt"},
            {"PGPSIGN",       "DoJobSign"},
            {"PGPVERIFY",     "DoJobVerify"},
            {"PUTFTP",        "DoJobPut"},
            {"RAR",           "DoJobZip"},
            {"REMOVEDIR",     "DoJobDeleteFolder"},
            {"RENAME",        "DoJobRename"},
            {"SIGN",          "DoJobSign"},
            {"SFTPDOWNLOAD",  "DoJobDownload"},
            {"SFTPUPLOAD",    "DoJobUpload"},
            {"SFTPGET",       "DoJobGet"},
            {"SFTPPUT",       "DoJobPut"},
            {"UNRAR",         "DoJobUnzip"},
            {"UNZIP",         "DoJobUnzip"},
            {"UPLOAD",        "DoJobUpload"},
            {"VERIFY",        "DoJobVerify"},
            {"ZIP",           "DoJobZip"},
        };

        const string attr_log = "LogDir";
        const string attr_stepid = "ID";
        const string attr_repeat = "Repeat";
        const string attr_ftpbuffersize = "FtpBufferSize";
        const string attr_exit = "Exit";

        private static string attrsCommon = attr_stepid + ':' + attr_repeat + ':' + attr_exit;
        private static int nParRepeat = -1;
        private static bool isExitByNoData = false;
        private static bool isDoneSilent = false;

        private static LoggerClass logger = null;
        private static BaseDBClass dbm = null;
        private static IniFiler ini = null;
        private static ReporterClass reporter = null;
        private static BaseSMTPClass smtp = null;
        private static MessagerClass messager = null;
        private static UNCAccess unc = null;

        enum LogType { Info = 0, Warning, Error }
        private static Mutex mutRes = null;

        const string TAB = "\t";

        public static void LogEvent(string e, string msg)
        {
            switch (e)
            {
                case "timeout":
                    WriteMessage(LogType.Warning, null, msg, true);
                    break;
                case "trace":
                    WriteMessage(LogType.Info, null, msg, true);
                    break;
            }
        }

        private static void WriteMessage(LogType Type, Step step, string msg, bool force = false)
        {
            int StatusID;
            string message;

            StatusID = (int)Type;

            if (isTest || isLiteTest)
            {
                switch (Type)
                {
                    case LogType.Info:
                        logger.WriteInfo(msg);
                        break;
                    case LogType.Warning:
                        logger.WriteWarning(msg);
                        break;
                    case LogType.Error:
                        logger.WriteError(msg);
                        break;
                }
            }

            if (isLiteTest && force)
            {
                message = string.Format(@"{0}{1}", (step == null ? "" : step.Name + TAB), msg);
                Console.WriteLine(message);
            }
            else if (isTest || force)
            {
                Console.WriteLine(msg);
            }
            else if (!(isTest || isLiteTest) && !isDoneSilent)
            {
                Console.WriteLine("silent");
                isDoneSilent = true;
            }
        }

        private static void SetConfigAttr(string attr, string value)
        {
            switch (attr)
            {
                case "SMTP":
                    string[] host = value.Split(':');
                    if (host.Length >= 2)
                    {
                        SmtpHost = host[0];
                        int.TryParse(host[1], out SmtpPort);
                        if (host.Length == 4)
                        {
                            SmtpLogin = host[2];
                            SmtpPassword = host[3];
                        }
                    }
                    else
                    {
                        SmtpHost = host[0];
                    }
                    break;
                case "DBServer":
                    sServer = value;
                    break;
                case "DBBase":
                    sBase = value;
                    break;
                case "DBUser":
                    sUser = value;
                    break;
                case "DBPass":
                    sPassword = value;
                    break;
                case "ReportDir":
                    sReportDir = value;
                    break;
                case "Alert":
                    sAlert = value;
                    break;
                case "Emergency":
                    sEmergency = value;
                    break;
                case "FtpBufferSize":
                    sFtpBufferSize = value;
                    break;
                default:
                    break;
            }
        }

        private static void CheckCommonAttr(string name, string task, Step step)
        {
            string value = "";
            int RC = 0;

            switch (name)
            {
                case attr_repeat:
                    RC = ini.GetJobParam(task, step.Action, step.Order, name, out value);
                    if (!int.TryParse(value, out nParRepeat) || nParRepeat == -1)
                        nParRepeat = 3;
                    break;
                case attr_stepid:
                    if (step.ID != "")
                        WriteMessage(LogType.Info, null, string.Format("{0} = {1}", name, step.ID));
                    break;
                case attr_exit:
                    RC = ini.GetJobParam(task, step.Action, step.Order, name, out value);
                    if (RC == 0 && value == "__NODATA__")
                        isExitByNoData = true;
                    break;
                default:
                    break;
            }
        }

        private static void SetCommonAttr(string name, BaseFileClass handler)
        {
            switch (name)
            {
                case attr_repeat:
                    handler.Repeat = nParRepeat;
                    //WriteMessage(LogType.Info, null, string.Format("{0} = {1}", name, nParRepeat.ToString()));
                    break;
                default:
                    break;
            }
        }

        protected static void ParseArgs(string[] args)
        {
            foreach (string arg in args)
            {
                if (arg == "/?")
                    isHelp = true;
                else if (arg.StartsWith("-"))
                {
                    string oname = arg.StartsWith("--") ? arg.Substring(2) : arg.Substring(1);
                    string lname = oname.ToLower();

                    if (oname.Length == 0)
                        continue;
                    else if (lname == "nodb")
                        isNoDB = true;
                    else if (lname == "nomap")
                        isNoMap = true;
                    else if (lname == "nocrypto")
                        isNoCrypto = true;
                    else if (lname == "nopgp")
                        isNoPGP = true;
                    else if (lname == "nomail")
                        isNoMail = true;
                    else if (lname == "nocheck")
                        isNoCheckProcess = true;
                    else if (lname == "nosenderror")
                        isNoSendError = true;
                    else if (lname == "noregfch")
                        isNoRegfch = true;
                    else if (lname.StartsWith("r:"))
                        sRepMode = oname.Substring(2).ToUpper();
                    else
                    {
                        if (lname.IndexOf('v') > -1)
                            isVersion = true;
                        if (lname.IndexOf('t') > -1)
                            isTest = true;
                        else if (lname.IndexOf('l') > -1)
                            isLiteTest = true;
                        if (oname.IndexOf("d", StringComparison.CurrentCulture) > -1)
                            IsDebug = 1;
                        else if (oname.IndexOf("D", StringComparison.CurrentCulture) > -1)
                            IsDeepDebug = 1;
                        if (lname.IndexOf("f", StringComparison.CurrentCulture) > -1)
                            IsFTPTrace = 1;
                        if (lname.IndexOf('s') > -1)
                            isSecure = true;
                        if (oname.IndexOf('H') > -1)
                        {
                            unc.HideConsole();

                            IsDebug = 0;
                            IsDeepDebug = 0;
                        }
                    }
                }
                else if (sIniFile.Length == 0)
                    sIniFile = arg;
                else
                    sLogDir = arg;
            }
        }

        static void Main(string[] args)
        {
            int RC;

            #region Configuration
            var appSettings = ConfigurationManager.AppSettings;
            #endregion

            #region System
            unc = new UNCAccess();
            Assembly assem = Assembly.GetExecutingAssembly();
            AssemblyName assemName = assem.GetName();
            sProjectName = assemName.Name;

            ParseArgs(args);

            if (sLogDir.Length == 0 && appSettings.AllKeys.Contains(attr_log))
            {
                sLogDir = appSettings[attr_log];
            }

            logger = new LoggerClass(sProjectName, sLogDir);

            if (isVersion)
                WriteMessage(LogType.Info, null, string.Format("{0}", version), true);

            if (isHelp)
            {
                List<string> help = new List<string>
                {
                    "Rosan Finance Inc.",
                    "I/O Exchange OutReceiver application.",
                    "",
                    "Format: OutReceiver.exe [options] <config> [<logger>]",
                    "",
                    "Valid options:",
                    "     -v         -- version",
                    "     -L         -- lite trace output",
                    "     -T         -- full trace output",
                    "     -d         -- debug (with <Press any key to continue> before exit)",
                    "     -D         -- deepdebug (with <Press any key to continue> after every step)",
                    "     -H         -- hide console stdout (don\'t use together with -d or -D)",
                    "     -f         -- ftptrace (show all requests)",
                    "     -s         -- secure mode",
                    "     -R:SD      -- report mode, 'S' - Stream enabled, 'D' - DB enabled",
                    "",
                    "     --nomap    -- no drive mapping",
                    "     --nodb     -- don't connect to LOG database",
                    "     --nocheck  -- don't check app mutex",
                    "     --nocrypto -- don't run any CryptoPro actions",
                    "     --nopgp    -- don't run any PGP actions",
                    "     --noregfch -- don't registry files exist checking.",
                    "",
                    "Arguments:",
                    "     <config>   -- job's xml-config",
                    "     <logger>   -- logger folder.",
                    "",
                    string.Format("{0}", version)
                };
                foreach(string s in help)
                {
                    Console.WriteLine(string.Format("--> {0}", s));
                }
                Environment.ExitCode = 0;
                return;
            }

            if (sIniFile == "" || args.Length < 1)
            {
                if (isVersion)
                    Environment.ExitCode = 0;
                else
                {
                    WriteMessage(LogType.Error, null, "Не задан параметр - файл настройки", true);
                    Environment.ExitCode = -2;
                }
                return;
            }

            WriteMessage(LogType.Info, null, string.Format("Рабочий журнал {0}", logger.LogFile));

            if (isTest)
            {
                WriteMessage(LogType.Info, null, string.Format("Конфигуратор {0}:", sIniFile));
                if (isNoCheckProcess)
                    WriteMessage(LogType.Info, null, "+isNoCheckProcess");
                if (isNoDB)
                    WriteMessage(LogType.Info, null, "+isNoDB");
                if (isNoMap)
                    WriteMessage(LogType.Info, null, "+isNoMap");
            }

            if (!isNoCheckProcess)
            {
                String sMutName = "Global\\" + assemName.Name + "_" + sIniFile.Substring(sIniFile.LastIndexOf("\\") + 2);
                Program.mutRes = new Mutex(false, sMutName);
                try
                {
                    if (!Program.mutRes.WaitOne(100))
                    {
                        WriteMessage(LogType.Error, null, "Такой процесс уже запущен", true);
                        //Program.mutRes.ReleaseMutex();
                        Environment.ExitCode = -3;
                        return;
                    }
                }
                catch (Exception)
                {
                    Environment.ExitCode = -4;
                    return;
                }
            }
            #endregion
            
            #region Initiation
            ini = new IniFiler(sIniFile);
            RC = ini.Open();
            if (RC != 0)
            {
                WriteMessage(LogType.Error, null, ini.Message, true);
                Environment.ExitCode = -5;
                return;
            }
            if (IsDeepDebug == 1)
                ini.Backup();
            #endregion

            #region GetConfigAttrList
            string value;
            foreach (string attr in attrsConfig)
            {
                RC = ini.GetConfigAttr(attr, out value);
                if (RC != 0)
                {
                    if (!(appSettings.AllKeys.Contains(attr) && appSettings[attr].Length > 0))
                    {
                        WriteMessage(LogType.Error, null, string.Format("Не найден параметр {0} {1}", attr, ini.Message), true);
                        continue;
                    }
                    else
                    {
                        value = appSettings[attr];
                    }
                }
                SetConfigAttr(attr, value);
                if (isSecure && "dbuser:dbpass".IndexOf(attr.ToLower()) > -1)
                    continue;
                WriteMessage(LogType.Info, null, attr + " = " + value);
            }
            #endregion

            #region DBConnect
            if (isNoDB)
            {
                WriteMessage(LogType.Info, null, "Database is deactivated\n");
                isDBOK = false;
                dbm = null;
            }
            else
            {
                dbm = new BaseDBClass(sServer, sBase, sUser);
                RC = dbm.Open(sPassword);
                if (RC != 0)
                {
                    WriteMessage(LogType.Error, null, string.Format("DB connection failed! {0} [{1}]\n", 
                        dbm.Message, dbm.ConnectionString), true);
                    isDBOK = false;
                    dbm = null;
                }
                else
                {
                    isDBOK = true;
                    WriteMessage(LogType.Info, null, "DB connection OK\n");
                }
            }
            #endregion

            #region SMTP/Messager
            smtp = new BaseSMTPClass(SmtpHost, SmtpPort, SmtpLogin, SmtpPassword);
            smtp.InitState();
            messager = new MessagerClass(ref smtp);
            messager.InitState(sAlert, sEmergency);
            #endregion

            #region SetReporter
            reporter = new ReporterClass(sReportDir, sProjectName);
            reporter.SetWriteMode(sRepMode);
            reporter.isDBOK = isDBOK;
            reporter.isTest = isTest;
            reporter.logger = logger;
            reporter.dbm = dbm;
            #endregion

            int errors = Run();

            #region SMTPRelease
            smtp.WaitCompleted();
            smtp.Release();

            foreach (string msg in smtp.GetErrorMessages())
            {
                if (msg.Length > 0)
                    WriteMessage(LogType.Error, null, msg);
            }
            
            smtp = null;
            #endregion

            WriteMessage(LogType.Info, null, string.Format("Выход\n"));

            #region Exit
            if (IsDebug == 1 || IsDeepDebug == 1)
            {
                Console.WriteLine("-> Press any key to continue:");
                Console.ReadLine();
            }
            #endregion

            #region Release
            if (!isNoCheckProcess)
            {
                Program.mutRes.ReleaseMutex();
            }
            #endregion

            Environment.Exit(errors);
        }

        static void InitState()
        {
            Qty = 0;

            List<string> hosts = new List<string> { "ftp", "sftp", "remote" };
            foreach (string host in hosts)
            {
                if (Host == null || Host.Length == 0)
                    ini.SetList.TryGetValue(host, out Host);
                else
                    break;
            }

            ini.SetList.TryGetValue("base", out BaseFolder);
            ini.SetList.TryGetValue("archive", out ArchiveFolder);
        }

        static int Run()
        {
            int RC;
            Dictionary<string, string[]> responses = new Dictionary<string, string[]>();
            int errors = 0;
            string[] items;
            string value;

            #region ConfigsMainCycle
            foreach (string config in ini.ConfigList) // Список сценариев <Configs>.<Config>
            {

                int ErrCount = 0, StepNo = 0;
                string action = "";
                string[] attrs;
                List<string> @params = new List<string>();

                WriteMessage(LogType.Info, null, string.Format("==> Сценарий {0}:", config));

                ini.InitState(config);
                reporter.SetAttr("Config", config);

                WriteMessage(LogType.Info, null, string.Format("Папка REP-журнала {0}", sReportDir));

                InitState();

                #region StepList
                string total_steps = ini.CurrentJobStepList.Count.ToString();
                bool isExit = false;
                foreach (Step step in ini.CurrentJobStepList) // Список шагов <JobSteps>.<JobStep>
                {
                    if (ErrCount > 0)
                        break;

                    if (isExit)
                        break;

                    WriteMessage(LogType.Info, step, string.Format("--> Шаг {0}.{1}:", step.Order, step.Action));

                    ++StepNo;

                    isExitByNoData = false;
                    @params.Clear();

                    #region SetJobStepHandler
                    BaseFileClass handler = null;

                    switch (step.Action)
                    {
                        case "CHECKMAP":
                            if (!isNoMap)
                                handler = new JobCheckMapClass(reporter);
                            break;
                        case "PGPVERIFY":
                        case "PGPSIGN":
                        case "PGPENCRYPT":
                        case "PGPDECRYPT":
                            if (!isNoPGP)
                                handler = new JobRosanPGPClass(reporter);
                            break;
                        case "VERIFY":
                        case "SIGN":
                        case "ENCRYPT":
                        case "DECRYPT":
                        case "CPEXPLORE":
                            if (!isNoCrypto)
                                handler = new JobRosanCryptoProClass(reporter);
                            break;
                        case "COPY":
                        case "MOVE":
                        case "DELETE":
                        case "RENAME":
                        case "CREATEDIR":
                        case "REMOVEDIR":
                            handler = new JobCopyClass(reporter);
                            break;
                        case "EMAIL":
                            if (!isNoMail)
                                handler = new JobSMTPClass(ref smtp, reporter);
                            break;
                        case "PUTFTP":
                        case "GETFTP":
                        case "UPLOAD":
                        case "DOWNLOAD":
                        case "DAUPLOAD":
                        case "DADOWNLOAD":
                            handler = new JobFTPClass(reporter);
                            if (IsFTPTrace == 1)
                                handler.TraceEnable();
                            handler.SetAttr("BufferSize", sFtpBufferSize);
                            break;
                        case "EXEC":
                            handler = new JobExecClass(reporter);
                            break;
                        case "ZIP":
                        case "UNZIP":
                        case "RAR":
                        case "UNRAR":
                            handler = new JobUnzipClass(reporter);
                            if (step.Action.IndexOf("ZIP") > -1)
                                @params.Add("zip");
                            break;
                        case "SFTPPUT":
                        case "SFTPGET":
                        case "SFTPUPLOAD":
                        case "SFTPDOWNLOAD":
                            handler = new JobSFTPClass(reporter);
                            if (IsFTPTrace == 1)
                                handler.TraceEnable();
                            handler.SetAttr("BufferSize", sFtpBufferSize);
                            break;
                        default:
                            WriteMessage(LogType.Error, step, string.Format("Шаг не предусмотрен: {0}", step), true);
                            continue;
                    }

                    if (handler != null)
                    {
                        handler.InitState();
                        handler.isTest = isTest;
                        handler.Config = config;
                        handler.Action = step.Action;
                        handler.StepInfo = string.Format("{0}:{1}:{2}", StepNo.ToString(), total_steps, step.Name);
                    }
                    else
                        continue;

                    RC = ini.GetJobParamList(config, step);
                    if (RC != 0)
                    {
                        WriteMessage(LogType.Error, step, "Не найден список параметров " + ini.Message, true);
                        handler.WriteReport(RC, ini.Message, "");
                        ++ErrCount;
                        continue;
                    }
                    handler.ParamList = ini.ParamList;
                    #endregion

                    #region GetCommonAttrs
                    foreach (string attr in attrsCommon.Split(':'))
                    {
                        CheckCommonAttr(attr, config, step);
                        SetCommonAttr(attr, handler);
                    }
                    #endregion

                    #region GetAttrs
                    attrs = attrsJob[step.Action].Split(':');

                    foreach (string a in attrs)
                    {
                        string attr = a;
                        bool isMandatory = true;

                        Match m = Regex.Match(a, @"\[(.*)\]", RegexOptions.IgnoreCase);
                        if (m.Value.Length > 0 && m.Groups.Count > 0)
                        {
                            attr = m.Groups[1].ToString();
                            isMandatory = false;
                        }

                        RC = ini.GetJobParam(config, step.Action, step.Order, attr, out value);
                        if (RC == 0)
                            handler.SetAttr(attr, value);
                        else if (isMandatory)
                        {
                            WriteMessage(LogType.Error, step, string.Format("Не найден параметр {0} {1}", attr, ini.Message), true);
                            handler.WriteReport(RC, ini.Message, ini.ParamList);
                            ++ErrCount;
                            continue;
                        }
                        else
                        {
                            ini.Reset();
                        }
                        value = handler.GetAttr(attr);
                        
                        string key = attr.ToLower();
                        if (isSecure && (key.IndexOf("login") > -1 || key.IndexOf("password") > -1))
                            continue;
                        if (value.Length > 0)
                            WriteMessage(LogType.Info, step, attr + " = " + value);
                    }
                    #endregion

                    #region RunStep
                    action = actions[step.Action];
                    handler.ValidateState(action);
                    handler.SetResponses(ref responses);

                    bool code = false;
                    items = new string[0];

                    /************************************************
                     *  RC: Handler return codes:
                     *       0 -- OK
                     *      -1 -- Unassigned action
                     *      -2 -- Invalid arguments
                     *      -3 -- Action is not performed (no data)
                     *    -100 -- Response is not valid (SMTP)
                     *    -200 -- Service error (SMTP/reporter)
                     *    -300 -- Timeout expired (FTP Async)
                     *    -500 -- Unexpected error
                     ************************************************/

                    CallParams args = new CallParams(action, @params.ToArray());

                    try
                    {
                        code = handler.Run(args);

                        items = args.Items;
                        RC = args.RC;
                    }
                    catch (Exception ex)
                    {
                        handler.Message = string.Format("!!!!!!!!! {0}{1}\n{2}\n", 
                            ex.Message, args.Message, ex.StackTrace);
                        code = true;
                        RC = -500;
                    }

                    if (args.Info.Count > 0)
                    {
                        foreach (string line in args.Info)
                        {
                            WriteMessage(LogType.Info, step, line.TrimEnd(), true);
                        }
                    }

                    if (code)
                    {
                        if (RC == -3)
                        {
                            WriteMessage(LogType.Warning, step, string.Format("Нет данных"), isLiteTest);
                            if (isExitByNoData)
                            {
                                isExit = true;
                            }
                        }
                        else if (RC == -100)
                        {
                            WriteMessage(LogType.Warning, step, handler.Message, isLiteTest);
                        }
                        else if (RC < -99)
                        {
                            WriteMessage(LogType.Error, step, handler.Message, isLiteTest);
                            handler.WriteReport(RC, ini.Message, ini.ParamList);
                            ++ErrCount;
                        }
                        else if (RC != 0)
                        {
                            WriteMessage(LogType.Error, step, string.Format(@"Ошибка !!!!!!!!! {0}", handler.Message), isLiteTest);
                            handler.WriteReport(RC, ini.Message, ini.ParamList);
                            ++ErrCount;
                        }
                        else if (items.Length > 0)
                        {
                            foreach (string item in items)
                            {
                                WriteMessage(LogType.Info, step, string.Format(@"Выполнено: {0}", item), isLiteTest);
                            }
                        }
                        else
                        {
                            WriteMessage(LogType.Info, step, "Шаг выполнен +++++++++", isLiteTest);
                            handler.WriteReport(RC, ini.Message, ini.ParamList);
                        }

                        if (RC != 0 && "-3:-100:".IndexOf(RC.ToString()+':') == -1 && !isNoSendError)
                        {
                            if (messager.SendError(handler.Message, config, step.Name, RC, "*") != 0)
                                WriteMessage(LogType.Error, step, string.Format(@"Ошибка SMTP {0}", messager.Message), isLiteTest); ;
                        }
                    }

                    if (step.ID.Length > 0)
                        responses[step.ID] = items;
                    #endregion

                    if (IsDeepDebug == 1)
                    {
                        Console.WriteLine("-> Step {0}. Press any key to continue (<q> to exit):", StepNo.ToString());
                        string e = Console.ReadLine();
                        if (e == "q")
                            isExit = true;
                    }
                }
                #endregion

                if (ErrCount == 0)
                    WriteMessage(LogType.Info, null, string.Format("Успешно завершен\n"));

                errors += ErrCount;
            }
            #endregion

            return errors;
        }
    }
}
