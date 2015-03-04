using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;

namespace ProcessWrappers
{
    public class ClientWrapper
    {
        #region notes and public fields / constructors
        /// Ideally, to act as a process client, an app should recognize:
        ///  - flag to determine whether it uses console or pipe io
        ///  - a method to call when there's data from the host
        ///  - an endpoint to send data to the host
        ///  - wrapper control methods - start, cleanup
        /// Data and processing here are shouldn't be synchronous
        /// 
        /// in the client process:
        ///     ClientWrapper myClient = new ClientWrapper( args );
        ///     myHost.Start(args);     // pipes or streams set up automatically, just use the provided r/w methods...
        ///     ...
        ///     do {
        ///     ...
        ///         temp = myClient.ClientReadLine();    // blocking read from server
        ///     ...
        ///         Task<string> readTask = myClient.ClientReadLineAsync(); // non blocking read from server
        ///     ...
        ///         if (readTask.IsCompleted)   // check it at some point...
        ///             temp = readTask.Result;
        ///     ...
        ///         myClient.ClientMessage("Send something back to the server from the client");
        ///     ...
        ///     } while( my end condition isn't met... );
        ///     ...
        ///     myClient.Cleanup()
        ///     ...

        public ClientWrapper(string[] args)
        {
            pipeClientIn = null;
            pipeClientOut = null;
            StreamIn = null;
            StreamOut = null;
            usePipeIO = (args.Length > 1);    // ok - both pipes specified, otherwise use stdio...
            if (usePipeIO)
            {
                inPipeID = args[0];
                outPipeID = args[1];
            }
        }
        #endregion

        #region Private fields
        
        PipeStream pipeClientIn;
        PipeStream pipeClientOut;
        StreamReader StreamIn;
        StreamWriter StreamOut;
        bool usePipeIO;
        string inPipeID;
        string outPipeID;

        #endregion

        #region Init
        public void Start()
        {
            if (usePipeIO)
                OpenPipes();
            CreateStreamOnPipes();

            //TestPipeMode();
        }
        private void OpenPipes()
        {
            pipeClientIn = new AnonymousPipeClientStream(PipeDirection.In, inPipeID);
            pipeClientOut = new AnonymousPipeClientStream(PipeDirection.Out, outPipeID);
        }
        private void CreateStreamOnPipes()
        {
            if (usePipeIO)
            {
                StreamIn = new StreamReader(pipeClientIn);
                StreamOut = new StreamWriter(pipeClientOut);
                StreamOut.AutoFlush = true;
            }
            else
            {
                StreamIn = new StreamReader(Console.OpenStandardInput());
                StreamOut = new StreamWriter(Console.OpenStandardOutput());
            }
        }
        private void TestPipeMode()
        {
            // Show that anonymous Pipes do not support Message mode. 
            try
            {
                if (pipeClientIn != null)
                {
                    ClientMessage("[CLIENT] Setting ReadMode to \"Message\".");
                    pipeClientIn.ReadMode = PipeTransmissionMode.Message;
                }
            }
            catch (NotSupportedException e)
            {
                ClientMessage("[CLIENT] Execption:\n    " + e.Message);
            }

            if (pipeClientIn != null)
                ClientMessage("[CLIENT] Using pipe io...Current TransmissionMode: " + pipeClientIn.TransmissionMode.ToString() + ".");
            else
                ClientMessage("[CLIENT] Using stdio...");
        }
        #endregion

        #region IO
        public void ClientMessage(string msg)
        {
            StreamOut.WriteLine(msg);
            StreamOut.Flush();
            if (usePipeIO)
                pipeClientOut.WaitForPipeDrain();
        }
        public string ClientReadLine()
        {
            return StreamIn.ReadLine();
        }
        public Task<string> ClientReadLineAsync()
        {
            return Task.Run(() => StreamIn.ReadLine());
        }

#endregion

        #region Cleanup

        public void Cleanup()
        {
            CleanupStreams();
            CleanupPipes();
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
            if (pipeClientOut != null)
                pipeClientOut.Dispose();
            if (pipeClientIn != null)
                pipeClientIn.Dispose();
        }

        #endregion

    }
}
