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
        protected async override void OnClick()
        {
            try
            {
                OpenItemDialog selectAoiDialog = new OpenItemDialog()
                {
                    Title = "Select AOI Folder",
                    MultiSelect = false,
                    Filter = ItemFilters.Folders
                };
                if (selectAoiDialog.ShowDialog() == true)
                {
                    Module1.DeactivateState("Aoi_Selected_State");
                    IEnumerable<Item> selectedItems = selectAoiDialog.Items;
                    var e = selectedItems.FirstOrDefault();
                    BA_Objects.Aoi oAoi = await GeneralTools.SetAoiAsync(e.Path, null);
                    if (oAoi != null)
                    {
                        Module1.Current.CboCurrentAoi.SetAoiName(oAoi.Name);
                        MessageBox.Show("AOI is set to " + oAoi.Name + "!", "BAGIS PRO");

                        if (!oAoi.ValidForecastData)
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.Append("This AOI does not have the required forecast station information. ");
                            sb.Append("Use the Batch Tools menu to run the 'Forecast Station Data' tool to update ");
                            sb.Append("the forecast station data. Next use the Batch Tools menu to run the ");
                            sb.Append("'Generate AOI Reports' tool to create the analysis layers required by BAGIS-Pro.");
                            MessageBox.Show(sb.ToString(), "BAGIS PRO");
                        }
                        else
                        {
                            string[] arrButtonNames = { "bagis_pro_Menus_MnuMaps_BtnMapLoad", "bagis_pro_Buttons_BtnExcelTables",
                                "bagis_pro_WinExportPdf"};
                            int intButtonsDisabled = 0;
                            for (int i = 0; i < arrButtonNames.Length; i++)
                            {
                                var plugin = FrameworkApplication.GetPlugInWrapper(arrButtonNames[i]);
                                if (plugin == null)
                                {
                                    intButtonsDisabled++;
                                }
                                else if (!plugin.Enabled)
                                {
                                    intButtonsDisabled++;
                                }
                            }
                            if (intButtonsDisabled > 0)
                            {
                                StringBuilder sb = new StringBuilder();
                                sb.Append("This AOI is missing at least one required layer. Use the Batch Tools menu to run the ");
                                sb.Append("'Generate AOI Reports' tool to create the analysis layers required by BAGIS-Pro.");
                                MessageBox.Show(sb.ToString(), "BAGIS PRO");
                            }
                        }
                        // @ToDo: Re-enable when I'm ready to work on DockAoiInfo
                        //DockAoiInfoViewModel.Show();

                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to set the AOI!! " + e.Message, "BAGIS PRO");
            }

        }
    }

}
