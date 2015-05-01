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
        ProcessWrapper.ProcessControlHandler thisHandler;
        ConnectionDetail thisConnectionDetail;

        string paramString = "";

        // this really should only tell the client what it should be listening to, right.  
        // TypeID and clientID are pretty app-specific
        // and most of the actual IO should be deferred to this class, 
        // (and mirrored on the client side...) if we're going to this trouble...
        public QueueIOModel()
        {
            thisHandler = null;
        }
        public void BaseInit(string modelParams)
        {
            string[] tokens = modelParams.Split('|');
            thisConnectionDetail = new ConnectionDetail(tokens[1], Convert.ToInt32(tokens[2]), tokens[0], "topic", tokens[3], tokens[4]);
            postRoutes = new List<string>();
            paramString = thisConnectionDetail.exchName + "|" + thisConnectionDetail.host + "|" + thisConnectionDetail.port + "|" + thisConnectionDetail.user + "|" + thisConnectionDetail.pass;
            int i = 5;
            string postSet = "";
            for (; i < tokens.Length && tokens[i] != "#"; i++)
            {
                thisConnectionDetail.routeKeys.Add(tokens[i]);
                postSet += "|" + tokens[i];
            }
            string listenSet = "";
            i++;
            for (; i < tokens.Length; i++)
            {
                postRoutes.Add(tokens[i]);
                listenSet += "|" + tokens[i];
            }
            paramString += listenSet + "|#" + postSet;

            Console.WriteLine(thisConnectionDetail.routeKeys.Count.ToString() + " listen routes, " + postRoutes.Count.ToString() + " post routes");
        }
        public void SetReadHandler(ProcessWrapper.ProcessControlHandler clientHandler)
        {
            thisHandler = clientHandler;
        }

        public void InitProcess(string procName)
        {
            string queueName = "ClientQueue";
            clientProcess = null;
            if (procName != null)
            {
                queueName = "ServerQueue";
                clientProcess = new Process();
                clientProcess.StartInfo.FileName = procName;
            }
            QueueCommon.ConnectionDetail listenDetail = thisConnectionDetail.UpdateQueueDetail(queueName, thisConnectionDetail.routeKeys);
            queueClient = new QueueingModel(listenDetail);
        }
        public void InitComms()
        {
            if (clientProcess == null) return;

            // need to be able to init the queue connection detail for the process here as arguments
            // exch, port, uid, pwd, typeid
            clientProcess.StartInfo.Arguments = IOModelHelper.IOTypeParam[IOType.QUEUES]+ " " + paramString;
            clientProcess.StartInfo.UseShellExecute = false;
        }
        public void StartProcess()
        {
            if (clientProcess == null) return;
            // there is no other activity...
            clientProcess.Start();

            ConnectOutputComms();
        }

        private void ConnectOutputComms()
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
        public void PostReadResult()
        {
            string s = ReadResult();
            if (s != null)
                thisHandler(s);
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
                catch (Exception) { }
            }
        }
    }
}
