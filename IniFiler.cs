using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;

using Common;

namespace OutReceiver
{
    class IniFiler
    {
        public bool IsOpened;
        public string IniFile, ParamList, Message;
        public Dictionary<string, string> AttrList;
        public Dictionary<string, string> SetList;
        public List<string> ConfigList;
        public List<Step> CurrentJobStepList;

        private const string NOWDT = "yyyyMMddHHmmss";
        private string[] SVS = { "::" };

        XmlDocument xDoc;

        public IniFiler(string sIniFile)
        {
            Message = "";
            IsOpened = false;
            IniFile = sIniFile;
            xDoc = new XmlDocument();
            AttrList = new Dictionary<string, string>();
            ConfigList = new List<string>();
            CurrentJobStepList = new List<Step>();
        }

        public int InitState(string Config)
        {
            int RC;

            SetList = new Dictionary<string, string>();
            
            RC = GetSetList(Config, "/Sets/Set");
            RC = GetSetList(Config);

            if (RC == 0)
                RC = GetStepList(Config);
            return RC;
        }

        public void Reset()
        {
            this.Message = "";
        }

        protected void ParseToggles(ref string Value)
        {
            if (Value.IndexOf('%') == -1)
                return;

            Value = Value.Replace("%now%", DateTime.Now.ToString(NOWDT));

            foreach (string key in SetList.Keys)
            {
                Value = Value.Replace(string.Format("%{0}%", key), SetList[key]);
            }
        }

        protected int GetConfigList()
        {
            string sX;
            XmlNodeList xConfigList;

            Message = "";
            ConfigList = new List<string>();
            sX = "//Config/Type";
            try
            {
                xConfigList = xDoc.SelectNodes(sX);
                foreach (XmlNode xConfig in xConfigList)
                    ConfigList.Add(xConfig.InnerText);
            }
            catch (Exception ex)
            {
                Message = "IniFiler.GetConfigList: " + ex.Message;
                return -1;
            }
            return 0;
        }

        protected int GetAttrList()
        {
            string sX;
            XmlNodeList xAttrList;

            Message = "";
            ConfigList = new List<string>();
            sX = "//Attrs";
            try
            {
                xAttrList = xDoc.SelectNodes(sX);
                foreach (XmlNode xNode in xAttrList)
                {
                    foreach (XmlNode xAttr in xNode.ChildNodes)
                    {
                        AttrList[xAttr.Name] = xAttr.InnerText;
                    }
                }
            }
            catch (Exception ex)
            {
                Message = "IniFiler.GetAttrList: " + ex.Message;
                return -1;
            }
            return 0;
        }

        protected int SortJobSteps()
        {
            int RC;
            string sX;
            
            XmlNodeList xSteps;

            Message = "";
            sX = "//JobSteps";
            try
            {
                xSteps = xDoc.SelectNodes(sX);
                foreach (XmlNode xJobSteps in xSteps)
                {
                    RC = SortNode(xJobSteps);
                    if (RC != 0)
                        return RC;
                }
            }
            catch (Exception ex)
            {
                Message = "IniFiler.SortJobSteps: " + ex.Message;
                return -1;
            }

            return 0;
        }

        protected string GetNodeOrder(XmlNode xNode, int i)
        {
            string s = "";
            
            foreach (XmlNode x in xNode.ChildNodes[i].ChildNodes)
            {
                if (x.Name == "Order")
                {
                    s = x.InnerText;
                    break;
                }
            }
            
            return s;
        }

        protected int SortNode(XmlNode xNode)
        {
            bool changed = true;

            try
            {
                while (changed)
                {
                    changed = false;
                    for (int i = 1; i < xNode.ChildNodes.Count; i++)
                    {
                        string s1 = GetNodeOrder(xNode, i);
                        string s2 = GetNodeOrder(xNode, i-1);
                        
                        if (String.Compare(s1, s2, true) < 0)
                        {
                            xNode.InsertBefore(xNode.ChildNodes[i], xNode.ChildNodes[i - 1]);
                            changed = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Message = "IniFiler.SortNode: " + ex.Message;
                return -1;
            }
           
            return 0;
        }

        protected int GetConfigParam(string Config, string Param, out string Value)
        {
            string sX;
            XmlNode xParVal;

            Value = "";
            Message = "";

            sX = "//Config[Type='" + Config + "']/" + Param;
            xParVal = xDoc.SelectSingleNode(sX);
            if (xParVal == null)
            {
                Message = "IniFiler.GetConfigParam: Не найден параметр " + sX;
                return -1;
            }
            Value = xParVal.InnerText;
            ParseToggles(ref Value);
            return 0;
        }

        public int GetConfigAttr(string Attr, out string Value)
        {
            Value = "";
            if (AttrList.ContainsKey(Attr))
            {
                Value = AttrList[Attr].ToString();
                return 0;
            }
            return -1;
        }

        protected int GetStepList(string Config)
        {
            XmlNodeList xStepList;

            Message = "";
            CurrentJobStepList = new List<Step>();
            xStepList = xDoc.SelectNodes("//Config[Type='" + Config + "']/JobSteps/JobStep");
            try
            {
                foreach (XmlNode xStep in xStepList)
                {
                    Step step = new Step();
                    XmlNode sid = xStep.SelectSingleNode("ID");
                    XmlNode stype = xStep.SelectSingleNode("Action");
                    XmlNode sorder = xStep.SelectSingleNode("Order");
                    if (sid != null)
                        step.ID = sid.InnerText;
                    if (stype != null)
                        step.Action = stype.InnerText;
                    if (sorder != null)
                        step.Order = sorder.InnerText;
                    step.Name = string.Format("{0}:{1}", step.Order, step.ID != "" ? step.ID : step.Action);
                    CurrentJobStepList.Add(step);
                }
            }
            catch (Exception ex)
            {
                Message = "IniFiler.GetConfigList: " + ex.Message;
                return -1;
            }
            return 0;
        }

        protected int GetSetList(string Config, string Set = "/Set")
        {
            string sX;
            XmlNodeList xSetList;

            Message = "";
            sX = "//Config[Type='" + Config + "']" + Set;

            try
            {
                xSetList = xDoc.SelectNodes(sX);
                foreach (XmlNode xSet in xSetList)
                {
                    string[] s = xSet.InnerText.Split(SVS, StringSplitOptions.None);
                    string key = s[0];
                    string Value = s.Length > 1 ? s[1] : "";

                    Match m = Regex.Match(Value, @"\[(.*)\]", RegexOptions.IgnoreCase);
                    if (m.Value.Length > 0 && m.Groups.Count > 0)
                    {
                        Value = m.Groups[1].ToString();
                    }
                    ParseToggles(ref Value);

                    SetList[key] = Value;
                }
            }
            catch (Exception ex)
            {
                Message = "IniFiler.GetSetList: " + ex.Message;
                return -1;
            }
            
            return 0;
        }

        public int GetJobParamList(string Config, Step step)
        {
            string sX;
            XmlNode xParL;

            Message = "";

            sX = "//Config[Type='" + Config + "']/JobSteps/JobStep[Action='" + step.Action + "' and Order='" + step.Order + "']/Params";
            xParL = xDoc.SelectSingleNode(sX);
            if (xParL == null)
            {
                Message = "IniFiler.GetJobParamList: Не найден узел " + sX;
                return -1;
            }
            ParamList = xParL.InnerXml;
            return 0;
        }

        public int GetJobParam(string Config, string Job, string Order, string Param, out string Value)
        {
            string sX;
            XmlNode xParVal;

            Value = "";
            Message = "";

            if (Order.Length > 0)
                sX = "//Config[Type='" + Config + "']/JobSteps/JobStep[Action='" + Job + "' and Order='" + Order + "']/Params/" + Param;
            else
                sX = "//Config[Type='" + Config + "']/JobSteps/JobStep[Action='" + Job + "]/Params/" + Param;
            xParVal = xDoc.SelectSingleNode(sX);
            if (xParVal == null)
            {
                Message = "IniFiler.GetJobParam: Не найден параметр " + sX;
                return -1;
            }
            Value = xParVal.InnerText;
            ParseToggles(ref Value);

            return 0;
        }

        protected void ExtendByPath(XmlNode config, string xpath, string tag)
        {
            foreach (XmlNode extended in config.SelectNodes(xpath))
            {
                string path_to_base = extended.InnerText;

                if (path_to_base.Length == 0 || !File.Exists(path_to_base))
                    continue;

                XmlDocument xBase = new XmlDocument();

                xBase.Load(path_to_base);

                XmlNode node = xBase.SelectSingleNode("//"+ tag);

                if (node.InnerXml.Length > 0 && node.ChildNodes.Count > 0)
                {
                    XmlNode imported = xDoc.ImportNode(node, true);
                    config.ReplaceChild(imported, extended);
                }
            }
        }

        protected int LoadExtends()
        {
            int RC = 0;

            try
            {
                foreach (XmlNode config in xDoc.SelectNodes("//Config"))
                {
                    XmlNode x = config["Type"];

                    if (x == null || x.InnerText.Length == 0)
                        continue;

                    string xpath = "//Config[Type='" + x.InnerText + "']";

                    ExtendByPath(config, xpath + "/ExtendSets", "Common/Sets");
                    ExtendByPath(config, xpath + "/ExtendConfig", "JobSteps");
                }
            }
            catch (Exception ex)
            {
                Message = "IniFiler.LoadExtends: " + ex.Message;
                RC = -1;
            }

            return RC;
        }

        public void Backup()
        {
            xDoc.Save(string.Format("{0}.backup", IniFile));
        }

        public int Open()
        {
            int RC = 0;

            Message = "";
            IsOpened = false;
            xDoc = new XmlDocument();

            if (!File.Exists(IniFile))
            {
                Message = "IniFiler.Open: Не найден файл " + IniFile;
                return -1;
            }
            try
            {
                xDoc.Load(IniFile);
            }
            catch (Exception ex)
            {
                Message = "IniFiler.Open: " + ex.Message;
                return -1;
            }

            RC = LoadExtends();

            if (SortJobSteps() == 0 && GetAttrList() == 0 && GetConfigList() == 0)
                IsOpened = true;
            else
                RC = -1;
            
            return RC;
        }
    }

}
