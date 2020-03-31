using ArcGIS.Core.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro.BA_Objects
{
    class Aoi
    {
        string m_strName;
        string m_strFilePath;
        double m_dblMinElev = Constants.VALUE_NO_DATA_9999;
        double m_dblMaxElev = Constants.VALUE_NO_DATA_9999;
        double m_dblSiteElevRangeFeet = 500;
        double m_dblSiteBufferDistMiles = 5.642;

        public Aoi()
        {

        }

        public Aoi(string strName, string strFilePath)
        {
            m_strName = strName;
            m_strFilePath = strFilePath;
            m_dblMinElev = Constants.VALUE_NO_DATA_9999;
            m_dblMaxElev = Constants.VALUE_NO_DATA_9999;
        }

        public string Name
        {
            get { return m_strName; }
            set
            {
                m_strName = value;
            }
        }

        public string FilePath
        {
            get { return m_strFilePath; }
            set
            {
                m_strFilePath = value;
            }
        }

        public LinearUnit LinearUnits
        {
            get { return LinearUnit.Meters; }
        }

        public double MinElevMeters
        {
            get { return m_dblMinElev; }
            set
            {
                m_dblMinElev = value;
            }
        }

        public double MaxElevMeters
        {
            get { return m_dblMaxElev; }
            set
            {
                m_dblMaxElev = value;
            }
        }

        public double SiteElevRangeFeet
        {
            get { return m_dblSiteElevRangeFeet; }
        }

        public double SiteBufferDistMiles
        {
            get { return m_dblSiteBufferDistMiles; }
        }

        public string SnapRasterPath
        {
            get { return GeodatabaseTools.GetGeodatabasePath(m_strFilePath, GeodatabaseNames.Surfaces, true) + Constants.FILE_DEM_FILLED; }
        }
    }
}
