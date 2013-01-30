using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EECloud.API;
using EECloud.Host;
using IRC.plugin;

namespace IRC.host
{
    class Program
    {
        static void Main(string[] args)
        {
            EECloud.Host.EECloud.RunDebugMode(typeof(IRC.plugin.IRC));
        }
    }
}
