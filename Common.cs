using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    class Step
    {
        public string ID = "", Name = "", Action = "", Order = "";
    }

    class CallParams
    {
        public string Action;
        public string[] Args;
        /*
        public string Action 
        { 
            get { return this.action; }
        }
        public string[] Args { get; set; }
        */

        public int RC;
        public string[] Items;
        public List<string> Info;
        public string Message;
        /*
        public int RC 
        {
            get { return this.rc; }
            set { this.rc = value; }
        }
        public string[] Items { get; set; }
        */

        public CallParams(string action, string[] args)
        {
            this.Info = new List<string>();
            this.Action = action;
            this.Args = args;
            this.Message = "";
        }
    }
}
