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
        Process clientProcess;
        StreamWriter StreamOut;
        DataReceivedEventHandler thisHandler;

        public StdIOModel(DataReceivedEventHandler handler)
        {
            thisHandler = handler;
        }
        public void InitProcess(string procName)
        {
            clientProcess = new Process();
            clientProcess.StartInfo.FileName = procName;
        }
        public void InitComms()
        {
            clientProcess.StartInfo.Arguments = "Stdio ";
            clientProcess.StartInfo.UseShellExecute = false;

            clientProcess.StartInfo.RedirectStandardInput = true;
            clientProcess.StartInfo.RedirectStandardOutput = true;
            clientProcess.StartInfo.RedirectStandardError = true;
            clientProcess.StartInfo.CreateNoWindow = false;
            clientProcess.OutputDataReceived += thisHandler;
        }
        public void StartProcess()
        {
            clientProcess.Start();
            clientProcess.BeginOutputReadLine();
        }
        public void ConnectOutputComms()
        {
            StreamOut = clientProcess.StandardInput;
        }
        public bool CheckRead()
        {
            return false;   // noop
        }
        public string ReadResult()
        {
            return null;    // noop
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
