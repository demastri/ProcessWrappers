using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.IO.Pipes;

namespace Client
{
    class Client
    {
        static PipeStream pipeClientOut;
        static StreamReader sr;
        static StreamWriter StreamOut;
        
        static void Main(string[] args)
        {
            if (args.Length > 1)
            {
                using (PipeStream pipeClient =
                    new AnonymousPipeClientStream(PipeDirection.In, args[0]))
                {
                    using (pipeClientOut =
                        new AnonymousPipeClientStream(PipeDirection.Out, args[1]))
                    {
                        using (sr = new StreamReader(pipeClient))
                        {
                            StreamOut = new StreamWriter(pipeClientOut);
                            // Read user input and send that to the client process. 
                            StreamOut.AutoFlush = true;

                            // Show that anonymous Pipes do not support Message mode. 
                            try
                            {
                                ClientMessage("[CLIENT] Setting ReadMode to \"Message\".");
                                pipeClient.ReadMode = PipeTransmissionMode.Message;
                            }
                            catch (NotSupportedException e)
                            {
                                ClientMessage("[CLIENT] Execption:\n    " + e.Message);
                            }

                            ClientMessage("[CLIENT] Current TransmissionMode: " + pipeClient.TransmissionMode.ToString() + ".");

                            // Display the read text to the console 
                            string temp;

                            // Wait for 'sync message' from the server. 
                            do
                            {
                                ClientMessage("[CLIENT] Wait for sync...");
                                temp = sr.ReadLine();
                            }
                            while (!temp.StartsWith("SYNC"));

                            ClientMessage("[CLIENT] Received sync...");

                            // Read the server data and echo to the console. 
                            do
                            {
                                //ClientMessage("[CLIENT] waiting for something...");
                                temp = sr.ReadLine();
                                //ClientMessage("[CLIENT] Got something...");
                                if (temp != null)
                                    ClientMessage("[CLIENT] Echo: " + temp);
                            }
                            while (!temp.StartsWith("quit"));
                            ClientMessage("[CLIENT] quitting client process...");
                        }
                    }
                }
            }
        }
        static void ClientMessage(string msg)
        {
            bool usePipe = true;
            if (!usePipe)
                Console.WriteLine("[CLIENTCONSOLE] " +msg);
            if (usePipe)
            {
                StreamOut.WriteLine(msg);
                //pipeClientOut.WaitForPipeDrain();
            }
        }
    }
}
