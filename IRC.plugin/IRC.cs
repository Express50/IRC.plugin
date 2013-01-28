using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.IO;
using EECloud.API;

namespace IRC.plugin
{
    [Plugin(Authors = new string[] { "Bass5098", "Express50" },
           Category = PluginCategory.Tool,
           ChatName = "IRC",
           Description = "IRC interface for executing commands in your EECloud plugin.",
           Version = "0.0.1")]
    public class IRC : Plugin<Player, IRC>
    {
        private static string server = "irc.rizon.net";
        private static int port = 6667;
        private static string nick = "RunBot";
        private static string channel = "#RunEE";

        static NetworkStream nstream = null;
        static StreamWriter writer = null;
        static StreamReader reader = null;

        private static TcpClient client = null;

        #region EECloud
        protected override void OnConnect()
        {

        }

        protected override void OnDisable()
        {

        }

        protected override void OnEnable()
        {

        }
        #endregion

        #region IRC Functions
        public static void Connect()
        {
            try
            {
                client = new TcpClient(server, port);
                nstream = client.GetStream();

                reader = new StreamReader(nstream);
                writer = new StreamWriter(nstream);

                Identify();
            }

            catch
            {

            }
        }

        private static void Disconnect()
        {
            SendData("QUIT");

            if (reader != null)
                reader.Close();

            if (writer != null)
                writer.Close();

            if (nstream != null)
                nstream.Close();

            if (client != null)
                client.Close();
        }

        private static void Identify()
        {
            SendData("USER", nick + " - " + server + " :" + nick);
            SendData("NICK", nick);
            //SendData("NICKSERV", "IDENTIFY");
        }

        private static void SendData(string cmd, string param = null)
        {
            if (param == null)
            {
                writer.WriteLine(cmd);
                writer.Flush();
            }

            else
            {
                writer.WriteLine(cmd + " " + param);
                writer.Flush();
            }
        }
        #endregion
    }
}
