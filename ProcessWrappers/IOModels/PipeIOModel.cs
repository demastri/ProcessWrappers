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
    internal class PipeIOModel : IOModel
    {
        Process clientProcess;
        string inPipeID;
        string outPipeID;

        StreamWriter StreamOut;
        StreamReader StreamIn;

        HostWrapper me;
        Task<string> pipeReaderTask;

        AnonymousPipeServerStream pipeServerIn;
        AnonymousPipeServerStream pipeServerOut;

        PipeStream pipeIn;
        PipeStream pipeOut;

        public PipeIOModel()
        {
            inPipeID = outPipeID = null;
            pipeReaderTask = null;
        }
        public PipeIOModel(string inPipeTag, string outPipeTag)
        {
            inPipeID = inPipeTag;
            outPipeID = outPipeTag;
            pipeReaderTask = null;
        }
        public void InitProcess(string procName)
        {
            if (procName != null)
            {
                clientProcess = new Process();
                clientProcess.StartInfo.FileName = procName;
            }
        }
        public void InitComms()
        {
            OpenPipes();
            if (clientProcess == null) return;

            clientProcess.StartInfo.Arguments = "Pipes " + outPipeID + " " + inPipeID;
            clientProcess.StartInfo.UseShellExecute = false;
        }
        public void StartProcess()
        {
            if (clientProcess == null) return;
            clientProcess.Start();
            pipeServerOut.DisposeLocalCopyOfClientHandle();
            pipeServerIn.DisposeLocalCopyOfClientHandle();
        }
        public void ConnectOutputComms()
        {
            // Read user input and send that to the client process. 
            StreamOut = new StreamWriter(pipeOut);
            StreamOut.AutoFlush = true;

            StreamIn = new StreamReader(pipeIn);
        }
        private void OpenPipes()
        {
            if (inPipeID == null)
            {
                pipeIn = pipeServerIn = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
                inPipeID = pipeServerIn.GetClientHandleAsString();
            }
            else
                pipeIn = new AnonymousPipeClientStream(PipeDirection.In, inPipeID);

            if (outPipeID == null)
            {
                pipeOut = pipeServerOut = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
                outPipeID = pipeServerOut.GetClientHandleAsString();
            }
            else
                pipeOut = new AnonymousPipeClientStream(PipeDirection.Out, outPipeID);
        }
        public bool CheckRead()
        {
            if (pipeReaderTask == null)
                pipeReaderTask = ReadStreamAsync(StreamIn);
            return pipeReaderTask.IsCompleted;
        }
        private Task<string> ReadStreamAsync(StreamReader sr)
        {
            return Task.Run(() => sr.ReadLine());
        }

        public string ReadResult()
        {
            if (pipeReaderTask != null && pipeReaderTask.IsCompleted)
            {
                string s = pipeReaderTask.Result;
                pipeReaderTask = null;
                return s;
            }
            return null;
        }
        public void Write(string msg)
        {
            StreamOut.WriteLine(msg);
            StreamOut.Flush();
        }
        public void Cleanup()
        {
            if (StreamOut != null)
                StreamOut.Dispose();
            if (StreamIn != null)
                StreamIn.Dispose();

            if (pipeOut != null)
                pipeOut.Dispose();
            if (pipeIn != null)
                pipeIn.Dispose();

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


        public void TestMe()
        {
            try
            {
                if (pipeIn != null)
                {
                    Console.WriteLine("[SERVER] Setting ReadMode to \"Message\".");
                    pipeIn.ReadMode = PipeTransmissionMode.Message;
                }
            }
            catch (NotSupportedException e)
            {
                Console.WriteLine("[SERVER] Execption:\n    " + e.Message);
            }

            if (pipeIn != null)
                Console.WriteLine("[SERVER] Using pipe io...Current TransmissionMode: " + pipeIn.TransmissionMode.ToString() + ".");
            else
                Console.WriteLine("[SERVER] Using stdio...");
        }
    }
}
