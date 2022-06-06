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
    class JobRosanCryptoProClass : BaseFileClass
    {
        /* КриптоПРО(для работы нужен криптопровайдер КриптоПро и установленный собственный 
         * сертификат с закрытым ключом в реестре, в хранилище ‘личные’(по умолчанию), 
         * сертификаты клиентов файлы с расширением *.cer):
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
         * Open(*Название нашего ключа в реестре*) - Отыскивает указанный сертификат в личном хранилище (в реестре)
         * с помощью которого подписывает и расшифровывает файлы
         */

        private string
            Certificate = "",
            RosanCert = "",
            Password = "",
            ClientCert = "",
            CryptoModule = "";

        private bool
            IsRemoveSignatureOnly = true;

        public override void SetAttr(string attr, string value)
        {
            base.SetAttr(attr, value);

            if (attr.Length == 0)
                return;
            else if (attr == "Certificate")
            {
                this.Certificate = value;
                string[] items = value.Split(SVS, StringSplitOptions.None);

                /***************************
                 * 1 : RosanCert
                 * 2 : Password
                 * 3 : ClientCert
                 * 4 : CryptoModule
                 * 5 : IsRemoveSignatureOnly
                 ***************************/

                this.RosanCert = items[0];
                this.Password = items.Length > 1 ? items[1] : "";
                this.ClientCert = items.Length > 2 ? items[2] : "";
                this.CryptoModule = items.Length > 3 ? items[3] : "";
                this.IsRemoveSignatureOnly = items.Length > 4 ? 
                    (CONST_IS_FALSE.IndexOf(items[4].ToLower()) > -1 ? false : true) : 
                    this.IsRemoveSignatureOnly;
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
        CryptoProLib Cry = null;

        public JobRosanCryptoProClass(ReporterClass Reporter) : base(Reporter)
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
                Message = "JobRosanCryptoProClass.Rename: " + ex.Message;
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
                    ext[1] = ".p7s";
                    errmsg = "Ошибка подписи";
                    msg = "подписан";
                    break;
                case "encrypt":
                    ext[0] = "";
                    ext[1] = ".p7m";
                    errmsg = "Ошибка кодирования";
                    msg = "закодирован";
                    break;
                case "decrypt":
                    ext[0] = ".p7m";
                    ext[1] = "";
                    errmsg = "Ошибка декодирования";
                    msg = "раскодирован";
                    break;
                case "verify":
                    ext[0] = ".p7s";
                    ext[1] = "";
                    errmsg = "Ошибка верификации";
                    msg = "проверена подпись";
                    break;
                case "explore":
                    ext[0] = "";
                    ext[1] = "";
                    errmsg = "Ошибка доступа к данным";
                    msg = "получена информация о сертификатах";
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
                        string options = string.Format("{0} -dn \"{1}\" {2} {3}",
                                mode == "sign" ? "-sign -nochain" : (
                                mode == "encrypt" ? " -encr -nochain" : (
                                mode == "decrypt" ? "-decr -norev" : (
                                mode == "verify" ? "-verify -norev -verall" : (
                                "")))),
                            this.RosanCert, 
                            source, destination
                        );

                        RC = this.RunProcess(this.CryptoModule, options);
                    }
                    else
                    {
                        Cry = new CryptoProLib();

                        if (Cry.Open(this.RosanCert))
                        {
                            if (mode == "")
                                RC = -12;
                            else if (mode == "sign")
                                RC = Cry.aSignFile(source, destination);
                            else if (mode == "encrypt")
                                RC = Cry.aEncryptFile(source, destination, this.ClientCert);
                            else if (mode == "decrypt")
                                RC = Cry.aDecryptFile(source, destination);
                            else if (mode == "verify")
                                RC = Cry.aVerifyFile(source, destination, this.ClientCert, this.IsRemoveSignatureOnly);
                            else if (mode == "explore")
                                RC = Cry.aExploreFile(source, out this.runInfo);

                            if (RC < 0)
                            {
                                Message = string.Format("{0}. {1}", errmsg, Cry.aMessage);
                            }
                        }
                        else
                        {
                            Message = string.Format("{0}! SDK CryptoPro не активен. {1}", errmsg, Cry.aMessage);
                            RC = -2;
                        }
                    }

                    if (RC == 0 && mode != "explore")
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

                if (mode == "explore" && this.runInfo.Count() > 0)
                {
                    this.reporter.Register("certificate", RC, Message, String.Join(EOL, this.runInfo));
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
                case "DoJobCryptoProExplore":
                    args.RC = this.DoJob("explore");
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

            return true;
        }
    }
}
