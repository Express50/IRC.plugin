using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRC.plugin.Utils
{
    public class User
    {
        private string nick;

        public string Nick
        {
            get
            {
                return this.nick;
            }

            set
            {
                this.nick = Nick;
            }
        }

        private string hostname;

        public string Hostname
        {
            get
            {
                return this.hostname;
            }

            set
            {
                this.hostname = Hostname;
            }
        }

        private string realname;

        public string Realname
        {
            get
            {
                return this.realname;
            }

            set
            {
                this.realname = Realname;
            }
        }

        private string modes;

        public string Modes
        {
            get
            {
                return this.modes;
            }

            set
            {
                this.modes = Modes;
            }
        }
    }
}
