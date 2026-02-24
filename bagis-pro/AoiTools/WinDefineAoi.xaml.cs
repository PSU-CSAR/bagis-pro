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

namespace bagis_pro.AoiTools
{
    /// <summary>
    /// Interaction logic for WinDefineAoi.xaml
    /// </summary>
    public partial class WinDefineAoi : ArcGIS.Desktop.Framework.Controls.ProWindow
    {
        public WinDefineAoi()
        {
            InitializeComponent();
            this.DataContext = new WinDefineAoiModel(this);
        }
        private async void MyGrid_Loaded(object sender, RoutedEventArgs e)
        {
            WinDefineAoiModel oModel = this.DataContext as WinDefineAoiModel;
            await oModel.InitializeAsync();
            if (lstAoi.Items.Count > 0)
            {
                lstAoi.SelectedIndex = 0;
            }
        }
    }
}
