using System;
using System.Collections.Generic;
using System.IO;
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
using ArcGIS.Desktop.Mapping;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace bagis_pro
{
    internal class DockBatchPdfExportViewModel : DockPane
    {
        private const string _dockPaneID = "bagis_pro_DockBatchPdfExport";

        protected DockBatchPdfExportViewModel()
        {
            // Set path to settings file if we need to
            if (String.IsNullOrEmpty(this.SettingsFile))
            {
                // Find batch tool settings file
                string strSettingsPath = GeneralTools.GetBagisSettingsPath();
                if (!string.IsNullOrEmpty(strSettingsPath))
                {
                    string strFullPath = strSettingsPath + @"\" + Constants.FOLDER_SETTINGS
                        + @"\" + Constants.FILE_BATCH_TOOL_SETTINGS;
                    if (!System.IO.File.Exists(strFullPath))
                    {
                        Webservices ws = new Webservices();
                        var success = Task.Run(() => ws.DownloadBatchSettingsAsync(Module1.Current.DefaultEbagisServer,
                            strFullPath));
                        if ((BA_ReturnCode) success.Result == BA_ReturnCode.Success)
                        {
                            this.SettingsFile = strFullPath;
                        }
                    }
                    else
                    {
                        this.SettingsFile = strFullPath;
                    }

                    // read JSON directly from a file
                    using (FileStream fs = File.OpenRead(this.SettingsFile))
                    {
                        using (JsonTextReader reader = new JsonTextReader(new StreamReader(fs)))
                        {
                            dynamic oBatchSettings = (JObject)JToken.ReadFrom(reader);
                            if (oBatchSettings != null)
                            {
                                Module1.Current.BatchToolSettings = oBatchSettings;
                            }
                            Publisher = (string) oBatchSettings.Publisher;
                        }
                    }
                }
            }
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

        /// <summary>
        /// Text shown near the top of the DockPane.
        /// </summary>
        private string _heading = "Batch PDF Export";
        private string _aoiFolder;
        private string _settingsFile;
        private string _publisher;
        private string _comments;
        private bool _archiveChecked = false;
        private bool _cmdRunEnabled = false;
        public string Heading
        {
            get { return _heading; }
            set
            {
                SetProperty(ref _heading, value, () => Heading);
            }
        }

        public string AoiFolder
        {
            get { return _aoiFolder; }
            set
            {
                SetProperty(ref _aoiFolder, value, () => AoiFolder);
            }
        }

        public string SettingsFile
        {
            get { return _settingsFile; }
            set
            {
                SetProperty(ref _settingsFile, value, () => SettingsFile);
            }
        }

        public string Publisher
        {
            get { return _publisher; }
            set
            {
                SetProperty(ref _publisher, value, () => Publisher);
            }
        }

        public string Comments
        {
            get { return _comments; }
            set
            {
                SetProperty(ref _comments, value, () => Comments);
            }
        }

        public bool ArchiveChecked
        {
            get { return _archiveChecked; }
            set
            {
                SetProperty(ref _archiveChecked, value, () => ArchiveChecked);
            }
        }

        public bool CmdRunEnabled
        {
            get { return _cmdRunEnabled; }
            set
            {
                SetProperty(ref _cmdRunEnabled, value, () => CmdRunEnabled);
            }
        }

        public ICommand CmdAoiFolder
        {
            get
            {
                return new RelayCommand(() =>
                {
                    //Display the filter in an Open Item dialog
                    OpenItemDialog aNewFilter = new OpenItemDialog
                    {
                        Title = "Select a basin, aoi, or folder",
                        MultiSelect = false,
                        Filter = ItemFilters.folders
                    };
                    bool? ok = aNewFilter.ShowDialog();
                    bool bOk = ok ?? false;
                    if (bOk)
                    {
                        AoiFolder = "";
                        var arrFileNames = aNewFilter.Items;
                        foreach (var item in arrFileNames)
                        {
                            AoiFolder = item.Path;
                        }
                    }
                    if (String.IsNullOrEmpty(AoiFolder))
                    {
                        CmdRunEnabled = false;
                    }
                    else
                    {
                        CmdRunEnabled = true;
                    }
                });
            }
        }

        public ICommand CmdRun
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    // Make sure the maps_publish folder exists under the selected folder
                    string strLogFile = AoiFolder + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\" + Constants.FILE_BATCH_LOG;
                    if (!Directory.Exists(Path.GetDirectoryName(strLogFile)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(strLogFile));
                    }
                    // Create initial log entry
                    string strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Starting batch tool to publish in " +
                        Path.GetDirectoryName(strLogFile) + "\r\n";
                    File.WriteAllText(strLogFile, strLogEntry);
                    // @ToDo: Make sure the folder is an AOI. This will change when we implement iteration
                    FolderType fType = await GeodatabaseTools.GetAoiFolderTypeAsync(AoiFolder);
                    if (fType != FolderType.AOI)
                    {
                        MessageBox.Show("This folder is not an AOI!");
                        return;
                    }

                    // Save off the publisher name if it is different than previous
                    string strPublisher = (string)Module1.Current.BatchToolSettings.Publisher;
                    if (!Publisher.Trim().Equals(strPublisher))
                    {
                        Module1.Current.BatchToolSettings.Publisher = Publisher;
                        String json = JsonConvert.SerializeObject(Module1.Current.BatchToolSettings, Formatting.Indented);
                        File.WriteAllText(SettingsFile, json);
                    }

                    // Make directory for required folders if they don't exist
                    // Make sure that maps and maps_publish folders exist
                    string[] arrDirectories = { AoiFolder + "\\" + Constants.FOLDER_MAPS, AoiFolder + "\\" + Constants.FOLDER_MAP_PACKAGE,
                                                AoiFolder + "\\" + Constants.FOLDER_LOGS};
                    foreach (var directory in arrDirectories)
                    {
                        if (!Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                    }

                    // Set logger to AOI directory
                    string logFolderName = AoiFolder + "\\" + Constants.FOLDER_LOGS;
                    Module1.Current.ModuleLogManager.UpdateLogFileLocation(logFolderName);

                    // Set current AOI
                    BA_Objects.Aoi oAoi = new BA_Objects.Aoi(Path.GetFileName(AoiFolder), AoiFolder);
                    Module1.Current.Aoi = oAoi;

                    // Elevation zones
                    BA_ReturnCode success = await AnalysisTools.CalculateElevationZonesAsync();

                    // Slope zones
                    string strLayer = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Surfaces, true) +
                        Constants.FILE_SLOPE;
                    string strZonesRaster = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SLOPE_ZONE;
                    string strMaskPath = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_BUFFERED_VECTOR;
                    IList<BA_Objects.Interval> lstInterval = AnalysisTools.GetSlopeClasses();
                    success = await AnalysisTools.CalculateZonesAsync(AoiFolder, strLayer,
                        lstInterval, strZonesRaster, strMaskPath, "SLOPE");

                    // Aspect zones
                    strLayer = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Surfaces, true) +
                        Constants.FILE_ASPECT;
                    strZonesRaster = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_ASPECT_ZONE;
                    strMaskPath = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_BUFFERED_VECTOR;
                    lstInterval = AnalysisTools.GetAspectClasses(Convert.ToInt16(Module1.Current.BatchToolSettings.AspectDirectionsCount));
                    success = await AnalysisTools.CalculateZonesAsync(AoiFolder, strLayer,
                        lstInterval, strZonesRaster, strMaskPath, "ASPECT");

                    // Check for PRISM units
                    string strPrismPath = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Prism, true)
                        + PrismFile.Annual.ToString();
                    string pBufferDistance = "";
                    string pBufferUnits = "";
                    string strBagisTag = await GeneralTools.GetBagisTagAsync(strPrismPath, Constants.META_TAG_XPATH);
                    if (!string.IsNullOrEmpty(strBagisTag))
                    {
                        pBufferDistance = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_BUFFER_DISTANCE, ';');
                        pBufferUnits = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_XUNIT_VALUE, ';');
                    }
                    // Clip PRISM
                    string strDefaultBufferDistance = (string)Module1.Current.BatchToolSettings.PrecipBufferDistance;
                    string strDefaultBufferUnits = (string)Module1.Current.BatchToolSettings.PrecipBufferUnits;
                    //@ToDo: re-enable
                    //success = await AnalysisTools.ClipLayersAsync(AoiFolder, Constants.DATA_TYPE_PRECIPITATION,
                    //    pBufferDistance, pBufferUnits, strDefaultBufferDistance, strDefaultBufferUnits);

                    // PRISM Zones
                    strLayer = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Prism, true) +
                        Path.GetFileName((string) Module1.Current.BatchToolSettings.AoiPrecipFile);
                    strZonesRaster = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_PRECIP_ZONE;
                    strMaskPath = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_PRISM_VECTOR;
                    lstInterval = await AnalysisTools.GetPrismClassesAsync(Module1.Current.Aoi.FilePath,
                        strLayer, (int) Module1.Current.BatchToolSettings.PrecipZonesCount);
                    success = await AnalysisTools.CalculateZonesAsync(Module1.Current.Aoi.FilePath, strLayer,
                        lstInterval, strZonesRaster, strMaskPath, "PRISM");

                    // Clip SWE
                    //@ToDo: re-enable
                    //success = await AnalysisTools.ClipSweLayersAsync(pBufferDistance, pBufferUnits,
                    //    strDefaultBufferDistance, strDefaultBufferUnits);

                    // Clip Snotel and Snow Course
                    double dblDistance = -1;
                    bool isDouble = Double.TryParse((string)Module1.Current.BatchToolSettings.SnotelBufferDistance, out dblDistance);
                    if (! isDouble)
                    {
                        dblDistance = 0;
                    }
                    string snoBufferDistance = dblDistance + " " + (string) Module1.Current.BatchToolSettings.SnotelBufferUnits;
                    //@ToDo: Renable when ready
                    //success = await AnalysisTools.ClipSnoLayersAsync(Module1.Current.Aoi.FilePath, true, snoBufferDistance,
                    //    true, snoBufferDistance);

                    //if (success == BA_ReturnCode.Success)
                    //{
                    //    double siteBufferDistanceMiles = (double)Module1.Current.BatchToolSettings.SiteBufferDistMiles;
                    //    double siteElevRangeFeet = (double)Module1.Current.BatchToolSettings.SiteElevRangeFeet;
                    //    success = await AnalysisTools.GenerateSiteLayersAsync(siteBufferDistanceMiles, siteElevRangeFeet);
                    //}

                    // Clip Roads
                    string snoBufferUnits = (string)Module1.Current.BatchToolSettings.SnotelBufferUnits;
                    string strOutputFc = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Layers, true)
                        + Constants.FILE_ROADS;
                    success = await AnalysisTools.ClipFeatureLayerAsync(AoiFolder, strOutputFc, Constants.DATA_TYPE_ROADS,
                        Convert.ToString(dblDistance), snoBufferUnits);
                    if (success == BA_ReturnCode.Success)
                    {
                        // Buffer clipped roads for analysis
                        Uri uri = new Uri(GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Layers));
                        bool bExists = await GeodatabaseTools.FeatureClassExistsAsync(uri, Constants.FILE_ROADS);
                        if (!bExists)
                        {
                            Module1.Current.ModuleLogManager.LogDebug(nameof(CmdRun),
                                "Unable to buffer roads because fs_roads layer does not exist. Process stopped!!");
                        }
                        else
                        {
                            string strDistance = Module1.Current.BatchToolSettings.RoadsAnalysisBufferDistance + " " +
                                Module1.Current.BatchToolSettings.RoadsAnalysisBufferUnits;
                            success = await AnalysisTools.GenerateProximityRoadsLayerAsync(uri, strDistance);
                        }
                    }

                    // Clip public lands
                    strOutputFc = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Layers, true)
                        + Constants.FILE_PUBLIC_LAND;
                    success = await AnalysisTools.ClipFeatureLayerAsync(AoiFolder, strOutputFc, Constants.DATA_TYPE_PUBLIC_LAND,
                        Convert.ToString(dblDistance), snoBufferUnits);
                    if (success == BA_ReturnCode.Success)
                    {
                        // Create public lands layer for potential site analysis
                        success = await AnalysisTools.GetPublicLandsAsync(AoiFolder);
                    }

                    // Clip Vegetation layer
                    string strOutputRaster = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Layers, true)
                        + Constants.FILE_VEGETATION_EVT;
                    success = await AnalysisTools.ClipRasterLayerAsync(AoiFolder, strOutputRaster, Constants.DATA_TYPE_VEGETATION,
                        Convert.ToString(dblDistance), snoBufferUnits);
                    if (success == BA_ReturnCode.Success)
                    {
                        // Create area below treeline layer for potential site analysis
                        success = await AnalysisTools.ExtractBelowTreelineAsync(AoiFolder);
                    }

                    // Generate Potential Sites layer
                    success = await AnalysisTools.CalculatePotentialSitesAreaAsync(AoiFolder);

                    // Generate Elevation Precipitation Correlation layer
                    strLayer = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Prism, true) +
                        Path.GetFileName((string)Module1.Current.BatchToolSettings.AoiPrecipFile);
                    Uri uriPrism = new Uri(GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Prism));
                    success = await AnalysisTools.CalculateElevPrecipCorr(AoiFolder, uriPrism,
                        Path.GetFileName((string)Module1.Current.BatchToolSettings.AoiPrecipFile));


                    // Generate complete PDF document
                    //@ToDo: Re-enable when ready
                    //try
                    //{
                    //    // Delete any old PDF files
                    //    foreach (var item in Constants.FILES_EXPORT_ALL_PDF)
                    //    {
                    //        string strPath = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE
                    //            + "\\" + item;
                    //        if (System.IO.File.Exists(strPath))
                    //        {
                    //            try
                    //            {
                    //                System.IO.File.Delete(strPath);
                    //            }
                    //            catch (Exception)
                    //            {
                    //                System.Windows.MessageBoxResult res =
                    //                    MessageBox.Show("Unable to delete file before creating new pdf. Do you want to close the file and try again?",
                    //                    "BAGIS-PRO", System.Windows.MessageBoxButton.YesNo);
                    //                if (res == System.Windows.MessageBoxResult.Yes)
                    //                {
                    //                    return;
                    //                }
                    //            }
                    //        }
                    //    }

                    //    success = await MapTools.PublishMapsAsync(); // export the maps to pdf
                    //    if (success != BA_ReturnCode.Success)
                    //    {
                    //        MessageBox.Show("An error occurred while generating the maps!!", "BAGIS-PRO");
                    //    }
                    //    //@ToDo: Renable when charts are working
                    //    //success = await GeneralTools.GenerateTablesAsync(false);   // export the tables to pdf
                    //    //if (success != BA_ReturnCode.Success)
                    //    //{
                    //    //    MessageBox.Show("An error occurred while generating the Excel tables!!", "BAGIS-PRO");
                    //    //}
                    //    await GeneralTools.GenerateMapsTitlePageAsync("", "");
                    //    string outputPath = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\" +
                    //          Constants.FILE_EXPORT_MAPS_ALL_PDF;
                    //    GeneralTools.PublishFullPdfDocument(outputPath);    // Put it all together into a single pdf document

                    //    MessageBox.Show("Map package exported to " + outputPath + "!!", "BAGIS-PRO");
                    //}
                    //catch (Exception e)
                    //{
                    //    MessageBox.Show("An error occurred while trying to export the maps!! " + e.Message, "BAGIS PRO");
                    //}


                    // Concluding log entry
                    strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Batch tool finished!! \r\n";
                    using (StreamWriter sw = File.AppendText(strLogFile))
                    {
                        sw.WriteLine(strLogEntry);
                    }
                });
            }
        }
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class DockBatchPdfExport_ShowButton : Button
    {
        protected override void OnClick()
        {
            var layersPane = (DockBatchPdfExportViewModel) FrameworkApplication.DockPaneManager.Find("bagis_pro_DockBatchPdfExport");

            DockBatchPdfExportViewModel.Show();
        }
    }
}
