using ArcGIS.Desktop.Core.Utilities;
using bagis_pro.BA_Objects;
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
    /// Interaction logic for DockCreateAOIfromExistingBNDView.xaml
    /// </summary>
    public partial class DockCreateAOIfromExistingBNDView : UserControl
    {
        public DockCreateAOIfromExistingBNDView()
        {
            InitializeComponent();
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
            MessageBox.Show(strMessage, "Why Smooth DEM",MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void LblBuffer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            string strMessage = "Layers can be clipped to an AOI using a buffered AOI boundaries." + 
        " This practice allows users to include data outside the AOI boundaries in basin analysis." + 
        " When this option is checked, all AOI associated layers, including DEM," + 
        " its derivatives, SNOTEL, snow courses, and other participating layers" + 
        " are clipped to the AOI using the buffered boundaries.\r\n \r\n " +
        "Due to the significantly coarser resolution of PRISM precipitation layers, " +
        " a different buffer distance is always used in clipping PRISM layers."+
        " The default buffer distance for PRISM is 1000 meters."+
        " Using any value smaller than 1000 could result in missing PRISM pixel values within the AOI boundaries.";
            MessageBox.Show(strMessage, "Why Smooth DEM", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void BufferAOI_Checked(object s, RoutedEventArgs e)
        {
            var chkBox = s as CheckBox;
            bool bChecked = chkBox.IsChecked ?? false;
            txtBufferDistance.IsEnabled = bChecked;
            txtPrismBufferDist.IsEnabled = bChecked;
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
