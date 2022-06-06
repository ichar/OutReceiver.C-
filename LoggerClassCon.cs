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
        //public RichTextBox MesBox = null;

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
            
            LogDir = LogDir + "\\Log_" + ProjectName;
            LogFile = LogDir + "\\" + DateTime.Today.ToString("yyyyMMdd") + "_" + ProjectName + ".log";
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

            if (!Directory.Exists(LogDir))
            {
                DirectoryInfo dInfo = Directory.CreateDirectory(LogDir);
                if (!dInfo.Exists)
                    return -1;
            }
            try
            {
                lStream = new StreamWriter(LogFile, !File.Exists(LogFile) ? false : true);
                if (lStream == null)
                    return -2;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return 0;
            }

            lStream.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+"\t" + Type + "\t" + Message);
            lStream.Close();

            return 0;
        }
    }
}
