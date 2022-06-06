using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace BaseFTPAsync
{
    /* https://msdn.microsoft.com/en-us/library/system.net.httpwebrequest.begingetresponse.aspx
     * https://stuff.seans.com/2009/01/05/using-httpwebrequest-for-asynchronous-downloads/
     * https://msdn.microsoft.com/ru-ru/library/system.threading.tasks.task(v=vs.110).aspx
     * https://msdn.microsoft.com/ru-ru/library/system.net.ftpstatuscode(v=vs.110).aspx
     * https://msdn.microsoft.com/ru-ru/library/system.net.ftpwebrequest(v=vs.110).aspx
     * https://msdn.microsoft.com/ru-ru/library/2e08f6yc(v=vs.110).aspx
     * http://metanit.com/sharp/tutorial/4.8.php
     */

    abstract class WebRequestState
    {
        public int bytesCount;             // Number of bytes read during current transfer
        public long totalBytes;            // Total bytes to read
        public string infoContent;         // Content of stream answer
        public Stream streamResponse;      // Stream to read from
        public byte[] buffer;              // Buffer to read data into
        public Uri fileURI;                // Uri of object being downloaded
        public string FTPMethod;           // What was the previous FTP command?  (e.g. get file size vs. download)

        private int packetNumber;

        private WebRequest _request;
        public virtual WebRequest request
        {
            get { return null; }
            set { _request = value; }
        }

        private WebResponse _response;
        public virtual WebResponse response
        {
            get { return null; }
            set { _response = value; }
        }

        private Exception _operationException = null;
        public virtual Exception OperationException
        {
            get { return _operationException; }
            set { _operationException = value; }
        }

        public WebRequestState(int buffSize)
        {
            bytesCount = 0;
            buffer = new byte[buffSize];
            streamResponse = null;
        }

        public void InitPacket()
        {
            packetNumber = 0;
        }

        public void EncPacket()
        {
            ++packetNumber;
        }

        public string GetPacketStr()
        {
            return packetNumber.ToString();
        }
    }

    class HttpWebRequestState : WebRequestState
    {
        private HttpWebRequest _request;
        public override WebRequest request
        {
            get
            {
                return _request;
            }
            set
            {
                _request = (HttpWebRequest)value;
            }
        }

        private HttpWebResponse _response;
        public override WebResponse response
        {
            get
            {
                return _response;
            }
            set
            {
                _response = (HttpWebResponse)value;
            }
        }

        public HttpWebRequestState(int buffSize) : base(buffSize) { }
    }

    class FtpWebRequestState : WebRequestState
    {
        private FtpWebRequest _request;
        public override WebRequest request
        {
            get
            {
                return _request;
            }
            set
            {
                _request = (FtpWebRequest)value;
            }
        }

        private FtpWebResponse _response;
        public override WebResponse response
        {
            get
            {
                return _response;
            }
            set
            {
                _response = (FtpWebResponse)value;
            }
        }

        public FtpWebRequestState(int buffSize) : base(buffSize) { }
    }

    class RequestedAction
    {
        private string _action;
        private string _remote_item;
        private string _local_item;

        public string Action { get { return _action; } set { _action = value; } }
        public string RemoteItem { get { return _remote_item; } set { _remote_item = value; } }
        public string LocalItem { get { return _local_item; } set { _local_item = value; } }

        private string _info;
        public string Info { get { return _info; } }

        private bool _is_new_request;
        public bool IsNewRequest { get { return _is_new_request; } }

        public RequestedAction(
            string action, string remote_item = null, string local_item = null, bool is_new_request = true)
        {
            this.Action = action;
            this.RemoteItem = remote_item;
            this.LocalItem = local_item;

            this._is_new_request = is_new_request;
            this._info = "";
        }
    }

    class BaseFTPClass
    {
        public bool IsTrace = false;

        public string Message = "";

        private string address = null;
        private string host = null;
        private string login = null;
        private string password = null;
        private string proxy = null;

        private bool is_keep_alive = false;
        private bool is_enable_ssl = true;

        /* Current service state:
         *  0 - idle
         *  1 - processed (started)
         *  2 - done (should be closed)
         *  --- Errors: ---
         * -1 - Operational timeout expired. Action was cancelled!
         * -2 - Invalid buffer Read/Write bytes count!
         * -3 - Invalid buffer Total bytes count!
         * -4 - Response StatusCode (with error)
         * -5 - No response available!
         */
        private int currentState = 0;

        const int DEFAULT_BUFFER_SIZE = 2048 * 100;
        const int DefaultTimeout = 60 * 1000; // 1 minutes timeout

        private int buffer_size = 0;
        private int timeout = -1;

        public static ManualResetEvent allDone;
        public RegisteredWaitHandle waitHandle;

        private Stack<RequestedAction> action_stack = null;
        private FtpWebRequestState state = null;
        private WebRequest request = null;
        private FtpWebResponse response = null;
        private Stream responseStream = null;
        private Stream requestStream = null;
        private FileStream localFileStream = null;
        private Uri uri = null;

        private RequestedAction current_action = null;
        private bool isLocalStreamActive = false;

        public BaseFTPClass(string host, string user, string password, string proxy = null) 
        {
            this.host = host;
            this.login = user;
            this.password = password;
            this.proxy = proxy;
        }

        protected static bool ValidateServerCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public virtual void InitState(bool keep_alive = false, bool enable_ssl = true)
        {
            this.currentState = 0;
            this.is_keep_alive = keep_alive;
            this.is_enable_ssl = enable_ssl;
            this.buffer_size = DEFAULT_BUFFER_SIZE;
        }

        public void SetTimeout(int value)
        {
            this.timeout = value;
        }

        public void SetBufferSize(int value)
        {
            if (value > 0) this.buffer_size = value;
        }

        public void ActivateTrace(bool enabled)
        {
            IsTrace = enabled;
        }

        public void WaitCompleted(int timeout = 1000, int limit = 60)
        {
            int n = 0;
            while (this.GetState() > 0)
            {
                if (n > limit)
                    break;
                System.Threading.Thread.Sleep(timeout);
                ++n;
            }
        }

        protected void ReleaseState()
        {
            this.request = null;
        }

        protected int GetState()
        {
            return this.currentState;
        }

        protected void TraceEvent(string msg, string type = "trace", bool force = false)
        {
            if (force)
                OutReceiver.Program.LogEvent(type, ">>> " + msg);
            else if (IsTrace)
                Console.WriteLine(">>> " + msg);
        }

        protected bool IsStateActive()
        {
            return this.currentState > 0 && this.state.response != null ? true : false;
        }

        protected bool IsStateShouldBeClosed()
        {
            return this.currentState == 2 ? true : false;
        }

        // ----------------------
        //  FTP ASYNC DISPATCHER
        // ----------------------

        private void Init(string source)
        {
            string s = source.Trim().Replace("\\", "/");
            if (s.Substring(0, 1) != "/")
                s = "/" + s;
            this.address = this.host + s;

            this.action_stack = new Stack<RequestedAction>();
            this.state = new FtpWebRequestState(this.buffer_size);

            this.isLocalStreamActive = true;

            if (this.is_enable_ssl)
            {
                ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(
                    ValidateServerCertificate);
            }

            waitHandle = null;
        }

        private int Dispatcher(out string info)
        {
            int RC = 0;

            info = "";

            while (RC == 0 && this.action_stack.Count > 0)
            {
                this.current_action = this.action_stack.Pop();

                if (this.current_action.IsNewRequest)
                {
                    if ("directorylistsimple:directorylistdetailed".IndexOf(this.current_action.Action) > -1)
                        this.address = this.address.TrimEnd('/') + "/";

                    this.uri = new Uri(this.address);

                    this.request = (FtpWebRequest)FtpWebRequest.Create(this.uri);
                    this.request.Credentials = new NetworkCredential(this.login, this.password);

                    ((FtpWebRequest)this.request).KeepAlive = this.is_keep_alive;
                    ((FtpWebRequest)this.request).EnableSsl = this.is_enable_ssl;
                    if (this.proxy == null) ((FtpWebRequest)this.request).Proxy = null;
                }

                #region Actions
                switch (this.current_action.Action)
                {
                    case "directorylistdetailed":
                        this.state.FTPMethod = WebRequestMethods.Ftp.ListDirectoryDetails;
                        break;
                    case "directorylistsimple":
                        this.state.FTPMethod = WebRequestMethods.Ftp.ListDirectory;
                        break;
                    case "getfilesize":
                        this.state.FTPMethod = WebRequestMethods.Ftp.GetFileSize;
                        break;
                    case "download":
                        this.state.FTPMethod = WebRequestMethods.Ftp.DownloadFile;
                        ((FtpWebRequest)this.request).UseBinary = true;
                        break;
                    case "read":
                        break;
                    case "upload":
                        this.state.FTPMethod = WebRequestMethods.Ftp.UploadFile;
                        ((FtpWebRequest)this.request).UseBinary = true;
                        break;
                    case "commitUpload":
                        //this.state.FTPMethod = null;
                        break;
                    case "delete":
                        this.state.FTPMethod = WebRequestMethods.Ftp.DeleteFile;
                        break;
                }
                #endregion

                if (this.current_action.IsNewRequest)
                {
                    if (this.state.FTPMethod != null)
                        this.request.Method = this.state.FTPMethod;

                    this.state.request = this.request;
                    this.state.fileURI = this.uri;
                }

                this.currentState = 1;

                RC = ResponseAsyncHandler();

                #region CheckCurrentState&Errors
                if (this.currentState == -1)
                {
                    Message = "Operational timeout expired. Action was cancelled!";
                    RC = -301;
                    break;
                }
                if (this.currentState == -2)
                {
                    Message = "Invalid buffer Read/Write bytes count!";
                    RC = -302;
                    break;
                }
                if (this.currentState == -3)
                {
                    Message = "Invalid buffer Total bytes count!";
                    RC = -303;
                    break;
                }
                if (this.currentState == -4)
                {
                    if (this.response != null && this.response.StatusDescription != null)
                        Message = string.Format("Response StatusCode {0}. Description: {1}\nURI: {2}", 
                            this.response.StatusCode,
                            this.response.StatusDescription.Trim(),
                            this.address
                        );
                    else
                        Message = string.Format("No response available!\nURI: {0}", 
                            this.address
                        );
                    RC = -304;
                    break;
                }
                if (this.currentState == -5)
                {
                    Message = string.Format("No response available!\nURI: {0}",
                        this.address
                    );
                    RC = -305;
                    break;
                }
                #endregion

                switch (this.current_action.Action)
                {
                    case "directorylistdetailed":
                    case "directorylistsimple":
                        info = this.state.infoContent;
                        break;
                    case "getfilesize":
                        info = this.state.totalBytes.ToString();
                        break;
                }

                this.currentState = 0;
            }

            ReleaseState();
            
            return RC;
        }

        private int ResponseAsyncHandler()
        {
            int RC = 0;

            TraceEvent(string.Format("Start ResponseAsyncHandler. Action {0}", this.current_action.Action));

            try
            {
                allDone = new ManualResetEvent(false);

                IAsyncResult result = null;

                switch (this.current_action.Action)
                {
                    case "directorylistdetailed":
                    case "directorylistsimple":
                    case "getfilesize":
                    case "delete":
                        this.state.bytesCount = 0;
                        result = (IAsyncResult)this.request.BeginGetResponse(
                            new AsyncCallback(BeginResponseCallback),
                            this.state);
                        break;
                    case "download":
                        if (this.isLocalStreamActive && this.current_action.LocalItem != null)
                            this.localFileStream = new FileStream(this.current_action.LocalItem, FileMode.Create);
                        this.state.bytesCount = 0;
                        this.state.InitPacket();
                        result = (IAsyncResult)this.request.BeginGetResponse(
                            new AsyncCallback(BeginResponseCallback),
                            this.state);
                        break;
                    case "read":
                        this.responseStream = this.state.response.GetResponseStream();
                        result = this.responseStream.BeginRead(
                            this.state.buffer, 
                            0, 
                            this.buffer_size, 
                            new AsyncCallback(ReadCallback), 
                            this.state);
                        break;
                    case "upload":
                        this.localFileStream = File.OpenRead(this.current_action.LocalItem);
                        this.state.bytesCount = 0;
                        result = (IAsyncResult)this.request.BeginGetRequestStream(
                            new AsyncCallback(WriteCallback),
                            this.state);
                        break;
                    case "commitUpload":
                        result = (IAsyncResult)this.request.BeginGetResponse(
                            new AsyncCallback(EndResponseCallback),
                            this.state);
                        break;
                }

                waitHandle = ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, 
                    new WaitOrTimerCallback(TimeoutCallback),
                    this.request,
                    this.timeout > 0 ? this.timeout : DefaultTimeout, 
                    true);

                allDone.WaitOne();

                if (waitHandle != null)
                    waitHandle.Unregister(null);

                if (this.state.OperationException != null)
                {
                    throw this.state.OperationException;
                }
            }
            catch (Exception ex)
            {
                Message = string.Format("BaseFTPAsync.ResponseAsyncHandler: {0}", ex.Message);
                RC = -1;
            }

            TraceEvent(string.Format("Go allDone"));

            if (IsStateActive())
                this.state.response.Close();

            if (IsStateShouldBeClosed())
                Done();

            return RC;
        }

        private void TimeoutCallback(object answer, bool timedOut)
        {
            string cause = "";
            string method = "";
            
            this.request = answer as FtpWebRequest;

            if (this.request != null)
                method = this.request.Method;

            if (timedOut)
            {
                cause = "TIMEDOUT";

                if (this.request != null)
                    this.request.Abort();

                this.currentState = -1;

                allDone.Set();
            }
            else
            {
                cause = "SIGNALED";
            }

            TraceEvent(string.Format("BaseFTPAsync TimeoutCallback: {0} {1} {2} {3} {4}",
                timedOut.ToString(), method, cause, this.currentState, this.current_action.Action),
                "timeout", true);
        }

        private void BeginResponseCallback(IAsyncResult asyncResult)
        {
            TraceEvent(string.Format("BeginResponseCallback started"));

            this.state = ((FtpWebRequestState)(asyncResult.AsyncState));
            this.request = this.state.request;

            try
            {
                this.response = ((FtpWebResponse)(this.request.EndGetResponse(asyncResult)));
                this.state.response = this.response;
            }
            catch (WebException ex)
            {
                this.response = (FtpWebResponse)ex.Response;
                this.state.response = this.response;
            }
            catch (Exception)
            {
                this.state.response = this.response = null;
            }

            if (this.response == null)
            {
                this.currentState = -5;
            }
            else if (IsStateActive() && (
                this.response.StatusCode == FtpStatusCode.CommandOK ||
                this.response.StatusCode == FtpStatusCode.OpeningData ||
                this.response.StatusCode == FtpStatusCode.FileStatus ||
                this.response.StatusCode == FtpStatusCode.FileActionOK
                //this.response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable
                ))
            {
                switch (this.current_action.Action)
                {
                    case "directorylistdetailed":
                    case "directorylistsimple":
                        this.responseStream = this.state.response.GetResponseStream();
                        this.state.streamResponse = this.responseStream;
                        
                        StreamReader streamReader = new StreamReader(this.responseStream);
                        this.state.infoContent = "";
                        while (streamReader.Peek() != -1)
                        {
                            this.state.infoContent += streamReader.ReadLine() + "|"; 
                        }
                        break;
                    case "getfilesize":
                        this.state.totalBytes =
                            this.response.StatusCode == FtpStatusCode.FileStatus ? 
                            this.response.ContentLength :
                            0;
                        break;
                    case "download":
                        this.responseStream = this.state.response.GetResponseStream();
                        this.state.streamResponse = this.responseStream;
                        
                        this.action_stack.Push(new RequestedAction(
                            "read", 
                            this.current_action.RemoteItem, 
                            this.current_action.LocalItem,
                            false
                            ));

                        this.currentState = 0;
                        break;
                    case "commitUpload":
                        break;
                    case "delete":
                        break;
                }
            }
            else
            {
                this.currentState = -4;
            }

            allDone.Set();
        }

        private void ReadCallback(IAsyncResult asyncResult)
        {
            TraceEvent(string.Format("ReadCallback started"));

            this.state = ((FtpWebRequestState)(asyncResult.AsyncState));
            this.responseStream = this.state.streamResponse;

            this.state.buffer.Initialize();

            int bytes = this.responseStream.EndRead(asyncResult);
            if (bytes > 0)
            {
                this.localFileStream.Write(this.state.buffer, 0, bytes);
                this.state.bytesCount += bytes;

                this.state.EncPacket();

                if (this.state.totalBytes > 0 && this.state.bytesCount > this.state.totalBytes)
                {
                    this.currentState = -3;
                }
                else
                {
                    this.action_stack.Push(new RequestedAction(
                        "read",
                        this.current_action.RemoteItem,
                        this.current_action.LocalItem,
                        false
                        ));
                        
                    this.currentState = 0;
                }
            }
            else
            {
                this.currentState = 2; // Close response/request
            }

            TraceEvent(string.Format("ReadCallback: {0} {1}", bytes.ToString(), this.state.GetPacketStr()));

            allDone.Set();
        }

        private void WriteCallback(IAsyncResult asyncResult)
        {
            TraceEvent(string.Format("WriteCallback started"));

            this.state = ((FtpWebRequestState)(asyncResult.AsyncState));
            this.requestStream = this.state.request.EndGetRequestStream(asyncResult);

            this.state.buffer.Initialize();
            this.state.InitPacket();

            int bytes = 0;
            //while ((bytes = this.localFileStream.Read(this.state.buffer, 0, this.buffer_size)) > 0)
            do
            {
                bytes = this.localFileStream.Read(this.state.buffer, 0, this.buffer_size);
                if (bytes > 0)
                {
                    this.requestStream.Write(this.state.buffer, 0, bytes);
                    this.state.bytesCount += bytes;

                    this.state.EncPacket();
                }

                TraceEvent(string.Format("WriteCallback: {0} {1}", bytes.ToString(), this.state.GetPacketStr()));
            }
            while (bytes > 0);

            this.action_stack.Push(new RequestedAction(
                "commitUpload",
                this.current_action.RemoteItem,
                this.current_action.LocalItem,
                false
                ));

            //Done();

            allDone.Set();
        }

        private void EndResponseCallback(IAsyncResult asyncResult)
        {
            TraceEvent(string.Format("EndResponseCallback started"));

            this.state = ((FtpWebRequestState)(asyncResult.AsyncState));
            this.request = this.state.request;

            this.response = ((FtpWebResponse)(this.request.EndGetResponse(asyncResult)));
            this.state.response = this.response;

            this.currentState = 2; // Close response/request

            allDone.Set();
        }

        private void Done()
        {
            if (this.isLocalStreamActive && this.localFileStream != null)
                this.localFileStream.Close();
            if (this.responseStream != null)
                this.responseStream.Close();
            if (this.requestStream != null)
                this.requestStream.Close();
        }

        // ------------------
        //  PUBLIC INTERFACE
        // ------------------

        public int download(string remoteFile, string localFile)
        {
            string info = "";

            Init(remoteFile);

            this.action_stack.Push(new RequestedAction("download", remoteFile, localFile));
            this.action_stack.Push(new RequestedAction("getfilesize", remoteFile));

            int RC = Dispatcher(out info);

            return RC;
        }

        public int upload(string localFile, string remoteFile)
        {
            string info = "";

            Init(remoteFile);

            this.action_stack.Push(new RequestedAction("upload", remoteFile, localFile));

            int RC = Dispatcher(out info);

            return RC;
        }

        public int delete(string remoteFile)
        {
            string info = "";

            Init(remoteFile);

            this.action_stack.Push(new RequestedAction("delete", remoteFile));

            this.isLocalStreamActive = false;

            int RC = Dispatcher(out info);

            return RC;
        }

        public int rename(string remoteFile, string newFileName)
        {
            string info = "";

            Init(remoteFile);

            this.action_stack.Push(new RequestedAction("rename", remoteFile, newFileName));

            this.isLocalStreamActive = false;

            int RC = Dispatcher(out info);

            return RC;
        }

        public int createDirectory(string newDirectory)
        {
            string info = "";

            Init("");

            this.action_stack.Push(new RequestedAction("createdirectory", newDirectory));

            this.isLocalStreamActive = false;

            int RC = Dispatcher(out info);

            return RC;
        }

        public int getFileCreatedDateTime(string remoteFile, out string info)
        {
            Init(remoteFile);

            this.action_stack.Push(new RequestedAction("getfilecreateddatetime", remoteFile));

            this.isLocalStreamActive = false;

            int RC = Dispatcher(out info);

            return RC;
        }

        public long getFileSize(string remoteFile)
        {
            string info = "";
            long size = 0;

            Init(remoteFile);

            this.action_stack.Push(new RequestedAction("getfilesize", remoteFile));

            int RC = Dispatcher(out info);
            
            long.TryParse(info, out size);

            return size;
        }

        public bool isFileExist(string fileName)
        {
            return getFileSize(fileName) > 0 ? true : false;
        }

        public int directoryListSimple(string remotePath, out string[] directoryList)
        {
            string info = "";

            Init(remotePath);

            this.action_stack.Push(new RequestedAction("directorylistsimple", remotePath));

            this.isLocalStreamActive = false;

            int RC = Dispatcher(out info);

            if (RC == 0)
                directoryList = info.Split("|".ToCharArray());
            else
                directoryList = new string[0];

            return RC;
        }

        public int directoryListDetailed(string remotePath, out string[] directoryList)
        {
            string info = "";

            Init(remotePath);

            this.action_stack.Push(new RequestedAction("directorylistdetailed", remotePath));

            this.isLocalStreamActive = false;

            int RC = Dispatcher(out info);

            if (RC == 0)
                directoryList = info.Split("|".ToCharArray());
            else
                directoryList = new string[0];

            return RC;
        }
    }
}
