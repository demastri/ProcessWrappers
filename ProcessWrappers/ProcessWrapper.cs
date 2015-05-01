using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using ProcessWrappers.IOModels;

namespace ProcessWrappers
{
    public class ProcessWrapper
    {
        // ### add logging
        /// /////////////////////////////
        /// notes on wrapper functionality
        /// 
        ///     the wrapper encapsulates an IOModel representing an external process to which the base is communicating
        ///     inputs can come from the IOModel or from this base's processing or hosting function
        ///     outputs will derive from the base's processing to either the IOModel or this base's hosting function
        /// 
        ///     the only difference between a client and a host, is that a host has the additional responsibility of setting
        ///     up the client that it's communicating with.
        /// 
        ///     the job of the wrapper is to take inputs from the "process" side and transfer them to the "IOModel" side
        ///     Period.
        /// 
        /// typical calling pattern:
        ///     ProcessWrapper hw = new HostWrapper();
        /// 
        ///     hw.Init(some process details);
        /// 
        ///     while( !hw.CheckProgress() != ending ) 
        ///     {
        ///         do some work;
        ///         hw.SendProcessMessage("somemessage")
        ///     }
        ///     hw.Cleanup()
        /// 
        ///     OnIncomingProcessData( string msg )    // received from the IOModel
        ///     {
        ///         taks some other action
        ///     }
        /// 
        /// within the process it should be straightforward:
        /// 
        ///     Constructor:
        ///         set up handler shell
        /// 
        ///     Init:
        ///         set up the client process if necessary
        ///         set up the appropriate IOModel and plumbing
        ///         send an init/setup message across the iomodel - should act as a reset as well
        /// 
        ///     CheckProgress:
        ///         place to monitor any sync processes
        ///         and ensure that the "other" side hasn't died or asked to stop
        ///         if data is available for the host, Will raise the ReceiveProcessDataEvent
        ///         if data is available for the process, will post to the appropriate IOModel
        /// 
        ///     SendProcessMessage:
        ///         add the string to the list of outgoing messages
        /// 
        /// internal to the base there's communication work:
        ///     Async data received from the IOModel should be added to the list of incoming messages to be pulled by the client
        /// 
        /// /////////////////////////////

        ////////////////////////////////////////////////////////////////
        public delegate void ProcessControlHandler(string nextDataElt);
        public enum ProcesssStatus { Init, Running, Ending, Stopped };

        public event ProcessControlHandler IncomingProcessData;

        public ProcessWrapper()
        {
            currentStatus = ProcesssStatus.Stopped;
            thisIO = null;
            outgoing = new List<string>();
            incoming = new List<string>();
        }

        public bool Init(string procLocation, IOType useIOType, string modelParams, ProcessControlHandler clientHandler)
        {
            currentStatus = ProcesssStatus.Init;

            // setup IOModel
            thisIO = IOModelHelper.IOModelFactory(useIOType, modelParams, IOModelDataHandler);

            thisIO.InitProcess(procLocation);
            thisIO.InitComms();
            thisIO.StartProcess();

            IncomingProcessData += clientHandler;

            currentStatus = ProcesssStatus.Running;
            return false;
        }
        public ProcesssStatus CheckProgress()
        {
            if (thisIO.CheckRead())
                thisIO.PostReadResult();

            while (incoming.Count > 0)
            {
                string s = incoming[0];
                incoming.RemoveAt(0);
                IncomingProcessData(s);
            }

            while (WaitingForWrite())
            {
                string s = outgoing[0];
                outgoing.RemoveAt(0);
                thisIO.Write(s);
            }

            return currentStatus;
        }
        public bool WaitingForWrite()
        {
            return outgoing.Count > 0;
        }
        public void Cleanup()
        {
            thisIO.Cleanup();
        }
        public bool SendProcessMessage(string msg)
        {
            outgoing.Add(msg);
            return true;
        }
        public ProcesssStatus UpdateStatus(ProcesssStatus newStatus)
        {
            return currentStatus = newStatus;
        }

        ////////////////////////////////////////////////////////////////
        ProcesssStatus currentStatus;
        IOModel thisIO;
        List<string> incoming;  // incoming from IOModel to host process
        List<string> outgoing;  // outgoing from host process to IOModel

        private void IOModelDataHandler(string msg)
        {
            incoming.Add(msg);
        }
    }
}
