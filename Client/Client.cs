using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using ProcessWrappers;

namespace Client
{
    class Client
    {
        static void Main(string[] args)
        {
            ClientWrapper myClient = new ClientWrapper(args);

            myClient.Start();
            if (myClient.useQueueIO)
                myClient.UpdatePostRoute("workRequest", "workComplete");

            string temp;    // Display the read text to the console 
            bool done = false;
            // Wait for 'sync message' from the server. 
            do
            {
                myClient.ClientMessage("[CLIENT] Wait for sync...");
                temp = myClient.ClientReadLine();
            }
            while (!temp.StartsWith("SYNC"));
            myClient.ClientMessage("[CLIENT] Received sync...");

            // Read the server data and echo to the console. 
            Task<string> readTask = myClient.ClientReadLineAsync();
            do
            {
                if (readTask.IsCompleted)
                {
                    temp = readTask.Result;
                    myClient.ClientMessage("[CLIENT] Echo: " + temp);
                    if (temp.StartsWith("QUIT"))
                        done = true;
                    else 
                        readTask = myClient.ClientReadLineAsync();
                }
                else
                {
                    System.Threading.Thread.Sleep(750);
                    myClient.ClientMessage("[CLIENT] Wait...");
                }
            }
            while (!done);

            myClient.ClientMessage("[CLIENT] Press Enter to Quit...");
            temp = myClient.ClientReadLine();

            myClient.ClientMessage("[CLIENT] quitting client process...");
            myClient.ClientMessage("QUIT"); // mark to the server that we're done...

            myClient.Cleanup();
        }

    }
}
