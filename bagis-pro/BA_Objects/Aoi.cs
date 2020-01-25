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

        public Aoi()
        {

        }

        public Aoi(string strName, string strFilePath)
        {
            m_strName = strName;
            m_strFilePath = strFilePath;
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
    }
}
