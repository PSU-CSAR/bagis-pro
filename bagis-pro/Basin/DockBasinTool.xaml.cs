using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace bagis_pro.Basin
{
    /// <summary>
    /// Interaction logic for DockBasinToolView.xaml
    /// </summary>
    public partial class DockBasinToolView : UserControl
    {
        public DockBasinToolView()
        {
            InitializeComponent();
        }
        private async void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = sender as ListViewItem;
            if (item != null && item.IsSelected)
            {
                FolderEntry oFolderEntry = item.Content as FolderEntry;
                DockBasinToolViewModel oModel = (DockBasinToolViewModel)DataContext;
                _ = await oModel.LstFolders_MouseDoubleClick(LstFolders.SelectedIndex, oFolderEntry);
            }
        }
        private async void ListViewItem_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var item = sender as ListViewItem;
            if (item != null)
            {                
                FolderEntry oFolderEntry = item.Content as FolderEntry;
                DockBasinToolViewModel oModel = (DockBasinToolViewModel)DataContext;
                if (oModel != null && oFolderEntry != null)
                {
                    _ = await oModel.LstFolders_PreviewMouseDown(oFolderEntry);
                }
            }
        }
    }
}
