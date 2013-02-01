﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.IO;
using System.Text.RegularExpressions;
using EECloud.API;
using IRC.plugin.Utils;
using IRC.plugin.Parts;
using System.Threading;

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
        private Channel channel;

        private NetworkStream nstream = null;
        private StreamWriter writer = null;
        private StreamReader reader = null;

        private TcpClient client = null;

        private Thread ListenThread;

        public bool isConnected = false;

        #region EECloud
        protected override void OnConnect()
        {
            Cloud.Logger.Log(LogPriority.Debug, "Connected");
            this.Connect();
        }

        protected override void OnDisable()
        {
            Cloud.Logger.Log(LogPriority.Debug, "Disabled");
        }

        protected override void OnEnable()
        {
            Cloud.Logger.Log(LogPriority.Debug, "Enabled");
            server = "irc.rizon.net";
            port = 6667;
            nick = "RunBot";
            channel = new Channel("#RunEE");
            ListenThread = new Thread(Listen);
        }
        #endregion

        #region IRC Functions
        public void Connect()
        {
            try
            {
                Cloud.Logger.Log(LogPriority.Debug, "Attempting to establish a connection...");
                client = new TcpClient(server, port);
                nstream = client.GetStream();

                reader = new StreamReader(nstream);
                writer = new StreamWriter(nstream);

                isConnected = true;

                ListenThread.Start();

                Cloud.Logger.Log(LogPriority.Debug, "Successfully connected");

                Identify();

                Cloud.Logger.Log(LogPriority.Debug, "Successfully identified");

                //Cloud.Logger.Log(LogPriority.Debug, "Joining channel...");
                //SendData("JOIN", channel.Name);

                Cloud.Logger.Log(LogPriority.Debug, "Retrieving channel modes...");
                SendData("MODE", channel.Name);

                //SendData("NAMES", channel.Name);

                //SendData("PRIVMSG", channel.Name + " Test");
            }

            catch (Exception ex)
            {
                Cloud.Logger.Log(LogPriority.Debug, "Failed to connect to irc server");
                Cloud.Logger.Log(LogPriority.Debug, ex.Message);
            }
        }

        private void Disconnect()
        {
            if (isConnected)
            {
                SendData("QUIT");
            }

            ListenThread.Abort();

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
            try
            {
                Cloud.Logger.Log(LogPriority.Debug, "Attempting to identify");
                SendData("USER", nick + " - " + server + " :" + nick);
                SendData("NICK", nick);
                //SendData("NICKSERV", "IDENTIFY");
            }

            catch (Exception ex)
            {
                Cloud.Logger.Log(LogPriority.Debug, "Failed to identify");
            }
        }

        public void Listen()
        {
            while (isConnected)
            {
                ParseReceivedData(reader.ReadLine());
                Thread.Sleep(100);
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
            Cloud.Logger.Log(LogPriority.Debug, data);
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
                    case (int)Numerics.RPL_CHANNELMODEIS:
                        if (message[3] == channel.Name)
                        {
                            onChannelModeInit(message[4]);
                        }
                        break;
                    default:
                        break;
                }
                return;
            }

            //Parse command
            else if (message[0].StartsWith(":"))
            {
                for (int i = 0; i < message.Length; i++)
                {
                    if (message[i].StartsWith(":"))
                        message[i] = message[i].Remove(0, 1);
                }

                hostmask = message[0].Substring(message[0].IndexOf(':') + 1);

                if (hostmask.Contains('@') && hostmask.Contains('!'))
                {
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
        }

        private void onNames(string[] message)
        {
            if (message[4] == channel.Name)
            {
                for (int i = 6; i < message.Length; i++)
                {
                    if (!(channel.Users.Exists(
                        delegate(User user)
                        {
                            return user.Nick == message[i].Substring(1);
                        })))
                    {
                        channel.Users.Add(new User(message[i].Substring(1)));
                    }
                }
            }
        }

        private void onPrivMsg(string hostmask, string[] message)
        {
            User sender = ExtractUserInfo(hostmask);
            //message[3] = message[3].Remove(0, 1); //remove ':'

            if (message[2] == channel.Name && message[3].StartsWith("!"))
            {
                ExecuteCommand(message[3]);
            }
        }

        private void onNotice(string hostmask, string[] message)
        {
            User sender = ExtractUserInfo(hostmask);
        }

        private void onChannelModeInit(string modes)
        {
            channel.Modes = modes.Remove(0, 1); //remove '+' and set modes
        }

        private void onMode(string hostmask, string[] message)
        {
            User sender = ExtractUserInfo(hostmask);

            //Split into onChannelMode() and onUserMode()

            if (sender.Nick == nick) //your own mode
            {
                return;
            }

            else if (message[3].Contains('#') && message[3] == channel.Name) //channel mode
            {
                onChannelMode(sender, message[4]);
            }

            else //user mode
            {
                onUserMode(sender, GetUserByNick(message[4]), message[3]);
            }
        }

        private void onChannelMode(User sender, string modes)
        {
            bool addMode = false;
            for (int i = 0; i < modes.Length; i++)
            {
                if (modes[i] == '+') //switch to adding modes
                {
                    addMode = true;
                }

                else if (modes[i] == '-') //switch to removing modes
                {
                    addMode = false;
                }

                else
                {
                    if (addMode == true && !(channel.Modes.Contains(modes[i])))
                    {
                        channel.Modes.Insert(channel.Modes.Length, modes[i].ToString()); //insert new mode
                    }

                    else if (addMode == false && channel.Modes.Contains(modes[i]))
                    {
                        channel.Modes.Remove(channel.Modes.IndexOf(modes[i]), 1); //remove existing mode
                    }
                }
            }
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

        private void ExecuteCommand(string command)
        {
            command = command.Remove(0, 1); //remove cmd char
            command.ToLower();

            string[] cmdParts = command.Split(' ');

            switch (cmdParts[0])
            {
                case "hi":
                case "hello":
                    SendData("PRIVMSG", channel.Name + " Hey!");
                    break;
                case "quit":
                    Disconnect();
                    break;
                default:
                    break;
            }
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
            try
            {
                User user = new User();

                Match matches = Regex.Match(hostmask, @"^([A-Za-z0-9\-]+)!([A-Za-z0-9\-\~]+)\@([A-Za-z0-9\.\-]+)", RegexOptions.IgnoreCase);

                if (matches.Success == false)
                    throw new Exception();

                else
                {
                    user.Nick = matches.Groups[1].Value;
                    user.Realname = matches.Groups[2].Value;
                    user.Hostname = matches.Groups[3].Value;
                }

                return user;
            }

            catch (Exception ex)
            {
                Cloud.Logger.Log(LogPriority.Error, "Couldn't extract user info from hostmask: " + hostmask);
                return null;
            }
        }

        #endregion
    }
}
