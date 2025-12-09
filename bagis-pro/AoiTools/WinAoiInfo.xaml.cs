using ActiproSoftware.Windows.Controls;
using bagis_pro.Basin;
using Microsoft.VisualBasic.Devices;
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
        private void ClipCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            bool bEnableReclip = false;
            bool bChecked = ckReclipPrism.IsChecked ?? false;
            if (bChecked)
            {
                bEnableReclip = true;
            }
            else
            {
                bChecked = ckReclipSnotel.IsChecked ?? false;
                if (bChecked)
                {
                    bEnableReclip = true;
                }
                else
                {
                    bChecked = ckReclipSnowCos.IsChecked ?? false;
                    if (bChecked)
                    {
                        bEnableReclip = true;
                    }
                }
            }
            btnReclip.IsEnabled = bEnableReclip;
        }

    }
}
