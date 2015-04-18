using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.IO.Pipes;
using System.Diagnostics;

using QueueCommon;

namespace ProcessWrappers
{
    public class HostWrapper
    {
        #region notes and public fields / constructors

        /// Ideally, to act as a process host, a calling app should provide:
        ///  - the client executable
        ///  - a flag to determine whether it uses console or pipe io
        ///  - a method to call when there's data from the client
        ///  - an endpoint to send data to the client
        ///  - control methods - start, cleanup, checkprogress
        /// Data and processing shouldn't be synchronous
        /// Clients should be able to be killed as needed
        /// 
        /// in the server process:
        ///     HostWrapper myHost = new HostWrapper( myExeLoc, true, myDataSink );
        ///     myHost.Start();
        ///     ...
        ///     while( myHost.CheckProgress() != HostWrapper.IsEnding )
        ///     {
        ///         myHost.WriteToClient( "Do Something" );
        ///         // go on with your life here
        ///         ...
        ///         // once we're done:
        ///         myHost.SendData( "QUIT" );  // marker to client?
        ///     }
        ///     myHost.Cleanup()
        ///     ...
        ///     int myDataSync( HostWrapper thisHost ) {
        ///        Console.WriteLine( "here's some content" );
        ///        if( thisHost.incoming[0].StartsWith("QUIT") )    // ack from client
        ///             return HostWrapper.IsEnding;        // queue that client is ending
        ///        return HostWrapper.IsRunning;
        ///     }

        public enum IOType { PIPES = 0, StdIO = 1, QUEUES = 2 };

        public List<string> outgoing;
        public List<string> incoming;
        public delegate int ProcessControlHandler(string nextDataElt);

        public const int IsEnding = 1;
        public const int IsRunning = 2;

        string logLocation = "C:\\HostWrapper\\logfile.txt";
        bool logging;

        public HostWrapper(string processLoc, IOType thisIOType,
                        string exch, string host, string port, string uid, string pwd,
                        List<string> postToRoutes, List<string> readFromRoutes,
                        ProcessControlHandler datasink)
        {
            qexch = exch;
            qhost = host;
            qport = port;
            quid = uid;
            qpwd = pwd;
            listenRoutes = readFromRoutes;
            postRoutes = postToRoutes;

            logging = false;
            outgoing = new List<string>();
            incoming = new List<string>();
            processLocation = processLoc;
            useIOType = thisIOType;
            thisHandler = datasink;
            InitIOModel();
        }
        public HostWrapper(string processLoc, IOType thisIOType, ProcessControlHandler datasink)
        {
            qexch = qhost = qport = quid = qpwd = "";
            listenRoutes = new List<string>();
            postRoutes = new List<string>();

            logging = false;
            outgoing = new List<string>();
            incoming = new List<string>();
            processLocation = processLoc;
            useIOType = thisIOType;
            thisHandler = datasink;
            InitIOModel();
        }
        public HostWrapper(string processLoc, IOType thisIOType, ProcessControlHandler datasink, bool setLog)
        {
            qexch = qhost = qport = quid = qpwd = "";
            listenRoutes = new List<string>();
            postRoutes = new List<string>();

            logging = setLog;
            outgoing = new List<string>();
            incoming = new List<string>();
            processLocation = processLoc;
            useIOType = thisIOType;
            thisHandler = datasink;
            InitIOModel();
        }
        #endregion

        #region Private fields

        delegate void OutgoingDataHandler();
        delegate void IncomingDataHandler();
        delegate void ProcessCompletedHandler();

        event OutgoingDataHandler OutgoingData;
        event IncomingDataHandler IncomingData;

        IOModel thisIO;

        string qexch;
        string qport;
        string qhost;
        string quid;
        string qpwd;
        List<string> postRoutes;
        List<string> listenRoutes;

        string processLocation = "";
        IOType useIOType;
        ProcessControlHandler thisHandler;

        #endregion

        #region Init

        private void InitIOModel()
        {
            thisIO = IOFactory(useIOType, this);
        }
        public void Start()
        {
            thisIO.InitProcess(processLocation);

            thisIO.InitComms();
            thisIO.StartProcess();
            thisIO.ConnectOutputComms();

            RegisterIncomingEvents();
            RegisterOutgoingEvents();
        }

        public void TestPipeMode()
        {
            // Show that anonymous Pipes do not support Message mode. 
            PipeIOModel myIO = (PipeIOModel)thisIO;
            myIO.TestMe();
        }

        #endregion

        #region events
        private void RegisterOutgoingEvents()
        {
            OutgoingData += WriteToStream;
        }
        private void RegisterIncomingEvents()
        {
            IncomingData += WriteToConsole;
        }
        private void RaiseIncomingEvent()
        {
            IncomingData();
        }
        private void RaiseOutgoingEvent()
        {
            OutgoingData();
        }
        private void clientProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            incoming.Add(e.Data);
        }

        #endregion

        #region IO
        public int CheckProgress()          // visible to server, check data from client
        {
            CheckProcessIO();               // only actually needed for pipeIO or queueIO...
            if (DataAvailable)
                return thisHandler(NextData);       // ok - call the provided handler
            return HostWrapper.IsRunning;
        }
        public void WriteToClient(string s) // visible to server, send data to client
        {
            outgoing.Add(s);
            RaiseOutgoingEvent();
        }

        private void WriteToConsole()
        {
            while (incoming.Count > 0)
            {
                WriteLog("Incoming -> " + incoming[0]);
                Console.WriteLine(incoming[0]);
                incoming.RemoveAt(0);
            }
        }

        private void WriteToStream()
        {
            while (outgoing.Count > 0)
            {
                string msg = outgoing[0];
                WriteLog("Outgoing -> " + msg);
                thisIO.Write(msg);
                outgoing.RemoveAt(0);
            }
        }

        private void CheckProcessIO()
        {
            if (thisIO.CheckRead())
                incoming.Add(thisIO.ReadResult());
        }

        public bool DataAvailable { get { return incoming.Count > 0; } }
        public string NextData { get { if (!DataAvailable)return null; string s = incoming[0]; incoming.RemoveAt(0); return s; } }

        public Task<string> ReadConsoleAsync()
        {
            return Task.Run(() => Console.ReadLine());
        }

        public void WriteLog(string msg)
        {
            if (!logging)
                return;
            StreamWriter log = new StreamWriter(logLocation, true);
            log.WriteLine(DateTime.Now.ToString() + ": " + msg);
            log.Flush();
            log.Close();
        }
        #endregion

        #region Cleanup

        public void Cleanup()
        {
            thisIO.Cleanup();
        }
        #endregion

        #region IOModel abstraction
        public interface IOModel
        {
            void InitProcess(string procName);
            void InitComms();
            void StartProcess();
            void ConnectOutputComms();
            bool CheckRead();
            string ReadResult();
            void Write(string msg);
            void Cleanup();
        }

        public static IOModel IOFactory(HostWrapper.IOType ioType, HostWrapper hw)
        {
            switch (ioType)
            {
                case IOType.StdIO: return new StdIOModel(hw.clientProcess_OutputDataReceived);
                case IOType.PIPES: return new PipeIOModel();
                case IOType.QUEUES: return new QueueIOModel(hw);
            }
            return null;
        }

        public class StdIOModel : IOModel
        {
            Process clientProcess;
            StreamWriter StreamOut;
            DataReceivedEventHandler thisHandler;

            public StdIOModel(DataReceivedEventHandler handler)
            {
                thisHandler = handler;
            }
            public void InitProcess(string procName)
            {
                clientProcess = new Process();
                clientProcess.StartInfo.FileName = procName;
            }
            public void InitComms()
            {
                clientProcess.StartInfo.Arguments = "Stdio ";
                clientProcess.StartInfo.UseShellExecute = false;

                clientProcess.StartInfo.RedirectStandardInput = true;
                clientProcess.StartInfo.RedirectStandardOutput = true;
                clientProcess.StartInfo.RedirectStandardError = true;
                clientProcess.StartInfo.CreateNoWindow = false;
                clientProcess.OutputDataReceived += thisHandler;
            }
            public void StartProcess()
            {
                clientProcess.Start();
                clientProcess.BeginOutputReadLine();
            }
            public void ConnectOutputComms()
            {
                StreamOut = clientProcess.StandardInput;
            }
            public bool CheckRead()
            {
                return false;   // noop
            }
            public string ReadResult()
            {
                return null;    // noop
            }
            public void Write(string msg)
            {
                StreamOut.WriteLine(msg);
                StreamOut.Flush();
            }
            public void Cleanup()
            {
                if (StreamOut != null)
                    StreamOut.Dispose();

                if (clientProcess != null)
                {
                    try
                    {
                        clientProcess.Kill();
                        clientProcess.WaitForExit();
                        clientProcess.Close();
                    }
                    catch (Exception e) { }
                }
            }
        }
        public class PipeIOModel : IOModel
        {
            Process clientProcess;

            StreamWriter StreamOut;
            StreamReader StreamIn;

            HostWrapper me;
            Task<string> pipeReaderTask;

            AnonymousPipeServerStream pipeServerIn;
            AnonymousPipeServerStream pipeServerOut;

            public PipeIOModel()
            {
                pipeReaderTask = null;
            }
            public void InitProcess(string procName)
            {
                clientProcess = new Process();
                clientProcess.StartInfo.FileName = procName;
            }
            public void InitComms()
            {
                OpenPipes();
                clientProcess.StartInfo.Arguments = "Pipes " + pipeServerOut.GetClientHandleAsString() + " " + pipeServerIn.GetClientHandleAsString();
                clientProcess.StartInfo.UseShellExecute = false;
            }
            public void StartProcess()
            {
                clientProcess.Start();
                pipeServerOut.DisposeLocalCopyOfClientHandle();
                pipeServerIn.DisposeLocalCopyOfClientHandle();
            }
            public void ConnectOutputComms()
            {
                // Read user input and send that to the client process. 
                StreamOut = new StreamWriter(pipeServerOut);
                StreamOut.AutoFlush = true;

                StreamIn = new StreamReader(pipeServerIn);
            }
            private void OpenPipes()
            {
                pipeServerOut =
                   new AnonymousPipeServerStream(PipeDirection.Out,
                   HandleInheritability.Inheritable);

                pipeServerIn =
                   new AnonymousPipeServerStream(PipeDirection.In,
                   HandleInheritability.Inheritable);
            }
            public bool CheckRead()
            {
                if (pipeReaderTask == null)
                    pipeReaderTask = ReadStreamAsync(StreamIn);
                return pipeReaderTask.IsCompleted;
            }
            private Task<string> ReadStreamAsync(StreamReader sr)
            {
                return Task.Run(() => sr.ReadLine());
            }

            public string ReadResult()
            {
                if (pipeReaderTask != null && pipeReaderTask.IsCompleted)
                {
                    string s = pipeReaderTask.Result;
                    pipeReaderTask = null;
                    return s;
                }
                return null;
            }
            public void Write(string msg)
            {
                StreamOut.WriteLine(msg);
                StreamOut.Flush();
            }
            public void Cleanup()
            {
                if (StreamOut != null)
                    StreamOut.Dispose();
                if (StreamIn != null)
                    StreamIn.Dispose();

                if (pipeServerOut != null)
                    pipeServerOut.Dispose();
                if (pipeServerIn != null)
                    pipeServerIn.Dispose();

                if (clientProcess != null)
                {
                    try
                    {
                        clientProcess.Kill();
                        clientProcess.WaitForExit();
                        clientProcess.Close();
                    }
                    catch (Exception e) { }
                }
            }


            public void TestMe()
            {
                try
                {
                    if (pipeServerIn != null)
                    {
                        Console.WriteLine("[SERVER] Setting ReadMode to \"Message\".");
                        pipeServerIn.ReadMode = PipeTransmissionMode.Message;
                    }
                }
                catch (NotSupportedException e)
                {
                    Console.WriteLine("[SERVER] Execption:\n    " + e.Message);
                }

                if (pipeServerIn != null)
                    Console.WriteLine("[SERVER] Using pipe io...Current TransmissionMode: " + pipeServerIn.TransmissionMode.ToString() + ".");
                else
                    Console.WriteLine("[SERVER] Using stdio...");
            }
        }
        public class QueueIOModel : IOModel
        {
            Process clientProcess;
            public QueueingModel queueClient;
            List<string> postRoutes;

            string paramString = "";

            // this really should only tell the client what it should be listening to, right.  
            // TypeID and clientID are pretty app-specific
            // ### and most of the actual IO should be deferred to this class, 
            // (and mirrored on the client side...) if we're going to this trouble...
            public QueueIOModel(HostWrapper hw)
            {
                postRoutes = new List<string>();
                paramString = hw.qexch + "|" + hw.qhost + "|" + hw.qport + "|" + hw.quid + "|" + hw.qpwd;
                foreach (string s in hw.postRoutes)
                {
                    paramString += "|" + s;
                    postRoutes.Add(s);
                }

                List<string> routes = new List<string>();
                queueClient = new QueueingModel(hw.qexch, "topic", "ServerQueue", hw.listenRoutes, hw.qhost, hw.quid, hw.qpwd, Convert.ToInt32(hw.qport));
            }
            public void InitProcess(string procName)
            {
                clientProcess = new Process();
                clientProcess.StartInfo.FileName = procName;
            }
            public void InitComms()
            {
                // need to be able to init the queue connection detail for the process here as arguments
                // exch, port, uid, pwd, typeid
                clientProcess.StartInfo.Arguments = "Queue " + paramString;
                clientProcess.StartInfo.UseShellExecute = false;
            }
            public void StartProcess()
            {
                // there is no other activity...
                clientProcess.Start();
            }
            public void ConnectOutputComms()
            {
                // there is no other activity...
            }

            public bool CheckRead()
            {
                return !queueClient.QueueEmpty();
            }
            public string ReadResult()
            {
                if (CheckRead())
                    return queueClient.ReadMessageAsString();
                return null;
            }
            public void Write(string msg)
            {
                int sep = msg.IndexOf('#');
                int q = 0;
                if (sep > 0 && Int32.TryParse(msg.Substring(0, sep), out q))
                {
                    msg = msg.Substring(sep + 1);
                }
                else
                {
                    q = 0;
                }
                queueClient.PostMessage(msg, postRoutes[q]);
            }
            public void Cleanup()
            {
                queueClient.CloseConnections();
                if (clientProcess != null)
                {
                    try
                    {
                        clientProcess.Kill();
                        clientProcess.WaitForExit();
                        clientProcess.Close();
                    }
                    catch (Exception e) { }
                }
            }
        }
        #endregion
    }
}
