using ArcGIS.Core.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro.BA_Objects
{
    public class Site
    {
        public int ObjectId;
        public string Name;
        public SiteType SiteType;
        public double ElevMeters = Constants.VALUE_NO_DATA_9999;
        public Geometry Buffer;
    }
}
