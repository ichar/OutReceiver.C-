using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml;
using System.IO;
using System.Xml.XPath;
using System.Diagnostics;
using System.Threading.Tasks;

using Common;
using Logger;
using BaseByte;

namespace OutReceiver
{
    class BaseFileClass
    {
        public bool isTest = false;
        public bool isTrace = false;

        public string 
            Config = "",
            Action = "",
            StepInfo = "",
            ParamList = "",
            Message = "",
            CurrentFile = "",
            DestinationFile = "";

        private string 
            Input = "",
            Output = "";

        public string 
            Mode = "",       // {S|A} : Simple(Default)/Advanced(with RegEx)
            Mask = "",
            TargetMask = "",
            InputDir = "",
            InputLogin = "",
            InputPassword = "";

        public string 
            OutputDir = "",
            OutputLogin = "",
            OutputPassword = "";

        public DateTime Started, Finished;

        private string[] validCodes = new string[] { "ST", "OK", "ER" };
        public string[] SVS = { "::" };
        
        public const string EOL = "\n";
        public const string TAB = "\t";
        public const string CONST_IS_FALSE = "false:0:no";

        public Dictionary<string, string[]> responses = new Dictionary<string, string[]>();
        public List<string> performed = new List<string>();
        public List<string> runInfo = new List<string>();
        public bool isAsync = false;

        public int Repeat = 0;

        public virtual void SetAttr(string attr, string value)
        {
            if (attr.Length == 0)
                return;
            else if (attr == "Input")
            {
                this.Input = value;
                string[] items = value.Split(SVS, StringSplitOptions.None);

                /**********************
                 * 0 : Mode
                 * 1 : Mask
                 * 2 : TargetMask
                 * 3 : InputDir
                 * 4 : Login
                 * 5 : Password
                 **********************/

                this.Mode = items[0].Length > 0 ? items[0].Substring(0, 1).ToUpper() : "S";
                this.Mask = items.Length > 1 ? items[1] : "";
                this.TargetMask = items.Length > 2 ? items[2] : "";
                this.InputDir = items.Length > 3 ? items[3] : "";
                this.InputLogin = items.Length > 4 ? items[4] : "";
                this.InputPassword = items.Length > 5 ? items[5] : "";
            }
            else if (attr == "Mode")
                this.Mode = value.Length > 0 ? value.Substring(0, 1).ToUpper() : "S";
            else if (attr == "Mask")
                this.Mask = value;
            else if (attr == "TargetMask")
                this.TargetMask = value;
            else if (attr == "InputDir")
                this.InputDir = value;
            else if (attr == "InputLogin")
                this.InputLogin = value;
            else if (attr == "InputPassword")
                this.InputPassword = value;
            else if (attr == "Output")
            {
                this.Output = value;
                string[] items = value.Split(SVS, StringSplitOptions.None);

                /**********************
                 * 1 : OutputDir
                 * 2 : OutputLogin
                 * 3 : OutputPassword
                 **********************/

                this.OutputDir = items[0];
                this.OutputLogin = items.Length > 1 ? items[1] : "";
                this.OutputPassword = items.Length > 2 ? items[2] : "";
            }
            else if (attr == "OutputDir")
                this.OutputDir = value;
            else if (attr == "OutputLogin")
                this.OutputLogin = value;
            else if (attr == "OutputPassword")
                this.OutputPassword = value;
        }

        public virtual string GetAttr(string attr)
        {
            if (attr.Length == 0)
                return "";
            else if (attr == "Input")
                return this.Input;
            else if (attr == "Mode")
                return this.Mode;
            else if (attr == "Mask")
                return this.Mask;
            else if (attr == "TargetMask")
                return this.TargetMask;
            else if (attr == "InputDir")
                return this.InputDir;
            else if (attr == "InputLogin")
                return this.InputLogin;
            else if (attr == "InputPassword")
                return this.InputPassword;
            else if (attr == "Output")
                return this.Output;
            else if (attr == "OutputDir")
                return this.OutputDir;
            else if (attr == "OutputLogin")
                return this.OutputLogin;
            else if (attr == "OutputPassword")
                return this.OutputPassword;

            return "";
        }

        public LoggerClass logger = null;
        public ReporterClass reporter = null;

        public BaseFileClass(ReporterClass Reporter)
        {
            this.Message = "";
            this.reporter = Reporter;
        }

        public virtual void InitState()
        {
            this.Repeat = 3;
        }

        public virtual void ValidateState(string action)
        {
            return;
        }

        public virtual void TraceEnable()
        {
            this.isTrace = true;
        }

        public virtual void TraceDisable()
        {
            this.isTrace = false;
        }

        public void Sleep(int timeout = 1000)
        {
            //System.Threading.Thread.Sleep(timeout);
            //System.Timers.Timer timer = new System.Timers.Timer();
            //timer.Interval = timeout;
            //timer.Start();

            int time = Environment.TickCount;
            do
            {
                if (Environment.TickCount - time >= timeout)
                    break;
            } while (time > 0);
        }

        public int CountOrderCards(string filePath, string type = "S")
        {
            int rows = 0;
            string[] s = type.IndexOf(SVS[0]) == -1 ? type.Split(':') : type.Split(SVS, StringSplitOptions.None);
            string mode = s[0].ToUpper();
            string tag = "";
            Match m = null;

            if (s.Length > 1)
            {
                m = Regex.Match(s[1], @"\[(.*)\]", RegexOptions.IgnoreCase);
                if (m.Value.Length > 0 && m.Groups.Count > 0)
                {
                    tag = m.Groups[1].ToString();
                }
            }

            if ("S:C:D:R".IndexOf(mode) > -1)
            {
                string line;
                int n;
                System.IO.StreamReader file = new System.IO.StreamReader(filePath);
                while ((line = file.ReadLine()) != null)
                {
                    if (line == null)
                        continue;

                    // Simple lines count
                    if (mode == "S")
                    {
                        if (line.Length > 0)
                            ++rows;
                    }
                    // Lines with the given context count
                    else if (mode == "C")
                    {
                        if (line.IndexOf(tag) > -1)
                            ++rows;
                    }
                    // Lines with the multiple given context count
                    else if (mode == "D")
                    {
                        n = -1;
                        while ((n = line.IndexOf(tag, n + 1)) > -1)
                        {
                            ++rows;
                        }
                    }
                    // Regular expression with the given context count
                    else if (mode == "R" && tag.Length > 0)
                    {
                        m = Regex.Match(line, tag, RegexOptions.IgnoreCase);
                        if (m.Value.Length > 0)
                        {
                            ++rows;
                        }
                    }
                }
                file.Close();
            }

            else if ("X:W".IndexOf(mode) > -1)
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(filePath);

                switch (mode)
                {
                    // Simple XML node path
                    case "X":
                        rows = doc.SelectNodes(tag).Count;
                        break;
                    // XPath query
                    case "W":
                        string query = string.Format("count({0})", tag);
                        XPathNavigator nav = doc.CreateNavigator();
                        XPathExpression expr = nav.Compile(query);
                        rows = (int)(double)nav.Evaluate(expr);
                        break;
                    default:
                        rows = 0;
                        break;
                }
            }

            return rows;
        }

        public int WriteReport(int nResultCode, string sMessage, string ParamList = "")
        {
            if (reporter != null)
            {
                return reporter.Write(Config, Action, nResultCode,
                    ParamList + "<FileName>" + CurrentFile + "</FileName>" + "<Message>" + reporter.Prepare(sMessage) + "</Message>");
            }
            else
                return 0;
        }

        public string CheckSimpleMask(string fileMask)
        {
            return Regex.Replace(fileMask, @"[\(\)\[\]\{\}\\]", ""); // \?
        }

        public string GetValidMask(string fileMask)
        {
            return CheckSimpleMask(fileMask
                .Replace(".", "[.]")
                .Replace("*", ".*")
                .Replace("?", "."));
        }

        public Regex GetValidRegexMask(string fileMask)
        {
            return new Regex('^' + GetValidMask(fileMask) + '$', RegexOptions.IgnoreCase);
        }

        public Regex GetRemask(string mode, string mask)
        {
            Regex remask;
            if (mode.StartsWith("S"))
                remask = GetValidRegexMask(mask);
            else
                remask = new Regex(mask, RegexOptions.IgnoreCase);
            return remask;
        }

        public byte[] GetSha1(byte[] Data)
        {
            SHA1 sha = new SHA1CryptoServiceProvider();
            return sha.ComputeHash(Data);
        }

        public long GetSize(string fileName)
        {
            return new FileInfo(fileName).Length;
        }

        public string GetStructuredFileInfo(string fileName)
        {
            FileInfo info = new FileInfo(fileName);
            return string.Format("{0} {1}",
                info.Length > 1024000 ? 
                    (Math.Round(info.Length/1024000.0, 2)).ToString() + "M" : (
                        info.Length > 1024 ? (Math.Round(info.Length/1024.0, 2)).ToString() + "K" : 
                            info.Length.ToString()),
                info.CreationTime.ToString("yyyyMMddTHHmm")
                );
        }

        protected string _decode(string output)
        {
            return output;
            /*
            string decodedText = "";
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(output);

            foreach (System.Text.EncodingInfo encodingInfo in System.Text.Encoding.GetEncodings())
            {
                System.Text.Encoding encoding = encodingInfo.GetEncoding();
                decodedText = encoding.GetString(bytes);
                System.Console.Out.WriteLine("Encoding: {0}, Decoded Bytes: {1}", encoding.EncodingName, decodedText);
            }

            return decodedText;
            */
        }

        public int RunProcess(string module, string options, string work = "", int timeout = 0)
        {
            int RC = 0;
            string stdout = "";
            string stderr = "";

            ProcessStartInfo info;
            Process process;

            info = new ProcessStartInfo();
            info.FileName = module;
            info.Arguments = options;
            info.WindowStyle = ProcessWindowStyle.Hidden;
            info.UseShellExecute = false;
            info.WorkingDirectory = work;

            try
            {
                process = new Process();
                process.StartInfo = info;
                process.Start();
                process.WaitForExit();
                RC = process.ExitCode;
            }
            catch (Exception ex)
            {
                Message = "RunProcess: " + ex.Message + ":";
                return -1;
            }

            if (RC != 0)
            {
                this.Message = stderr.Length > 0 ? stderr : (stdout.Length > 0 ? stdout :
                    string.Format("Ошибка исполнения [{0} {1}].", module, options));
            }
            else if (timeout > 0)
            {
                this.Sleep(timeout);
            }

            return RC;
        }

        public virtual string Rename(string sFileName, string sTargetMask)
        {
            string sName, sExt, sNameMask, sNamePref, sNamePost, sExtMask;

            sName = sFileName.Substring(0, sFileName.LastIndexOf("."));
            sExt = sFileName.Substring(sFileName.LastIndexOf(".") + 1);
            sNameMask = sTargetMask.Substring(0, sTargetMask.LastIndexOf("."));
            sExtMask = sTargetMask.Substring(sTargetMask.LastIndexOf(".") + 1);

            if (sNameMask.IndexOf("*") >= 0)
            {
                sNamePref = sNameMask.Substring(0, sNameMask.IndexOf("*"));
                sNamePost = sNameMask.Substring(sNameMask.IndexOf("*") + 1);
            }
            else
            {
                sNamePref = "";
                sNamePost = "";
            }
            sName = sNamePref + sName + sNamePost;
            if (sExtMask != "*")
                sExt = sExtMask;

            return sName + "." + sExt;
        }

        public int RenameFile(FileInfo file, string destination)
        {
            return CopyFile(file, destination, true);
        }

        public int CopyFile(FileInfo file, string destination, bool is_rename = false)
        {
            int i, RC = 0;
            byte[] fileBytes, SHA1, SHA2;

            BaseByteClass Byt = new BaseByteClass();

            string output = is_rename == true ? destination : Path.Combine(destination, file.Name);
            //string output = is_rename == true ? destination : destination +"\\x1";

            fileBytes = File.ReadAllBytes(file.FullName);
            SHA1 = GetSha1(fileBytes);

            for (i = 0, RC = 0; i < Repeat; ++i)
            {
                File.WriteAllBytes(output, fileBytes);
                fileBytes = File.ReadAllBytes(output);
                SHA2 = GetSha1(fileBytes);

                if (Byt.CompareBytes(SHA1, SHA2) == 0)
                {
                    RC = 0;
                    break;
                }
                RC = 100;
            }

            return RC;
        }

        public int CopyFolder(DirectoryInfo folder, string destination)
        {
            int RC = 0;

            if (!Directory.Exists(destination))
            {
                Directory.CreateDirectory(destination);
            }

            foreach (FileInfo file in folder.GetFiles())
            {
                if (RC != 0) 
                    break;
                
                RC = this.CopyFile(file, destination);
            }

            DirectoryInfo[] directories = folder.GetDirectories();
            foreach (DirectoryInfo subfolder in folder.GetDirectories())
            {
                if (RC != 0)
                    break;

                string output = Path.Combine(destination, subfolder.Name);
                RC = CopyFolder(subfolder, output);
            }

            return RC;
        }

        public int DeleteFile(FileInfo file)
        {
            int RC = 0;

            File.Delete(file.FullName);
            if (File.Exists(file.FullName))
                RC = 100;
            
            return RC;
        }

        public int DeleteFolder(DirectoryInfo folder)
        {
            int RC = 0;

            Directory.Delete(folder.FullName);
            if (Directory.Exists(folder.FullName))
                RC = 100;

            return RC;
        }

        public void SetResponses(ref Dictionary<string, string[]> responses)
        {
            this.responses = responses;
        }

        public bool IsResponsesValid(string Responses)
        {
            bool code = true;
            string[] ids = Responses.Split(':');

            if (ids.Length == 0)
                return code;

            foreach (string key in ids)
            {
                string id = key;
                bool isMandatory = true;
                if (key.StartsWith("{") && key.EndsWith("}"))
                {
                    id = key.Substring(1, key.Length - 2);
                    isMandatory = false;
                }
                if (responses.ContainsKey(id) && responses[id].Length == 0 && isMandatory)
                {
                    code = false;
                    break;
                }
            }
            return code;
        }

        public void SetOrderPackageName(string name, bool is_check = false)
        {
            // ----------------------
            // Set Order Package Name
            // ----------------------
            Program.PackageName = is_check ? "CHECK" : name.Split('.')[0];
        }

        public virtual void Register(string Code, int ResultCode = 0, string Message = "")
        {
            if (reporter == null || !validCodes.Contains(Code))
                return;

            reporter.SetAttr("Action", this.Action);
            reporter.SetAttr("SourceFile", this.CurrentFile);
            reporter.SetAttr("DestinationFile", this.DestinationFile);
            reporter.SetAttr("Code", Code);
            reporter.SetAttr("StepInfo", this.StepInfo);
            reporter.SetAttr("Location", this.InputDir);

            reporter.Register("order");

            if (Code == "ST")
            {
                this.Started = DateTime.Now;
                return;
            }
            else
            {
                this.Finished = DateTime.Now;
            }

            reporter.SetDateTime("Started", this.Started);
            reporter.SetDateTime("Finished", this.Finished);

            reporter.Register("event", ResultCode, Message);
        }

        public virtual bool Run(CallParams args)
        {
            args.Items = new string[0];
            performed.Clear();

            switch (args.Action)
            {
                default:
                    args.RC = -1;
                    break;
            }

            if (performed.Count > 0)
                args.Items = performed.ToArray();
            else if (args.RC == 0)
                args.RC = -3;

            return true;
        }
    }
}
