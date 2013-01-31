using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EECloud.API;

namespace IRC.plugin.Parts
{
    class Commands : PluginPart<Player, IRC>
    {
        protected override void OnEnable()
        {

        }

        protected override void OnDisable()
        {

        }

        protected override void OnConnect()
        {
            CommandManager.Load(this);
        }
    }
}
