using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using ArcGIS.Desktop.Layouts;
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
                        if ((BA_ReturnCode)success.Result == BA_ReturnCode.Success)
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
                            Publisher = (string)oBatchSettings.Publisher;
                        }
                    }
                }
            }
            Names = new ObservableCollection<BA_Objects.Aoi>();
            ArchiveChecked = false;
            SiteAnalysisChecked = true;
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
        private string _heading = "Batch Report Export";
        private string _aoiFolder;
        private string _settingsFile;
        private string _publisher;
        private string _comments;
        private bool _archiveChecked = false;
        private bool _siteAnalysisChecked = true;
        private bool _cmdRunEnabled = false;
        private bool _cmdLogEnabled = false;
        private ObservableCollection<string> _aoiList = new ObservableCollection<string>();
        private string _strLogFile;
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

        public bool SiteAnalysisChecked
        {
            get { return _siteAnalysisChecked; }
            set
            {
                SetProperty(ref _siteAnalysisChecked, value, () => SiteAnalysisChecked);
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

        public bool CmdLogEnabled
        {
            get { return _cmdLogEnabled; }
            set
            {
                SetProperty(ref _cmdLogEnabled, value, () => CmdLogEnabled);
            }
        }

        public ObservableCollection<string> AoiList
        {
            get { return _aoiList; }
            set
            {
                SetProperty(ref _aoiList, value, () => AoiList);
            }
        }

        public ObservableCollection<BA_Objects.Aoi> Names { get; set; }

        public ICommand CmdAoiFolder
        {
            get
            {
                return new RelayCommand(async () =>
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

                    _strLogFile = AoiFolder + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\" + Constants.FILE_BATCH_LOG;
                    // Make sure the maps_publish folder exists under the selected folder
                    if (!Directory.Exists(Path.GetDirectoryName(_strLogFile)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(_strLogFile));
                    }

                    // Enable the view log button if log exists
                    CmdLogEnabled = File.Exists(_strLogFile);

                    Names.Clear();
                    IList<BA_Objects.Aoi> lstAois = await GeneralTools.GetAoiFoldersAsync(AoiFolder, _strLogFile);
                    foreach (var pAoi in lstAois)
                    {
                        Names.Add(pAoi);
                    }
                    if (Names.Count > 0)
                    {
                        CmdRunEnabled = true;
                    }
                    else
                    {
                        MessageBox.Show("No valid AOIs were found in the selected folder!", "BAGIS-PRO");
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CmdAoiFolder),
                            "No valid AOIs were found in the selected folder!");
                        CmdRunEnabled = false;
                    }
                });
            }
        }

        public ICommand CmdRun
        {
            get
            {
                if (_runCommand == null)
                    _runCommand = new RelayCommand(RunImplAsync, () => true);
                return _runCommand;
            }
        }

        public ICommand CmdLog
        {
            get
            {
                return new RelayCommand(() =>
                {
                    if (File.Exists(_strLogFile))
                    {
                        System.Diagnostics.Process.Start(_strLogFile);
                    }
                    else
                    {
                        MessageBox.Show("Could not find log file!", "BAGIS-PRO");
                        CmdLogEnabled = false;
                    }
                    
                });
            }
        }

        /// <summary>
        /// Command to browse for item and thumbnail
        /// </summary>
        private RelayCommand _runCommand;
        public ICommand RunCommand
        {
            get
            {
                if (_runCommand == null)
                    _runCommand = new RelayCommand(RunImplAsync, () => true);
                return _runCommand;
            }
        }

        private async void RunImplAsync(object param)
        {
            // Create initial log entry
            string strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Starting batch tool to publish in " +
                Path.GetDirectoryName(_strLogFile) + "\r\n";
            File.WriteAllText(_strLogFile, strLogEntry);    // overwrite file if it exists

            // Check for existing map package files and warn user
            if (ArchiveChecked)
            {
                string[] filePaths = Directory.GetFiles(AoiFolder + "\\" + Constants.FOLDER_MAP_PACKAGE, "*.pdf",
                             SearchOption.TopDirectoryOnly);
                if (filePaths.Length > 0)
                {
                    System.Windows.MessageBoxResult res = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("BAGIS-PRO found at least one .pdf document in the " +
                        "maps\\publish folder. These document(s) may be overwritten during the batch process. Uncheck " +
                        "the 'Copy Reports' checkbox to stop copying documents to the maps\\publish folder. " +
                        "The map packages will still be created in each AOI. Do you wish to continue and overwrite " +
                        "the documents ?", "BAGIS-PRO",
                        System.Windows.MessageBoxButton.YesNo);
                    if (res != System.Windows.MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
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
            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
            {
                if (Names[idxRow].AoiBatchIsSelected)
                {
                    int errorCount = 0; // keep track of any non-fatal errors
                    AoiFolder = Names[idxRow].FilePath;
                    Names[idxRow].AoiBatchStateText = AoiBatchState.Started.ToString();  // update gui
                    string[] arrFolders = { AoiFolder + "\\" + Constants.FOLDER_MAPS, AoiFolder + "\\" + Constants.FOLDER_MAP_PACKAGE,
                                                AoiFolder + "\\" + Constants.FOLDER_LOGS};
                    foreach (var directory in arrFolders)
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
                    BA_Objects.Aoi oAoi = await GeneralTools.SetAoiAsync(AoiFolder);
                    if (Module1.Current.CboCurrentAoi != null)
                    {
                        FrameworkApplication.Current.Dispatcher.Invoke(() =>
                        {
                            // Do something on the GUI thread
                            Module1.Current.CboCurrentAoi.SetAoiName(oAoi.Name);
                        });
                    }

                    // Create opening log entry for AOI
                    strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Starting batch PDF export for " +
                        oAoi.Name + "\r\n";
                    File.AppendAllText(_strLogFile, strLogEntry);       // append

                    // Bring GP History tool forward
                    var cmdShowHistory = FrameworkApplication.GetPlugInWrapper("esri_geoprocessing_showToolHistory") as ICommand;
                    if (cmdShowHistory != null)
                    {
                        if (cmdShowHistory.CanExecute(null))
                        {
                            cmdShowHistory.Execute(null);
                        }
                    }

                    oAoi = Module1.Current.Aoi;
                    // Elevation zones
                    BA_ReturnCode success = await AnalysisTools.CalculateElevationZonesAsync(Module1.Current.Aoi.FilePath);
                    if (success != BA_ReturnCode.Success)
                    {
                        errorCount++;
                    }

                    // Slope zones
                    string strLayer = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Surfaces, true) +
                        Constants.FILE_SLOPE;
                    string strZonesRaster = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SLOPE_ZONE;
                    string strMaskPath = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_BUFFERED_VECTOR;
                    IList<BA_Objects.Interval> lstInterval = AnalysisTools.GetSlopeClasses();
                    success = await AnalysisTools.CalculateZonesAsync(AoiFolder, strLayer,
                        lstInterval, strZonesRaster, strMaskPath, "SLOPE");
                    if (success != BA_ReturnCode.Success)
                    {
                        errorCount++;
                    }

                    // Check for PRISM buffer units
                    string[] arrPrismBufferInfo = await GeneralTools.QueryBufferDistanceAsync(AoiFolder, GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Aoi),
                        Constants.FILE_AOI_PRISM_VECTOR, false);
                    string pBufferDistance = arrPrismBufferInfo[0];
                    string pBufferUnits = arrPrismBufferInfo[1];

                    // Clip PRISM
                    string strDefaultBufferDistance = (string)Module1.Current.BatchToolSettings.PrecipBufferDistance;
                    string strDefaultBufferUnits = (string)Module1.Current.BatchToolSettings.PrecipBufferUnits;
                    success = await AnalysisTools.ClipLayersAsync(AoiFolder, Constants.DATA_TYPE_PRECIPITATION,
                        pBufferDistance, pBufferUnits, strDefaultBufferDistance, strDefaultBufferUnits);
                    if (success == BA_ReturnCode.Success)
                    {
                        success = await AnalysisTools.UpdateSitesPropertiesAsync(Module1.Current.Aoi.FilePath, SiteProperties.Precipitation);
                    }
                    if (success != BA_ReturnCode.Success)
                    {
                        errorCount++;
                    }

                    // PRISM Zones
                    strLayer = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Prism, true) +
                        Path.GetFileName((string)Module1.Current.BatchToolSettings.AoiPrecipFile);
                    strZonesRaster = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_PRECIP_ZONE;
                    success = await AnalysisTools.CalculatePrecipitationZonesAsync(strLayer, strZonesRaster);
                    if (success != BA_ReturnCode.Success)
                    {
                        errorCount++;
                    }

                    // Winter Precipitation
                    success = await AnalysisTools.GenerateWinterPrecipitationLayerAsync(oAoi);
                    if (success != BA_ReturnCode.Success)
                    {
                        errorCount++;
                    }

                    // Clip SWE
                    success = await AnalysisTools.ClipSweLayersAsync(pBufferDistance, pBufferUnits,
                        strDefaultBufferDistance, strDefaultBufferUnits);
                    if (success != BA_ReturnCode.Success)
                    {
                        errorCount++;
                    }

                    // Generate SWE Delta Layers
                    success = await AnalysisTools.CalculateSWEDeltaAsync(AoiFolder);

                    // Clip Snotel and Snow Course
                    double dblDistance = -1;
                    bool isDouble = Double.TryParse((string)Module1.Current.BatchToolSettings.SnotelBufferDistance, out dblDistance);
                    if (!isDouble)
                    {
                        dblDistance = 0;
                    }
                    Module1.Current.ModuleLogManager.LogDebug(nameof(RunImplAsync), "Buffer distance from settings: " + dblDistance);
                    string snoBufferDistance = dblDistance + " " + (string)Module1.Current.BatchToolSettings.SnotelBufferUnits;
                    Module1.Current.ModuleLogManager.LogDebug(nameof(RunImplAsync), "Sites buffer distance string: " + snoBufferDistance);
                    success = await AnalysisTools.ClipSnoLayersAsync(Module1.Current.Aoi.FilePath, true, snoBufferDistance,
                        true, snoBufferDistance);
                    if (success != BA_ReturnCode.Success)
                    {
                        errorCount++;
                    }

                    // Represented Area
                    if (success == BA_ReturnCode.Success)
                    {
                        double siteBufferDistanceMiles = (double)Module1.Current.BatchToolSettings.SiteBufferDistMiles;
                        double siteElevRangeFeet = (double)Module1.Current.BatchToolSettings.SiteElevRangeFeet;
                        success = await AnalysisTools.GenerateSiteLayersAsync(siteBufferDistanceMiles, siteElevRangeFeet);
                        if (success != BA_ReturnCode.Success)
                        {
                            errorCount++;
                        }

                        // Sites Zones
                        Uri uri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers));
                        bool hasSnotel = await GeodatabaseTools.FeatureClassExistsAsync(uri, Constants.FILE_SNOTEL);
                        bool hasSnowCourse = await GeodatabaseTools.FeatureClassExistsAsync(uri, Constants.FILE_SNOW_COURSE);
                        if (hasSnotel || hasSnowCourse)
                        {
                            success = await AnalysisTools.CalculateSitesZonesAsync(Module1.Current.Aoi.FilePath, hasSnotel, hasSnowCourse);
                            if (success != BA_ReturnCode.Success)
                            {
                                errorCount++;
                            }
                        }
                        else
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(CmdRun),
                                "No sites found to create sites zone layers!!");
                        }
                    }
                    // Precipitation Contribution; Passing in -1 for threshold so we use STDEV
                    success = await AnalysisTools.CalculatePrecipitationContributionAsync(Module1.Current.Aoi.FilePath, -1);
                    if (success != BA_ReturnCode.Success)
                    {
                        errorCount++;
                    }

                    // Quarterly Precipitation Contribution
                    success = await AnalysisTools.CalculateQuarterlyPrecipitationAsync(Module1.Current.Aoi);
                    if (success != BA_ReturnCode.Success)
                    {
                        errorCount++;
                    }

                    // Aspect zones
                    success = await AnalysisTools.CalculateAspectZonesAsync();
                    if (success != BA_ReturnCode.Success)
                    {
                        errorCount++;
                    }

                    string[] arrUnmanagedBufferInfo = await GeneralTools.QueryBufferDistanceAsync(AoiFolder, GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Aoi),
                        Constants.FILE_AOI_BUFFERED_VECTOR, false);
                    string unmanagedBufferDistance = arrPrismBufferInfo[0];
                    string unmanagedBufferUnits = arrPrismBufferInfo[1];
                    if (SiteAnalysisChecked)
                    {
                        // Clip Roads
                        string strOutputFc = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Layers, true)
                            + Constants.FILE_ROADS;
                        success = await AnalysisTools.ClipFeatureLayerAsync(AoiFolder, strOutputFc, Constants.DATA_TYPE_ROADS,
                            unmanagedBufferDistance, unmanagedBufferUnits);
                        if (success != BA_ReturnCode.Success)
                        {
                            errorCount++;
                        }
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
                                if (success != BA_ReturnCode.Success)
                                {
                                    errorCount++;
                                }
                            }
                        }

                        // Clip public lands
                        strOutputFc = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Layers, true)
                            + Constants.FILE_PUBLIC_LAND;
                        success = await AnalysisTools.ClipFeatureLayerAsync(AoiFolder, strOutputFc, Constants.DATA_TYPE_PUBLIC_LAND,
                            unmanagedBufferDistance, unmanagedBufferUnits);
                        if (success != BA_ReturnCode.Success)
                        {
                            errorCount++;
                        }
                        if (success == BA_ReturnCode.Success)
                        {
                            // Create public lands layer for potential site analysis
                            success = await AnalysisTools.GetFederalNonWildernessLandsAsync(AoiFolder);
                            if (success != BA_ReturnCode.Success)
                            {
                                errorCount++;
                            }
                        }

                        // Clip Vegetation layer
                        string strOutputRaster = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Layers, true)
                            + Constants.FILE_VEGETATION_EVT;
                        success = await AnalysisTools.ClipRasterLayerAsync(AoiFolder, strOutputRaster, Constants.DATA_TYPE_VEGETATION,
                            unmanagedBufferDistance, unmanagedBufferUnits);
                        if (success != BA_ReturnCode.Success)
                        {
                            errorCount++;
                        }
                        if (success == BA_ReturnCode.Success)
                        {
                            // Create area below forested area layer for potential site analysis
                            success = await AnalysisTools.ExtractForestedAreaAsync(AoiFolder);
                            if (success != BA_ReturnCode.Success)
                            {
                                errorCount++;
                            }
                        }

                        // Generate Potential Sites layer
                        success = await AnalysisTools.CalculatePotentialSitesAreaAsync(AoiFolder);
                        if (success != BA_ReturnCode.Success)
                        {
                            errorCount++;
                        }
                    }

                    // Clip Land cover
                    success = await AnalysisTools.ClipLandCoverAsync(AoiFolder, unmanagedBufferDistance, unmanagedBufferUnits);
                    
                    // Generate Elevation Precipitation Correlation layer
                    strLayer = GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Prism, true) +
                        Path.GetFileName((string)Module1.Current.BatchToolSettings.AoiPrecipFile);
                    Uri uriPrism = new Uri(GeodatabaseTools.GetGeodatabasePath(AoiFolder, GeodatabaseNames.Prism));
                    success = await AnalysisTools.CalculateElevPrecipCorrAsync(AoiFolder, uriPrism,
                        Path.GetFileName((string)Module1.Current.BatchToolSettings.AoiPrecipFile));
                    if (success != BA_ReturnCode.Success)
                    {
                        errorCount++;
                    }
                    if (success == BA_ReturnCode.Success)
                    {
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CmdRun),
                            "Generated Elevation Precipitation Correlation layer");
                    }

                    // Generate complete PDF document
                    try
                    {
                        // Delete any old PDF files
                        //string[] arrAllPdfFiles = new string[Constants.FILES_EXPORT_WATERSHED_PDF.Length + FILES_EXPORT_SITE_ANALYSIS_PDF.Length];
                        //Array.Copy(Constants.FILES_EXPORT_WATERSHED_PDF, arrAllPdfFiles, Constants.FILES_EXPORT_WATERSHED_PDF.Length);
                        //Array.Copy(Constants.FILES_EXPORT_SITE_ANALYSIS_PDF, 0, arrAllPdfFiles, 
                        //    Constants.FILES_EXPORT_WATERSHED_PDF.Length, Constants.FILES_EXPORT_SITE_ANALYSIS_PDF.Length);
                        foreach (var item in Constants.FILES_EXPORT_WATERSHED_PDF)
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

                        // Always load the maps in case we are running through multiple Aois
                        success = await MapTools.DisplayMaps(Module1.Current.Aoi.FilePath, oLayout, false);
                        if (success != BA_ReturnCode.Success)
                        {
                            MessageBox.Show("Unable to load maps. The map package cannot be exported!!", "BAGIS-PRO");
                            Names[idxRow].AoiBatchStateText = AoiBatchState.Failed.ToString();
                            return;
                        }
                        // Legend
                        success = await MapTools.DisplayLegendAsync(Constants.MAPS_DEFAULT_MAP_FRAME_NAME, oLayout,
                            "ArcGIS Colors", "1.5 Point", true);
                        if (success != BA_ReturnCode.Success)
                        {
                            errorCount++;
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
                                await FrameworkApplication.Current.Dispatcher.Invoke(async () =>
                                {
                                    // Do something on the GUI thread
                                    ILayoutPane iNewLayoutPane = await FrameworkApplication.Panes.CreateLayoutPaneAsync(oLayout); //GUI thread
                                    (iNewLayoutPane as Pane).Activate();
                                });
                            }
                        }

                        int pdfExportResolution = Constants.PDF_EXPORT_RESOLUTION;
                        if (Module1.Current.BatchToolSettings.PdfExportResolution != null)
                        {
                            pdfExportResolution = (int)Module1.Current.BatchToolSettings.PdfExportResolution;
                        }

                        success = await MapTools.PublishMapsAsync(ReportType.Watershed, pdfExportResolution); // export the watershed maps to pdf
                        if (success != BA_ReturnCode.Success)
                        {
                            MessageBox.Show("An error occurred while generating the watershed characteristics maps!!", "BAGIS-PRO");
                            errorCount++;
                        }

                        //if (SiteAnalysisChecked)
                        //{
                        //    success = await MapTools.PublishMapsAsync(ReportType.SiteAnalysis); // export the site analysis maps to pdf
                        //    if (success != BA_ReturnCode.Success)
                        //    {
                        //        MessageBox.Show("An error occurred while generating the site analysis maps!!", "BAGIS-PRO");
                        //        errorCount++;
                        //    }
                        //}
 
                        success = await GeneralTools.GenerateTablesAsync(false);   // export the tables to pdf
                        if (success != BA_ReturnCode.Success)
                        {
                            MessageBox.Show("An error occurred while generating the Excel tables!!", "BAGIS-PRO");
                            errorCount++;
                        }
                        else
                        {
                           // Generate the crtical precip map; It has to follow the tables
                           Uri uriAnalysis = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis));
                           if (await GeodatabaseTools.FeatureClassExistsAsync(uriAnalysis, Constants.FILE_CRITICAL_PRECIP_ZONE))
                           {
                                success = await MapTools.DisplayCriticalPrecipitationZonesMap(uriAnalysis);
                                string strButtonState = "MapButtonPalette_BtnCriticalPrecipZone_State";
                                if (success.Equals(BA_ReturnCode.Success))
                                      Module1.ActivateState(strButtonState);
                                int foundS1 = strButtonState.IndexOf("_State");
                                string strMapButton = strButtonState.Remove(foundS1);
                                    ICommand cmd = FrameworkApplication.GetPlugInWrapper(strMapButton) as ICommand;
                                    Module1.Current.ModuleLogManager.LogDebug(nameof(RunImplAsync),
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
                                    success = await GeneralTools.ExportMapToPdfAsync(150);    // export map to pdf
                                if (success == BA_ReturnCode.Success)
                                {
                                    // append the map and chart together for posting
                                    IList<string> lstToConcat = new List<string>();
                                    lstToConcat.Add(GeneralTools.GetFullPdfFileName(Constants.FILE_EXPORT_MAP_CRITICAL_PRECIPITATION_ZONES_PDF));
                                    lstToConcat.Add(GeneralTools.GetFullPdfFileName(Constants.FILE_EXPORT_TABLE_PRECIP_REPRESENT_PDF));
                                    success = GeneralTools.ConcatenatePagesInPdf(GeneralTools.GetFullPdfFileName(Constants.FILE_EXPORT_CRITICAL_PRECIPITATION_ZONES_PDF),
                                        lstToConcat);
                                }
                                else
                                {
                                     Module1.Current.ModuleLogManager.LogError(nameof(RunImplAsync),
                                        "Unable to generate critical precipitation zones map!!");
                                }
                            }
                        }

                        success = await GeneralTools.GenerateSitesTableAsync(Module1.Current.Aoi);
                        success = await GeneralTools.GenerateMapsTitlePageAsync(ReportType.Watershed, strPublisher, Comments);
                        if (success != BA_ReturnCode.Success)
                        {
                            MessageBox.Show("An error occurred while generating the Title page!!", "BAGIS-PRO");
                            errorCount++;
                        }
                        string outputPath = GeneralTools.GetFullPdfFileName(Constants.FILE_EXPORT_WATERSHED_REPORT_PDF);
                        success = GeneralTools.PublishFullPdfDocument(outputPath, ReportType.Watershed);    // Put it all together into a single pdf document
                        if (success != BA_ReturnCode.Success)
                        {
                            errorCount++;
                        }
                        //if (SiteAnalysisChecked)
                        //{
                        //    success = await GeneralTools.GenerateMapsTitlePageAsync(ReportType.SiteAnalysis, strPublisher, Comments);
                        //    outputPath = GeneralTools.GetFullPdfFileName(Constants.FILE_EXPORT_SITE_ANALYSIS_REPORT_PDF);
                        //    success = GeneralTools.PublishFullPdfDocument(outputPath, ReportType.SiteAnalysis);    // Put it all together into a single pdf document
                        //}
                        if (success != BA_ReturnCode.Success)
                        {
                            errorCount++;
                        }
                        else if (ArchiveChecked)
                        {
                            string reportName = Constants.FILE_EXPORT_WATERSHED_REPORT_PDF;
                            // Copy final watershed analysis report to a central location
                            if (File.Exists(outputPath))
                            {
                                File.Copy(outputPath, GeneralTools.GetFullPdfFileName(reportName), true);
                            }
                            //if (SiteAnalysisChecked)
                            //{
                            //    reportName = Constants.FILE_EXPORT_SITE_ANALYSIS_REPORT_PDF;
                            //    File.Copy(outputPath, GeneralTools.GetFullPdfFileName(reportName), true);
                            //}
                        }
                        // Create closing log entry for AOI
                        if (errorCount == 0)
                        {
                            strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Completed batch PDF export for " +
                            oAoi.Name + ". The output is located at " + oAoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE + "\r\n";
                            Names[idxRow].AoiBatchStateText = AoiBatchState.Completed.ToString();
                        }
                        else
                        {
                            strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Completed batch PDF export WITH ERRORS for " +
                            oAoi.Name + ". The output is located at " + oAoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE + "\r\n" +
                            "Check for errors in the logs at " + oAoi.FilePath + "\\" + Constants.FOLDER_LOGS + "! \r\n";
                            Names[idxRow].AoiBatchStateText = AoiBatchState.Errors.ToString();
                        }
                        File.AppendAllText(_strLogFile, strLogEntry);                        
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("An error occurred while running the Batch PDF Tool!! " + e.Message, "BAGIS PRO");
                        Module1.Current.ModuleLogManager.LogError(nameof(RunImplAsync),
                            e.StackTrace);
                        strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Batch PDF export failed for " +
                            oAoi.Name + ". Check for errors in the logs at " + oAoi.FilePath + "\\" + Constants.FOLDER_LOGS + "!\r\n";
                        File.AppendAllText(_strLogFile, strLogEntry);
                        Names[idxRow].AoiBatchStateText = AoiBatchState.Failed.ToString();
                    }
                }
            }
            MessageBox.Show("Done!");

            // Concluding log entry
            strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Batch tool finished!! \r\n";
            using (StreamWriter sw = File.AppendText(_strLogFile))
            {
                sw.WriteLine(strLogEntry);
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
