using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;

using QueueCommon;

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
            useStdIO = false;
            usePipeIO = false;
            useQueueIO = false;
            hostID = routeID = "";

            if (args.Length == 0)
                useStdIO = true;
            else// ok - both pipes specified, otherwise use stdio...
                switch (args[0].ToLower().Trim())
                {
                    case "stdio":
                        useStdIO = true;
                        break;
                    case "pipes":
                        usePipeIO = true;
                        inPipeID = args[1];
                        outPipeID = args[2];
                        break;
                    case "queue":
                        queueParams = args[1];
                        useQueueIO = true;
                        break;
                }
        }
        #endregion

        #region Private fields

        QueueingModel queueClient;
        PipeStream pipeClientIn;
        PipeStream pipeClientOut;
        StreamReader StreamIn;
        StreamWriter StreamOut;
        bool useStdIO;
        bool useQueueIO;
        bool usePipeIO;
        string inPipeID;
        string outPipeID;
        string queueParams;
        string hostID;
        string routeID;
        List<string> queueMsgs;

        #endregion

        #region Init
        public void Start()
        {
            if (usePipeIO)
                OpenPipes();
            if (useQueueIO) // the assumption is that the component will use stdio for IO and this wrap will turn that into queue msgs
                OpenQueue();
            CreateStreamOnPipes();

            //TestPipeMode();
        }
        private void OpenPipes()
        {
            pipeClientIn = new AnonymousPipeClientStream(PipeDirection.In, inPipeID);
            pipeClientOut = new AnonymousPipeClientStream(PipeDirection.Out, outPipeID);
        }
        private void OpenQueue()
        {
            queueMsgs = new List<string>();
            string[] param = queueParams.Split('|');
            string typeID = param[5];
            List<string> routes = new List<string>();
            routes.Add("*.workRequest." + typeID);
            routes.Add("*.workerCommand." + typeID);
            hostID = param[6];
            routeID = hostID+".workComplete."+typeID;

            queueClient = new QueueingModel(param[0], "topic", typeID, routes, param[1], param[3], param[4], Convert.ToInt32(param[2]));
            queueClient.SetListenerCallback(HandlePosts);
        }
        private void HandlePosts(byte[] msg, string routeKey)
        {
            string thisMsg = System.Text.Encoding.Default.GetString(msg);
            queueMsgs.Add(thisMsg);
        }
        private void CreateStreamOnPipes()
        {
            if (usePipeIO)
            {
                StreamIn = new StreamReader(pipeClientIn);
                StreamOut = new StreamWriter(pipeClientOut);
                StreamOut.AutoFlush = true;
            }
            if (useStdIO)
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
            ClientMessage(msg, routeID);
        }
        public void ClientMessage(string msg, string route)
        {
            if (useQueueIO)
            {
                queueClient.PostMessage(msg, route);
            }
            else
            {
                StreamOut.WriteLine(msg);
                StreamOut.Flush();
                if (usePipeIO)
                    pipeClientOut.WaitForPipeDrain();
            }
        }
        public string ClientReadLine()
        {
            if (useQueueIO)
            {
                while (true)
                {
                    if(queueMsgs.Count > 0)
                    {
                        string outStr = queueMsgs[0];
                        queueMsgs.RemoveAt(0);
                        return outStr;
                    }
                    System.Threading.Thread.Sleep(250);
                }
            }
            return StreamIn.ReadLine();
        }
        public Task<string> ClientReadLineAsync()
        {
            return Task.Run(() => ClientReadLine());
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
