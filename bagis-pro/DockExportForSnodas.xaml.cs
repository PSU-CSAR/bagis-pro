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
    /// Interaction logic for DockExportForSnodasView.xaml
    /// </summary>
    public partial class DockExportForSnodasView : UserControl
    {
        public DockExportForSnodasView()
        {
            InitializeComponent();
        }

        private void StationTripletChanged(object sender, RoutedEventArgs e)
        {
            // Couldn't get 2 separate controls to line up well on the form
            string strStationTriplet = txtStationTriplet.Text.Replace(":", "_");
            tbOutputPathLabel.Text = $@"The name of the output file is {strStationTriplet}.geojson";
        }
    }
}
