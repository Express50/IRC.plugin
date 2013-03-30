using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Threading;
using System.Configuration;
using EECloud.API;
using IRC.plugin.Utils;

namespace IRC.plugin
{
    [Plugin(Authors = new string[] { "Bass5098", "Express50" },
           Category = PluginCategory.Admin,
           ChatName = "IRC",
           Description = "IRC interface for executing commands in your EECloud plugin.",
           Version = "0.2")]
    public class IRC : Plugin<Player, IRC>
    {
        private string version = Assembly.GetExecutingAssembly().GetName().Version.Major.ToString() + "." + Assembly.GetExecutingAssembly().GetName().Version.Minor.ToString();

        private string server;
        private int port;
        private string nick;
        private string password;
        private Channel channel;

        private NetworkStream nstream = null;
        private StreamWriter writer = null;
        private StreamReader reader = null;

        private TcpClient client = null;

        private Thread ListenThread;
        private Thread RestartThread;
        private Thread DisconnectThread;
        private object locker = new object();

        private int[] DontPrint = {(int)Numerics.RPL_MOTD, 
                                   (int)Numerics.RPL_WHOREPLY, 
                                   (int)Numerics.RPL_ENDOFWHO,
                                   (int)Numerics.RPL_MOTDSTART,
                                   (int)Numerics.RPL_ENDOFMOTD,
                                   (int)Numerics.RPL_ENDOFSERVICES,
                                   (int)Numerics.RPL_SERVICE,
                                   (int)Numerics.RPL_SERVICEINFO,
                                   (int)Numerics.RPL_YOURHOST,
                                   (int)Numerics.RPL_YOUREOPER,
                                   (int)Numerics.RPL_WHOISACCOUNT,
                                   (int)Numerics.RPL_WHOISACTUALLY,
                                   (int)Numerics.RPL_WHOISCHANNELS,
                                   (int)Numerics.RPL_WHOISIDLE,
                                   (int)Numerics.RPL_WHOISOPERATOR,
                                   (int)Numerics.RPL_WHOISSERVER,
                                   (int)Numerics.RPL_WHOISUSER,
                                   (int)Numerics.RPL_CREATED, 
                                   (int)Numerics.RPL_MYINFO,
                                   (int)Numerics.RPL_ISUPPORT,
                                   (int)Numerics.RPL_LUSERCLIENT,
                                   (int)Numerics.RPL_LUSERCHANNELS,
                                   (int)Numerics.RPL_LUSERME,
                                   (int)Numerics.RPL_LUSEROP,
                                   (int)Numerics.RPL_LUSERUNKNOWN,
                                   (int)Numerics.RPL_HOSTHIDDEN,
                                   (int)Numerics.RPL_WELCOME,
                                   (int)Numerics.RPL_TOPIC,
                                   (int)Numerics.RPL_TOPICWHOTIME,
                                   (int)Numerics.RPL_ENDOFNAMES,
                                   (int)Numerics.RPL_NAMREPLY,
                                   042,
                                   265,
                                   266,
                                   900};

        public bool isConnected = false;
        public bool isRestarting = false;
        public bool isDisconnecting = false;

        #region EECloud
        protected override void OnConnect()
        {
            CommandManager.Load(this);
            this.Connect();

            Cloud.Logger.Log(LogPriority.Debug, "Connected");
        }

        protected override void OnDisable()
        {
            Cloud.Logger.Log(LogPriority.Debug, "Disabled");
        }

        protected override void OnEnable()
        {
            server = "irc.rizon.net";
            port = 6667;
            ExeConfigurationFileMap configMap = new ExeConfigurationFileMap();
            configMap.ExeConfigFilename = @"IRC.config";
            Configuration config = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);

            if (config.HasFile)
            {
                server = config.AppSettings.Settings["Server"].Value.ToString();
                nick = config.AppSettings.Settings["Username"].Value.ToString();
                password = config.AppSettings.Settings["Password"].Value.ToString();
                channel = new Channel(config.AppSettings.Settings["Channel"].Value.ToString());
            }

            else
            {
                Cloud.Logger.Log(LogPriority.Warning, "Couldn't find IRC.config file");
                server = "irc.rizon.net";
                nick = "DefaultIRCPBot";
                channel = new Channel("#IRCP-Testing");
            }

            Cloud.Logger.Log(LogPriority.Debug, "Enabled");
        }
        #endregion

        #region EE to IRC Commands

        [Command("ircnotify", EECloud.API.Group.Trusted, Aliases = new string[] { "notify" })]
        public void CommandIRCNotify(ICommand<Player> cmd, string target, string message)
        {
            if (cmd.Sender != null)
            {
                string fullmessage = cmd.CommandText.Substring(cmd.CommandText.IndexOf(target) + (target.Length + 1));
                if (channel.Users.Contains(channel.Users.GetByNick(target)))
                {
                    SendData("PRIVMSG", channel.Name + " :[" + cmd.Sender.Username.ToUpper() + "] @" + target + ": " + fullmessage);
                }

                SendData("MEMOSERV", ":SEND " + target + " This is an automated memo: [" + cmd.Sender.Username.ToUpper() + "] " + fullmessage);
            }
        }

        [Command("ircreload", EECloud.API.Group.Admin, Aliases = new string[] { "ircreconnect", "ircrestart" })]
        public void CommandIRCReload(ICommand<Player> cmd)
        {
            RestartThread = new Thread(DoRestart);
            RestartThread.Start();
        }

        [Command("ircquit", EECloud.API.Group.Admin)]
        public void CommandIRCQuit(ICommand<Player> cmd)
        {
            DisconnectThread = new Thread(Disconnect);
            DisconnectThread.Start();
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
                isRestarting = false;

                Cloud.Logger.Log(LogPriority.Info, "Attempting to establish a connection to IRC server...");
                client = new TcpClient(server, port);
                nstream = client.GetStream();

                reader = new StreamReader(nstream);
                writer = new StreamWriter(nstream);

                isConnected = true;

                ListenThread = new Thread(Listen);
                ListenThread.Start();

                Identify();
            }

            catch (Exception ex)
            {
                Cloud.Logger.Log(LogPriority.Error, "Failed to connect to irc server");
                Cloud.Logger.LogEx(ex);
            }
        }

        /// <summary>
        /// Disconnect from the IRC channel and close all open streams.
        /// </summary>
        public void Disconnect()
        {
            lock (locker)
            {
                try
                {
                    isDisconnecting = true;

                    Cloud.Logger.Log(LogPriority.Info, "Disconnecting: closing all open streams...");

                    if (isConnected)
                    {
                        SendData("QUIT");
                    }

                    while (ListenThread.IsAlive)
                    {
                        //Wait for ListenThread to die
                    }

                    if (reader != null)
                        reader.Close();

                    if (writer != null)
                        writer.Close();

                    if (nstream != null)
                        nstream.Close();

                    if (client != null)
                        client.Close();

                    Cloud.Logger.Log(LogPriority.Info, "Disconnected from IRC server");

                    isConnected = false;
                }

                catch (Exception ex)
                {
                    Cloud.Logger.LogEx(ex);
                }
            }
        }

        /// <summary>
        /// Set the name of the connecting user.
        /// </summary>
        private void Identify()
        {
            try
            {
                Cloud.Logger.Log(LogPriority.Info, "Attempting to identify IRC connection...");
                SendData("USER", nick + " - " + server + " :" + nick);
                SendData("NICK", nick);
            }

            catch (Exception ex)
            {
                Cloud.Logger.Log(LogPriority.Error, "Failed to identify IRC connection");
                Cloud.Logger.LogEx(ex);
                Disconnect();
            }
        }

        private void NickservIdentify()
        {
            try
            {
                if (!String.IsNullOrEmpty(password))
                {
                    SendData("PRIVMSG", "NICKSERV :IDENTIFY " + password);
                }
            }

            catch (Exception ex)
            {
                Cloud.Logger.Log(LogPriority.Error, "Failed to identify IRC client");
                Cloud.Logger.LogEx(ex);
                Disconnect();
            }
        }

        /// <summary>
        /// Join the designated channel
        /// </summary>
        private void JoinChannel()
        {
            try
            {
                Cloud.Logger.Log(LogPriority.Info, "Joining IRC channel...");
                SendData("JOIN", channel.Name);
                SendData("WHO", channel.Name);
            }

            catch (Exception ex)
            {
                Cloud.Logger.Log(LogPriority.Error, "Couldn't join IRC channel");
                Cloud.Logger.LogEx(ex);
            }
        }

        /// <summary>
        /// Listen for messages from the IRC server.
        /// </summary>
        public void Listen()
        {
            while (isConnected == true && isRestarting == false && isDisconnecting == false)
            {
                string str = reader.ReadLine();

                if (str != String.Empty)
                {
                    ParseReceivedData(str);
                }
                //Thread.Sleep(50);
            }

            return;
        }

        /// <summary>
        /// Performs a restart operation.
        /// </summary>
        private void DoRestart()
        {
            if (isRestarting == false)
            {
                try
                {
                    Cloud.Logger.Log(LogPriority.Info, "Performing restart...");

                    isRestarting = true;

                    if (isConnected)
                    {
                        Disconnect();
                    }

                    Connect();
                }

                catch (Exception ex)
                {
                    Cloud.Logger.Log(LogPriority.Error, "Failed to restart IRC.plugin.");
                    Cloud.Logger.LogEx(ex);
                }
            }
        }

        /// <summary>
        /// Send a specific command to the IRC server.
        /// </summary>
        /// <param name="cmd">The primary command to send.</param>
        /// <param name="param">Any extra parameters to send.</param>
        private void SendData(string cmd, string param = null)
        {
            try
            {
                if (param == null)
                {
                    writer.WriteLine(cmd);
                    writer.Flush();

                    Cloud.Logger.Log(LogPriority.Info, "[SEND] " + cmd);
                }

                else
                {
                    writer.WriteLine(cmd + " " + param);
                    writer.Flush();

                    Cloud.Logger.Log(LogPriority.Info, "[SEND] " + cmd + " " + param);
                }
            }

            catch (Exception ex)
            {
                Cloud.Logger.Log(LogPriority.Error, "Failed to send data: '" + cmd + param + "'");
                Cloud.Logger.LogEx(ex);
            }
        }

        /// <summary>
        /// Parse data sent by the IRC server.
        /// </summary>
        /// <param name="data">The line of data to parse.</param>
        private void ParseReceivedData(string data)
        {
            try
            {
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
                        case (int)Numerics.RPL_WELCOME:
                            NickservIdentify();
                            JoinChannel();
                            break;

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

                        case (int)Numerics.ERR_NOTREGISTERED:


                        default:
                            break;
                    }

                    if (!DontPrint.Contains(numeric))
                    {
                        Cloud.Logger.Log(LogPriority.Info, data);
                    }

                    else if (numeric == (int)Numerics.RPL_WELCOME)
                    {
                        Cloud.Logger.Log(LogPriority.Info, "Welcome to " + server);
                    }

                    else if (numeric == (int)Numerics.RPL_TOPIC)
                    {
                        string topic = "";

                        for (int i = 0; i < message.Length; i++)
                        {
                            if (message[i].StartsWith(":"))
                                message[i] = message[i].Remove(0, 1);
                        }

                        for (int i = Constants.RPLTOPIC_TOPIC_START_INDEX; i < message.Length; i++)
                        {
                            topic += message[i] + " ";
                        }

                        Cloud.Logger.Log(LogPriority.Info, "Topic for " + message[Constants.RPLTOPIC_CHANNEL_NAME_INDEX] + " is: " + topic);
                    }

                    return;
                }

                //Parse command
                else if (message[Constants.SENDER_INDEX].StartsWith(":"))
                {
                    //Cloud.Logger.Log(LogPriority.Info, data);
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

            catch (Exception ex)
            {
                Cloud.Logger.Log(LogPriority.Error, "Failed to parse data: '" + data + "'");
                Cloud.Logger.LogEx(ex);
            }
        }

        #endregion

        #region Message Handlers

        /// <summary>
        /// Called when you receive a WHO reply message from the IRC server.
        /// </summary>
        /// <param name="message">The message sent.</param>
        private void onWho(string[] message)
        {
            //Reply value:
            //channel realname hostname server[*] nick flags hopcount info
            //3

            if (channel.Users.GetUser(message[Constants.RPLWHOREPLY_NICK_INDEX]) != null)
            {
                channel.Users.GetUser(message[Constants.RPLWHOREPLY_NICK_INDEX]).Realname = message[Constants.RPLWHOREPLY_REALNAME_INDEX]; //Set the realname
                channel.Users.GetUser(message[Constants.RPLWHOREPLY_NICK_INDEX]).Hostname = message[Constants.RPLWHOREPLY_HOSTNAME_INDEX]; //Set the hostname

                Match flagMatch = Regex.Match(message[Constants.RPLWHOREPLY_FLAGS_INDEX], @"([\+\%\@\&\~])");

                if (flagMatch.Success)
                {
                    switch (flagMatch.Groups[1].ToString())
                    {
                        case "+": channel.Users.GetUser(message[Constants.RPLWHOREPLY_NICK_INDEX]).Rank = Rank.Voice;
                            break;

                        case "%": channel.Users.GetUser(message[Constants.RPLWHOREPLY_NICK_INDEX]).Rank = Rank.HalfOp;
                            break;

                        case "@": channel.Users.GetUser(message[Constants.RPLWHOREPLY_NICK_INDEX]).Rank = Rank.FullOp;
                            break;

                        case "&": channel.Users.GetUser(message[Constants.RPLWHOREPLY_NICK_INDEX]).Rank = Rank.Admin;
                            break;

                        case "~": channel.Users.GetUser(message[Constants.RPLWHOREPLY_NICK_INDEX]).Rank = Rank.Owner;
                            break;

                        default:
                            break;
                    }
                }

                else
                {
                    channel.Users.GetUser(message[Constants.RPLWHOREPLY_NICK_INDEX]).Rank = Rank.None;
                }
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

                string users = "";
                foreach (User user in channel.Users)
                {
                    users += user.Nick + " ";
                }
                Cloud.Logger.Log(LogPriority.Info, "Users in " + channel.Name + ": " + users);
            }
        }

        /// <summary>
        /// Called when you receive a PRIVMSG message from the IRC server.
        /// </summary>
        /// <param name="hostmask">The hostmask of the sender.</param>
        /// <param name="message">The message sent.</param>
        private void onPrivMsg(string hostmask, string[] message)
        {
            User sender = ExtractUserInfo(hostmask);
            //sender = channel.Users.GetUser(sender.Hostname);

            string fullmessage = "";
            for (int i = Constants.PRIVMSG_MESSAGE_INDEX; i < message.Length; i++)
            {
                fullmessage += message[i] + " ";
            }

            Cloud.Logger.Log(LogPriority.Info, "[" + channel.Name + "] " + sender.Nick + ": " + fullmessage);

            //TODO: Implement better version sending
            /*if (sender.Hostname == "ctcp-scanner.rizon.net")
            {
                SendData("NOTICE", sender.Nick + " :VERSION " + " IRC.plugin " + version);
            }*/

            if (message[Constants.PRIVMSG_TARGET_INDEX] == channel.Name)
            {
                if (message[Constants.PRIVMSG_MESSAGE_INDEX].StartsWith("!"))
                {
                    string command = "";
                    for (int i = Constants.PRIVMSG_MESSAGE_INDEX; i < message.Length; i++)
                    {
                        command += message[i] + " ";
                    }

                    ExecuteCommand(command, sender);
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
            User sender = ExtractUserInfo(hostmask);

            if (sender.Nick.ToLower() == "nickserv")
            {
                string fullmessage = "";
                for (int i = Constants.NOTICE_MESSAGE_INDEX; i < message.Length; i++)
                {
                    fullmessage += message[i] + " ";
                }

                if (fullmessage.ToLower().Contains("password accepted"))
                {
                    Cloud.Logger.Log(LogPriority.Info, "Your password was accepted. You are identified.");
                }
            }
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
            User sender = ExtractUserInfo(hostmask);
            sender = channel.Users.GetUser(sender.Hostname);
            SendData("WHO", channel.Name);

            //Split into onChannelMode() and onUserMode()

            /*if (sender.Nick == nick) //your own mode
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
            }*/
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
            User sender = ExtractUserInfo(hostmask);
            sender = channel.Users.GetUser(sender.Hostname);

            if (sender != null)
            {
                if (channel.Users.Contains(channel.Users.GetUser(message[Constants.KICK_TARGET_USER_INDEX])))
                {
                    channel.Users.Remove(channel.Users.GetUser(message[Constants.KICK_TARGET_USER_INDEX]));

                    if (message[Constants.KICK_TARGET_USER_INDEX] == nick)
                    {
                        JoinChannel();
                    }
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
            sender = channel.Users.GetUser(sender.Hostname);

            if (sender != null)
            {
                if (!(channel.Users.Contains(sender)))
                {
                    channel.Users.Add(sender);
                }
            }

            SendData("WHO", channel.Name);
        }

        /// <summary>
        /// Called when you receive a PART message from the IRC server.
        /// </summary>
        /// <param name="hostmask">The hostmask of the sender.</param>
        /// <param name="message">The message sent.</param>
        private void onPart(string hostmask, string[] message)
        {
            User sender = ExtractUserInfo(hostmask);
            sender = channel.Users.GetUser(sender.Hostname);

            if (sender != null)
            {
                if (channel.Users.Contains(sender))
                {
                    channel.Users.Remove(sender);
                }
            }
        }
        #endregion

        #region Helper Functions
        /// <summary>
        /// Execute a command from PRIVMSG if prefixed by the command char.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="sender">The user that called the command.</param>
        private void ExecuteCommand(string command, User sender)
        {
            command = command.Remove(0, 1).ToLower().Trim(' ', '\n', '\r');

            string[] cmdParts = command.Split(' ');

            try
            {
                switch (cmdParts[0])
                {
                    case "irc":
                        //Handle irc commands
                        ExecuteIRCCommand(cmdParts, sender);
                        break;

                    default:
                        CommandManager.InvokeCommand(null, command, (EECloud.API.Group)sender.Rank); //Worked without '!'
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
        /// Executes any IRC related command.
        /// </summary>
        /// <param name="cmdParts">The command to execute.</param>
        /// <param name="sender">The user that called the command.</param>
        private void ExecuteIRCCommand(string[] cmdParts, User sender)
        {
            try
            {
                switch (cmdParts[1])
                {
                    case "quit":
                        DisconnectThread = new Thread(Disconnect);
                        DisconnectThread.Start();
                        break;

                    case "version":
                        SendData("PRIVMSG", channel.Name + " :@" + sender.Nick + ": IRC.plugin " + version);
                        break;

                    case "reconnect":
                    case "restart":
                    case "reload":
                        RestartThread = new Thread(DoRestart);
                        RestartThread.Start();
                        break;

                    default:
                        break;
                }
            }

            catch (Exception ex)
            {
                Cloud.Logger.Log(LogPriority.Error, "Couldn't execute IRC command");
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
                Cloud.Logger.LogEx(ex);
                return null;
            }
        }

        #endregion
    }
}
