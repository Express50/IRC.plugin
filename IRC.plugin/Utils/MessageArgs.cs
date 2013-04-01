using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRC.plugin.Utils
{
    public class MessageArgs : EventArgs
    {
        public readonly string hostmask;
        public readonly string[] message;

        public MessageArgs(string Hostmask, string[] Message)
        {
            hostmask = Hostmask;
            message = Message;
        }
    }
}
