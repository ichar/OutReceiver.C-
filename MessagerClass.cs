using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using BaseSMTP;

namespace OutReceiver
{
    class MessagerClass
    {
        public string 
            Message = "";
        
        private string
            Address = "",
            Name = "",
            Caption = "",
            Recipients = "",
            Subject = "";

        private BaseSMTPClass smtpClient = null;
        private string default_subject = "I/O RECEIVER ERROR!";
        private string default_caption = "Ошибка при выполнении операции инфообмена:";

        const string EOL = "\n";
        const string TAB = "\t";

        public MessagerClass(ref BaseSMTPClass smtp)
        {
            this.smtpClient = smtp;
        }

        public void InitState(string alert, string emergency)
        {
            string[] s;
            
            s = alert.Split('|');

            this.Subject = s.Length > 0 && s[0].Length > 0 ? s[0] : default_subject;
            this.Caption = s.Length > 1 && s[1].Length > 0 ? s[1] : default_caption;

            s = emergency.Split('|');

            this.Recipients = s.Length > 0 && s[0].Length > 0 ? s[0] : "";
            this.Address = s.Length > 1 && s[1].Length > 0 ? s[1] : "";
            this.Name = s.Length > 2 && s[2].Length > 0 ? s[2] : "";
        }

        protected void ValidateBodyContent(ref string body)
        {
            body = Regex.Replace(body, @"\\n", EOL);
            body = Regex.Replace(body, @"\\t", TAB);
        }

        public int SendWarning(string msg)
        {
            return 0;
        }

        public int SendError(string msg, string config, string step, int code, string file)
        {
            int RC = 0;
            string subject, body;
            string newline = EOL;

            if (msg.Length == 0 || code == 0)
                return RC;

            Message = "";

            subject = this.Subject;
            body = string.Format(
                "{0}:" + newline +
                "- Config  \t: {1}" + newline +
                "- StepName\t: {2}" + newline +
                "- File  \t\t: {3}" + newline +
                "- Code    \t: {4}" + EOL + EOL,
                this.Caption, config, step, file, code.ToString());

            ValidateBodyContent(ref body);

            body += msg.Trim() + EOL;

            RC = smtpClient.Send(this.Address, this.Name, Recipients, subject, body);
            if (RC != 0)
            {
                Message = smtpClient.Message;
            }

            return RC;
        }
    }
}
