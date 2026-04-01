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
    /// Interaction logic for WinCreateAoi.xaml
    /// </summary>
    public partial class WinCreateAoi : ArcGIS.Desktop.Framework.Controls.ProWindow
    {
        public WinCreateAoi()
        {
            InitializeComponent();
            this.DataContext = new WinCreateAoiModel(this);
        }
        private void CkSnapPP_Checked(object sender, RoutedEventArgs e)
        {
            bool bChecked = ckSnapPP.IsChecked ?? false;
            txtSnapDistance.IsEnabled = bChecked;
        }

        private void CkAoiBuffer_Checked(object sender, RoutedEventArgs e)
        {
            bool bChecked = ckAoiBuffer.IsChecked ?? false;
            txtBufferDistance.IsEnabled = bChecked;
            txtPrismBufferDistance.IsEnabled = bChecked;
        }

        private void LblBuffer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            string strMessage = "Layers can be clipped to an AOI using a buffered AOI boundaries." +
        " This practice allows users to include data outside the AOI boundaries in basin analysis." +
        " When this option is checked, all AOI associated layers, including DEM," +
        " its derivatives, SNOTEL, snow courses, and other participating layers" +
        " are clipped to the AOI using the buffered boundaries.\r\n \r\n" +
        "Due to the significantly coarser resolution of PRISM precipitation layers, " +
        " a different buffer distance is always used in clipping PRISM layers." +
        " The default buffer distance for PRISM is 1000 meters." +
        " Using any value smaller than 1000 could result in missing PRISM pixel values within the AOI boundaries.";
            MessageBox.Show(strMessage, "Why Buffer an AOI", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
