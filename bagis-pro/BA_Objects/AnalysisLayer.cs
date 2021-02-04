using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace bagis_pro.BA_Objects
{
    public class AnalysisLayer : INotifyPropertyChanged
    {
        public AnalysisLayer()
        {
            Recalculate = false;
        }

        public AnalysisLayer(string strLayerName, bool bPresent, string strFilePath)
        {
            LayerName = strLayerName;
            Present = bPresent;
            FilePath = strFilePath;
            Recalculate = false;
        }

        public string LayerName { get; set; }
        public bool Present { get; set; }
        public string FilePath { get; set; }
        public bool Recalculate { get; set; }

    public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property.
        // The CallerMemberName attribute that is applied to the optional propertyName
        // parameter causes the property name of the caller to be substituted as an argument.
        private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
