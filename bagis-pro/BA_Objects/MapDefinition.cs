using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro.BA_Objects
{
    public class MapDefinition
    {
        private IList<string> m_layerList;
        private IList<string> m_legendLayerList;
        private readonly string m_subTitle;
        private readonly string m_unitsText;
        private readonly string m_pdfFileName;

        public MapDefinition(string subTitle, string unitsText, string pdfFileName)
        {
            m_subTitle = subTitle;
            m_unitsText = unitsText;
        }

        public string SubTitle
        {
            get { return m_subTitle; }
        }

        public string UnitsText
        {
            get { return m_unitsText; }
        }

        public string PdfFileName
        {
            get { return m_pdfFileName; }
        }

        public IList<string> LayerList
        {
            get { return m_layerList; }
            set
            {
                m_layerList = value;
            }

        }

        public IList<string> LegendLayerList
        {
            get { return m_legendLayerList; }
            set
            {
                m_legendLayerList = value;
            }

        }
    }

}
