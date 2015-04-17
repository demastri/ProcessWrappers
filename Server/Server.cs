using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.IO.Pipes;
using System.Diagnostics;

using ProcessWrappers;

namespace Server
{
    class Server
    {
        static bool usePipeIO = false;
        static bool useStdIO = false;
        static bool useQueueIO = false;


        static void Main(string[] args)
        {
            //UseStdIo();
            //UsePipesIo();
            UseQueueIo();
            Console.WriteLine("[SERVER] Done with testing - ENTER");
            Console.ReadLine();
        }
        static void UseStdIo()
        {
            Console.WriteLine("[SERVER] Initializing StdIO");
            usePipeIO = false;
            useStdIO = true;
            useQueueIO = false;
            Run();
        }
        static void UsePipesIo()
        {
            Console.WriteLine("[SERVER] Initializing Pipes");
            usePipeIO = true;
            useStdIO = false;
            useQueueIO = false;
            Run();
        }
        static void UseQueueIo()
        {
            Console.WriteLine("[SERVER] Will be Initializing Queues");
            usePipeIO = false;
            useStdIO = false;
            useQueueIO = true;
            Run();
        }

        static void Run()
        {
            string myExeLoc = "C:\\Projects\\JPD\\BBRepos\\Chess\\PipesCommsExamples\\Client\\bin\\Debug\\Client.exe";
            myExeLoc = "D:\\Projects\\Workspaces\\BBRepos\\Chess\\PipesCommsExamples\\Client\\bin\\Debug\\Client.exe";
            //myExeLoc = "C:\\Projects\\JPD\\BBRepos\\Chess\\engines\\stockfish\\stockfish_5_32bit.exe";

            HostWrapper myHost;
            string clientID = Guid.NewGuid().ToString();
            string typeID = "TestProcess.PrintSort";

            if (useQueueIO)
            {
                myHost = new HostWrapper(myExeLoc, useStdIO, useQueueIO, usePipeIO,
                    "myExch", "localhost", "5672", "guest", "guest", typeID, clientID,
                    ProcessControl);
            }
            else
                myHost = new HostWrapper(myExeLoc, useStdIO, ProcessControl);

            myHost.Start();

            //myHost.WriteToClient("SYNC");
            //myHost.WriteToClient("uci");

            string localBuffer = "";
            Task<string> readTask = myHost.ReadConsoleAsync();
            do
            {
                if (readTask.IsCompleted)
                {
                    localBuffer = readTask.Result;
                    myHost.WriteToClient(localBuffer);
                    readTask = myHost.ReadConsoleAsync();
                }
            } while (myHost.CheckProgress() != HostWrapper.IsEnding);
            //myHost.WriteToClient("quit");

            myHost.Cleanup();
            Console.WriteLine("[SERVER] Client quit. Server terminating.");
        }

        public static int ProcessControl(HostWrapper thisHost)
        {
            System.Threading.Thread.Sleep(250);
            while (thisHost.incoming.Count > 0)
            {
                string s = thisHost.incoming[0];
                Console.WriteLine(" From Client: <" + s + ">");
                thisHost.incoming.RemoveAt(0);
                if (s == null || s.StartsWith("uciok"))
                    return HostWrapper.IsEnding;
            }
            return HostWrapper.IsRunning;
        }

    }
}
