using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRC.plugin.Utils
{
    public class Channel
    {
        private List<User> users;

        public List<User> Users
        {
            get
            {
                return users;
            }

            set
            {
                users = Users;
                return;
            }
        }
    }
}
