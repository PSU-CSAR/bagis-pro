using ArcGIS.Core.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro.BA_Objects
{
    class Settings
    {
        public string m_pourpointUri = "https://services.arcgis.com/SXbDpmb7xQkk44JV/arcgis/rest/services/stations_USGS_ACTIVE/FeatureServer/0";
        public string m_nameField = "name";
        public string m_eBagisServer = "https://test.ebagis.geog.pdx.edu";
        public string m_sweDisplayUnits = "Inches";
        public string m_demUnits = "Meters";
        public string m_demDisplayUnits = "Feet";
        public string m_precipFile = @"\prism.gdb\Annual";
        public int m_precipZonesCount = 6;
        public double m_elevInterval = 1000.0F;
        public double m_prismBufferDistance = 1000.0F;
        public string m_prismBufferUnits = "Meters";
        public double m_snotelBufferDistance = 100.0F;
        public string m_snotelBufferUnits = "Meters";
        public int m_aspectDirections = 8;
        public int m_minElevZones = 10;

        public Settings()
        {
 

        }

    }

}
