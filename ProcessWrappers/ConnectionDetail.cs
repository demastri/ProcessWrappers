using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QueueCommon
{
    public class ConnectionDetail
    {
        public string host;
        public int port;

        public string exchName;
        public string exchType;

        public string queueName;
        public List<string> routeKeys;

        public string user;
        public string pass;
        public string Uri { get { return "amqp://" + user + ":" + pass + "@" + host + ":" + port.ToString(); } } //        amqp://user:pass@hostName:port/vhost"; 
        public ConnectionDetail(
            string xhost, int xPort, string xName, string xType, string uid, string pwd)
        {
            host = xhost;
            port = xPort;

            exchName = xName;
            exchType = xType;

            queueName = "";
            routeKeys = new List<string>();

            user = uid;
            pass = pwd;
        }
        public ConnectionDetail(
            string xhost, int xPort, string xName, string xType, string qName, List<string> rKeys, string uid, string pwd)
        {
            host = xhost;
            port = xPort;

            exchName = xName;
            exchType = xType;

            queueName = qName;
            routeKeys = new List<string>();
            foreach (string s in rKeys)
                if( s != "" )
                    routeKeys.Add(s);

            user = uid;
            pass = pwd;
        }
        public ConnectionDetail(
            string xhost, int xPort, string xName, string xType, string qName, string rKey, string uid, string pwd)
        {
            host = xhost;
            port = xPort;

            exchName = xName;
            exchType = xType;

            queueName = qName;
            routeKeys = new List<string>();
            if( rKey != "" )
                routeKeys.Add(rKey);

            user = uid;
            pass = pwd;
        }
        public ConnectionDetail()
        {
            host = "localhost";
            port = 5672;

            exchName = System.Guid.NewGuid().ToString();
            exchType = "direct";

            queueName = "";
            routeKeys = new List<string>();

            user = "guest";
            pass = "guest";
        }
        public ConnectionDetail Update(ConnectionDetail connDetail)
        {
            ConnectionDetail outConn = new ConnectionDetail();
            outConn.host = (connDetail == null || connDetail.host == "") ? host : connDetail.host;
            outConn.port = (connDetail == null || connDetail.port == -1) ? port : connDetail.port;
            outConn.exchName = (connDetail == null || connDetail.exchName == "") ? exchName : connDetail.exchName;
            outConn.exchType = (connDetail == null || connDetail.exchType == "") ? exchType : connDetail.exchType;
            outConn.queueName = (connDetail == null || connDetail.queueName == "") ? queueName : connDetail.queueName;
            outConn.host = (connDetail == null || connDetail.host == "") ? host : connDetail.host;
            if (connDetail != null && connDetail.routeKeys != null && connDetail.routeKeys.Count() > 0)
                foreach (string s in connDetail.routeKeys)
                    outConn.routeKeys.Add(s);
            else
                foreach (string s in routeKeys)
                    outConn.routeKeys.Add(s);
            outConn.user = (connDetail == null || connDetail.user == "") ? user : connDetail.user;
            outConn.pass = (connDetail == null || connDetail.pass == "") ? pass : connDetail.pass;

            return outConn;
        }
        public ConnectionDetail Copy()
        {
            return Update(null);
        }
        public ConnectionDetail UpdateQueueDetail(string name, List<string> keys)
        {
            ConnectionDetail outConn = Copy();
            outConn.queueName = name;
            if (keys != null)
            {
                outConn.routeKeys.Clear();
                foreach (string s in keys)
                    outConn.routeKeys.Add(s);
            }
            return outConn;
        }
    }
}
