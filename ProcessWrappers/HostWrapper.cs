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

        public List<string> outgoing;
        public List<string> incoming;
        public delegate int ProcessControlHandler(HostWrapper thisHost);

        public const int IsEnding = 1;
        public const int IsRunning = 2;

        string logLocation = "C:\\HostWrapper\\logfile.txt";
        bool logging;

        public HostWrapper(string processLoc, bool stdio, bool queueio, bool pipeio,
                        string exch, string host, string port, string uid, string pwd, string typeid, string clientID, ProcessControlHandler datasink)
        {
            qexch = exch;
            qhost = host;
            qport = port;
            quid = uid;
            qpwd = pwd;
            qtypeid = typeid;
            qclientid = clientID;

            logging = false;
            outgoing = new List<string>();
            incoming = new List<string>();
            processLocation = processLoc;
            useStdIO = stdio;
            usePipeIO = pipeio;
            useQueueIO = queueio;
            thisHandler = datasink;
            pipeReaderTask = null;
            InitIOModel();
        }
        public HostWrapper(string processLoc, bool stdio, ProcessControlHandler datasink)
        {
            qexch = qhost = qport = quid = qpwd = qtypeid = qclientid = "";

            logging = false;
            outgoing = new List<string>();
            incoming = new List<string>();
            processLocation = processLoc;
            useStdIO = stdio;
            usePipeIO = !useStdIO;
            useQueueIO = false;
            thisHandler = datasink;
            pipeReaderTask = null;
            InitIOModel();
        }
        public HostWrapper(string processLoc, bool stdio, ProcessControlHandler datasink, bool setLog)
        {
            qexch = qhost = qport = quid = qpwd = qtypeid = qclientid = "";

            logging = setLog;
            outgoing = new List<string>();
            incoming = new List<string>();
            processLocation = processLoc;
            useStdIO = stdio;
            usePipeIO = !useStdIO;
            useQueueIO = false;
            thisHandler = datasink;
            pipeReaderTask = null;
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
        Process clientProcess;
        AnonymousPipeServerStream pipeServerOut;
        StreamWriter StreamOut;

        AnonymousPipeServerStream pipeServerIn;
        StreamReader StreamIn;

        public QueueingModel queueClient;

        string qexch;
        string qport;
        string qhost;
        string quid;
        string qpwd;
        string qtypeid;
        string qclientid;


        string processLocation = "";
        bool useQueueIO = false;
        bool usePipeIO = false;
        bool useStdIO = true;
        ProcessControlHandler thisHandler;
        Task<string> pipeReaderTask;

        #endregion

        #region Init

        private void InitIOModel()
        {
            if (usePipeIO)
                thisIO = new PipeIOModel(this);
            else if (useStdIO)
                thisIO = new StdIOModel(this);
            else if (useQueueIO)
            {
                thisIO = new QueueIOModel(this);

                List<string> routes = new List<string>();
                routes.Add(qclientid + ".workUpdate." + qtypeid);
                routes.Add(qclientid + ".workComplete." + qtypeid);
                queueClient = new QueueingModel(qexch, "topic", "ServerQueue", routes, qhost, quid, qpwd, Convert.ToInt32(qport));
            }
            else
                thisIO = null;

        }
        public void Start()
        {
            InitProcess(processLocation);

            thisIO.InitComms();
            thisIO.StartProcess();
            thisIO.ConnectOutputComms();

            RegisterIncomingEvents();
            RegisterOutgoingEvents();

            //TestPipeMode();
        }

        private void InitProcess(string procName)
        {
            clientProcess = new Process();
            clientProcess.StartInfo.FileName = procName;
        }

        private void TestPipeMode()
        {
            // Show that anonymous Pipes do not support Message mode. 
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
            CheckProcessIO();               // only actually needed for pipeIO...
            return thisHandler(this);       // ok - call the provided handler
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
            try
            {
                while (outgoing.Count > 0)
                {
                    WriteLog("Outgoing -> " + outgoing[0]);
                    if (useQueueIO)
                    {
                        queueClient.PostMessage(outgoing[0], qclientid + ".workRequest." + qtypeid);
                    }
                    else
                    {
                        StreamOut.WriteLine(outgoing[0]);

                        StreamOut.Flush();
                    }
                    outgoing.RemoveAt(0);
                }
            }
            catch (Exception e)
            {
            }
        }

        private void CheckProcessIO()
        {
            if (usePipeIO)  // StdIO is handled through BeginOutputReadline/OutputDataReceived
            {
                if (pipeReaderTask == null)
                    pipeReaderTask = ReadStreamAsync(StreamIn);
                if (pipeReaderTask.IsCompleted)
                {
                    incoming.Add(pipeReaderTask.Result);
                    pipeReaderTask = null;
                }
            }
            if (useQueueIO)  // StdIO is handled through BeginOutputReadline/OutputDataReceived
            {
                while (!queueClient.QueueEmpty())
                {
                    string s = queueClient.ReadMessageAsString();
                    incoming.Add(s);
                }
            }
        }
        private Task<string> ReadStreamAsync(StreamReader sr)
        {
            return Task.Run(() => sr.ReadLine());
        }
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
            CleanupStreams();
            CleanupPipes();
            CleanupProcess();
        }

        private void CleanupStreams()
        {
            if (useStdIO)
            {
                if (StreamOut != null)
                    StreamOut.Dispose();
                if (StreamIn != null)
                    StreamIn.Dispose();
            }
        }
        private void CleanupPipes()
        {
            if (pipeServerOut != null)
                pipeServerOut.Dispose();
            if (pipeServerIn != null)
                pipeServerIn.Dispose();
        }
        private void CleanupProcess()
        {
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
        #endregion

        #region IOModel abstraction
        public interface IOModel
        {
            void InitComms();
            void StartProcess();
            void ConnectOutputComms();
        }

        public class StdIOModel : IOModel
        {
            HostWrapper me;
            public StdIOModel(HostWrapper hw)
            {
                me = hw;
            }
            public void InitComms()
            {
                me.clientProcess.StartInfo.Arguments = "Stdio ";
                me.clientProcess.StartInfo.UseShellExecute = false;

                me.clientProcess.StartInfo.RedirectStandardInput = true;
                me.clientProcess.StartInfo.RedirectStandardOutput = true;
                me.clientProcess.StartInfo.RedirectStandardError = true;
                me.clientProcess.StartInfo.CreateNoWindow = false;
                me.clientProcess.OutputDataReceived += me.clientProcess_OutputDataReceived;
            }
            public void StartProcess()
            {
                me.clientProcess.Start();
                me.clientProcess.BeginOutputReadLine();
            }
            public void ConnectOutputComms()
            {
                me.StreamOut = me.clientProcess.StandardInput;
            }
        }
        public class PipeIOModel : IOModel
        {
            HostWrapper me;
            public PipeIOModel(HostWrapper hw)
            {
                me = hw;
            }
            public void InitComms()
            {
                OpenPipes();
                me.clientProcess.StartInfo.Arguments = "Pipes " + me.pipeServerOut.GetClientHandleAsString() + " " + me.pipeServerIn.GetClientHandleAsString();
                me.clientProcess.StartInfo.UseShellExecute = false;
            }
            public void StartProcess()
            {
                me.clientProcess.Start();
                me.pipeServerOut.DisposeLocalCopyOfClientHandle();
                me.pipeServerIn.DisposeLocalCopyOfClientHandle();
            }
            public void ConnectOutputComms()
            {
                me.StreamOut = new StreamWriter(me.pipeServerOut);
                // Read user input and send that to the client process. 
                me.StreamOut.AutoFlush = true;

                me.StreamIn = new StreamReader(me.pipeServerIn);
            }
            private void OpenPipes()
            {
                me.pipeServerOut =
                   new AnonymousPipeServerStream(PipeDirection.Out,
                   HandleInheritability.Inheritable);

                me.pipeServerIn =
                   new AnonymousPipeServerStream(PipeDirection.In,
                   HandleInheritability.Inheritable);
            }

        }
        public class QueueIOModel : IOModel
        {
            HostWrapper me;
            string paramString = "";

            public QueueIOModel(HostWrapper hw)
            {
                me = hw;
                paramString = me.qexch + "|" + me.qhost + "|" + me.qport + "|" + me.quid + "|" + me.qpwd + "|" + me.qtypeid + "|" + me.qclientid;
            }
            public void InitComms()
            {
                // ### need to be able to init the queue connection detail for the process here as arguments
                // exch, port, uid, pwd, typeid
                me.clientProcess.StartInfo.Arguments = "Queue " + paramString;
                me.clientProcess.StartInfo.UseShellExecute = false;
            }
            public void StartProcess()
            {
                // there is no other activity...
                me.clientProcess.Start();
            }
            public void ConnectOutputComms()
            {
                // there is no other activity...
            }
        }
        #endregion
    }
}
