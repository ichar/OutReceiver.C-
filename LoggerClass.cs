using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Logger
{
    class LoggerClass
    {
        public string LogDir, LogFile, ProjectName;

        public Encoding encoding = Encoding.GetEncoding("windows-1251");

        protected void set_logfile()
        {
            LogDir = string.Format("{0}\\Log_{1}", LogDir, ProjectName);
            LogFile = string.Format("{0}\\{1}_{2}.log", LogDir, DateTime.Today.ToString("yyyyMMdd"), ProjectName);
        }

        public LoggerClass(string sProjectName, string sLogDir = "")
        {
            int n;
            ProjectName = sProjectName;

            if (sLogDir == "")
            {
                LogDir = Environment.CommandLine;

                if (LogDir.IndexOf("\"") == 0)
                {
                    LogDir = LogDir.Substring(1);
                    LogDir = LogDir.Substring(0, LogDir.IndexOf("\""));
                }

                if (LogDir.IndexOf(" ") > 0)
                {
                    LogDir = LogDir.Substring(0, LogDir.IndexOf(" "));
                }

                n = LogDir.LastIndexOf("\\");
                LogDir = n < 0 ? Directory.GetCurrentDirectory() : LogDir.Substring(0, n);
            }
            else
                LogDir = sLogDir;

            set_logfile();
        }

        public int WriteInfo(string Message)
        {
            return Write("OK", Message);
        }

        public int WriteWarning(string Message)
        {
            return Write("WARNING", Message);
        }

        public int WriteError(string Message)
        {
            return Write("ERROR", Message);
        }

        public int Write(string Type, string Message)
        {
            StreamWriter lStream = null;

            if (LogDir.Length == 0 || LogFile.Length == 0)
                set_logfile();

            if (!Directory.Exists(LogDir))
            {
                DirectoryInfo dInfo = Directory.CreateDirectory(LogDir);
                if (!dInfo.Exists)
                    return -1;
            }

            if (!File.Exists(LogFile))
                lStream = new StreamWriter(LogFile, false, this.encoding);
            else
                lStream = new StreamWriter(LogFile, true, this.encoding);

            if (lStream == null)
                return -2;

            lStream.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + Type + "\t" + Message);
            lStream.Close();

            //Console.WriteLine(Message);

            return 0;
        }
    }
}
