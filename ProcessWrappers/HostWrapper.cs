using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.IO.Pipes;
using System.Diagnostics;

using QueueCommon;
using ProcessWrappers.IOModels;

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
        public delegate int ProcessControlHandler(string nextDataElt);

        public const int IsEnding = 1;
        public const int IsRunning = 2;

        internal ConnectionDetail thisConnectionDetail;
        public List<string> hostPostKeys;

        public HostWrapper(string processLoc, IOType thisIOType, ConnectionDetail connDetail, List<string> postKeys, ProcessControlHandler datasink)
        {
            // the keys in ConnDetail are the ones this host will listen to (and they're the ones the worker should post to)
            // the keys in post keys are where this host will post (and a flavor of what the worker should listen to)
            thisConnectionDetail = connDetail.Copy();
            hostPostKeys = new List<string>();
            foreach (string s in postKeys)
                hostPostKeys.Add(s);

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
            thisConnectionDetail = null;
            hostPostKeys = new List<string>();

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
            thisConnectionDetail = null;
            hostPostKeys = new List<string>();

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

        string logLocation = "C:\\HostWrapper\\logfile.txt";
        bool logging;

        string processLocation = "";
        IOType useIOType;
        ProcessControlHandler thisHandler;

        #endregion

        #region Init

        private void InitIOModel()
        {
            thisIO = IOModelHelper.IOModelFactory(useIOType, this, clientProcess_OutputDataReceived);
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

    }
}
