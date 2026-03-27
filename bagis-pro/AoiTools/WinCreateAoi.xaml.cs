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

        }
    }
}
