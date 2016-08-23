using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Share_Across_Devices.Controls
{
    public class Options
    {
        public string Glyph
        {
            get;
            set;
        }
        public string Name
        {
            get;
            set;
        }
        public Options(string glyph, string name)
        {
            this.Glyph = glyph;
            this.Name = name;
        }
    }
}
