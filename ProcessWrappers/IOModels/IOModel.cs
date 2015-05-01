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
        public static Dictionary<IOType, string> IOTypeParam = new Dictionary<IOType, string>() { { IOType.PIPES, "pipes" }, { IOType.StdIO, "stdio" }, { IOType.QUEUES, "queues" } };
        public static Dictionary<string, IOType> IOTypeDict = new Dictionary<string, IOType>() { { "pipes", IOType.PIPES }, { "stdio", IOType.StdIO }, { "queues", IOType.QUEUES } };
        public static IOType GetIOType(string s) { return IOTypeDict.ContainsKey(s.ToLower().Trim()) ? IOTypeDict[s.ToLower().Trim()] : IOType.UNDEF; }
        
        internal static IOModel IOModelFactory(IOType ioType, string args, ProcessWrapper.ProcessControlHandler handler)
        {
            IOModel outModel = null;
            switch (ioType)
            {
                case IOType.StdIO: outModel = new StdIOModel(); break;
                case IOType.PIPES: outModel = new PipeIOModel(); break;
                case IOType.QUEUES: outModel = new QueueIOModel(); break;
            }
            if (outModel != null)
            {
                outModel.BaseInit(args);
                outModel.SetReadHandler(handler);
            }
            return outModel;
        }
    }

    internal interface IOModel
    {
        void BaseInit(string modelParams);

        void SetReadHandler(ProcessWrapper.ProcessControlHandler clientHandler);
        void InitProcess(string procName);
        void InitComms();
        void StartProcess();
        bool CheckRead();
        void PostReadResult();

        void Write(string msg);
        void Cleanup();
    }
}
