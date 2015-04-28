using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.IO.Pipes;
using System.Diagnostics;

using QueueCommon;

namespace ProcessWrappers.IOModels
{
    public class QueueIOModel : IOModel
    {
        Process clientProcess;
        public QueueingModel queueClient;
        List<string> postRoutes;

        string paramString = "";

        // this really should only tell the client what it should be listening to, right.  
        // TypeID and clientID are pretty app-specific
        // ### and most of the actual IO should be deferred to this class, 
        // (and mirrored on the client side...) if we're going to this trouble...
        public QueueIOModel(HostWrapper hw)
        {
            ConnectionDetail cd = hw.thisConnectionDetail;
            postRoutes = new List<string>();
            paramString = cd.exchName + "|" + cd.host + "|" + cd.port + "|" + cd.user + "|" + cd.pass;
            foreach (string s in hw.hostPostKeys)
            {
                paramString += "|" + s;
                postRoutes.Add(s);
            }
            if (true)  // ### do actually tell it where we're listening (and have it care...)
            {
                paramString += "|#";
                foreach (string s in cd.routeKeys)
                {
                    if (s.Trim() == "")
                        continue;
                    paramString += "|" + s;
                }
            }

            List<string> routes = new List<string>();
            QueueCommon.ConnectionDetail listenDetail = cd.UpdateQueueDetail("ServerQueue", cd.routeKeys);
            queueClient = new QueueingModel(listenDetail);
        }
        public void InitProcess(string procName)
        {
            if (procName == null)
            {
                clientProcess = null;
            }
            else
            {
                clientProcess = new Process();
                clientProcess.StartInfo.FileName = procName;
            }
        }
        public void InitComms()
        {
            if (clientProcess == null) return;

            // need to be able to init the queue connection detail for the process here as arguments
            // exch, port, uid, pwd, typeid
            clientProcess.StartInfo.Arguments = "Queues " + paramString;
            clientProcess.StartInfo.UseShellExecute = false;
        }
        public void StartProcess()
        {
            if (clientProcess == null) return;
            // there is no other activity...
            clientProcess.Start();
        }
        public void ConnectOutputComms()
        {
            // there is no other activity...
        }

        public bool CheckRead()
        {
            return !queueClient.QueueEmpty();
        }
        public string ReadResult()
        {
            if (CheckRead())
                return queueClient.ReadMessageAsString();
            return null;
        }
        public void Write(string msg)
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
            queueClient.PostMessage(msg, postRoutes[q]);
        }
        public void Cleanup()
        {
            // ### should actually unbind here as well...
            queueClient.CloseConnections();
            if (clientProcess != null)
            {
                try
                {
                    clientProcess.Kill();
                    clientProcess.WaitForExit();
                    clientProcess.Close();
                }
                catch (Exception e) { }
            }
        }
    }
}
