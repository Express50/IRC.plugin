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

        Voice = 1,

        HalfOp = 2,

        FullOp = 3,

        Admin = 4,

        Owner = 5
    }
}
