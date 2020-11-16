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
        private readonly string m_Title;
        private readonly string m_unitsText;
        private readonly string m_pdfFileName;

        public MapDefinition(string title, string unitsText, string pdfFileName)
        {
            m_Title = title;
            m_unitsText = unitsText;
            m_pdfFileName = pdfFileName;
        }

        public string Title
        {
            get { return m_Title; }
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
