using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace BaseSFTP
{
    class BaseSFTPClass
    {
        /* https://dzone.com/articles/sshnet
         * http://stackoverflow.com/questions/23703040/download-files-from-sftp-with-ssh-net-library
         * http://stackoverflow.com/questions/34039810/ssh-net-async-file-download
         * http://benohead.com/downloading-files-directories-via-sftp-using-ssh-net/
         * http://sshnet.codeplex.com/
         */
        public string Message = "";

        const int DELAY_LIMIT = 60;

        private string[] ERS = { "||" };

        private int buffer_size = 0;
        private int timeout = 1000;

        private string host = null;
        private int port = 22;
        private string login = "";
        private string password = "";

        //private bool keep_alive = false;

        /* Current service state:
         * 0 - idle
         * 1 - processed
         */
        private int currentState = 0;

        private SftpClient sftp = null;

        public BaseSFTPClass(string host, string login = "", string password = "") 
        {
            Message = "";

            string[] x = host.Split(':');
            this.host = x[0];
            if (x.Length > 1)
                int.TryParse(x[1], out this.port);
            this.login = login;
            this.password = password;
        }

        public virtual void InitState()
        {
            this.currentState = 0;
        }

        public void ActivateTrace(bool enabled)
        {
            return;
        }

        public void SetTimeout(int value)
        {
            this.timeout = value;
        }

        public void SetBufferSize(int value)
        {
            if (value > 0) this.buffer_size = value;
        }

        public int GetState()
        {
            return this.currentState;
        }

        public void Release()
        {
            if (this.sftp == null)
                return;

            if (this.GetState() == 1 && this.sftp.IsConnected)
                this.sftp.Disconnect();
            
            this.sftp.Dispose();

            this.currentState = 0;
        }

        public void WaitCompleted(int timeout = 1000)
        {
            while (this.GetState() > 0)
            {
                System.Threading.Thread.Sleep(timeout);
            }
        }

        protected void Connect()
        {
            if (this.sftp == null)
                return;

            if (!this.sftp.IsConnected)
                this.sftp.Connect();

            this.currentState = 1;
        }

        protected int runService(string method, string sourceFile, string destinationFile, out string info)
        {
            int RC = 0;

            this.Message = "";

            string s = "";
            if ("upload:".IndexOf(method) > -1)
                s = destinationFile;
            else
                s = sourceFile;
            s = s.Trim().Replace("\\", "/");
            if (s.Substring(0, 1) != "/")
                s = "/" + s;
            string address = s;

            if ("directorylistsimple:directorylistdetailed".IndexOf(method) > -1)
                address = address.TrimEnd('/') + "/";

            info = "";
            bool isLocalStreamActive = "download:upload".IndexOf(method) > -1 ? true : false;

            this.sftp = new SftpClient(this.host, this.port, this.login, this.password);

            try
            {
                this.Connect();

                if (!this.sftp.IsConnected)
                {
                    Message = string.Format("{0} {1}\n{2}, address[{3}]", method, "Service is unavailable!", "", address);
                    return -1;
                }

                FileStream localFileStream = isLocalStreamActive ? (
                        method == "download" ? 
                        new FileStream(destinationFile, FileMode.Create) :
                        new FileStream(sourceFile, FileMode.Open)
                    ) : null;

                SftpFile file;

                switch (method)
                {
                    case "download":
                        sftp.DownloadFile(address, localFileStream);
                        break;
                    case "upload":
                        sftp.UploadFile(localFileStream, address);
                        break;
                    case "delete":
                        if (sftp.Exists(address))
                            sftp.Delete(address);
                        break;
                    case "rename":
                        sftp.RenameFile(address, destinationFile);
                        break;
                    case "createdirectory":
                        sftp.CreateDirectory(address);
                        break;
                    case "getfiledatetime":
                        info = sftp.GetLastWriteTime(address).ToShortDateString();
                        break;
                    case "getfilesize":
                        file = sftp.Get(address);
                        info = file.Length.ToString();
                        break;
                    case "directorylistsimple":
                    case "directorylistdetailed":
                        foreach (SftpFile x in sftp.ListDirectory(address))
                        {
                            info += string.Format("{0}:{1}|", x.Name, x.Length);
                        }
                        break;
                }

                if (isLocalStreamActive && localFileStream != null)
                    localFileStream.Close();
            }
            catch (Exception ex)
            {
                Message = string.Format("{0} {1}\n{2}, address[{3}]", method, ex.Message, ex.StackTrace, address);
                RC = -1;
            }

            this.currentState = 0;

            return RC;
        }

        /* Download File */
        public int download(string remoteFile, string localFile)
        {
            int RC;
            string info;

            RC = runService("download", remoteFile, localFile, out info);

            return RC;
        }

        /* Upload File */
        public int upload(string localFile, string remoteFile)
        {
            int RC;
            string info;

            RC = runService("upload", localFile, remoteFile, out info);

            return RC;
        }

        /* Delete File */
        public int delete(string deleteFile)
        {
            int RC;
            string info;

            RC = runService("delete", deleteFile, "", out info);

            return RC;
        }

        /* Rename File */
        public int rename(string currentFileNameAndPath, string newFileName)
        {
            int RC;
            string info;

            RC = runService("rename", currentFileNameAndPath, newFileName, out info);

            return RC;
        }

        /* Create a New Directory on the FTP Server */
        public int createDirectory(string newDirectory)
        {
            int RC;
            string info;

            RC = runService("createdirectory", newDirectory, "", out info);

            return RC;
        }

        /* Get the Date/Time a File was Created */
        public int getFileCreatedDateTime(string fileName, out string fileInfo)
        {
            int RC;

            RC = runService("getfiledatetime", fileName, "", out fileInfo);

            return RC;
        }

        /* Get the Size of a File */
        public long getFileSize(string fileName)
        {
            int RC;
            string info;
            long size = 0;

            RC = runService("getfilesize", fileName, "", out info);
            long.TryParse(info, out size);

            return size;
        }

        public bool isFileExist(string fileName)
        {
            return getFileSize(fileName) > 0 ? true : false;
        }

        /* List Directory Contents File/Folder Name Only */
        public int directoryListSimple(string directory, out string[] directoryList)
        {
            int RC;
            string info;

            RC = runService("directorylistsimple", directory, "", out info);
            if (RC == 0)
                directoryList = info.Split("|".ToCharArray());
            else
                directoryList = new string[0];

            return RC;
        }

        /* List Directory Contents in Detail (Name, Size, Created, etc.) */
        public int directoryListDetailed(string directory, out string[] directoryList)
        {
            int RC;
            string info;

            RC = runService("directorylistdetailed", directory, "", out info);
            if (RC == 0)
                directoryList = info.Split("|".ToCharArray());
            else
                directoryList = new string[0];

            return RC;
        }
    }
}
