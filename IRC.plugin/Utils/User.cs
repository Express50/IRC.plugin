using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRC.plugin.Utils
{
    public class User
    {

        public string Nick { get; set; }

        public string Hostname { get; set; }

        public string Realname { get; set; }

        public string Modes { get; set; }

        public Rank Rank { get; set; }

        public User() { }

        public User(string nick)
        {
            Nick = nick;
        }
    }
}
