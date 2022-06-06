using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Common;

namespace OutReceiver
{
    class JobCheckMapClass : BaseFileClass
    {
        private string 
            SourceDir = "", 
            MapDir = "", 
            Login = "", 
            Password = "";

        public override void SetAttr(string attr, string value)
        {
            if (attr == "SourceDir")
                this.SourceDir = value;
            else if (attr == "MapDir")
                this.MapDir = value;
            else if (attr == "Login")
                this.Login = value;
            else if (attr == "Password")
                this.Password = value;
        }

        public override string GetAttr(string attr)
        {
            if (attr == "SourceDir")
                return this.SourceDir;
            else if (attr == "MapDir")
                return this.MapDir;
            else if (attr == "Login")
                return this.Login;
            else if (attr == "Password")
                return this.Password;
            return "";
        }

        public JobCheckMapClass(ReporterClass Reporter) : base(Reporter)
        {
            return;
        }

        protected int UnMap()
        {
            int RC = 0;

            try
            {
                RC = this.RunProcess("NET", string.Format("USE {0} /DELETE /YES", MapDir), ".\\");

                if (RC == 2)
                {
                    Message = "JobCheckMapClass.UnMap: Диск не был подключен.";
                    RC = 0;
                }
                else if (RC != 0)
                {
                    Message = "JobCheckMapClass.UnMap: Ошибка отключения!";
                }
            }
            catch (Exception ex)
            {
                Message = "JobCheckMapClass.UnMap: " + ex.Message;
                RC = -1;
            }

            return RC;
        }

        protected int Ping()
        {
            int RC = 0;

            try
            {
                RC = this.RunProcess("PING", string.Format("{0} -n 5", 
                    SourceDir.Substring(0, SourceDir.IndexOf("\\", 3)).Replace("\\\\", "")), ".\\");

                if (RC != 0)
                {
                    Message = "JobCheckMapClass.Ping: PING не проходит!";
                }
            }
            catch (Exception ex)
            {
                Message = "JobCheckMapClass.Ping: " + ex.Message;
                RC = -1;
            }

            return RC;
        }

        protected int Map()
        {
            int RC = 0;

            try
            {
                RC = this.RunProcess("NET", string.Format("USE {0} {1} {2} /USER:{3}",
                    MapDir, SourceDir, Password, Login), ".\\");

                if (RC != 0)
                {
                    Message = "JobCheckMapClass.Map: Ошибка подключения!";
                }
            }
            catch (Exception ex)
            {
                Message = "JobCheckMapClass.Map: " + ex.Message;
                RC = -1;
            }

            return RC;
        }

        private int DoJob()
        {
            int RC = 0;

            Message = "";

            if (MapDir.Length == 0)
                return RC;

            this.CurrentFile = "";
            this.Register("ST");

            if (RC == 0)
                RC = UnMap();
            if (RC == 0)
                RC = Ping();
            if (RC == 0)
                RC = Map();

            if (RC == 0)
            {
                Message = MapDir + " мапирован (" + SourceDir + ")";
                this.WriteReport(0, Message, ParamList);
                performed.Add(Message);

                this.Register("OK");
            }
            else
            {
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
                case "DoJobCheckMap":
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
