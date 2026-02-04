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
using bagis_pro.AoiTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro.Menus
{
    internal class MnuAoiTools_AddRefLayers : Button
    {
        protected async override void OnClick()
        {
            BA_ReturnCode success = await MapTools.DisplayReferenceLayersAsync();
        }
    }
    internal class MnuAoiTools_AOIShapefile : Button
    {
        protected override async void OnClick()
        {
            OpenItemDialog dlgShapefile = new OpenItemDialog()
            {
                Title = "Select a polygon shapefile",
                MultiSelect = false,
                Filter = ItemFilters.Shapefiles
            };
            if (dlgShapefile.ShowDialog() == true)
            {
                IEnumerable<Item> selectedItems = dlgShapefile.Items;
                var e = selectedItems.FirstOrDefault();
                string strName = e.Name;
                string strDirectory = Path.GetDirectoryName(e.Path);
                if (strName.Contains(' '))
                {
                    MessageBox.Show("An AOI cannot be created from a shapefile with a space in the name. Please " +
                        "rename the shapefile and try again.", "BAGIS-Pro", System.Windows.MessageBoxButton.OK, 
                        System.Windows.MessageBoxImage.Error);
                    return;
                }

                string strProjection = "Missing";
                Boolean bIsPolygon = false;
                await QueuedTask.Run(() =>
                {
                    using (FileSystemDatastore shapefile = new FileSystemDatastore(new FileSystemConnectionPath(new Uri(strDirectory), FileSystemDatastoreType.Shapefile)))
                    {
                        FeatureClass featureClass = shapefile.OpenDataset<FeatureClass>(strName);
                        var classDefinition = featureClass.GetDefinition() as FeatureClassDefinition;
                        var spatialReference = classDefinition.GetSpatialReference();
                        if (spatialReference != null)
                        {
                            strProjection = spatialReference.Name;
                        }
                        if (classDefinition.GetShapeType() == GeometryType.Polygon)
                        {
                            bIsPolygon = true;
                        }
                    }
                });
                string strMessage = $@"The input shapefile must be in the same projection as the source DEM specified in the BAGIS settings! The projection of the selected shapefile is: {strProjection}";
                MessageBox.Show(strMessage, "BAGIS-Pro", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                if (bIsPolygon)
                {
                    var pane = (DockCreateAOIfromExistingBNDViewModel) FrameworkApplication.DockPaneManager.Find("bagis_pro_DockCreateAOIfromExistingBND");
                    pane.SourceFile = e.Path;
                    string newAoiName = Path.GetFileNameWithoutExtension(e.Path);
                    pane.AoiName = newAoiName;
                    pane.DemElevUnit = (string) Module1.Current.BagisSettings.DemUnits;
                    pane.AoiBufferUnits = (string)Module1.Current.BagisSettings.AoiBufferUnits;
                    DockCreateAOIfromExistingBNDViewModel.Show();
                }
                else
                {
                    MessageBox.Show("Please select a polygon shapefile as the input!", "BAGIS-Pro", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }

    internal class MnuAoiTools_Options : Button
    {
        protected override void OnClick()
        {
            MessageBox.Show("You have options!");
        }
    }

    internal class MnuAoiTools_BtnSelectAoi : Button
    {
        private WinAoiInfo _winaoiinfo = null;
        protected override void OnClick()
        {
            try
            {
                //already open?
                if (_winaoiinfo != null)
                    return;
                _winaoiinfo = new WinAoiInfo();
                _winaoiinfo.Owner = FrameworkApplication.Current.MainWindow;
                _winaoiinfo.Closed += (o, e) => { _winaoiinfo = null; };
                _winaoiinfo.Show();
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to set the AOI!! " + e.Message, "BAGIS PRO");
            }

        }
    }

}
