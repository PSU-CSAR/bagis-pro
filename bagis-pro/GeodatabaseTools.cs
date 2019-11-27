using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro
{
    public class GeodatabaseNames
    {
        private GeodatabaseNames(string value) { Value = value; }

        public string Value { get; set; }

        public static GeodatabaseNames Aoi { get { return new GeodatabaseNames("aoi.gdb"); } }
        public static GeodatabaseNames Prism { get { return new GeodatabaseNames("prism.gdb"); } }
        public static GeodatabaseNames Layers { get { return new GeodatabaseNames("layers.gdb"); } }
        public static GeodatabaseNames Surfaces { get { return new GeodatabaseNames("surfaces.gdb"); } }
        public static GeodatabaseNames Analysis { get { return new GeodatabaseNames("analysis.gdb"); } }
    }

    public class GeodatabaseTools
    {
        public static string GetGeodatabasePath(string aoiPath, GeodatabaseNames gdbNames,
                                                bool hasTrailingBackSlash = false)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(aoiPath);
            sb.Append("\\");
            sb.Append(gdbNames.Value);
            if (hasTrailingBackSlash)
                sb.Append("\\");
            return sb.ToString();
        }
    }
}
