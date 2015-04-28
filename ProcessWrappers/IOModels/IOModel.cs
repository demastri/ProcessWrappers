using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using ProcessWrappers;

namespace ProcessWrappers.IOModels
{
    public enum IOType { PIPES = 0, StdIO = 1, QUEUES = 2, UNDEF = 3 };
    public class IOModelHelper
    {
        public static Dictionary<string, IOType> IOTypeDict = new Dictionary<string, IOType>() { { "pipes", IOType.PIPES }, { "stdio", IOType.StdIO}, { "queues", IOType.QUEUES} };
        public static IOType GetIOType(string s) { return IOTypeDict.ContainsKey(s.ToLower().Trim()) ? IOTypeDict[s.ToLower().Trim()] : IOType.UNDEF; } 
        internal static IOModel IOModelFactory(IOType ioType, HostWrapper hw, DataReceivedEventHandler handler)
        {
            switch (ioType)
            {
                case IOType.StdIO: return new StdIOModel(handler);
                case IOType.PIPES: return new PipeIOModel();
                case IOType.QUEUES: return new QueueIOModel(hw);
            }
            return null;
        }
        internal static IOModel IOModelFactory(IOType ioType, string[] args, DataReceivedEventHandler handler)
        {
            switch (ioType)
            {
                case IOType.StdIO: return new StdIOModel(handler);
                case IOType.PIPES: return new PipeIOModel( args[1], args[2] );
                case IOType.QUEUES: return new QueueIOModel(null);
            }
            return null;
        }
    }

    internal interface IOModel
    {
        void InitProcess(string procName);
        void InitComms();
        void StartProcess();
        void ConnectOutputComms();
        bool CheckRead();
        string ReadResult();
        void Write(string msg);
        void Cleanup();
    }
}
