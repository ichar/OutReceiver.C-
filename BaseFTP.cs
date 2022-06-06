using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
//using System.Threading;
using System.IO;

namespace BaseFTP
{
    /* Simple C# FTP Class By metastruct, 27 Mar 2013
    //Create Object Instance
    ftp ftpClient = new ftp(@"ftp://10.10.10.10/", "login", "password");

    // Upload a File 
    ftpClient.upload("etc/test.txt", @"C:\Users\metastruct\Desktop\test.txt");

    // Download a File 
    ftpClient.download("etc/test.txt", @"C:\Users\metastruct\Desktop\test.txt");

    // Delete a File 
    ftpClient.delete("etc/test.txt");

    // Rename a File 
    ftpClient.rename("etc/test.txt", "test2.txt");

    // Create a New Directory 
    ftpClient.createDirectory("etc/test");

    // Get the Date/Time a File was Created 
    string fileDateTime = ftpClient.getFileCreatedDateTime("etc/test.txt");
    Console.WriteLine(fileDateTime);

    // Get the Size of a File 
    string fileSize = ftpClient.getFileSize("etc/test.txt");
    Console.WriteLine(fileSize);

    // Get Contents of a Directory (Names Only) 
    string[] simpleDirectoryListing = ftpClient.directoryListDetailed("/etc");
    for (int i = 0; i < simpleDirectoryListing.Count(); i++) { Console.WriteLine(simpleDirectoryListing[i]); }

    // Get Contents of a Directory with Detailed File/Directory Info 
    string[] detailDirectoryListing = ftpClient.directoryListDetailed("/etc");
    for (int i = 0; i < detailDirectoryListing.Count(); i++) { Console.WriteLine(detailDirectoryListing[i]); }

    // Release Resources 
    ftpClient = null;
    */
    class BaseFTPClass
    {
        public string Message = "";

        private string host = null;
        private string login = null;
        private string password = null;
        private string proxy = null;
        private bool keep_alive;

        /* Current service state:
         * 0 - idle
         * 1 - processed
         */
        private int currentState = 0;
        private int bufferSize = 2048;
        private int timeout = -1;

        /* Construct Object */
        public BaseFTPClass(string hostIP, string userName, string password, string proxy = null) 
        {
            this.host = hostIP;
            this.login = userName;
            this.password = password;
            this.proxy = proxy;
            this.keep_alive = false;
        }

        protected static bool ValidateServerCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
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

        public int GetState()
        {
            return this.currentState;
        }

        public void Release()
        {
            return;
        }

        public void WaitCompleted(int timeout = 1000)
        {
            while (this.GetState() > 0)
            {
                System.Threading.Thread.Sleep(timeout);
            }
        }

        protected int runService(string method, string sourceFile, string destinationFile, out string info)
        {
            int RC = 0;

            this.currentState = 1;

            this.Message = "";

            string s = "";
            if ("upload:".IndexOf(method) > -1)
                s = destinationFile;
            else
                s = sourceFile;
            s = s.Trim().Replace("\\", "/");
            if (s.Substring(0, 1) != "/")
                s = "/" + s;
            string address = host + s;

            if ("directorylistsimple:directorylistdetailed".IndexOf(method) > -1)
                address = address.TrimEnd('/') + "/";

            info = "";
            bool isLocalStreamActive = "download:upload".IndexOf(method) > -1 ? true : false;

            FtpWebRequest ftpRequest = null;
            FtpWebResponse ftpResponse = null;
            StreamReader ftpReader = null;
            Stream ftpStream = null;

            try
            {
                ftpRequest = (FtpWebRequest)FtpWebRequest.Create(new Uri(address));
                ftpRequest.Credentials = new NetworkCredential(login, password);
                ftpRequest.UseBinary = true;
                ftpRequest.UsePassive = true;
                ftpRequest.KeepAlive = this.keep_alive;
                ftpRequest.Timeout = this.timeout;
                ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(ValidateServerCertificate);
                ftpRequest.EnableSsl = true;
                if (this.proxy == null) ftpRequest.Proxy = null;

                FileStream localFileStream = isLocalStreamActive ? (
                        method == "download" ? 
                        new FileStream(destinationFile, FileMode.Create) :
                        new FileStream(sourceFile, FileMode.Open)
                    ) : null;

                byte[] byteBuffer = isLocalStreamActive ? new byte[bufferSize] : null;
                int bytes = 0;

                switch (method)
                {
                    case "download":
                        ftpRequest.Method = WebRequestMethods.Ftp.DownloadFile;
                        ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();
                        ftpStream = ftpResponse.GetResponseStream();

                        do
                        {
                            bytes = ftpStream.Read(byteBuffer, 0, bufferSize);
                            localFileStream.Write(byteBuffer, 0, bytes);
                        }
                        while (bytes > 0);

                        ftpStream.Close();
                        break;
                    case "upload":
                        ftpRequest.Method = WebRequestMethods.Ftp.UploadFile;
                        ftpStream = ftpRequest.GetRequestStream();

                        do
                        {
                            bytes = localFileStream.Read(byteBuffer, 0, bufferSize);
                            ftpStream.Write(byteBuffer, 0, bytes);
                        }
                        while (bytes > 0);

                        ftpStream.Close();
                        break;
                    case "delete":
                        ftpRequest.Method = WebRequestMethods.Ftp.DeleteFile;
                        ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();
                        break;
                    case "rename":
                        ftpRequest.Method = WebRequestMethods.Ftp.Rename;
                        ftpRequest.RenameTo = destinationFile;
                        ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();
                        break;
                    case "createdirectory":
                        ftpRequest.Method = WebRequestMethods.Ftp.MakeDirectory;
                        ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();
                        break;
                    case "getfiledatetime":
                        ftpRequest.Method = WebRequestMethods.Ftp.GetDateTimestamp;
                        ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();
                        ftpStream = ftpResponse.GetResponseStream();

                        ftpReader = new StreamReader(ftpStream);
                        info = ftpReader.ReadToEnd();

                        ftpReader.Close();
                        ftpStream.Close();
                        break;
                    case "getfilesize":
                        ftpRequest.Method = WebRequestMethods.Ftp.GetFileSize;
                        try
                        {
                            ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();
                            info = ftpResponse.ContentLength.ToString();
                        }
                        catch (Exception)
                        {
                            info = "0";
                        }
                        /*
                        ftpStream = ftpResponse.GetResponseStream();
                        
                        ftpReader = new StreamReader(ftpStream);
                        while (ftpReader.Peek() != -1)
                        {
                            info = ftpReader.ReadToEnd();
                        }

                        ftpReader.Close();
                        ftpStream.Close();
                        */
                        break;
                    case "directorylistsimple":
                        ftpRequest.Method = WebRequestMethods.Ftp.ListDirectory;
                        ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();
                        ftpStream = ftpResponse.GetResponseStream();
                        
                        ftpReader = new StreamReader(ftpStream);
                        while (ftpReader.Peek() != -1) 
                        { 
                            info += ftpReader.ReadLine() + "|"; 
                        }

                        ftpReader.Close();
                        ftpStream.Close();
                        break;
                    case "directorylistdetailed":
                        ftpRequest.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                        ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();
                        ftpStream = ftpResponse.GetResponseStream();
                        
                        ftpReader = new StreamReader(ftpStream);
                        while (ftpReader.Peek() != -1) 
                        { 
                            info += ftpReader.ReadLine() + "|"; 
                        }
                        
                        ftpReader.Close();
                        ftpStream.Close();
                        break;
                }

                if (isLocalStreamActive && localFileStream != null)
                    localFileStream.Close();
                if (ftpResponse != null)
                    ftpResponse.Close();
                //ftpRequest = null;
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
