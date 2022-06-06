using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;

using Common;
using BaseByte;

namespace OutReceiver
{
    class JobUnzipClass : BaseFileClass
    {
        /*
            WinRAR also support command line operations. This is important to allow users to incorporate WinRAR commands into automated scripts. Here is a simple script I used to backup a directory, backup.bat:
            @rem backup.bat
            @rem Usage: backup directory zipfile password

            \local\winrar\winrar a -afzip -r -p%3 %2.zip %1\*.*
            My restore script is also very simple, restore.bat:
            @rem restore.bat
            @rem Usage: restore zipfile password

            \local\winrar\winrar x -p%2 %1.zip .\
            Note that:
            Command "a" is to create archive files.
            Option "-afzip" is to create archive files in ZIP format.
            Option "-r" is to take input files recursively to include sub-directories.
            Option "-p*" is to add password protection to archive files.
            Command "x" is to extract files out of archive files.
            For complete description of the command line interface of WinRAR, see the WinRAR help document.
        */
        private int IsDebug = 0;

        public bool isZip = false;
        public bool isUnvalid = false;

        private int default_timeout = 3000;

        private string
            Password = "",
            FileMode = "Single"/*"Multi"*/,
            Options = "",
            ArchName = "";

        public override void SetAttr(string attr, string value)
        {
            base.SetAttr(attr, value);

            if (attr.Length == 0)
                return;
            else if (attr == "Password")
                this.Password = value;
            else if (attr == "FileMode")
                this.FileMode = value;
            else if (attr == "Options")
                this.Options = value;
            else if (attr == "ArchName")
                this.ArchName = value;
        }

        public override string GetAttr(string attr)
        {
            if (attr.Length == 0)
                return "";
            else if (attr == "Password")
                return this.Password;
            else if (attr == "FileMode")
                return this.FileMode;
            else if (attr == "Options")
                return this.Options;
            else if (attr == "ArchName")
                return this.ArchName;

            return base.GetAttr(attr);
        }

        BaseByteClass Byt = new BaseByteClass();
        string the_rar;
        RegistryKey the_Reg;
        object the_Obj;

        public JobUnzipClass(ReporterClass Reporter) : base(Reporter)
        {
            return;
        }

        public override void InitState()
        {
            base.InitState();
            this.Mode = "Simple";

            try
            {
                the_Reg = Registry.ClassesRoot.OpenSubKey(@"Applications\WinRAR.exe\Shell\Open\Command");
                the_Obj = the_Reg.GetValue("");
                the_rar = the_Obj.ToString();
                the_Reg.Close();
                the_rar = the_rar.Substring(1, the_rar.Length - 7);
            }
            catch (Exception ex)
            {
                if (IsDebug == 1)
                {
                    Message = "JobUnzipClass.DoJob: " + ex.Message;
                    this.isUnvalid = true;
                }
                else
                {
                    // Add WinRAR.exe to the PATH !!!
                    the_rar = "WinRAR.exe";
                }
            }
        }

        protected int Rar(string InFile, string OutArch, string Psw, bool isZip)
        {
            int RC = 0;
            string options,
                sWDir = InFile.Substring(0, InFile.LastIndexOf("\\")),
                sFNam = InFile.Substring(InFile.LastIndexOf("\\") + 1);

            if (this.isUnvalid)
                return -1;

            Message = "";

            options = string.Format(" {0} {1} {2}",
                this.Options.Length > 0 ? this.Options : "A",
                isZip ? "-afzip" : "",
                Psw.Length > 0 ? "-p" + Psw : ""
            ).TrimEnd();

            options += string.Format(" {0}{1} {2}", OutArch, (isZip ? ".zip" : ".rar"), sFNam);

            try
            {
                RC = this.RunProcess(the_rar, options, sWDir, default_timeout);
            }
            catch (Exception ex)
            {
                Message = "JobUnzipClass.UnRar: " + ex.Message + ":" + options;
                return -1;
            }

            return RC;
        }

        protected int UnRar(string File, string OutDir, string Psw)
        {
            int RC = 0;
            string options,
                sWDir = File.Substring(0, File.LastIndexOf("\\")),
                sFNam = File.Substring(File.LastIndexOf("\\") + 1);

            if (this.isUnvalid)
                return -1;

            Message = "";

            options = string.Format("{0} {1}",
                this.Options.Length > 0 ? this.Options : "X -o+",
                Psw.Length > 0 ? "-p" + Psw : ""
            ).TrimEnd();

            options += string.Format(" {0} *.* {1}", sFNam, OutDir);

            try
            {
                RC = this.RunProcess(the_rar, options, sWDir, default_timeout);
            }
            catch (Exception ex)
            {
                Message = "JobUnzipClass.UnRar: " + ex.Message + ":" + options;
                return -1;
            }

            return RC;
        }

        private int DoJob(string mode)
        {
            int i, RC = 0;
            string errmsg, msg;
            bool isSingle = true;
            string sArch, sStamp;

            if (this.isUnvalid)
                return -1;

            Message = "";
            CurrentFile = "";
            if (Repeat <= 0) Repeat = 3;

            switch (mode)
            {
                case "zip":
                    errmsg = "Ошибка упаковки";
                    msg = "упакован";
                    break;
                case "unzip":
                    errmsg = "Ошибка распаковки";
                    msg = "распакован";
                    break;
                default:
                    return 0;
            }

            if (mode == "zip")
            {
                if (FileMode.Trim().Length > 0)
                    if (FileMode.Trim().Substring(0, 1).ToUpper() != "S")
                        isSingle = false;
                sStamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            }
            else
            {
                sStamp = "";
            }

            Regex remask = this.GetRemask(Mode, Mask);

            DirectoryInfo dir = new DirectoryInfo(InputDir);
            foreach (FileInfo file in dir.GetFiles(Mask))
            {
                if (!remask.IsMatch(file.Name))
                    continue;

                string sFileName;

                sFileName = file.FullName.Substring(file.FullName.LastIndexOf("\\") + 1);

                this.CurrentFile = sFileName;
                this.DestinationFile = OutputDir;
                this.SetOrderPackageName(file.Name);

                this.Register("ST");

                try
                {
                    if (mode == "zip")
                    {
                        if (isSingle)
                        {
                            sArch = sFileName.Substring(0, sFileName.LastIndexOf("."));
                        }
                        else
                        {
                            if (ArchName.Length > 0)
                                sArch = ArchName;
                            else
                                sArch = sStamp;
                        }

                        this.DestinationFile = sArch;
                        RC = Rar(file.FullName, OutputDir + "\\" + sArch, Password, isZip);
                    }
                    if (mode == "unzip")
                    {
                        RC = UnRar(file.FullName, OutputDir, Password);
                    }

                    if (RC != 0)
                    {
                        Message = string.Format("{0}: {1}", errmsg, file.FullName);
                    }
                    else
                    {
                        for (i = 0, RC = 0; i < Repeat; ++i)
                        {
                            File.Delete(file.FullName);
                            if (!File.Exists(file.FullName))
                            {
                                RC = 0;
                                break;
                            }
                            RC = 100;
                        }
                        if (RC == 100)
                        {
                            Message = string.Format("{0}: {1}", sFileName, "Не удалось удалить исходный файл!");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Message = "JobUnzipClass.DoJob: " + ex.Message;
                    RC = -1;
                }

                if (Message.Length > 0)
                    Message = Message.Replace("\r", " ").Replace("\n", " ");

                if (RC == 0)
                {
                    this.WriteReport(RC, string.Format("{0} {1}", sFileName, msg), ParamList);
                    performed.Add(string.Format("{0}:{1}", CurrentFile, sFileName));

                    this.Register("OK");
                }
                else
                {
                    this.Register("ER", RC, Message);
                    break;
                }
            }

            return RC;
        }

        public override bool Run(CallParams args)
        {
            isZip = args.Args.Length > 0 && args.Args[0] == "zip" ? true : false;

            args.Items = new string[0];
            performed.Clear();

            args.Message = string.Format(
                "\nInputDir: {0}, OutputDir: {1}, Mask: {2}, Options: {3}\n",
                this.InputDir,
                this.OutputDir,
                this.Mask,
                this.Options
            );

            switch (args.Action)
            {
                case "DoJobZip":
                    args.RC = this.DoJob("zip");
                    break;
                case "DoJobUnzip":
                    args.RC = this.DoJob("unzip");
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
