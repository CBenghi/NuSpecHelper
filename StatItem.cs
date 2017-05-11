using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sharpen;

namespace NuSpecHelper
{
    internal class StatItem
    {
        public IpGeo Ip;
        public List<DateTime> Launches  = new AList<DateTime>();
        
        public StatItem(IpGeo deser)
        {
            Ip = deser;

        }
    }
}
