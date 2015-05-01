using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.IO.Pipes;
using System.Diagnostics;

namespace ProcessWrappers.IOModels
{
    internal class StdIOModel : IOModel
    {
        /// when a message comes in from the redirected stdin, add it to incoming via client handler
        /// when a request to write comes in, add it to outgoing

        /// input from Stdin -> outgoing -> Write to IOModel.stdin -> PWhandler -> incoming -> client handler
        /// processwrite from client -> outgoing -> write to proc.stdout -> treated as stdin on the server and echoed??


        Process clientProcess;
        StreamWriter StreamOut; // this is the output from the process -> other side of ModelIO
        StreamReader StreamIn;  // this is the input from the other side of the modelIO -> the process handlers
        ProcessWrapper.ProcessControlHandler thisHandler;
        Task<string> thisReadTask;

        public StdIOModel()
        {
            thisReadTask = null;
            clientProcess = null;
        }
        public void InitProcess(string procName)
        {
            if (procName == null) return;
            clientProcess = new Process();
            clientProcess.StartInfo.FileName = procName;
        }
        public void BaseInit(string modelParams)
        {
        }
        public void SetReadHandler(ProcessWrapper.ProcessControlHandler clientHandler)
        {
            thisHandler = clientHandler;
        }
        public void InitComms()
        {
            if (clientProcess != null)
            {
                clientProcess.StartInfo.Arguments = IOModelHelper.IOTypeParam[IOType.StdIO];
                clientProcess.StartInfo.UseShellExecute = false;

                clientProcess.StartInfo.RedirectStandardInput = true;
                clientProcess.StartInfo.RedirectStandardOutput = true;
                clientProcess.StartInfo.RedirectStandardError = true;
                clientProcess.StartInfo.CreateNoWindow = true;
                clientProcess.OutputDataReceived += LocalHandler;
            }
        }
        public void StartProcess()
        {
            if (clientProcess != null)
                clientProcess.Start();
            ConnectOutputComms();
        }
        public bool CheckRead()
        {
            if (thisReadTask == null)
                thisReadTask = ClientReadLineAsync();
            return (thisReadTask != null && thisReadTask.IsCompleted);
        }
        public string ReadResult()
        {
            string outStr = null;
            if (CheckRead())
            {
                outStr = thisReadTask.Result;
                thisReadTask = null;
            }
            return outStr;
        }
        public void PostReadResult()
        {
            string s = ReadResult();
            if (s != null)
                thisHandler(s);
        }
        private void ConnectOutputComms()
        {
            if (clientProcess == null)
            {
                StreamIn = new StreamReader(Console.OpenStandardInput());
                StreamOut = new StreamWriter(Console.OpenStandardOutput());
            }
            else
            {
                StreamIn = clientProcess.StandardOutput;
                StreamOut = clientProcess.StandardInput;
            }
        }
        string lastMsg = "";
        public void Write(string msg)
        {
            StreamOut.WriteLine(lastMsg = msg);
            StreamOut.Flush();
        }
        public void Cleanup()
        {
            if (StreamOut != null)
                StreamOut.Dispose();
            if (StreamIn != null)
                StreamIn.Dispose();

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

        private void LocalHandler(object sender, DataReceivedEventArgs e)
        {
            thisHandler(e.Data);
        }
        public Task<string> ClientReadLineAsync()
        {
            return Task.Run(() => StreamIn.ReadLine());
        }
    }
}
