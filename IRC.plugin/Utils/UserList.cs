using System;
using System.Collections.Generic;
using System.Linq;

namespace IRC.plugin.Utils
{
    class UserList : List<User>
    {
        /// <summary>
        /// Gets a user from the list by their nick.
        /// </summary>
        /// <param name="nick">The nick to search for.</param>
        /// <returns>The User object if it finds one, null if it doesn't.</returns>
        public User GetByNick(string nick)
        {
            return this.FirstOrDefault(((user) => user.Nick == nick));
        }

        /// <summary>
        /// Gets a user from the list by their hostname.
        /// </summary>
        /// <param name="hostname">The hostname to search for.</param>
        /// <returns>The User object if it finds one, null if it doesn't.</returns>
        public User GetByHostname(string hostname)
        {
            return this.FirstOrDefault(((user) => user.Hostname == hostname));
        }
    }
}
