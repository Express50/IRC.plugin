using System;
using System.Collections.Generic;
using System.Linq;

namespace IRC.plugin.Utils
{
    public class UserList : List<User>
    {
        public User GetUser(string info)
        {
            User user = new User();
            user = GetByNick(info);

            if (user != null)
                return user;

            else
                user = GetByHostname(info);
                return user;

        }
        /// <summary>
        /// Gets a user from the list by their nick.
        /// </summary>
        /// <param name="nick">The nick to search for.</param>
        /// <returns>The User object if it finds one, null if it doesn't.</returns>
        public User GetByNick(string nick)
        {
            try
            {
                return this.FirstOrDefault(user => user.Nick.Equals(nick, StringComparison.InvariantCultureIgnoreCase));
            }

            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a user from the list by their hostname.
        /// </summary>
        /// <param name="hostname">The hostname to search for.</param>
        /// <returns>The User object if it finds one, null if it doesn't.</returns>
        public User GetByHostname(string hostname)
        {
            try
            {
                return this.FirstOrDefault(user => user.Hostname.Equals(hostname, StringComparison.InvariantCultureIgnoreCase));
            }

            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
