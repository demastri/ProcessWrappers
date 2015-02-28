using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.IO.Pipes;
using System.Diagnostics;

namespace Server
{
    class Server
    {
        static Process pipeClient;
        static AnonymousPipeServerStream pipeServerOut;
        static StreamWriter StreamOut;

        static AnonymousPipeServerStream pipeServerIn;
        static StreamReader StreamIn;

        static string localBuffer;
        static string processBuffer;

        static void Main(string[] args)
        {
            InitProcess("C:\\Projects\\JPD\\BBRepos\\Chess\\PipesCommsExamples\\Client\\bin\\Debug\\Client.exe");
            OpenPipes();
            ConnectPipeToProcess();
            CreateStreamOnPipes();

            try
            {
                // Send a 'sync message' and wait for client to receive it.
                WriteToStream("SYNC");

                bool running = true;
                while (running)
                {
                    running = CheckLocalIO();
                    CheckProcessIO();
                    running = HandleAllIO() && running;
                }
            }
            // Catch the IOException that is raised if the pipe is broken or disconnected. 
            catch (IOException e)
            {
                Console.WriteLine("[SERVER] Error: {0}", e.Message);
            }

            CleanupStreams();
            CleanupPipes();
            CleanupProcess();
        }

        static public void InitProcess(string procName)
        {
            pipeClient = new Process();
            pipeClient.StartInfo.FileName = procName;

        }

        static public void OpenPipes()
        {
            pipeServerOut =
               new AnonymousPipeServerStream(PipeDirection.Out,
               HandleInheritability.Inheritable);

            // Show that anonymous pipes do not support Message mode. 
            try
            {
                Console.WriteLine("[SERVER] Setting ReadMode to \"Message\".");
                pipeServerOut.ReadMode = PipeTransmissionMode.Message;
            }
            catch (NotSupportedException e)
            {
                Console.WriteLine("[SERVER] Exception:\n    {0}", e.Message);
            }

            Console.WriteLine("[SERVER] Current TransmissionMode: {0}.",
                pipeServerOut.TransmissionMode);

            pipeServerIn =
               new AnonymousPipeServerStream(PipeDirection.In,
               HandleInheritability.Inheritable);
      
        }
        static public void ConnectPipeToProcess()
        {
            // Pass the client process a handle to the server.
            pipeClient.StartInfo.Arguments =
                pipeServerOut.GetClientHandleAsString() + " " + pipeServerIn.GetClientHandleAsString();

            pipeClient.StartInfo.UseShellExecute = false;
            pipeClient.Start();

            pipeServerOut.DisposeLocalCopyOfClientHandle();
            pipeServerIn.DisposeLocalCopyOfClientHandle();
        }
        static public void CreateStreamOnPipes()
        {
            StreamOut = new StreamWriter(pipeServerOut);
            // Read user input and send that to the client process. 
            StreamOut.AutoFlush = true;

            StreamIn = new StreamReader(pipeServerIn);
        }

        static public void WriteToStream(string s)
        {
            //Console.WriteLine("[SERVER] Writing: <"+s+">.");
            StreamOut.WriteLine(s);
            //pipeServerOut.WaitForPipeDrain();
        }

        static Task<string> readAsync = null;
        static async public void CheckProcessIO()
        {
            if( readAsync == null )
                readAsync = StreamIn.ReadLineAsync();

            if( readAsync.IsCompleted ) 
            {
                processBuffer = await readAsync;
                readAsync.Dispose();
                readAsync = null;
            }
            else
                processBuffer = null;
        }
        static public bool CheckLocalIO()
        {
            if (Console.KeyAvailable)
            {
                localBuffer = Console.ReadLine();
            }
            return true;
        }
        static public bool HandleAllIO()
        {
            if (localBuffer != null)
            {
                // Send the console input to the client process.
                WriteToStream(localBuffer);

                if (localBuffer == "quit")
                    return false;
                Console.Write("[SERVER] Enter text: ");
                localBuffer = null;
            }
            if (processBuffer != null)
            {
                Console.WriteLine("");
                Console.WriteLine("   [CLIENT] from process: " + processBuffer);
                Console.Write("[SERVER] Enter text: ");
                processBuffer = null;
            }
            return true;
        }
        static public void CleanupStreams()
        {
            if (StreamOut != null)
                StreamOut.Dispose();
            if (StreamIn != null)
                StreamIn.Dispose();
        }
        static public void CleanupPipes()
        {
            if (pipeServerOut != null)
                pipeServerOut.Dispose();
        }
        static public void CleanupProcess()
        {
            if (pipeClient != null)
            {
                pipeClient.WaitForExit();
                pipeClient.Close();
                Console.WriteLine("[SERVER] Client quit. Server terminating.");
            }
        }
    }
}
