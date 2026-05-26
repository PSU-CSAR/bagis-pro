using bagis_pro.AoiTools;
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
    /// Interaction logic for WinBasinSettings.xaml
    /// </summary>
    public partial class WinBasinOptions : ArcGIS.Desktop.Framework.Controls.ProWindow
    {
        public WinBasinOptions()
        {
            InitializeComponent();
            this.DataContext = new WinBasinOptionsModel(this);            
        }

        private async void MyGrid_Loaded(object sender, RoutedEventArgs e)
        {
            WinBasinOptionsModel oModel = this.DataContext as WinBasinOptionsModel;
            await oModel.InitializeAsync();
        }
    }
}
