using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace NuSpecHelper
{
    internal interface IReporter
    {
        void AppendLine(string text, Brush brush = null);
    }
}
