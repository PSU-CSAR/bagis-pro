using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace bagis_pro
{
    /// <summary>
    /// Interaction logic for WinViewDemLayers.xaml
    /// </summary>
    public partial class WinViewDemLayers : ArcGIS.Desktop.Framework.Controls.ProWindow
    {
        public string FolderPath = "";
        public WinViewDemLayers()
        {
            InitializeComponent();
            this.DataContext = new WinViewDemLayersModel(this);
        }
    }
}
