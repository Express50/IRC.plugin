using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
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

        Parts.Commands Commands;

        #region EECloud
        protected override void OnConnect()
        {
            Cloud.Logger.Log(LogPriority.Debug, "Connected");
            CommandManager.Load(this);
            this.Connect();
        }

        protected override void OnDisable()
        {
            Cloud.Logger.Log(LogPriority.Debug, "Disabled");
        }

        protected override void OnEnable()
        {
            Commands = EnablePart<Parts.Commands>();
            Cloud.Logger.Log(LogPriority.Debug, "Enabled");
            server = "irc.rizon.net";
            port = 6667;
            nick = "RunBot";
            channel = new Channel("#RunEE");
            ListenThread = new Thread(Listen);
        }
        #endregion

        #region IRC Functions

        /// <summary>
        /// Try to connect to the IRC channel.
        /// </summary>
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

                Cloud.Logger.Log(LogPriority.Info, "Successfully connected");

                Identify();

                Cloud.Logger.Log(LogPriority.Info, "Successfully identified");

                JoinChannel();

                SendData("WHO", channel.Name);
            }

            catch (Exception ex)
            {
                Cloud.Logger.Log(LogPriority.Error, "Failed to connect to irc server");
                Cloud.Logger.Log(LogPriority.Debug, ex.Message);
            }
        }

        /// <summary>
        /// Disconnect from the IRC channel and close all open streams.
        /// </summary>
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

        /// <summary>
        /// Set the name of the connecting user.
        /// </summary>
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

        /// <summary>
        /// Join the designated channel
        /// </summary>
        private void JoinChannel()
        {
            try
            {
                Cloud.Logger.Log(LogPriority.Info, "Joining channel...");
                SendData("JOIN", channel.Name);
            }

            catch (Exception ex)
            {
                Cloud.Logger.Log(LogPriority.Error, "Couldn't join channel");
                Cloud.Logger.Log(LogPriority.Debug, ex.Message);
            }
        }

        /// <summary>
        /// Listen for messages from the IRC server.
        /// </summary>
        public void Listen()
        {
            while (isConnected)
            {
                ParseReceivedData(reader.ReadLine());
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// Send a specific command to the IRC server.
        /// </summary>
        /// <param name="cmd">The primary command to send.</param>
        /// <param name="param">Any extra parameters to send.</param>
        private void SendData(string cmd, string param = null)
        {
            if (param == null)
            {
                writer.WriteLine(cmd);
                writer.Flush();
                Cloud.Logger.Log(LogPriority.Debug, "send: " + cmd + " " + param);
            }

            else
            {
                writer.WriteLine(cmd + " " + param);
                writer.Flush();
                Cloud.Logger.Log(LogPriority.Debug, "send: " + cmd + " " + param);
            }
        }

        /// <summary>
        /// Parse data sent by the IRC server.
        /// </summary>
        /// <param name="data">The line of data to parse.</param>
        private void ParseReceivedData(string data)
        {
            Cloud.Logger.Log(LogPriority.Debug, data);
            string[] message = data.Split(' ');

            string hostmask;

            //Parse ping
            if (message[Constants.PING_INDEX] == "PING")
            {
                SendData("PONG", message[Constants.PINGER_INDEX]);
                return;
            }

            //Parse numerics
            int numeric;
            if (int.TryParse(message[Constants.NUMERIC_INDEX], out numeric))
            {
                switch (numeric)
                {
                    case (int)Numerics.RPL_NAMREPLY:
                        onNames(message);
                        break;

                    case (int)Numerics.RPL_CHANNELMODEIS:
                        if (message[Constants.RPLCHANNELMODEIS_CHANNEL_NAME_INDEX] == channel.Name)
                        {
                            onChannelModeInit(message[Constants.RPLCHANNELMODEIS_CHANNEL_MODES_INDEX]);
                        }
                        break;

                    case (int)Numerics.RPL_WHOREPLY:
                        if (message[Constants.RPLWHOREPLY_CHANNEL_NAME_INDEX] == channel.Name)
                        {
                            onWho(message);
                        }
                        break;

                    default:
                        break;
                }
                return;
            }

            //Parse command
            else if (message[Constants.SENDER_INDEX].StartsWith(":"))
            {
                //Remove useless chars
                for (int i = 0; i < message.Length; i++)
                {
                    if (message[i].StartsWith(":"))
                        message[i] = message[i].Remove(0, 1);
                }

                hostmask = message[Constants.SENDER_INDEX];

                if (hostmask.Contains('@') && hostmask.Contains('!'))
                {
                    switch (message[Constants.IRC_COMMAND_INDEX])
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

        /// <summary>
        /// Called when you receive a WHO reply message from the IRC server.
        /// </summary>
        /// <param name="message">The message sent.</param>
        private void onWho(string[] message)
        {
            //Reply value:
            //channel realname hostname server[*] nick flags hopcount info

            if (channel.Users.GetUser(message[Constants.RPLWHOREPLY_NICK_INDEX]) != null)
            {
                channel.Users.GetUser(message[Constants.RPLWHOREPLY_NICK_INDEX]).Realname = message[Constants.RPLWHOREPLY_REALNAME_INDEX]; //Set the realname
                channel.Users.GetUser(message[Constants.RPLWHOREPLY_NICK_INDEX]).Hostname = message[Constants.RPLWHOREPLY_HOSTNAME_INDEX]; //Set the hostname
            }
        }

        /// <summary>
        /// Called when you receive a NAMES message from the IRC server.
        /// </summary>
        /// <param name="message">The message sent.</param>
        private void onNames(string[] message)
        {
            if (message[Constants.NAMES_CHANNEL_NAME_INDEX] == channel.Name)
            {
                for (int i = Constants.NAMES_CHANNEL_USERS_START_INDEX; i < message.Length; i++)
                {
                    if (message[i].StartsWith("+") || message[i].StartsWith("%") || message[i].StartsWith("@") || message[i].StartsWith("~"))
                    {
                        //Implement initialization of modes here...
                        message[i] = message[i].Remove(0, 1);
                        if (!(channel.Users.Exists((user) => user.Nick == message[i])))
                        {
                            channel.Users.Add(new User(message[i]));
                        }
                    }

                    else
                    {
                        if (!(channel.Users.Exists((user) => user.Nick == message[i])))
                        {
                            channel.Users.Add(new User(message[i]));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Called when you receive a PRIVMSG message from the IRC server.
        /// </summary>
        /// <param name="hostmask">The hostmask of the sender.</param>
        /// <param name="message">The message sent.</param>
        private void onPrivMsg(string hostmask, string[] message)
        {
            //Channel Privmsg
            //hostmask PRIVMSG channel message

            //Privmsg
            //hostmask PRIVMSG [bot's nick] message


            User sender = ExtractUserInfo(hostmask);

            if (sender.Hostname == "ctcp-scanner.rizon.net")
            {
                SendData("NOTICE", sender.Nick + " IRC.plugin " + Assembly.GetExecutingAssembly().GetName().Version);
            }

            else if (message[Constants.PRIVMSG_TARGET_INDEX] == channel.Name)
            {
                if (message[Constants.PRIVMSG_MESSAGE_INDEX].StartsWith("!"))
                {
                    ExecuteCommand(message[Constants.PRIVMSG_MESSAGE_INDEX]);
                }
            }
        }

        /// <summary>
        /// Called when you receive a NOTICE message from the IRC server.
        /// </summary>
        /// <param name="hostmask">The hostmask of the sender.</param>
        /// <param name="message">The message sent.</param>
        private void onNotice(string hostmask, string[] message)
        {
            //Ignore NOTICES for the time being
        }

        /// <summary>
        /// Called on initial channel MODE message when you join a channel.
        /// </summary>
        /// <param name="modes">The modes set on the channel.</param>
        private void onChannelModeInit(string modes)
        {
            channel.Modes = modes.Remove(0, 1); //remove '+' and set modes
        }

        /// <summary>
        /// Called when you receive a MODE message from the IRC server.
        /// </summary>
        /// <param name="hostmask">The hostmask of the sender.</param>
        /// <param name="message">The message sent.</param>
        private void onMode(string hostmask, string[] message)
        {
            //User mode:
            //hostmask MODE channel modes target

            //Channel mode:
            //hostmask MODE channel modes


            User sender = ExtractUserInfo(hostmask);

            //Split into onChannelMode() and onUserMode()

            if (sender.Nick == nick) //your own mode
            {
                return;
            }

            else if (message[Constants.MODE_CHANNEL_NAME_INDEX] == channel.Name) //Ignore it unless it came from the designated channel
            {
                if (string.IsNullOrEmpty(message[Constants.MODE_TARGET_USER_INDEX])) //channel mode
                {
                    onChannelMode(sender, message[Constants.MODE_MODES_INDEX]);
                }

                else //user mode
                {
                    onUserMode(sender, channel.Users.GetUser(message[Constants.MODE_TARGET_USER_INDEX]), message[Constants.MODE_MODES_INDEX]);
                }
            }
        }

        /// <summary>
        /// Called when you receive a MODE message regarding a channel.
        /// </summary>
        /// <param name="sender">The user that set the modes.</param>
        /// <param name="modes">The modes set.</param>
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

        /// <summary>
        /// Called when you receive a MODE message regarding a user.
        /// </summary>
        /// <param name="sender">The user that set the modes.</param>
        /// <param name="target">The target user.</param>
        /// <param name="modes">The modes set.</param>
        private void onUserMode(User sender, User target, string modes)
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
                    if (addMode == true && !(target.Modes.Contains(modes[i])))
                    {
                        target.Modes.Insert(target.Modes.Length, modes[i].ToString()); //insert new mode
                    }

                    else if (addMode == false && target.Modes.Contains(modes[i]))
                    {
                        target.Modes.Remove(target.Modes.IndexOf(modes[i]), 1); //remove existing mode
                    }
                }
            }
        }

        /// <summary>
        /// Called when you receive a KICK message from the IRC server.
        /// </summary>
        /// <param name="hostmask">The hostmask of the sender.</param>
        /// <param name="message">The message sent.</param>
        private void onKick(string hostmask, string[] message)
        {
            //Kick someone
            //hostmask KICK channel target kickmessage
            User sender = ExtractUserInfo(hostmask);

            if (channel.Users.Contains(channel.Users.GetUser(message[Constants.KICK_TARGET_USER_INDEX])))
            {
                channel.Users.Remove(channel.Users.GetUser(message[Constants.KICK_TARGET_USER_INDEX]));

                if (message[Constants.KICK_TARGET_USER_INDEX] == nick)
                {
                    JoinChannel();
                }
            }
        }

        /// <summary>
        /// Called when you receive a JOIN message from the IRC server.
        /// </summary>
        /// <param name="hostmask">The hostmask of the sender.</param>
        /// <param name="message">The message sent.</param>
        private void onJoin(string hostmask, string[] message)
        {
            User sender = ExtractUserInfo(hostmask);

            if (!(channel.Users.Contains(sender)))
            {
                channel.Users.Add(sender);
            }
        }

        /// <summary>
        /// Called when you receive a PART message from the IRC server.
        /// </summary>
        /// <param name="hostmask">The hostmask of the sender.</param>
        /// <param name="message">The message sent.</param>
        private void onPart(string hostmask, string[] message)
        {
            User sender = ExtractUserInfo(hostmask);

            if (channel.Users.Contains(sender))
            {
                channel.Users.Remove(sender);
            }
        }

        /// <summary>
        /// Execute a command from PRIVMSG if prefixed by the command char.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        private void ExecuteCommand(string command)
        {
            Player player = null;
            command.ToLower();

            string[] cmdParts = command.Split(' ');

            try
            {
                switch (cmdParts[0])
                {
                    case "!quit":
                        Disconnect();

                        break;

                    default:
                        //CommandManager.InvokeCommand(player, command, EECloud.API.Group.Moderator); //--Throws exception
                        break;
                }
            }

            catch (Exception ex)
            {
                Cloud.Logger.Log(LogPriority.Error, "Failed to execute command");
                Cloud.Logger.LogEx(ex);
            }
        }

        /// <summary>
        /// Initializes a User instance based on the given hostmask.
        /// </summary>
        /// <param name="hostmask">The hostmask of the user.</param>
        /// <returns>User instance with the information from the hostmask.</returns>
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
