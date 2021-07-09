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
        private SiteType m_siteType;
        public double ElevMeters = Constants.VALUE_NO_DATA_9999;
        public double Elevation;
        public string ElevationText;
        public string UpperElevText;
        public string LowerElevText;
        public Geometry Buffer;
        public bool IncludeInAnalysis;
        public double Latitude;
        public double Longitude;

        public SiteType SiteType
        {
            get
            {
                return m_siteType;
            }
        }

        public string SiteTypeText
        {
            get
            {
                return m_siteType.ToString();
            }
            set
            {
                m_siteType = (SiteType) Enum.Parse(typeof(SiteType), value, true); // case insensitive
            }
        }

        public string LatitudeText
        {
            get
            {
                //return String.Format("{0:0.##}", Latitude);
                return "latitude";
            }
            set
            {
                // Does nothing; Required for XSL deserialization
            }
        }

        public string LongitudeText
        {
            get
            {
                return String.Format("{0:0.##}", Longitude);
            }
            set
            {
                // Does nothing; Required for XSL deserialization
            }
        }

    }


}
