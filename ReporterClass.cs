using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Logger;
using BaseDB;

namespace OutReceiver
{
    class ReporterClass
    {
        public bool 
            isTest = true, 
            isDBOK = false;
        
        public string ReportDir, ReportFile, ProjectName;

        private const string DBRDT = "yyyy-MM-dd HH:mm:ss.fff";
        private string
            sBank = "",
            sConfig = "",
            sAction = "",
            sSourceFile = "",
            sDestinationFile = "",
            sType = "",
            sCode = "",
            sStepInfo = "",
            sStarted = "", 
            sFinished = "",
            sRD = "",
            sLocation = "";

        private bool isStreemEnabled = true;
        private bool isDBEnabled = true;

        public void SetDateTime(string attr, DateTime value)
        {
            if (attr == "Started")
                this.sStarted = value.ToString(DBRDT);
            else if (attr == "Finished")
                this.sFinished = value.ToString(DBRDT);
        }

        public void SetAttr(string attr, string value, string format = "")
        {
            if (attr == "Bank")
                this.sBank = value;
            else if (attr == "Config")
            {
                string[] v = value.Split('-');
                this.sConfig = value;
                if (v.Length > 1)
                {
                    this.sBank = v[0];
                    this.sType = v[v.Length-1].Substring(0, 1).ToUpper();
                }
                else
                {
                    this.sBank = "ANY";
                    this.sType = value.Substring(0, 1).ToUpper();
                }
                this.sRD = DateTime.Now.ToString(DBRDT);
            }
            else if (attr == "Action")
                this.sAction = value;
            else if (attr == "File" || attr == "SourceFile")
                this.sSourceFile = value;
            else if (attr == "DestinationFile")
                this.sDestinationFile = value;
            else if (attr == "Type")
                this.sType = value;
            else if (attr == "Code")
                this.sCode = value;
            else if (attr == "StepInfo")
                this.sStepInfo = value;
            else if (attr == "Started")
                this.sStarted = value;
            else if (attr == "Finished")
                this.sFinished = value;
            else if (attr == "Location")
                this.sLocation = value;
        }

        public LoggerClass logger = null;
        public BaseDBClass dbm = null;

        public ReporterClass(string sReportDir, string sProjectName)
        {
            ReportDir = sReportDir;
            ProjectName = sProjectName;
        }

        public string Prepare(string sMes)
        {
            string sR = sMes;

            sR = sR.Replace("&", "&amp;");
            sR = sR.Replace("\"", "&quot;");
            sR = sR.Replace(">", "&gt;");
            sR = sR.Replace("<", "&lt;");

            return sR;
        }

        public void SetWriteMode(string Mode)
        {
            if (Mode.Length > 0)
            {
                isStreemEnabled = Mode.IndexOf('S') > -1 ? true : false;
                isDBEnabled = Mode.IndexOf('D') > -1 ? true : false;
            }
        }

        public int Write(string Type, string JobStep, int ResultCode, string Message)
        {
            if (isStreemEnabled == false)
                return 0;

            StreamWriter lStream = null;
            
            ReportFile = ReportDir + "\\" + DateTime.Today.ToString("yyyyMMdd") + "_" + ProjectName + ".rep";
            if (!Directory.Exists(ReportDir))
            {
                DirectoryInfo dInfo = Directory.CreateDirectory(ReportDir);
                if (!dInfo.Exists)
                    return -1;
            }
            
            try
            {
                if (!File.Exists(ReportFile))
                    lStream = new StreamWriter(ReportFile, false);
                else
                    lStream = new StreamWriter(ReportFile, true);
            }
            catch (Exception ex)
            {
                if (logger != null)
                    logger.WriteError(ex.Message);
                return -200;
            }
            
            if (lStream == null)
                return -2;
            
            lStream.WriteLine(
                "<Report Type=\"" + Type + 
                "\" JobStep=\"" + JobStep + 
                "\" ResultCode=\"" + ResultCode.ToString() +
                "\" DTime=\"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffff") + 
                "\">" + Message + "</Report>");
            lStream.Close();

            //WriteDB("command", Type, JobStep, ResultCode, Message);
            
            return 0;
        }

        public void Register(string Mode, int ResultCode = 0, string Message = "", string Info = "")
        {
            if (isDBEnabled == false)
                return;

            int RC;
            string sQ;
            string status = "";

            if (isDBOK && dbm != null)
            {
                try
                {
                    Dictionary<string, string[]> @params = new Dictionary<string, string[]>();

                    switch (Mode)
                    {
                        case "order":
                            sQ = "[dbo].[UPDATE_Order_sp]";
                            @params.Add("Package", new[] { Program.PackageName });
                            @params.Add("Qty", new[] { Program.Qty.ToString(), "", "int" });
                            @params.Add("Client", new[] { this.sBank });
                            @params.Add("Host", new[] { Program.Host });
                            @params.Add("BaseFolder", new[] { Program.BaseFolder });
                            @params.Add("ArchiveFolder", new[] { Program.ArchiveFolder });
                            @params.Add("Status", new[] { "", "output" });
                            RC = dbm.RunExec(sQ, @params, out status);
                            break;
                        case "certificate":
                            sQ = "[dbo].[REGISTER_IOCertificate_sp]";
                            @params.Add("Package", new[] { Program.PackageName });
                            @params.Add("Client", new[] { this.sBank });
                            @params.Add("Config", new[] { this.sConfig });
                            @params.Add("SourceFile", new[] { this.sSourceFile });
                            @params.Add("Type", new[] { this.sType });
                            @params.Add("Info", new[] { Info });
                            @params.Add("RD", new[] { this.sRD });
                            @params.Add("Status", new[] { "", "output" });
                            RC = dbm.RunExec(sQ, @params, out status);
                            break;
                        case "event":
                            sQ = "[dbo].[REGISTER_IOEvent_sp]";
                            @params.Add("Package", new[] { Program.PackageName });
                            @params.Add("Client", new[] { this.sBank });
                            @params.Add("Config", new[] { this.sConfig });
                            @params.Add("Action", new[] { this.sAction });
                            @params.Add("SourceFile", new[] { this.sSourceFile });
                            @params.Add("DestinationFile", new[] { this.sDestinationFile });
                            @params.Add("Type", new[] { this.sType });
                            @params.Add("Code", new[] { this.sCode });
                            @params.Add("StepInfo", new[] { this.sStepInfo });
                            @params.Add("ErrorInfo", new[] { string.Format("{0}:{1}", ResultCode.ToString(), Message.Replace("'", "")) });
                            @params.Add("Started", new[] { this.sStarted });
                            @params.Add("Finished", new[] { this.sFinished });
                            @params.Add("Location", new[] { this.sLocation });
                            @params.Add("RD", new[] { this.sRD });
                            @params.Add("Status", new[] { "", "output" });
                            RC = dbm.RunExec(sQ, @params, out status);
                            break;
                        default:
                            sQ = "insert into dbo.OutReceiverRep_tb (Task,JobStep,ResultCode,Message) values " +
                                string.Format("('{0}','{1}','{2}','{3}')",
                                    this.sConfig,
                                    this.sAction,
                                    ResultCode.ToString(),
                                    Message.Replace("'", "")
                            );
                            RC = dbm.RunCommand(sQ);
                            break;
                    }
                    if (RC < 0)
                    {
                        if (logger != null)
                            logger.WriteError(dbm.Message);
                    }
                }
                catch (Exception ex)
                {
                    if (logger != null)
                        logger.WriteError(ex.Message);
                }
            }
        }
    }
}
