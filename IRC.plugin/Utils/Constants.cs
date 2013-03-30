using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRC.plugin.Utils
{
    class Constants
    {
        public const int PING_INDEX = 0;
        public const int PINGER_INDEX = 1;

        public const int SENDER_INDEX = 0;

        public const int NUMERIC_INDEX = 1;
        public const int RPLCHANNELMODEIS_CHANNEL_NAME_INDEX = 3;
        public const int RPLCHANNELMODEIS_CHANNEL_MODES_INDEX = 4;

        public const int RPLWHOREPLY_CHANNEL_NAME_INDEX = 3;
        public const int RPLWHOREPLY_REALNAME_INDEX = 4;
        public const int RPLWHOREPLY_HOSTNAME_INDEX = 5;
        public const int RPLWHOREPLY_NICK_INDEX = 7;
        public const int RPLWHOREPLY_FLAGS_INDEX = 8;

        public const int RPLTOPIC_CHANNEL_NAME_INDEX = 3;
        public const int RPLTOPIC_TOPIC_START_INDEX = 4;

        public const int IRC_COMMAND_INDEX = 1;

        public const int NAMES_CHANNEL_NAME_INDEX = 4;
        public const int NAMES_CHANNEL_USERS_START_INDEX = 6;

        public const int PRIVMSG_TARGET_INDEX = 2;
        public const int PRIVMSG_MESSAGE_INDEX = 3;

        public const int NOTICE_TARGET_INDEX = 2;
        public const int NOTICE_MESSAGE_INDEX = 3;

        public const int MODE_CHANNEL_NAME_INDEX = 2;
        public const int MODE_MODES_INDEX = 3;
        public const int MODE_TARGET_USER_INDEX = 4;

        public const int KICK_TARGET_USER_INDEX = 3;

    }
}
