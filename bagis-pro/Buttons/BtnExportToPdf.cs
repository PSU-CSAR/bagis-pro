using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Layouts;

namespace bagis_pro.Buttons
{
    internal class BtnExportToPdf : Button
    {
        protected async override void OnClick()
        {
            ReportType rType = ReportType.Watershed;
            try
            {
                string outputDirectory = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE;
                if (! System.IO.Directory.Exists(outputDirectory))
                {
                    System.IO.Directory.CreateDirectory(outputDirectory);
                }

                // Delete any old PDF files
                //string[] arrFilesToDelete = Constants.FILES_EXPORT_WATERSHED_PDF.Concat(Constants.FILES_EXPORT_SITE_ANALYSIS_PDF).ToArray();
                foreach (var item in Constants.FILES_EXPORT_WATERSHED_PDF)
                {
                    string strPath = GeneralTools.GetFullPdfFileName(item);
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
                string strTestState = Constants.STATES_WATERSHED_MAP_BUTTONS[0];
                //if (rType.Equals(ReportType.SiteAnalysis))
                //{
                //    strTestState = Constants.STATES_SITE_ANALYSIS_MAP_BUTTONS[0];
                //}
                if (!FrameworkApplication.State.Contains(strTestState))
                {
                    success = await MapTools.DisplayMaps(Module1.Current.Aoi.FilePath, oLayout, true);
                    success = await MapTools.DisplayLegendAsync(Constants.MAPS_DEFAULT_MAP_FRAME_NAME, oLayout,
                        "ArcGIS Colors", "1.5 Point",true);
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
                        if (layoutPane.LayoutView != null &&
                            layoutPane.LayoutView.Layout == oLayout) //if there is a match, activate the view  
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
                success = await MapTools.PublishMapsAsync(rType, Constants.PDF_EXPORT_RESOLUTION); // export the maps to pdf
                if (success != BA_ReturnCode.Success)
                {
                    MessageBox.Show("An error occurred while generating the maps!!", "BAGIS-PRO");
                }
                // Only run critical precip for watershed report
                if (rType.Equals(ReportType.Watershed))
                {
                    success = await GeneralTools.GenerateTablesAsync(false);   // export the tables to pdf
                    if (success != BA_ReturnCode.Success)
                    {
                        MessageBox.Show("An error occurred while generating the Excel tables!!", "BAGIS-PRO");
                    }
                    else
                    {
                        // Generate the critical precip map; It has to follow the tables
                        Uri uriAnalysis = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis));
                        if (await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, Constants.FILE_CRITICAL_PRECIP_ZONE))
                        {
                            success = await MapTools.DisplayCriticalPrecipitationZonesMapAsync(uriAnalysis);
                            string strButtonState = "MapButtonPalette_BtnCriticalPrecipZone_State";
                            if (success.Equals(BA_ReturnCode.Success))
                                Module1.ActivateState(strButtonState);
                            int foundS1 = strButtonState.IndexOf("_State");
                            string strMapButton = strButtonState.Remove(foundS1);
                            ICommand cmd = FrameworkApplication.GetPlugInWrapper(strMapButton) as ICommand;
                            Module1.Current.ModuleLogManager.LogDebug(nameof(OnClick),
                                "About to toggle map button " + strMapButton);
                            if ((cmd != null))
                            {
                                do
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(0.4));  // build in delay until the command can execute
                                }
                                while (!cmd.CanExecute(null));
                                cmd.Execute(null);
                            }

                            do
                            {
                                await Task.Delay(TimeSpan.FromSeconds(0.4));  // build in delay so maps can load
                            }
                            while (Module1.Current.MapFinishedLoading == false);
                            success = await GeneralTools.ExportMapToPdfAsync(Constants.PDF_EXPORT_RESOLUTION);    // export map to pdf
                            if (success != BA_ReturnCode.Success)
                            {
                                MessageBox.Show("Unable to generate critical precipitation zones map!!", "BAGIS-PRO");
                            }
                        }
                    }
                }

                int sitesAppendixCount = await GeneralTools.GenerateSitesTableAsync(Module1.Current.Aoi);
                string strPublisher = (string)Module1.Current.BatchToolSettings.Publisher;
                success = await GeneralTools.GenerateMapsTitlePageAsync(rType, strPublisher, "");
                string[] arrPieces = Module1.Current.Aoi.StationTriplet.Split(':');
                string outputPath = GeneralTools.GetFullPdfFileName(Constants.FILE_EXPORT_WATERSHED_REPORT_PDF);
                if (arrPieces.Length != 3)
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(OnClick), "Unable to determine station triplet for document title!");

                }
                else
                {
                    string strBaseFileName = Module1.Current.Aoi.StationTriplet.Replace(':', '_') + "_Watershed-Report.pdf";
                    outputPath = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\" + strBaseFileName;
                }
                //if (rType.Equals(ReportType.SiteAnalysis))
                //{
                //    outputPath = GeneralTools.GetFullPdfFileName(Constants.FILE_EXPORT_SITE_ANALYSIS_REPORT_PDF);
                //}
                GeneralTools.PublishFullPdfDocument(outputPath, rType, sitesAppendixCount);    // Put it all together into a single pdf document

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
