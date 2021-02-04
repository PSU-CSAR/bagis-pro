using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;


namespace bagis_pro
{
    internal class DockMapPackageViewModel : DockPane
    {
        private const string _dockPaneID = "bagis_pro_DockMapPackage";

        protected DockMapPackageViewModel()
        {
            AnalysisLayers = new ObservableCollection<BA_Objects.AnalysisLayer>();
            AnalysisLayers.CollectionChanged += AnalysisLayersCollectionChanged;

            BA_Objects.AnalysisLayer oLayer = new BA_Objects.AnalysisLayer(Constants.MAPS_ELEV_ZONE, true,
                @"analysis.gdb\" + Constants.FILE_ELEV_ZONE);
            AnalysisLayers.Add(oLayer);
            BA_Objects.AnalysisLayer oLayer2 = new BA_Objects.AnalysisLayer(Constants.MAPS_PRISM_ZONE, true,
                @"analysis.gdb\" + Constants.FILE_ELEV_ZONE);
            AnalysisLayers.Add(oLayer2);
            BA_Objects.AnalysisLayer oLayer3 = new BA_Objects.AnalysisLayer("Sites Zones", true,
                @"analysis.gdb\" + Constants.FILE_ELEV_ZONE);
            AnalysisLayers.Add(oLayer3);


        }

        /// <summary>
        /// Show the DockPane.
        /// </summary>
        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Activate();
        }


        public ObservableCollection<BA_Objects.AnalysisLayer> AnalysisLayers { get; set; }

        public void AnalysisLayersCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Allows us to respond when properties of items in the collection change; ie: including the AOI in the list to migrate
            if (e.OldItems != null)
                foreach (BA_Objects.AnalysisLayer oldItem in e.OldItems)
                    oldItem.PropertyChanged -= AnalysisLayersCollection_PropertyChanged;

            if (e.NewItems != null)
                foreach (BA_Objects.AnalysisLayer newItem in e.NewItems)
                    newItem.PropertyChanged += AnalysisLayersCollection_PropertyChanged;
        }

        private void AnalysisLayersCollection_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            //ManageRunButton();
        }

    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class DockMapPackage_ShowButton : Button
    {
        protected override void OnClick()
        {
            DockMapPackageViewModel.Show();
        }
    }
}
