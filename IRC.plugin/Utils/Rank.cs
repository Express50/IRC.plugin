using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRC.plugin.Utils
{
    public enum Rank
    {
        None = 0,

        Voice = 0,

        HalfOp = 100,

        FullOp = 200,

        Admin = 300,

        Owner = 400
    }
}
