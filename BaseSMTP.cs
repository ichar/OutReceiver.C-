using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Net.Mail;
using System.Threading;
using System.ComponentModel;

namespace BaseSMTP
{
    class BaseSMTPClass
    {
        /*  https://msdn.microsoft.com/ru-ru/library/x5x13z6h(v=vs.110).aspx
         *  https://msdn.microsoft.com/ru-ru/library/system.net.mail.smtpclient(v=vs.110).aspx
         *  http://stackoverflow.com/questions/5700115/how-to-send-email-to-gmail-using-smtpclient-in-c
         */
        public string Message = "";

        const int DELAY_LIMIT = 60;

        private string[] ERS = { "||" };
        private int timeout = 1000;
        private string host = null;
        private int port = 25;
        private string login = "";
        private string password = "";

        private SmtpClient smtp = null;
        private Stack mailStack = new Stack();

        private int mails;

        public BaseSMTPClass(string host, int port, string login = "", string password = "") 
        {
            Message = "";

            this.host = host;
            this.port = port;
            this.login = login;
            this.password = password;
        }

        public virtual void InitState()
        {
            smtp = new SmtpClient(this.host, this.port);
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtp.SendCompleted += new SendCompletedEventHandler(SendCompletedCallback);
            if (this.login.Length > 0)
            {
                smtp.Credentials = new System.Net.NetworkCredential(this.login, this.password);
                smtp.UseDefaultCredentials = false;
            }
            else
            {
                smtp.UseDefaultCredentials = true;
            }
            smtp.EnableSsl = true;

            this.mails = 0;
        }

        public int GetState()
        {
            return this.mails;
        }

        public void Release()
        {
            smtp.Dispose();
        }

        public string[] GetErrorMessages()
        {
            return Message.Split(ERS, StringSplitOptions.None);
        }

        private void SendCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            if (this.mails <= 0)
                return;

            String token = (string)e.UserState;
            MailMessage mail = (MailMessage) mailStack.Pop();

            if (e.Error != null)
            {
                if (Message.Length > 0)
                    Message += ERS[0];
                Message += string.Format("[{0}] {1}", token, e.Error.ToString());
            }

            else if (mail != null)
            {
                mail.Dispose();
                mail = null;
            }

            --this.mails;
        }

        public void WaitCompleted()
        {
            int n = 0;

            while (GetState() > 0 && n < DELAY_LIMIT)
            {
                System.Threading.Thread.Sleep(this.timeout);
                ++n;
            }
        }

        public int Send(string sFrom, string sFromName, string sTo, string sSubject, string sBody)
        {
            WaitCompleted();

            int RC = 0;
            string[] sp = { ";" };
            string[] addrsTo = sTo.Split(sp, StringSplitOptions.RemoveEmptyEntries);

            Message = "";

            MailMessage mail = new MailMessage();

            mail.From = new MailAddress(sFrom, sFromName);
            foreach (string addr in addrsTo)
            {
                if (addr.Length > 0) mail.To.Add(addr);
            }

            //mail.SubjectEncoding = Encoding.Default;
            mail.SubjectEncoding = System.Text.Encoding.UTF8;
            mail.Subject = sSubject;
            mail.BodyEncoding = System.Text.Encoding.UTF8;
            mail.Body = sBody;

            //System.Net.Mail.Attachment attachment;
            //attachment = new System.Net.Mail.Attachment(AttFile);
            //mail.Attachments.Add(attachment);

            mailStack.Push(mail);

            try
            {
                smtp.SendAsync(mail, "msg" + this.mails.ToString());
                ++this.mails;
            }
            catch (Exception ex)
            {
                Message = string.Format("Сообщение не отправлено {0}:{1} {2}", this.host, this.port.ToString(), ex.Message);
                RC = -1;
            }

            //Console.WriteLine(mail.Subject);

            return RC;
        }
    }
}
