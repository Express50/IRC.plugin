﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRC.plugin.Utils
{
    public class Channel
    {
        public string Name { get; set; }

        public string Modes { get; set; }

        public UserList Users { get; set; }

        public Channel(string name)
        {
            Name = name;
            Modes = "";
            Users = new UserList();
        }
    }
}
