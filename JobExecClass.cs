using System;
using System.Text;

using Common;

namespace OutReceiver
{
    class JobExecClass : BaseFileClass
    {
        private string 
            Command = "",
            Module = "",
            Options = "",
            Work = "";

        private int
            Timeout = 0,
            SuccessCode = 0;

        public override void SetAttr(string attr, string value)
        {
            base.SetAttr(attr, value);

            if (attr.Length == 0)
                return;
            else if (attr == "Command")
            {
                this.Command = value;
                string[] items = value.Split(SVS, StringSplitOptions.None);

                /**********************
                 * 0 : Module
                 * 1 : Options
                 * 2 : Work
                 * 3 : Timeout
                 * 4 : SuccessCode
                 **********************/

                this.Module = items[0].Length > 0 ? items[0] : "";
                this.Options = items.Length > 1 ? items[1] : "";
                this.Work = items.Length > 2 ? items[2] : "";
                if (items.Length > 3)
                {
                    int.TryParse(items[3], out this.Timeout);
                }
                if (items.Length > 4)
                {
                    int.TryParse(items[4], out this.SuccessCode);
                }
            }
            if (attr == "Module")
                this.Module = value;
            else if (attr == "Options")
                this.Options = value;
            else if (attr == "Work")
                this.Work = value;
            else if (attr == "Timeout")
            {
                int x;
                int.TryParse(value, out x);
                this.Timeout = x;
            }
            else if (attr == "SuccessCode")
            {
                int x;
                int.TryParse(value, out x);
                this.SuccessCode = x;
            }
        }

        public override string GetAttr(string attr)
        {
            if (attr == "Module")
                return this.Module;
            else if (attr == "Options")
                return this.Options;
            else if (attr == "Work")
                return this.Work;
            else if (attr == "Timeout")
                return this.Timeout.ToString();
            else if (attr == "SuccessCode")
                return this.SuccessCode.ToString();

            return base.GetAttr(attr);
        }

        public JobExecClass(ReporterClass Reporter) : base(Reporter)
        {
            return;
        }

        public override void InitState()
        {
            base.InitState();
            this.Work = ".\\";
            this.Timeout = 0;
        }

        private int DoJob()
        {
            int RC = 0;

            Message = "";

            if (this.Module.Length == 0)
                return RC;

            this.CurrentFile = string.Format("{0} {1} {2} {3}", this.Module, this.Options, this.Work, this.Timeout);
            this.Register("ST");

            RC = this.RunProcess(this.Module, this.Options, this.Work, this.Timeout);

            if (RC == 0 || RC == this.SuccessCode)
            {
                Message = string.Format("Модуль {0} выполнен", this.CurrentFile);
                this.WriteReport(0, Message, ParamList);
                performed.Add(Message);

                RC = 0;

                this.Register("OK");
            }
            else
            {
                Message = string.Format("Модуль завершен с кодом ошибки {0}!", RC.ToString());
                this.Register("ER", RC, Message);
            }

            return RC;
        }

        public override bool Run(CallParams args)
        {
            args.Items = new string[0];
            performed.Clear();

            this.CurrentFile = "";

            switch (args.Action)
            {
                case "DoJobExec":
                    args.RC = this.DoJob();
                    break;
                default:
                    args.RC = -1;
                    break;
            }

            if (performed.Count > 0)
                args.Items = performed.ToArray();
            return true;
        }
    }
}
