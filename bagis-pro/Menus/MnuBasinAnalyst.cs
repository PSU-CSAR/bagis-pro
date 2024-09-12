using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
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

namespace bagis_pro.Menus
{
    internal class MnuBasinAnalyst_AddRefLayers : Button
    {
        protected override void OnClick()
        {
            Module1.ToggleState("MnuBasinAnalyst_BasinInfo_State");
            MessageBox.Show("Add reference layers");
        }
    }

    internal class MnuBasinAnalyst_SaveMxd : Button
    {
        protected override void OnClick()
        {
            MessageBox.Show("Save AOI MXD");
        }
    }

    internal class MnuBasinAnalyst_BasinInfo : Button
    {
        protected override void OnClick()
        {
            MessageBox.Show("Basin info");
        }
    }

    internal class MnuBasinAnalyst_AoiUtilities : Button
    {
        protected override void OnClick()
        {
            MessageBox.Show("AOI Utilities");
        }
    }

    internal class MnuBasinAnalyst_AOIShapefile : Button
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

                }
                else
                {
                    MessageBox.Show("Please select a polygon shapefile as the input!", "BAGIS-Pro", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }

    internal class MnuBasinAnalyst_Options : Button
    {
        protected override void OnClick()
        {
            MessageBox.Show("You have options!");
        }
    }

    internal class MnuBasinAnalyst_About : Button
    {
        protected override void OnClick()
        {
            MessageBox.Show("About BAGIS");
        }
    }
}
