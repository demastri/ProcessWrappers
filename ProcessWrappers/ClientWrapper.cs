using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;

using ProcessWrappers;
using ProcessWrappers.IOModels;
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

        public IOType useIOType;
        public List<string> postRoutes;
        public List<string> listenRoutes;

        public ClientWrapper(string[] args)
        {
            incoming = new List<string>();
            outgoing = new List<string>();

            StreamIn = null;
            StreamOut = null;
            useIOType = IOType.UNDEF;
            useIOType = (args.Length == 0 ? IOType.StdIO : IOModelHelper.GetIOType(args[0]));

            if( useIOType == IOType.QUEUES )
                queueParams = args[1];

            InitIOModel( args );
        }
        private void InitIOModel(string [] theseArgs)
        {
            thisIO = null;
            if( useIOType  == IOType.PIPES )
                thisIO = IOModelHelper.IOModelFactory(useIOType, theseArgs, clientProcess_OutputDataReceived);
        }

        #endregion

        #region Private fields

        IOModel thisIO;
        delegate void OutgoingDataHandler();
        delegate void IncomingDataHandler();
        delegate void ProcessCompletedHandler();

        event OutgoingDataHandler OutgoingData;
        event IncomingDataHandler IncomingData;


        QueueingModel queueClient;
        StreamReader StreamIn;
        StreamWriter StreamOut;
        string queueParams;
        List<string> incoming;
        List<string> outgoing;

        #endregion

        #region Init
        public void Start()
        {
            if (thisIO != null)
            {
                thisIO.InitProcess(null); // this is already a client process, I don't need to start a sub process here...

                thisIO.InitComms();
                thisIO.StartProcess();
                thisIO.ConnectOutputComms();

                RegisterIncomingEvents();
                RegisterOutgoingEvents();
            }
            else
            {
                if (useIOType == IOType.QUEUES) // the assumption is that the component will use stdio for IO and this wrap will turn that into queue msgs
                    OpenQueue();
                CreateStreamOnPipes();
            }
        }
        private void OpenQueue()
        {
            //Console.WriteLine(queueParams);

            string[] param = queueParams.Split('|');
            listenRoutes = new List<string>();
            postRoutes = new List<string>();

            int i = 5;
            for (; i < param.Count(); i++)
                if (param[i].Trim() == "#")
                    break;
                else
                {
                    //Console.WriteLine(param[i]);
                    listenRoutes.Add(param[i]);
                }
            while (++i < param.Count())
            {
                //Console.WriteLine(param[i]);
                postRoutes.Add(param[i]);
            }

            queueClient = new QueueingModel(param[0], "topic", "clientQueue", listenRoutes, param[1], param[3], param[4], Convert.ToInt32(param[2]));
            queueClient.SetListenerCallback(HandlePosts);

            //Console.WriteLine(listenRoutes.Count.ToString() + " listen routes, " + postRoutes.Count.ToString() + " post routes");
        }
        public void UpdatePostRoute(string src, string dest)
        {
            postRoutes.Clear();
            foreach (string s in listenRoutes)
            {
                if (s.Contains(src))
                {
                    postRoutes.Add(s.Replace(src, dest));
                }
            }
        }
        private void HandlePosts(byte[] msg, string routeKey)
        {
            string thisMsg = System.Text.Encoding.Default.GetString(msg);
            incoming.Add(thisMsg);
        }

        private void CreateStreamOnPipes()
        {
            if (useIOType == IOType.StdIO)
            {
                StreamIn = new StreamReader(Console.OpenStandardInput());
                StreamOut = new StreamWriter(Console.OpenStandardOutput());
            }
        }
        #endregion

        #region IO
        public void ClientMessage(string msg)
        {
            if (useIOType == IOType.QUEUES)
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
                ClientMessage(msg, postRoutes[q]);
            }
            else
                ClientMessage(msg, "");
        }
        public void ClientMessage(string msg, string route)
        {
            if (thisIO != null)
            {
                thisIO.Write(msg);
            }
            else if (useIOType == IOType.QUEUES)
            {
                queueClient.PostMessage(msg, route);
            }
            else
            {
                StreamOut.WriteLine(msg);
                StreamOut.Flush();
            }
        }
        public string ClientReadLine()
        {
            if (thisIO != null)
            {
                while (true)
                {
                    if (thisIO.CheckRead())
                        return thisIO.ReadResult();
                    System.Threading.Thread.Sleep(250);
                }
            }
            else if (useIOType == IOType.QUEUES)
            {
                while (true)
                {
                    if (incoming.Count > 0)
                    {
                        string outStr = incoming[0];
                        incoming.RemoveAt(0);
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


        private void WriteToConsole()
        {
            while (incoming.Count > 0)
            {
                //WriteLog("Incoming -> " + incoming[0]);
                Console.WriteLine(incoming[0]);
                incoming.RemoveAt(0);
            }
        }

        private void WriteToStream()
        {
            while (outgoing.Count > 0)
            {
                string msg = outgoing[0];
                //WriteLog("Outgoing -> " + msg);
                thisIO.Write(msg);
                outgoing.RemoveAt(0);
            }
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


        #region Cleanup

        public void Cleanup()
        {
            CleanupStreams();
            if (thisIO != null)
                thisIO.Cleanup();
        }
        private void CleanupStreams()
        {
            if (StreamOut != null)
                StreamOut.Dispose();
            if (StreamIn != null)
                StreamIn.Dispose();
        }


        #endregion

    }
}
