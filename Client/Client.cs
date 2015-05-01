using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using ProcessWrappers;
using ProcessWrappers.IOModels;

namespace Client
{
    class Client
    {
        static ProcessWrapper myClient;
        enum ClientState { Init, Running, WaitforEndAck, Ending };
        static ClientState myState = ClientState.Init;
        static void Main(string[] args)
        {
            myState = ClientState.Init;

            string paramStr = args.Count() >= 2 ? args[1] : "";

            myClient = new ProcessWrapper();
            myClient.Init(null, IOModelHelper.GetIOType(args[0]), paramStr, ProcessControl);
            myClient.UpdateStatus(ProcessWrapper.ProcesssStatus.Running);

            Console.WriteLine("In the client...console");
            myClient.SendProcessMessage("In the client...msg");
            for (int i = 10; i > 0; i--)
                myClient.SendProcessMessage("[CLIENT] Wait for sync..."+i.ToString());
            while (myState != ClientState.Ending && myClient.CheckProgress() != ProcessWrapper.ProcesssStatus.Ending)
            {
                System.Threading.Thread.Sleep(750);
                if( myState == ClientState.Running )
                    myClient.SendProcessMessage("[CLIENT] Wait...");
            }
            myClient.SendProcessMessage("[CLIENT] quitting client process...");
            myClient.SendProcessMessage("QUIT"); // mark to the server that we're done...
            myClient.UpdateStatus(ProcessWrapper.ProcesssStatus.Ending);

            while (myClient.WaitingForWrite())
                myClient.CheckProgress();

            myClient.Cleanup();
        }

        public static void ProcessControl(string s)
        {
            switch (myState)
            {
                case ClientState.Init:
                    if (s.StartsWith("SYNC"))
                    {
                        myClient.SendProcessMessage("[CLIENT] Received sync...");
                        myState = ClientState.Running;
                    }
                    break;
                case ClientState.Running:
                    myClient.SendProcessMessage("[CLIENT] Echo: " + s);
                    if (s.StartsWith("QUIT"))
                    {
                        myClient.SendProcessMessage("[CLIENT] Press Enter to Quit...");
                        myState = ClientState.WaitforEndAck;
                    }
                    break;
                case ClientState.WaitforEndAck:
                    myState = ClientState.Ending;
                    break;
                case ClientState.Ending:
                    break;
            }
            if (myState == ClientState.Ending)
                myClient.UpdateStatus(ProcessWrapper.ProcesssStatus.Ending);
            else
                myClient.UpdateStatus(ProcessWrapper.ProcesssStatus.Running);
        }

    }
}
