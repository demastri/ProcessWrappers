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

namespace Server
{
    class Server
    {
        static ProcessWrapper myHost;
        static IOType thisPass;

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
            thisPass = IOType.StdIO;
            Run();
        }
        static void UsePipesIo()
        {
            Console.WriteLine("[SERVER] Initializing Pipes");
            thisPass = IOType.PIPES;
            Run();
        }
        static void UseQueueIo()
        {
            Console.WriteLine("[SERVER] Initializing Queues");
            thisPass = IOType.QUEUES;
            Run();
        }

        static void Run()
        {
            // this is a simple cycle - reads from host and posts to the process
            StartClient();

            Task<string> readTask = ReadConsoleAsync();
            do
            {
                if (readTask.IsCompleted)
                {
                    string localBuffer = readTask.Result;
                    myHost.SendProcessMessage(localBuffer);  // simplest task possible, echo console data to the worker process
                    readTask = ReadConsoleAsync();
                }
                System.Threading.Thread.Sleep(250);
            } while (myHost.CheckProgress() == ProcessWrapper.ProcesssStatus.Running);

            myHost.Cleanup();
            Console.WriteLine("[SERVER] Client quit. Server terminating.");
        }

        static public void StartClient() 
        {
            string myExeLoc = "C:\\Projects\\JPD\\BBRepos\\ProcessWrappers\\Client\\bin\\Debug\\Client.exe";
            //myExeLoc = "D:\\Projects\\Workspaces\\BBRepos\\ProcessWrappers\\Client\\bin\\Debug\\Client.exe";
            //myExeLoc = "C:\\Projects\\JPD\\BBRepos\\Chess\\engines\\stockfish\\stockfish_5_32bit.exe";

            myHost = new ProcessWrapper();
            string modelParams = IOModelHelper.IOTypeParam[thisPass];

            switch (thisPass)
            {
                case IOType.PIPES:
                    modelParams += " ";
                    break;
                case IOType.QUEUES:
                    modelParams += " ";
                    break;
                case IOType.StdIO:
                    modelParams += " ";
                    break;
            }
            modelParams = "";

            if (thisPass == IOType.QUEUES)
            {
                string clientID = Guid.NewGuid().ToString();
                string typeID = "TestProcess.PrintSort";
                List<string> listenRoutes = new List<string>();
                List<string> postRoutes = new List<string>();
                listenRoutes.Add(clientID + ".workUpdate." + typeID);
                listenRoutes.Add(clientID + ".workComplete." + typeID);
                postRoutes.Add(clientID + ".workRequest." + typeID);
                ConnectionDetail thisConn = new ConnectionDetail("localhost", 5672, "myExch", "topic", clientID, listenRoutes, "guest", "guest");

                modelParams = "myExch|localhost|5672|guest|guest|";
                modelParams += clientID + ".workUpdate." + typeID + "|";
                modelParams += clientID + ".workComplete." + typeID + "|#|";
                modelParams += clientID + ".workRequest." + typeID;
            }
            myHost.Init(myExeLoc, thisPass, modelParams, ProcessControl);
        }

        // ok -> here's the process return string - looking for a QUIT to change the status
        public static void ProcessControl(string s)
        {
            Console.WriteLine(" From Client: <" + s + ">");
            if (s == null || s.StartsWith("uciok") || s.StartsWith("QUIT"))
                myHost.UpdateStatus(ProcessWrapper.ProcesssStatus.Ending);
            else
                myHost.UpdateStatus(ProcessWrapper.ProcesssStatus.Running);
        }
        public static Task<string> ReadConsoleAsync()
        {
            return Task.Run(() => Console.ReadLine());
        }
    }
}
