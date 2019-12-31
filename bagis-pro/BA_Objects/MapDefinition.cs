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
        private readonly string m_legendTitle;
        private readonly string m_subTitle;
        private readonly string m_unitsText;

        public MapDefinition(string legendTitle, string subTitle, string unitsText)
        {
            m_legendTitle = legendTitle;
            m_subTitle = subTitle;
            m_unitsText = unitsText;
        }

        public string LegendTitle
        {
            get { return m_legendTitle; }
        }

        public string SubTitle
        {
            get { return m_subTitle; }
        }

        public string UnitsText
        {
            get { return m_unitsText; }
        }

        public IList<string> LayerList
        {
            get { return m_layerList; }
            set
            {
                m_layerList = value;
            }

        }
    }

}
