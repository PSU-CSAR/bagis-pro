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

namespace bagis_pro.Basin
{
    /// <summary>
    /// Interaction logic for WinClipDem.xaml
    /// </summary>
    public partial class WinClipDem : ArcGIS.Desktop.Framework.Controls.ProWindow
    {
        public WinClipDem()
        {
            InitializeComponent();
            this.DataContext = new WinClipDemModel(this);
        }
        private void LblSmooth_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            string strMessage = "Smoothing DEM using a directional filter can effectively remove the " +
                "striping artifact in older USGS 7.5 minute (i.e., 30 meters) DEM. " +
                "When present, the striping is most prominent on DEM derivative " +
                "surfaces such as slope, curvature, or hillshade. Please inspect " +
                "these derivatives right after a BASIN was created. If there is clear " +
                "striping, then recreate the BASIN with the smooth DEM option " +
                "checked. A recommended filter size is 3 by 7 (height by width)";
            MessageBox.Show(strMessage, "Why Smooth DEM", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        public void SmoothDem_Checked(object s, RoutedEventArgs e)
        {
            var chkBox = s as CheckBox;
            bool bChecked = chkBox.IsChecked ?? false;
            txtFilterHeight.IsEnabled = bChecked;
            txtFilterWidth.IsEnabled = bChecked;
        }
    }
}
