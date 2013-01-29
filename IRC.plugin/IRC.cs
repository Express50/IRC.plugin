﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.IO;
using EECloud.API;
using IRC.plugin.Utils;

namespace IRC.plugin
{
    [Plugin(Authors = new string[] { "Bass5098", "Express50" },
           Category = PluginCategory.Admin,
           ChatName = "IRC",
           Description = "IRC interface for executing commands in your EECloud plugin.",
           Version = "0.0.1")]
    public class IRC : Plugin<Player, IRC>
    {
        private static string server;
        private static int port;
        private static string nick;
        private static string channel;

        static NetworkStream nstream = null;
        static StreamWriter writer = null;
        static StreamReader reader = null;

        private static TcpClient client = null;

        private static bool isConnected = false;

        public IRC(string Nick, string Channel, int Port = 6667, string Server = "irc.rizon.net")
        {
            server = Server;
            port = Port;
            nick = Nick;
            channel = Channel;
        }

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

                isConnected = true;

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

        public void Listen()
        {
            while (isConnected)
            {
                if (nstream.DataAvailable)
                {

                }
            }
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

        private static void ParseMessage(string data)
        {
            string[] message = data.Split(':');
            User sender = new User();

            if (message[0] == "PING")
            {
                SendData("PONG", message[1]);
            }

            else if (message[0].StartsWith(":"))
            {
                sender.Nick = message[0].Substring(message[0].IndexOf(':') + 1, message[0].IndexOf('!'));
                sender.Realname = message[0].Substring(message[0].IndexOf('!') + 1, message[0].IndexOf('@'));
                sender.Hostname = message[0].Substring(message[0].IndexOf('@') + 1);

                switch (message[1])
                {
                    case "PRIVMSG":
                        if (message[2] == channel && message[3].StartsWith("!"))
                        {
                            ExecuteCommand(message[3].Substring(message[3].IndexOf(':') + 1);
                        }
                        break;
                    case "NOTICE":
                        break;
                    case "MODE":
                        break;
                    case "KICK":
                        break;
                    case "JOIN":
                        break;
                    case "PART":
                        break;
                }
            }
        }

        private static void ExecuteCommand(string command)
        {

        }

        #endregion
    }
}
