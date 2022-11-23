using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro.BA_Objects
{
    public class DataSource
    {
        public string uri;
        public string description;
        public DateTime DateClipped;
        public string units;
        public double minValue;
        public double maxValue;
        public string layerType;
        public string heading;
        public string shortDescription;

        public DataSource()
        {

        }

        public DataSource(dynamic dynSource)
        {
            uri = dynSource.uri;
            description = dynSource.description;
            units = dynSource.units;
            layerType = dynSource.layerType;
            heading = dynSource.heading;
            shortDescription = dynSource.shortDescription;
        }

        public string DateClippedText
        {
            get
            {
                return DateClipped.ToString("MMMM d, yyyy");
            }
            set
            { /*Do nothing; Required for serialization */}
        }

    }
}
