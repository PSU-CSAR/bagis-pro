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
using System.Windows.Forms;
using System.Windows.Input;

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
            lstRaster.Items.SortDescriptions.Add(
                new System.ComponentModel.SortDescription("", System.ComponentModel.ListSortDirection.Ascending));
            lstVector.Items.SortDescriptions.Add(
                new System.ComponentModel.SortDescription("", System.ComponentModel.ListSortDirection.Ascending));
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
        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            lstRaster.SelectedItems.Clear();
            lstVector.SelectedItems.Clear();
        }

    }
}
