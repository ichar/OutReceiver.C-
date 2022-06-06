using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

using Common;
using BaseByte;
using RosanCryptoLib;

namespace OutReceiver
{
    class JobRosanPGPClass : BaseFileClass
    {
        /* GPG(для работы нужен криптопровайдер pgp, установленные все используемые сертификаты 
         * в реестре): R:\Programmer\Nasibullin\PGPDesktop
         * 
         * Подписываем своим закрытым ключом (aSignFile)
         * Шифруем открытым ключом клиента (aEncryptFile)
         * Расшифровываем своим закрытым ключом (aDecryptFile)
         * Снимаем подпись открытым ключом клиента (aVerifyFile)
         * 
         * aMessage = 0 - успешно
         * aMessage =-1 - ошибка
         * aMessage > 0 - предупреждение
         * 
         */

        private string
            Certificate = "",
            RosanCert = "",
            Password = "",
            ClientCert = "",
            CryptoModule = "";

        public override void SetAttr(string attr, string value)
        {
            base.SetAttr(attr, value);

            if (attr.Length == 0)
                return;
            else if (attr == "Certificate")
            {
                this.Certificate = value;
                string[] items = value.Split(SVS, StringSplitOptions.None);

                /**********************
                 * 1 : RosanCert
                 * 2 : Password
                 * 3 : ClientCert
                 * 4 : CryptoModule
                 **********************/

                this.RosanCert = items[0];
                this.Password = items.Length > 1 ? items[1] : "";
                this.ClientCert = items.Length > 2 ? items[2] : "";
                this.CryptoModule = items.Length > 3 ? items[3] : "";
            }
            else if (attr == "RosanCert")
                this.RosanCert = value;
            else if (attr == "Password")
                this.Password = value;
            else if (attr == "ClientCert")
                this.ClientCert = value;
            else if (attr == "CryptoModule")
                this.CryptoModule = value;
        }

        public override string GetAttr(string attr)
        {
            if (attr.Length == 0)
                return "";
            else if (attr == "Certificate")
                return this.Certificate;
            else if (attr == "RosanCert")
                return this.RosanCert;
            else if (attr == "Password")
                return this.Password;
            else if (attr == "ClientCert")
                return this.ClientCert;
            else if (attr == "CryptoModule")
                return this.CryptoModule;

            return base.GetAttr(attr);
        }

        BaseByteClass Byt = new BaseByteClass();
        GPGCryptoLib Cry = null;

        public JobRosanPGPClass(ReporterClass Reporter) : base(Reporter)
        {
            return;
        }

        public override void InitState()
        {
            base.InitState();
            this.Mode = "Simple";
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
                        string x = Regex.Replace(sFileName, Mask, TargetMask, RegexOptions.IgnoreCase);
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
                Message = "JobRosanPGPClass.Rename: " + ex.Message;
            }
            return "";
        }

        private int DoJob(string mode)
        {
            int RC = 0;
            string errmsg, msg;
            string[] ext = new string[] { "", "" };

            Message = "";
            CurrentFile = "";
            if (Repeat <= 0) Repeat = 3;

            switch (mode)
            {
                case "sign":
                    ext[0] = "";
                    ext[1] = ".gpg";
                    errmsg = "Ошибка подписи";
                    msg = "подписан";
                    break;
                case "encrypt":
                    ext[0] = "";
                    ext[1] = ".gpg";
                    errmsg = "Ошибка кодирования";
                    msg = "закодирован";
                    break;
                case "decrypt":
                    ext[0] = ".gpg";
                    ext[1] = "";
                    errmsg = "Ошибка декодирования";
                    msg = "раскодирован";
                    break;
                case "verify":
                    ext[0] = ".gpg";
                    ext[1] = "";
                    errmsg = "Ошибка верификации";
                    msg = "проверена подпись";
                    break;
                default:
                    return 0;
            }

            Regex remask = this.GetRemask(Mode, Mask);

            DirectoryInfo dir = new DirectoryInfo(InputDir);
            foreach (FileInfo file in dir.GetFiles("*.*"))
            {
                if (!remask.IsMatch(file.Name))
                    continue;

                string sFileName = file.FullName.Substring(file.FullName.LastIndexOf("\\") + 1);

                this.CurrentFile = sFileName;

                if (ext[0].Length > 0)
                    sFileName = sFileName.Replace(ext[0], "");
                if (TargetMask.Length > 0)
                    sFileName = this.Rename(sFileName, TargetMask);
                else
                    sFileName = sFileName + ext[1];

                string source = file.FullName;
                string destination = OutputDir + "\\" + sFileName;

                this.DestinationFile = destination;
                this.SetOrderPackageName(sFileName);

                this.Register("ST");

                try
                {
                    if (this.CryptoModule.Length > 0)
                    {
                        string options = string.Format("--batch {0} \"{1}\"{2} -o {3} {4}",
                                mode == "sign" ? "-s -u" : (
                                mode == "encrypt" ? "-e -r" : (
                                mode == "decrypt" ? "-d -u" : (
                                mode == "verify" ? "-d -r" : (
                                "")))),
                            this.ClientCert,
                            this.Password.Length > 0 ? " --passphrase " + this.Password : "",
                            source, destination
                        );

                        RC = this.RunProcess(this.CryptoModule, options);
                    }
                    else
                    {
                        Cry = new GPGCryptoLib();

                        if (Cry != null)
                        {
                            if (mode == "")
                                RC = -12;
                            else if (mode == "sign")
                                RC = Cry.aSignFile(source, destination, this.ClientCert, this.Password);
                            else if (mode == "encrypt")
                                RC = Cry.aEncryptFile(source, destination, this.ClientCert, this.Password);
                            else if (mode == "decrypt")
                                RC = Cry.aDecryptFile(source, destination, this.ClientCert, this.Password);
                            else if (mode == "verify")
                                RC = Cry.aVerifyFile(source, destination, this.ClientCert, this.Password);

                            if (RC < 0)
                            {
                                Message = string.Format("{0}: {1}", errmsg, Cry.aMessage);
                            }
                        }
                        else
                        {
                            Message = string.Format("{0}! SDK PGP не активен.");
                            RC = -2;
                        }
                    }

                    if (RC == 0)
                    {
                        if (!File.Exists(destination))
                        {
                            Message = string.Format("{0}. Файл {1} не создан!", errmsg, destination);
                            RC = -10;
                        }
                        else
                            File.Delete(source);
                        if (File.Exists(source))
                        {
                            Message = string.Format("{0}. Файл {1} не удален!", errmsg, source);
                            RC = -11;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Message = ex.Message;
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
            args.Items = new string[0];
            performed.Clear();

            args.Message = string.Format(
                "\nCertificates: {0}[{1}:{2}], InputDir: {3}, OutputDir: {4}, Mask: {5}\n",
                this.ClientCert,
                this.RosanCert,
                this.Password,
                this.InputDir,
                this.OutputDir,
                this.Mask
            );

            switch (args.Action)
            {
                case "DoJobSign":
                    args.RC = this.DoJob("sign");
                    break;
                case "DoJobEncrypt":
                    args.RC = this.DoJob("encrypt");
                    break;
                case "DoJobDecrypt":
                    args.RC = this.DoJob("decrypt");
                    break;
                case "DoJobVerify":
                    args.RC = this.DoJob("verify");
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
