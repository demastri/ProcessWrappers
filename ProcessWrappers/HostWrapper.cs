using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.IO.Pipes;
using System.Diagnostics;

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

        public HostWrapper(string processLoc, bool stdio, ProcessControlHandler datasink)
        {
            outgoing = new List<string>();
            incoming = new List<string>();
            processLocation = processLoc;
            useStdIO = stdio;
            usePipeIO = !useStdIO;
            thisHandler = datasink;
            pipeReaderTask = null;
        }
        #endregion

        #region Private fields

        delegate void OutgoingDataHandler();
        delegate void IncomingDataHandler();
        delegate void ProcessCompletedHandler();

        event OutgoingDataHandler OutgoingData;
        event IncomingDataHandler IncomingData;


        Process clientProcess;
        AnonymousPipeServerStream pipeServerOut;
        StreamWriter StreamOut;

        AnonymousPipeServerStream pipeServerIn;
        StreamReader StreamIn;

        string processLocation = "";
        bool usePipeIO = false;
        bool useStdIO = true;
        ProcessControlHandler thisHandler;
        Task<string> pipeReaderTask;

        #endregion

        #region Init

        public void Start()
        {
            InitProcess(processLocation);
            if( usePipeIO )
                OpenPipes();
            ConnectPipeToProcess();
            CreateStreamOnPipes();

            RegisterIncomingEvents();
            RegisterOutgoingEvents();

            //TestPipeMode();
        }

        private void InitProcess(string procName)
        {
            clientProcess = new Process();
            clientProcess.StartInfo.FileName = procName;
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

        private void ConnectPipeToProcess()
        {
            // Pass the client process a handle to the server.
            clientProcess.StartInfo.Arguments =
                (usePipeIO ?
                (pipeServerOut.GetClientHandleAsString() + " " + pipeServerIn.GetClientHandleAsString()) :
                    "");

            clientProcess.StartInfo.UseShellExecute = false;
            if (useStdIO)
            {
                clientProcess.StartInfo.RedirectStandardInput = true;
                clientProcess.StartInfo.RedirectStandardOutput = true;
                clientProcess.StartInfo.RedirectStandardError = true;
                
                clientProcess.StartInfo.CreateNoWindow = true;

                clientProcess.OutputDataReceived += clientProcess_OutputDataReceived;
            }

            clientProcess.Start();

            if (useStdIO)
                clientProcess.BeginOutputReadLine();
            if (usePipeIO)
            {
                pipeServerOut.DisposeLocalCopyOfClientHandle();
                pipeServerIn.DisposeLocalCopyOfClientHandle();
            }
        }

        private void CreateStreamOnPipes()
        {
            if (useStdIO)
                StreamOut = clientProcess.StandardInput;
            if (usePipeIO)
            {
                StreamOut = new StreamWriter(pipeServerOut);
                // Read user input and send that to the client process. 
                StreamOut.AutoFlush = true;

                StreamIn = new StreamReader(pipeServerIn);
            }
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
                Console.WriteLine(incoming[0]);
                incoming.RemoveAt(0);
            }
        }

        private void WriteToStream()
        {
            while (outgoing.Count > 0)
            {
                StreamOut.WriteLine(outgoing[0]);

                StreamOut.Flush();
                outgoing.RemoveAt(0);
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
        }
        private Task<string> ReadStreamAsync(StreamReader sr)
        {
            return Task.Run(() => sr.ReadLine());
        }
        public Task<string> ReadConsoleAsync()
        {
            return Task.Run(() => Console.ReadLine());
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
            if (StreamOut != null)
                StreamOut.Dispose();
            if (StreamIn != null)
                StreamIn.Dispose();
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
                clientProcess.WaitForExit();
                clientProcess.Close();
            }
        }
#endregion

    }
}
