using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using EECloud.API;

namespace IRC.plugin
{
    [Plugin(Authors = new string[] { "Bass5098", "Express50" },
           Category = PluginCategory.Tool,
           ChatName = "Editor",
           Version = "0.0.1")]
    public class IRC : Plugin<Player, IRC>
    {
        private string server = "irc.rizon.net";
        int port = 6667;
        string nick = "RunBot";
        string channel = "#RunEE";

        NetworkStream ns = null;

        TcpClient irc = null;
    }
}
