using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Common;
using BaseSMTP;

namespace OutReceiver
{
    class JobSMTPClass : BaseFileClass
    {
        private string
            Address = "",
            AddrFrom = "", 
            FromName = "",
            AddrTo = "",
            Subject = "",
            Body = "",
            Attachment = "",
            Response = "";

        private string
            Input = "",
            Output = "";

        private BaseSMTPClass smtpClient = null;

        public override void SetAttr(string attr, string value)
        {
            if (attr.Length == 0)
                return;
            else if (attr == "Address")
            {
                this.Address = value;
                string[] items = value.Split(SVS, StringSplitOptions.None);

                /**********************
                 * 1 : AddrFrom
                 * 2 : FromName
                 * 3 : AddrTo
                 **********************/

                this.AddrFrom = items[0];
                this.FromName = items.Length > 1 ? items[1] : "";
                this.AddrTo = items.Length > 2 ? items[2] : "";
            }
            else if (attr == "From")
                this.AddrFrom = value;
            else if (attr == "FromName")
                this.FromName = value;
            else if (attr == "To")
                this.AddrTo = value;
            else if (attr == "Subject")
                this.Subject = value;
            else if (attr == "Body")
                this.Body = value;
            else if (attr == "Attachment")
                this.Attachment = value;
            else if (attr == "Response")
                this.Response = value;
            else if (attr == "Input")
                this.Input = value;
            else if (attr == "Output")
                this.Output = value;
        }

        public override string GetAttr(string attr)
        {
            if (attr.Length == 0)
                return "";
            else if (attr == "Address")
                return this.Address;
            else if (attr == "From")
                return this.AddrFrom;
            else if (attr == "FromName")
                return this.FromName;
            else if (attr == "To")
                return this.AddrTo;
            else if (attr == "Subject")
                return this.Subject;
            else if (attr == "Body")
                return this.Body;
            else if (attr == "Attachment")
                return this.Attachment;
            else if (attr == "Response")
                return this.Response;
            else if (attr == "Input")
                return this.Input;
            else if (attr == "Output")
                return this.Output;
            else
                return "";
        }

        public JobSMTPClass(ref BaseSMTPClass smtp, ReporterClass Reporter) : base(Reporter)
        {
            this.smtpClient = smtp;
        }

        protected string MakeInnerTag(string tag)
        {
            string s = "";
            string newline = EOL + TAB;
            string[] names;

            switch (tag)
            {
                case "responses":
                    foreach (string id in Response.Split(':'))
                    {
                        if (responses.ContainsKey(id))
                            s += newline + String.Join(newline, responses[id]);
                    }
                    break;
                case "output":
                    foreach (string id in Output.Split(':'))
                    {
                        if (responses.ContainsKey(id))
                        {
                            foreach (string response in responses[id])
                            {
                                names = response.Split(':');
                                if (names.Length < 2)
                                    continue;
                                s += newline + names[1];
                            }
                        }
                    }
                    break;
                case "input":
                    foreach (string id in Input.Split(':'))
                    {
                        if (responses.ContainsKey(id))
                        {
                            foreach (string response in responses[id])
                            {
                                names = response.Split(':');
                                s += newline + names[0];
                            }
                        }
                    }
                    break;
            }

            return s;
        }

        protected void ValidateBodyContent(ref string body)
        {
            body = Regex.Replace(body, @"%newline%|%nl%|%n%|\\n", EOL);
        }

        private int DoJob()
        {
            int RC = 0;
            string subject, body;

            Message = "";

            if (Response.Length == 0)
                return -3;
            else if (!IsResponsesValid(Response))
            {
                Message = "Шаг не подтвержден!";
                return -100;
            }

            subject = Subject;
            body = string.Format(@"{0}", Body
                .Replace("%responses%", MakeInnerTag("responses"))
                .Replace("%output%", MakeInnerTag("output"))
                .Replace("%input%", MakeInnerTag("input")));

            ValidateBodyContent(ref body);

            RC = smtpClient.Send(AddrFrom, FromName, AddrTo, subject, body);
            if (RC != 0)
            {
                Message = smtpClient.Message;
            }

            return RC;
        }

        public override bool Run(CallParams args)
        {
            bool code = false;

            args.Items = new string[0];
            performed.Clear();

            if (this.smtpClient == null)
            {
                args.RC = -200;
                return code;
            }

            code = true;

            switch (args.Action)
            {
                case "DoJobEmail":
                    args.RC = this.DoJob();
                    break;
                case "TestError":
                    args.RC = -101;
                    Message = "Тест ошибок SMTP!!!";
                    break;
                default:
                    args.RC = -1;
                    break;
            }

            return code;
        }
    }
}
