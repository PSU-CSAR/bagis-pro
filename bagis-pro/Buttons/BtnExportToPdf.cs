using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
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
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace bagis_pro.Buttons
{
    internal class BtnExportToPdf : Button
    {
        protected async override void OnClick()
        {
            try
            {
                string outputDirectory = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE;
                if (! System.IO.Directory.Exists(outputDirectory))
                {
                    System.IO.Directory.CreateDirectory(outputDirectory);
                }

                // Delete any old PDF files
                foreach (var item in Constants.FILES_EXPORT_ALL_PDF)
                {
                    string strPath = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE
                        + "\\" + item;
                    if (System.IO.File.Exists(strPath))
                    {
                        try
                        {
                            System.IO.File.Delete(strPath);
                        }
                        catch (Exception)
                        {
                            System.Windows.MessageBoxResult res =
                                MessageBox.Show("Unable to delete file before creating new pdf. Do you want to close the file and try again?",
                                "BAGIS-PRO", System.Windows.MessageBoxButton.YesNo);
                            if (res == System.Windows.MessageBoxResult.Yes)
                            {
                                return;
                            }
                        }
                    }
                }

                Layout oLayout = await MapTools.GetDefaultLayoutAsync(Constants.MAPS_DEFAULT_LAYOUT_NAME);

                // Load the maps if they aren't in the viewer already
                BA_ReturnCode success = BA_ReturnCode.Success;
                if (!FrameworkApplication.State.Contains(Constants.STATES_MAP_BUTTONS[0]))
                {
                    success = await MapTools.DisplayMaps(Module1.Current.Aoi.FilePath, oLayout, true);
                }

                if (success != BA_ReturnCode.Success)
                {
                    MessageBox.Show("Unable to load maps. The map package cannot be exported!!", "BAGIS-PRO");
                    return;
                }

                if (oLayout != null)
                {
                    bool bFoundIt = false;
                    //A layout view may exist but it may not be active
                    //Iterate through each pane in the application and check to see if the layout is already open and if so, activate it
                    foreach (var pane in FrameworkApplication.Panes)
                    {
                        if (!(pane is ILayoutPane layoutPane))  //if not a layout view, continue to the next pane    
                            continue;
                        if (layoutPane.LayoutView.Layout == oLayout) //if there is a match, activate the view  
                        {
                            (layoutPane as Pane).Activate();
                            bFoundIt = true;
                        }
                    }
                    if (!bFoundIt)
                    {
                        ILayoutPane iNewLayoutPane = await FrameworkApplication.Panes.CreateLayoutPaneAsync(oLayout); //GUI thread
                        (iNewLayoutPane as Pane).Activate();
                    }
                }
                // Legend
                success = await MapTools.DisplayLegendAsync(oLayout, "ArcGIS Colors", "1.5 Point");

                success = await MapTools.PublishMapsAsync(); // export the maps to pdf
                if (success != BA_ReturnCode.Success)
                {
                    MessageBox.Show("An error occurred while generating the maps!!", "BAGIS-PRO");
                }
                success = await GeneralTools.GenerateTablesAsync(false);   // export the tables to pdf
                if (success != BA_ReturnCode.Success)
                {
                    MessageBox.Show("An error occurred while generating the Excel tables!!", "BAGIS-PRO");
                }
                string strPublisher = (string)Module1.Current.BatchToolSettings.Publisher;
                success = await GeneralTools.GenerateMapsTitlePageAsync(strPublisher, "");
                string outputPath = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\" +
                      Constants.FILE_EXPORT_MAPS_ALL_PDF;
                GeneralTools.PublishFullPdfDocument(outputPath);    // Put it all together into a single pdf document

                MessageBox.Show("Map package exported to " + outputPath + "!!", "BAGIS-PRO");
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to export the maps!! " + e.Message, "BAGIS PRO");
            }
        }
    }

    internal class BtnExcelTables : Button
    {
        protected async override void OnClick()
        {
            try
            {
                Module1.DeactivateState("BtnExcelTables_State");
                var cmdShowHistory = FrameworkApplication.GetPlugInWrapper("esri_geoprocessing_showToolHistory") as ICommand;
                if (cmdShowHistory != null)
                {
                    if (cmdShowHistory.CanExecute(null))
                    {
                        cmdShowHistory.Execute(null);
                    }
                }
                BA_ReturnCode success = await GeneralTools.GenerateTablesAsync(true);
                Module1.ActivateState("BtnExcelTables_State");
                if (!success.Equals(BA_ReturnCode.Success))
                {
                    MessageBox.Show("An error occurred while generating the Excel tables!!", "BAGIS-PRO");
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("An exception occurred while trying to export the tables!! " + e.Message, "BAGIS PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(OnClick), 
                    "An error occurred while trying to export the tables!! " + e.Message);
            }
        }
    }
}
