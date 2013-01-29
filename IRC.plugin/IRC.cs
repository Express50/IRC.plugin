using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.IO;
using EECloud.API;
using IRC.plugin.Utils;
using IRC.plugin.Parts;

namespace IRC.plugin
{
    [Plugin(Authors = new string[] { "Bass5098", "Express50" },
           Category = PluginCategory.Admin,
           ChatName = "IRC",
           Description = "IRC interface for executing commands in your EECloud plugin.",
           Version = "0.0.1")]
    public class IRC : Plugin<Player, IRC>
    {
        private string server;
        private int port;
        private string nick;
        private Channel channel = new Channel();

        private NetworkStream nstream = null;
        private StreamWriter writer = null;
        private StreamReader reader = null;

        private TcpClient client = null;

        public bool isConnected = false;

        public IRC(string Nick, string ChannelName, int Port = 6667, string Server = "irc.rizon.net")
        {
            server = Server;
            port = Port;
            nick = Nick;
            channel.Name = ChannelName;
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
        public void Connect()
        {
            try
            {
                client = new TcpClient(server, port);
                nstream = client.GetStream();

                reader = new StreamReader(nstream);
                writer = new StreamWriter(nstream);

                isConnected = true;

                Identify();

                SendData("JOIN", channel.Name);
                Listen();
            }

            catch
            {

            }
        }

        private void Disconnect()
        {
            if (isConnected)
            {
                SendData("QUIT");
            }

            if (reader != null)
                reader.Close();

            if (writer != null)
                writer.Close();

            if (nstream != null)
                nstream.Close();

            if (client != null)
                client.Close();
        }

        private void Identify()
        {
            SendData("USER", nick + " - " + server + " :" + nick);
            SendData("NICK", nick);
            //SendData("NICKSERV", "IDENTIFY");
        }

        public void Listen()
        {
            while (isConnected)
            {
                ParseReceivedData(reader.ReadLine());
            }
        }

        private void SendData(string cmd, string param = null)
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

        private void ParseReceivedData(string data)
        {
            Console.WriteLine(data);
            string[] message = data.Split(' ');
            string hostmask;

            //Parse ping
            if (message[0] == "PING")
            {
                SendData("PONG", message[1]);
                return;
            }

            //Parse numerics
            int numeric;
            if (int.TryParse(message[1], out numeric))
            {
                switch (numeric)
                {
                    case (int)Numerics.RPL_NAMREPLY:
                        onNames(message);
                        break;
                    default:
                        break;
                }
                return;
            }

            //Parse command
            else if (message[0].StartsWith(":"))
            {
                hostmask = message[0].Substring(message[0].IndexOf(':') + 1);

                switch (message[1])
                {
                    case "PRIVMSG":
                        onPrivMsg(hostmask, message);
                        break;
                    case "NOTICE":
                        onNotice(hostmask, message);
                        break;
                    case "MODE":
                        onMode(hostmask, message);
                        break;
                    case "KICK":
                        onKick(hostmask, message);
                        break;
                    case "JOIN":
                        onJoin(hostmask, message);
                        break;
                    case "PART":
                        onPart(hostmask, message);
                        break;
                    default:
                        break;
                }
            }
        }

        private void onNames(string[] message)
        {
            if (message[2] == channel.Name)
            {
                for (int i = 3; i < message.Length; i++)
                {
                    if (channel.Users.Exists(
                        delegate(User user)
                        {
                            return user.Nick == message[i].Substring(1);
                        }))
                    {
                        channel.Users.Add(new User(message[i].Substring(1)));
                    }
                }
            }
        }

        private void onPrivMsg(string hostmask, string[] message)
        {
            User sender = ExtractUserInfo(hostmask);

            if (message[2] == channel.Name && message[3].StartsWith("!"))
            {
                //ExecuteCommand(message[3]);
            }
        }

        private void onNotice(string hostmask, string[] message)
        {
            User sender = ExtractUserInfo(hostmask);
        }

        private void onMode(string hostmask, string[] message)
        {
            User sender = ExtractUserInfo(hostmask);

            //Split into onChannelMode() and onUserMode()

            if (message.Length == 5)
            {
                onUserMode(sender, GetUserByNick(message[4]), message[3]);
            }

            else if (message.Length == 4)
            {
                onChannelMode(sender, message[3]);
            }
        }

        private void onChannelMode(User sender, string modes)
        {
            channel.Modes = modes;
        }

        private void onUserMode(User sender, User target, string modes)
        {

        }

        private void onKick(string hostmask, string[] message)
        {
            User sender = ExtractUserInfo(hostmask);
        }

        private void onJoin(string hostmask, string[] message)
        {
            User sender = ExtractUserInfo(hostmask);

            channel.Users.Add(sender);
        }

        private void onPart(string hostmask, string[] message)
        {
            User sender = ExtractUserInfo(hostmask);

            if (channel.Users.Contains(sender))
            {
                channel.Users.Remove(sender);
            }
        }

        private void ExecuteCommand(string data)
        {

        }

        private User GetUserByNick(string nick)
        {
            for (int i = 0; i < channel.Users.Count; i++)
            {
                if (channel.Users[i].Nick == nick)
                    return channel.Users[i];
            }

            return null;
        }

        private User GetUserByHostname(string hostname)
        {
            for (int i = 0; i < channel.Users.Count; i++)
            {
                if (channel.Users[i].Hostname == hostname)
                    return channel.Users[i];
            }

            return null;
        }

        private User ExtractUserInfo(string hostmask)
        {
            User user = new User();

            user.Nick = hostmask.Substring(hostmask.IndexOf(':') + 1, hostmask.IndexOf('!'));
            user.Realname = hostmask.Substring(hostmask.IndexOf('!') + 1, hostmask.IndexOf('@'));
            user.Hostname = hostmask.Substring(hostmask.IndexOf('@') + 1);

            return user;
        }

        #endregion
    }
}
