using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

using Common;
using Logger;
using BaseByte;
using BaseFTPAsync;

namespace OutReceiver
{
    class JobFTPClass : BaseFileClass
    {
        private string
            FTP = "",
            FTPAddress = "",
            Login = "",
            Password = "",
            Response = "";

        private bool
            isKeepAlive = false,
            isEnableSSL = true;

        private int
            Timeout = 30000,
            BufferSize = 0;

        public override void SetAttr(string attr, string value)
        {
            base.SetAttr(attr, value);

            if (attr.Length == 0)
                return;
            else if (attr == "FTP")
            {
                this.FTP = value;
                string[] items = value.Split(SVS, StringSplitOptions.None);

                /**********************
                 * 1 : FTPAddress
                 * 2 : Login
                 * 3 : Password
                 * 4 : Enable SSL
                 **********************/

                this.FTPAddress = items[0];
                this.Login = items.Length > 1 ? items[1] : "";
                this.Password = items.Length > 2 ? items[2] : "";
                this.isKeepAlive = items.Length > 3 ?
                    (CONST_IS_FALSE.IndexOf(items[3].ToLower()) > -1 ? false : true) :
                    this.isKeepAlive;
                this.isEnableSSL = items.Length > 4 ?
                    (CONST_IS_FALSE.IndexOf(items[4].ToLower()) > -1 ? false : true) : 
                    this.isEnableSSL;
            }
            else if (attr == "FTPAddress")
                this.FTPAddress = value;
            else if (attr == "Login")
                this.Login = value;
            else if (attr == "Password")
                this.Password = value;
            else if (attr == "Response")
                this.Response = value;
            else if (attr == "Timeout")
            {
                int x;
                int.TryParse(value, out x);
                this.Timeout = x;
            }
            else if (attr == "BufferSize")
            {
                int x;
                int.TryParse(value, out x);
                this.BufferSize = x;
            }
        }

        public override string GetAttr(string attr)
        {
            if (attr.Length == 0)
                return "";
            else if (attr == "FTP")
                return this.FTP;
            else if (attr == "FTPAddress")
                return this.FTPAddress;
            else if (attr == "Login")
                return this.Login;
            else if (attr == "Password")
                return this.Password;
            else if (attr == "Response")
                return this.Response;
            else if (attr == "Timeout")
                return this.Timeout.ToString();
            else if (attr == "BufferSize")
                return this.BufferSize.ToString();

            return base.GetAttr(attr);
        }

        public List<string> FileList;

        private string[] DirectoryListing = { };

        private bool is_simple = false;

        BaseFTPClass ftpClient = null;

        public JobFTPClass(ReporterClass Reporter) : base(Reporter)
        {
            return;
        }

        public override void InitState()
        {
            base.InitState();
            this.Mode = "Simple";
        }

        public override void ValidateState(string action)
        {
            if (this.Mode.Length == 0)
                this.Mode = "S";
            if (this.Mode == "S" && this.Mask.Length == 0)
                this.Mask = "*.*";
        }

        protected string getFileName(string item_list, bool is_simple = false)
        {
            string[] items = item_list.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (is_simple)
                return items[0];
            string fn = "";
            if (items.Length < 9)
                return "";
            for (int i = 5; i < items.Length; i++)
            {
                fn += items[i] + " ";
            }
            fn = fn.Trim();

            if (fn.Length < 14)
                return "";
            return fn.Substring(13);
        }

        private int DoJob(string mode)
        {
            int i, RC = 0;
            string msg = "";
            string s;

            Message = "";

            if (Response.Length > 0 && !IsResponsesValid(Response))
            {
                Message = "Шаг не подтвержден!";
                return -100;
            }

            this.CurrentFile = string.Format("CHECK[{0}]:{1}", Config, InputDir);
            this.DestinationFile = "";
            this.SetOrderPackageName("", true);

            if (!Program.isNoRegfch) this.Register("ST");

            FileList = new List<string>();
            try
            {
                ftpClient = new BaseFTPClass(FTPAddress, Login, Password);
                ftpClient.InitState(this.isKeepAlive, this.isEnableSSL);
                ftpClient.SetTimeout(this.Timeout);
                ftpClient.SetBufferSize(this.BufferSize);
                ftpClient.ActivateTrace(this.isTrace);
            }
            catch (Exception ex)
            {
                RC = -200;
                Message = "JobFTPClass.DoJob: " + ex.Message;
            }

            if (RC == 0)
            {
                Regex remask = this.GetRemask(Mode, Mask);

                switch (mode)
                {
                    case "deleteafterupload":
                    case "upload":
                    case "put":
                        DirectoryInfo dir = new DirectoryInfo(InputDir);
                        foreach (FileInfo file in dir.GetFiles(Mask))
                        {
                            s = file.FullName.Substring(file.FullName.LastIndexOf("\\") + 1);
                            if (!remask.IsMatch(s))
                                continue;
                            FileList.Add(s);
                        }
                        break;
                    case "deleteafterdownload":
                    case "download":
                    case "get":
                        if (!this.is_simple)
                            RC = ftpClient.directoryListDetailed(InputDir, out this.DirectoryListing);
                        else
                            RC = ftpClient.directoryListSimple(InputDir, out this.DirectoryListing);
                        if (RC != 0)
                        {
                            Message = ftpClient.Message;
                            break;
                        }
                        for (i = 0; i < this.DirectoryListing.Count(); i++)
                        {
                            if (this.DirectoryListing[i].Length < 10)
                                continue;
                            if (this.DirectoryListing[i].Substring(0, 1) == "d")
                                continue;
                            s = getFileName(this.DirectoryListing[i], this.is_simple).Trim();
                            if (!remask.IsMatch(s))
                                continue;
                            FileList.Add(s);
                        }
                        break;
                }

                if (!Program.isNoRegfch) this.Register("OK");
            }
            else
            {
                this.Register("ER", RC, Message);
                goto _JobFinish;
            }

            string source = "", destination = "";
            string info = "";
            long size = 0;

            foreach (string sFileName in FileList)
            {
                this.CurrentFile = sFileName;
                this.DestinationFile = OutputDir;
                this.SetOrderPackageName(sFileName);

                this.Register("ST");

                ftpClient.WaitCompleted();

                switch (mode)
                {
                    case "upload":
                    case "put":
                        source = Path.Combine(InputDir, sFileName);
                        destination = OutputDir + "/" + sFileName;
                        size = this.GetSize(source);
                        RC = ftpClient.upload(source, destination);
                        if (RC != 0)
                        {
                            Message = ftpClient.Message;
                        }
                        else
                        {
                            info = this.GetStructuredFileInfo(source);
                        }
                        this.SetOrderPackageName(sFileName);
                        break;
                    case "download":
                    case "get":
                        source = InputDir + "/" + sFileName;
                        destination = Path.Combine(OutputDir, sFileName);
                        size = ftpClient.getFileSize(source);
                        RC = ftpClient.download(source, destination);
                        if (RC != 0)
                        {
                            Message = ftpClient.Message;
                        }
                        else
                        {
                            info = this.GetStructuredFileInfo(destination);
                        }
                        this.SetOrderPackageName(sFileName);
                        break;
                    case "deleteafterupload":
                        source = Path.Combine(InputDir, sFileName);
                        break;
                    case "deleteafterdownload":
                        source = InputDir + "/" + sFileName;
                        break;
                }

                if (RC != 0)
                    goto _IterFinish;

                ftpClient.WaitCompleted();

                switch (mode)
                {
                    case "deleteafterupload":
                    case "upload":
                        if (size > 0 && destination.Length > 0 && ftpClient.getFileSize(destination) != size)
                        {
                            RC = 200;
                            break;
                        }
                        for (i = 0, RC = 0; i < Repeat; ++i)
                        {
                            File.Delete(source);
                            if (!File.Exists(source))
                            {
                                RC = 0;
                                break;
                            }
                            else
                                RC = 100;
                        }
                        break;
                    case "deleteafterdownload":
                    case "download":
                        if (size > 0 && destination.Length > 0 && this.GetSize(destination) != size)
                        {
                            RC = 200;
                            break;
                        }
                        for (i = 0, RC = 0; i < Repeat; ++i)
                        {
                            RC = ftpClient.delete(source);
                            if (RC != 0)
                            {
                                Message = ftpClient.Message;
                                break;
                            }
                            if (ftpClient.isFileExist(source))
                            {
                                RC = 100;
                            }
                            else
                                break;
                        }
                        break;
                }

                _IterFinish:

                if (RC == 100)
                    Message = sFileName + " Не удалось удалить исходный файл!";
                if (RC == 200)
                    Message = sFileName + " Размер файла не совпадает!";

                if (RC == 0)
                {
                    if ("put:upload".IndexOf(mode) > -1)
                        msg = " выгружен на FTP";
                    else if ("get:download".IndexOf(mode) > -1)
                        msg = " загружен с FTP";
                    else if (mode == "deleteafterupload")
                        msg = " удален с файл-сервера";
                    else if (mode == "deleteafterdownload")
                        msg = " удален с FTP";

                    this.WriteReport(0, string.Format("{0}: {1}", sFileName, msg), ParamList);
                    string item = string.Format("{0}:{1} {2}", this.CurrentFile, sFileName, info);
                    performed.Add(item);

                    this.Register("OK");
                }
                else
                {
                    this.Register("ER", RC, Message);
                    break;
                }
            }

            _JobFinish:

            ftpClient = null;
            return RC;
        }

        public override bool Run(CallParams args)
        {
            args.Items = new string[0];
            performed.Clear();

            this.DirectoryListing = new string[]{};
            this.is_simple = false;

            args.Message = string.Format(
                "\nURI: {0}[{1}:{2}], InputDir: {3}, OutputDir: {4}, Mask: {5}\n",
                this.FTPAddress,
                this.Login,
                this.Password,
                this.InputDir,
                this.OutputDir,
                this.Mask
            );

            switch (args.Action)
            {
                case "DoJobPut":
                    args.RC = this.DoJob("put");
                    break;
                case "DoJobGet":
                    args.RC = this.DoJob("get");
                    break;
                case "DoJobUpload":
                    args.RC = this.DoJob("upload");
                    break;
                case "DoJobDownload":
                    args.RC = this.DoJob("download");
                    break;
                case "DoJobDeleteAfterUpload":
                    args.RC = this.DoJob("deleteafterupload");
                    break;
                case "DoJobDeleteAfterDownload":
                    args.RC = this.DoJob("deleteafterdownload");
                    break;
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
