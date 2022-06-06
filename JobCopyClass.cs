using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

using Common;
using BaseByte;
using CommonFunctions;

namespace OutReceiver
{
    class JobCopyClass : BaseFileClass
    {
        private string
            CountCards = ""; // {S|C|R|X|W:<tag>} : Simple(Default)/XML(tags to count for)

        private const string NOWDT = "yyyyMMddHHmm";

        public override void SetAttr(string attr, string value)
        {
            base.SetAttr(attr, value);

            if (attr.Length == 0)
                return;
            else if (attr == "CountCards")
                this.CountCards = value;
        }

        public override string GetAttr(string attr)
        {
            if (attr.Length == 0)
                return "";
            else if (attr == "CountCards")
                return this.CountCards;

            return base.GetAttr(attr);
        }

        BaseByteClass Byt = new BaseByteClass();

        public JobCopyClass(ReporterClass Reporter) : base(Reporter)
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

        public override string Rename(string sFileName, string sTargetMask = "")
        {
            Message = string.Empty;
            try
            {
                if (Mode.StartsWith("A"))
                {
                    if (Mask.Length > 0 && TargetMask.Length > 0)
                    {
                        string x = Regex.Replace(sFileName, Mask, TargetMask);
                        return x;
                    }
                }
                else
                {
                    if (TargetMask.Length > 0)
                        return base.Rename(sFileName, TargetMask);
                }
            }
            catch (Exception ex)
            {
                Message = "JobCopyClass.Rename: " + ex.Message;
            }
            return "";
        }

        private int DoFolderJob(string mode)
        {
            int RC = 0;
            string sB = "";

            string sFolderName = this.InputDir;

            if (sFolderName.IndexOf('%') > -1)
            {
                sFolderName = sFolderName.ToLower().Replace("%now%", DateTime.Now.ToString(NOWDT));
            }

            Regex remask = this.GetRemask(Mode, Mask);

            if (sFolderName.Length > 0)
            {
                this.CurrentFile = sFolderName;
                this.DestinationFile = sFolderName;
                
                this.Register("ST");

                try
                {
                    sB = " не найден";
                    switch (mode)
                    {
                        case "delete":
                            if (Mask.Length > 0)
                            {
                                System.IO.DirectoryInfo folder = new DirectoryInfo(sFolderName);
                                foreach (DirectoryInfo dir in folder.GetDirectories())
                                {
                                    sFolderName = dir.FullName;

                                    if (!remask.IsMatch(dir.Name))
                                        continue;

                                    Directory.Delete(sFolderName, true);
                                    sB = " удален";

                                    this.CurrentFile = sFolderName;
                                    //break;
                                }
                            }
                            else
                            {
                                if (Directory.Exists(sFolderName))
                                {
                                    Directory.Delete(sFolderName, true);
                                    sB = " удален";
                                }
                            }
                            break;
                        case "create":
                            if (!Directory.Exists(sFolderName))
                            {
                                Directory.CreateDirectory(sFolderName);
                                sB = " создан";
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Message = "JobCopyClass.DoFolderJob: " + ex.Message;
                    RC = -1;
                }

                if (Message.Length > 0)
                    Message = Message.Replace("\r", " ").Replace("\n", " ");

                if (RC == 0)
                {
                    this.WriteReport(0, sFolderName + sB, ParamList);
                    performed.Add(string.Format("{0}", sFolderName));

                    this.Register("OK");
                }
                else
                {
                    this.Register("ER", RC, Message);
                }
            }

            return RC;
        }

        private int DoFileJob(bool Move, bool Delete = false)
        {
            int RC = 0;
            string sB;
            Process p = null;
            UNCAccess unc = null;

            Message = "";
            this.CurrentFile = "";
            if (Repeat <= 0) Repeat = 3;

            if (InputPassword != "")
            {
                p = Process.Start("net.exe", @"use  " + InputDir + " " + InputPassword + " /USER:" + InputLogin);
                p.WaitForExit();
                p = null;
            }
            if (OutputPassword != "")
            {
                p = Process.Start("net.exe", @"use  " + OutputDir + " " + OutputPassword + " /USER:" + OutputLogin);
                p.WaitForExit();
                p = null;
            }
            //File.Copy(@"\\computername\sharename\somefile.txt," @"C:\temp\somefile.txt");

            Regex remask = this.GetRemask(Mode, this.Mask);

            DirectoryInfo dir = new DirectoryInfo(InputDir);
            //foreach (FileInfo file in dir.GetFiles("*.*"))
            foreach (FileSystemInfo file in dir.GetFileSystemInfos("*.*"))
            {
                bool IsDirectory = (file is DirectoryInfo) ? true : false;
                bool IsRename = false;

                if (!remask.IsMatch(file.Name))
                    continue;

                string source = "", destination = OutputDir;
                string sFileName; 
                string info = "";
                int cards = 0;

                sFileName = file.FullName.Substring(file.FullName.LastIndexOf("\\") + 1);

                this.CurrentFile = sFileName;
                this.DestinationFile = Delete ? "" : OutputDir;
                this.SetOrderPackageName(file.Name);

                this.Register("ST");

                try
                {
                    source = file.FullName;
                    info = this.GetStructuredFileInfo(source);

                    if (CountCards.Length > 0)
                    {
                        cards = this.CountOrderCards(source, CountCards);
                        Program.Qty = cards;
                    }

                    if (Delete)
                    {
                        if (IsDirectory)
                        {
                            RC = this.DeleteFolder((DirectoryInfo)file);

                            if (RC == 100)
                                Message = sFileName + " Каталог не удален!";
                        }
                        else
                        {
                            RC = this.DeleteFile((FileInfo)file);

                            if (RC == 100)
                                Message = sFileName + " Файл не удален!";
                        }
                    }
                    else if (TargetMask.Length > 0 && !IsDirectory)
                    {
                        sFileName = this.Rename(sFileName);
                        if (Message.Length > 0)
                        {
                            Message = string.Format("{0} Переименование не выполнено [{1}]-[{2}]! {3}",
                                this.CurrentFile, Mask, TargetMask, Message);

                            RC = 200;
                        }
                        else
                        {
                            destination = Path.Combine(OutputDir, sFileName);
                            IsRename = true;
                        }
                    }

                    if (RC == 0 && !Delete)
                    {
                        if (IsDirectory)
                            RC = this.CopyFolder((DirectoryInfo)file, destination);
                        else if (IsRename)
                            RC = this.RenameFile((FileInfo)file, destination);
                        else
                            RC = this.CopyFile((FileInfo)file, destination);

                        if (RC == 100)
                            Message = sFileName + " Контрольная сумма не совпала!";

                        else if (Move)
                        {
                            if (IsDirectory)
                                RC = this.DeleteFolder((DirectoryInfo)file);
                            else
                                RC = this.DeleteFile((FileInfo)file);

                            if (RC == 100)
                                Message = sFileName + " Не удалось удалить исходный файл!";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Message = "JobCopyClass.DoFileJob: " + ex.Message;
                    RC = -1;
                }

                if (Message.Length > 0)
                    Message = Message.Replace("\r", " ").Replace("\n", " ");

                if (RC == 0)
                {
                    if (TargetMask.Length > 0)
                        sB = " переименован";
                    else if (Move)
                        sB = " перемещен";
                    else if (Delete)
                        sB = " удален";
                    else
                        sB = " скопирован";

                    this.WriteReport(0, sFileName + sB, ParamList);
                    string item = string.Format("{0}:{1} {2}{3}", this.CurrentFile, sFileName, info,
                        (CountCards.Length > 0 ? " = " + cards.ToString() : ""));
                    performed.Add(item);

                    this.Register("OK");
                }
                else
                {
                    this.Register("ER", RC, Message);
                    break;
                }
            }

            if (unc != null)
                unc.NetUseDelete();
            unc = null;

            return RC;
        }

        public override bool Run(CallParams args)
        {
            bool code = true;

            args.Items = new string[0];
            performed.Clear();

            args.Message = string.Format(
                "\nInputDir: {0}, OutputDir: {1}, Mask: {2}\n", 
                this.InputDir, 
                this.OutputDir,
                this.Mask
            );

            switch (args.Action)
            {
                case "DoJobCreateFolder":
                    args.RC = this.DoFolderJob("create");
                    break;
                case "DoJobDeleteFolder":
                    args.RC = this.DoFolderJob("delete");
                    break;
                case "DoJobDelete":
                    args.RC = this.DoFileJob(false, true);
                    break;
                case "DoJobCopy":
                    args.RC = this.DoFileJob(false);
                    break;
                case "DoJobMove":
                case "Rename":
                    args.RC = this.DoFileJob(true);
                    break;
                default:
                    args.RC = -1;
                    break;
            }

            if (performed.Count > 0)
                args.Items = performed.ToArray();
            else if (args.RC == 0)
                args.RC = -3;

            args.Info = this.runInfo;

            return code;
        }
    }
}
