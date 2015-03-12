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
        static void Main(string[] args)
        {
            string myExeLoc = "C:\\Projects\\JPD\\BBRepos\\Chess\\PipesCommsExamples\\Client\\bin\\Debug\\Client.exe";
            myExeLoc = "C:\\Projects\\JPD\\BBRepos\\Chess\\engines\\stockfish\\stockfish_5_32bit.exe";
            bool useStdIO = true;

            HostWrapper myHost = new HostWrapper(myExeLoc, useStdIO, ProcessControl);

            myHost.Start();

            //myHost.WriteToClient("SYNC");
            myHost.WriteToClient("uci");

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
            } while (myHost.CheckProgress() != HostWrapper.IsEnding );
            myHost.WriteToClient("quit");

            myHost.Cleanup();
            Console.WriteLine("[SERVER] Client quit. Server terminating.");
        }

        public static int ProcessControl(HostWrapper thisHost)
        {
            System.Threading.Thread.Sleep(250);
            while (thisHost.incoming.Count > 0)
            {
                string s = thisHost.incoming[0];
                Console.WriteLine(" From Client: <"+s+">");
                thisHost.incoming.RemoveAt(0);
                if (s.StartsWith("uciok"))
                    return HostWrapper.IsEnding;
            }
            return HostWrapper.IsRunning;
        }

    }
}
