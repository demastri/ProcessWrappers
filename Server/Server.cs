using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.IO.Pipes;
using System.Diagnostics;

using ProcessWrappers;
using QueueCommon;

namespace Server
{
    class Server
    {
        static HostWrapper.IOType thisPass;

        static void Main(string[] args)
        {
            UseStdIo();
            UsePipesIo();
            UseQueueIo();
            Console.WriteLine("[SERVER] Done with testing - ENTER");
            Console.ReadLine();
        }
        static void UseStdIo()
        {
            Console.WriteLine("[SERVER] Initializing StdIO");
            thisPass = HostWrapper.IOType.StdIO;
            Run();
        }
        static void UsePipesIo()
        {
            Console.WriteLine("[SERVER] Initializing Pipes");
            thisPass = HostWrapper.IOType.PIPES;
            Run();
        }
        static void UseQueueIo()
        {
            Console.WriteLine("[SERVER] Initializing Queues");
            thisPass = HostWrapper.IOType.QUEUES;
            Run();
        }

        static void Run()
        {
            string myExeLoc = "C:\\Projects\\JPD\\BBRepos\\Chess\\PipesCommsExamples\\Client\\bin\\Debug\\Client.exe";
            //myExeLoc = "D:\\Projects\\Workspaces\\BBRepos\\Chess\\PipesCommsExamples\\Client\\bin\\Debug\\Client.exe";
            //myExeLoc = "C:\\Projects\\JPD\\BBRepos\\Chess\\engines\\stockfish\\stockfish_5_32bit.exe";

            HostWrapper myHost;

            if (thisPass == HostWrapper.IOType.QUEUES)
            {
                string clientID = Guid.NewGuid().ToString();
                string typeID = "TestProcess.PrintSort";
                List<string> listenRoutes = new List<string>();
                List<string> postRoutes = new List<string>();
                listenRoutes.Add(clientID + ".workUpdate." + typeID);
                listenRoutes.Add(clientID + ".workComplete." + typeID);
                postRoutes.Add(clientID + ".workRequest." + typeID);

                ConnectionDetail thisConn = new ConnectionDetail("localhost", 5672, "myExch", "direct", clientID, listenRoutes, "guest", "guest");

                myHost = new HostWrapper("SomeLocation", HostWrapper.IOType.QUEUES, thisConn, postRoutes, ProcessControl);
            }
            else
                myHost = new HostWrapper(myExeLoc, thisPass, ProcessControl);

            myHost.Start();

            //myHost.WriteToClient("SYNC");
            //myHost.WriteToClient("uci");

            Task<string> readTask = myHost.ReadConsoleAsync();
            do
            {
                if (readTask.IsCompleted)
                {
                    string localBuffer = readTask.Result;
                    myHost.WriteToClient(localBuffer);  // simplest task possible, echo console data to the worker process
                    readTask = myHost.ReadConsoleAsync();
                }
                System.Threading.Thread.Sleep(250);
            } while (myHost.CheckProgress() != HostWrapper.IsEnding);
            //myHost.WriteToClient("quit");

            if (thisPass == HostWrapper.IOType.PIPES)
                myHost.TestPipeMode();

            myHost.Cleanup();
            Console.WriteLine("[SERVER] Client quit. Server terminating.");
        }

        public static int ProcessControl(string s)
        {
            Console.WriteLine(" From Client: <" + s + ">");
            if (s == null || s.StartsWith("uciok") || s.StartsWith("QUIT"))
                return HostWrapper.IsEnding;
            return HostWrapper.IsRunning;
        }

    }
}
