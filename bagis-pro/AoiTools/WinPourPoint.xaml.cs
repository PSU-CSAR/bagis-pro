using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace bagis_pro.AoiTools
{
    /// <summary>
    /// Interaction logic for WinPourPoint.xaml
    /// </summary>
    public partial class WinPourPoint : ArcGIS.Desktop.Framework.Controls.ProWindow
    {
        public WinPourPoint()
        {
            InitializeComponent();
            this.DataContext = new WinPourPointModel(this);
        }

        private async void MyGrid_Loaded(object sender, RoutedEventArgs e)
        {
            WinPourPointModel oModel = this.DataContext as WinPourPointModel;
            await oModel.InitializeAsync();
            if (lstPourPoints.Items.Count > 0)
            {
                lstPourPoints.SelectedIndex = 0;
            }
        }
        private async void lstPourPoints_SelectionChanged (object sender, SelectionChangedEventArgs e)
        {
            ListBox listBox = sender as ListBox;

            FeatureLayer oFeatureLayer = null;
            await QueuedTask.Run(() =>
            {
                Map map = null;
                MapProjectItem mpi =
                    Project.Current.GetItems<MapProjectItem>()
                    .FirstOrDefault(m => m.Name.Equals(Constants.MAPS_DEFAULT_MAP_NAME, StringComparison.CurrentCultureIgnoreCase));
                if (mpi != null)
                {
                    map = mpi.GetMap();
                    Layer oLayer =
                     map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(Constants.MAPS_GAUGE_STATIONS, StringComparison.CurrentCultureIgnoreCase));
                    if (oLayer != null)
                    {
                        oFeatureLayer = (FeatureLayer)oLayer;
                    }
                }
            });
            if (listBox.SelectedItem != null)
            {
                string ppName = Convert.ToString(listBox.SelectedItem);
                await QueuedTask.Run(() =>
                {                    
                    if (oFeatureLayer != null)
                    {
                        QueryFilter filter = new QueryFilter();
                        filter.WhereClause = $@"{Constants.FIELD_NAME} = '{ppName.Trim()}'";
                        oFeatureLayer.Select(filter);
                    }
                });
            }
            else
            {
                await QueuedTask.Run(() =>
                {
                    oFeatureLayer.ClearSelection();
                });
            }
        }
    }


}
