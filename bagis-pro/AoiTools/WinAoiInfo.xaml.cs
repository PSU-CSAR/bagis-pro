using ActiproSoftware.Windows.Controls;
using bagis_pro.Basin;
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

namespace bagis_pro.AoiTools
{
    /// <summary>
    /// Interaction logic for WinAoiInfo.xaml
    /// </summary>
    public partial class WinAoiInfo : ArcGIS.Desktop.Framework.Controls.ProWindow
    {
        public WinAoiInfo()
        {
            InitializeComponent();
            this.DataContext = new WinAoiInfoModel(this);
        }

    }
}
