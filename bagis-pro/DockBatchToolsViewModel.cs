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
using ArcGIS.Desktop.Core.Geoprocessing;
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
    internal class DockBatchToolsViewModel : DockPane
    {   
      private const string _dockPaneID = "bagis_pro_DockBatchTools";
 
      protected DockBatchToolsViewModel()
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
                    if (!File.Exists(strFullPath))
                    {
                        if (!Directory.Exists(strSettingsPath + @"\" + Constants.FOLDER_SETTINGS))
                        {
                            var dirInfo = Directory.CreateDirectory(strSettingsPath + @"\" + Constants.FOLDER_SETTINGS);
                            if (dirInfo == null)
                            {
                                MessageBox.Show("Unable to create BAGIS settings folder in " + strSettingsPath +
                                    "! Process stopped.");
                                return;
                            }
                        }
                        Webservices ws = new Webservices();
                        var success = Task.Run(() => ws.DownloadBatchSettingsAsync(strFullPath));
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

        private string _heading = "Batch Report Tools";
        private string _parentFolder;
        private string _settingsFile;
        private string _publisher;
        private string _comments;
        private bool _archiveChecked = false;
        private bool _siteAnalysisChecked = true;
        private bool _cmdRunEnabled = false;
        private bool _cmdForecastEnabled = false;
        private bool _cmdSnodasEnabled = false;
        private bool _cmdLogEnabled = false;
        private bool _alwaysNearChecked = false;
        private bool _mergeAoiVChecked = true;
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

        public string ParentFolder
        {
            get { return _parentFolder; }
            set
            {
                SetProperty(ref _parentFolder, value, () => ParentFolder);
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

        public bool AlwaysNearChecked
        {
            get { return _alwaysNearChecked; }
            set
            {
                SetProperty(ref _alwaysNearChecked, value, () => AlwaysNearChecked);
            }
        }

        public bool MergeAoiVChecked
        {
            get { return _mergeAoiVChecked; }
            set
            {
                SetProperty(ref _mergeAoiVChecked, value, () => MergeAoiVChecked);
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

        public bool CmdForecastEnabled
        {
            get { return _cmdForecastEnabled; }
            set
            {
                SetProperty(ref _cmdForecastEnabled, value, () => CmdForecastEnabled);
            }
        }

        public bool CmdSnodasEnabled
        {
            get { return _cmdSnodasEnabled; }
            set
            {
                SetProperty(ref _cmdSnodasEnabled, value, () => CmdSnodasEnabled);
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
                        Filter = ItemFilters.Folders
                    };
                    bool? ok = aNewFilter.ShowDialog();
                    bool bOk = ok ?? false;
                    if (bOk)
                    {
                        ParentFolder = "";
                        var arrFileNames = aNewFilter.Items;
                        foreach (var item in arrFileNames)
                        {
                            ParentFolder = item.Path;
                        }
                    }

                    _strLogFile = ParentFolder + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\" + Constants.FILE_BATCH_LOG;
                    // Make sure the maps_publish folder exists under the selected folder
                    if (!Directory.Exists(Path.GetDirectoryName(_strLogFile)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(_strLogFile));
                    }

                    // Enable the view log button if log exists
                    CmdLogEnabled = File.Exists(_strLogFile);

                    Names.Clear();
                    IList<BA_Objects.Aoi> lstAois = await GeneralTools.GetAoiFoldersAsync(ParentFolder, _strLogFile);
                    foreach (var pAoi in lstAois)
                    {
                        Names.Add(pAoi);
                    }
                    if (Names.Count > 0)
                    {
                        CmdRunEnabled = true;
                        CmdSnodasEnabled = true;
                        CmdForecastEnabled = true;
                    }
                    else
                    {
                        MessageBox.Show("No valid AOIs were found in the selected folder!", "BAGIS-PRO");
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CmdAoiFolder),
                            "No valid AOIs were found in the selected folder!");
                        CmdRunEnabled = false;
                        CmdSnodasEnabled = false;
                        CmdForecastEnabled = false;
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

        private RelayCommand _runSnodasCommand;
        public ICommand CmdSnodas
        {
            get
            {
                if (_runSnodasCommand == null)
                    _runSnodasCommand = new RelayCommand(RunSnodasImplAsync, () => true);
                return _runSnodasCommand;
            }
        }

        private RelayCommand _runForecastCommand;
        public ICommand CmdForecast
        {
            get
            {
                if (_runForecastCommand == null)
                    _runForecastCommand = new RelayCommand(RunForecastImplAsync, () => true);
                return _runForecastCommand;
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
                string[] filePaths = Directory.GetFiles(ParentFolder + "\\" + Constants.FOLDER_MAP_PACKAGE, "*.pdf",
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
                    string aoiFolder = Names[idxRow].FilePath;
                    Names[idxRow].AoiBatchStateText = AoiBatchState.Started.ToString();  // update gui
                    string[] arrFolders = { aoiFolder + "\\" + Constants.FOLDER_MAPS, aoiFolder + "\\" + Constants.FOLDER_MAP_PACKAGE,
                                                aoiFolder + "\\" + Constants.FOLDER_LOGS};
                    foreach (var directory in arrFolders)
                    {
                        if (!Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                    }

                    // Set logger to AOI directory
                    string logFolderName = aoiFolder + "\\" + Constants.FOLDER_LOGS;
                    Module1.Current.ModuleLogManager.UpdateLogFileLocation(logFolderName);

                    // Set current AOI
                    BA_Objects.Aoi oAoi = await GeneralTools.SetAoiAsync(aoiFolder);
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

                    bool tooManySites = await AnalysisTools.TooManySitesAsync(Module1.Current.Aoi.FilePath);
                    if (tooManySites)
                    {
                        strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + " Aborting batch PDF export for " +
                            oAoi.Name + " because there are too many sites! \r\n";
                        File.AppendAllText(_strLogFile, strLogEntry);       // append
                    }
                    else
                    {
                        // Elevation zones
                        BA_ReturnCode success = await AnalysisTools.CalculateElevationZonesAsync(Module1.Current.Aoi.FilePath);
                        if (success != BA_ReturnCode.Success)
                        {
                            errorCount++;
                        }

                        // Slope zones
                        string strLayer = GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Surfaces, true) +
                            Constants.FILE_SLOPE;
                        string strZonesRaster = GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Analysis, true) +
                            Constants.FILE_SLOPE_ZONE;
                        string strMaskPath = GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_BUFFERED_VECTOR;
                        IList<BA_Objects.Interval> lstInterval = AnalysisTools.GetSlopeClasses();
                        success = await AnalysisTools.CalculateZonesAsync(aoiFolder, strLayer,
                            lstInterval, strZonesRaster, strMaskPath, "SLOPE");
                        if (success != BA_ReturnCode.Success)
                        {
                            errorCount++;
                        }

                        // Check for PRISM buffer units
                        string[] arrPrismBufferInfo = await GeneralTools.QueryBufferDistanceAsync(aoiFolder, GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Aoi),
                            Constants.FILE_AOI_PRISM_VECTOR, false);
                        string pBufferDistance = arrPrismBufferInfo[0];
                        string pBufferUnits = arrPrismBufferInfo[1];

                        // Clip PRISM
                        string strDefaultBufferDistance = (string)Module1.Current.BatchToolSettings.PrecipBufferDistance;
                        string strDefaultBufferUnits = (string)Module1.Current.BatchToolSettings.PrecipBufferUnits;
                        success = await AnalysisTools.ClipLayersAsync(aoiFolder, BA_Objects.DataSource.GetPrecipitationKey,
                            pBufferDistance, pBufferUnits, strDefaultBufferDistance, strDefaultBufferUnits);
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
                        success = await AnalysisTools.CalculateSWEDeltaAsync(aoiFolder);

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
                        success = success = await AnalysisTools.ClipSnoLayersAsync(Module1.Current.Aoi.FilePath, true, snoBufferDistance,
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
                            string[] arrSitesLayers = new string[] { Constants.FILE_SNOTEL, Constants.FILE_SNOW_COURSE, Constants.FILE_SNOLITE, Constants.FILE_COOP_PILLOW };
                            bool hasSnotel = false;
                            bool hasSnowCourse = false;
                            for (int i = 0; i < arrSitesLayers.Length; i++)
                            {
                                if (await GeodatabaseTools.CountFeaturesAsync(uri, arrSitesLayers[i]) > 0)
                                {
                                    switch (i)
                                    {
                                        case 0:
                                            hasSnotel = true;
                                            break;
                                        case 1:
                                            hasSnowCourse = true;
                                            break;
                                        case 2:
                                            hasSnotel = true;
                                            break;
                                        case 3:
                                            hasSnotel = true;
                                            break;
                                    }
                                }
                            }
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

                        string[] arrUnmanagedBufferInfo = await GeneralTools.QueryBufferDistanceAsync(aoiFolder, GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Aoi),
                            Constants.FILE_AOI_BUFFERED_VECTOR, false);
                        string unmanagedBufferDistance = arrPrismBufferInfo[0];
                        string unmanagedBufferUnits = arrPrismBufferInfo[1];
                        if (SiteAnalysisChecked)
                        {
                            // Clip Roads
                            string strOutputFc = GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Layers, true)
                                + Constants.FILE_ROADS;
                            success = await AnalysisTools.ClipFeatureLayerAsync(aoiFolder, strOutputFc, Constants.DATA_TYPE_ROADS,
                                unmanagedBufferDistance, unmanagedBufferUnits);
                            if (success != BA_ReturnCode.Success)
                            {
                                errorCount++;
                            }
                            if (success == BA_ReturnCode.Success)
                            {
                                // Buffer clipped roads for analysis
                                Uri uri = new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Layers));
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
                            strOutputFc = GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Layers, true)
                                + Constants.FILE_LAND_OWNERSHIP;
                            success = await AnalysisTools.ClipFeatureLayerAsync(aoiFolder, strOutputFc, Constants.DATA_TYPE_LAND_OWNERSHIP,
                                unmanagedBufferDistance, unmanagedBufferUnits);
                            if (success != BA_ReturnCode.Success)
                            {
                                errorCount++;
                            }
                            if (success == BA_ReturnCode.Success)
                            {
                                // Create public lands layer for potential site analysis
                                success = await AnalysisTools.GetFederalNonWildernessLandsAsync(aoiFolder);
                                if (success != BA_ReturnCode.Success)
                                {
                                    errorCount++;
                                }
                            }

                            // Clip Land cover
                            success = await AnalysisTools.ClipLandCoverAsync(aoiFolder, unmanagedBufferDistance, unmanagedBufferUnits);
                            if (success != BA_ReturnCode.Success)
                            {
                                errorCount++;
                            }

                            if (success == BA_ReturnCode.Success)
                            {
                                // Create area below forested area layer for potential site analysis
                                success = await AnalysisTools.ExtractForestedAreaAsync(aoiFolder);
                                if (success != BA_ReturnCode.Success)
                                {
                                    errorCount++;
                                }
                            }

                            // Generate Potential Sites layer
                            success = await AnalysisTools.CalculatePotentialSitesAreaAsync(aoiFolder);
                            if (success != BA_ReturnCode.Success)
                            {
                                errorCount++;
                            }
                        }

                        // Generate Elevation Precipitation Correlation layer
                        strLayer = GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Prism, true) +
                            Path.GetFileName((string)Module1.Current.BatchToolSettings.AoiPrecipFile);
                        Uri uriPrism = new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Prism));
                        success = await AnalysisTools.CalculateElevPrecipCorrAsync(aoiFolder, uriPrism,
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
                                Module1.Current.ModuleLogManager.LogError(nameof(RunImplAsync),
                                    "An error occurred while trying to load the maps. The map package cannot be exported!");
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

                            success = await GeneralTools.GenerateTablesAsync(false);   // export the tables to pdf
                            if (success != BA_ReturnCode.Success)
                            {
                                MessageBox.Show("An error occurred while generating the Excel tables!!", "BAGIS-PRO");
                                errorCount++;
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
                                    if (success != BA_ReturnCode.Success)
                                    {
                                        Module1.Current.ModuleLogManager.LogError(nameof(RunImplAsync),
                                           "Unable to generate critical precipitation zones map!!");
                                    }
                                }
                            }

                            int sitesAppendixCount = await GeneralTools.GenerateSitesTableAsync(Module1.Current.Aoi);
                            success = await GeneralTools.GenerateMapsTitlePageAsync(ReportType.Watershed, strPublisher, Comments);
                            if (success != BA_ReturnCode.Success)
                            {
                                MessageBox.Show("An error occurred while generating the Title page!!", "BAGIS-PRO");
                                errorCount++;
                            }
                            string outputPath = GeneralTools.GetFullPdfFileName(Constants.FILE_EXPORT_WATERSHED_REPORT_PDF);
                            string[] arrPieces = Module1.Current.Aoi.StationTriplet.Split(':');
                            if (arrPieces.Length != 3)
                            {
                                Module1.Current.ModuleLogManager.LogDebug(nameof(RunImplAsync), "Unable to determine station triplet for document title!");

                            }
                            else
                            {
                                string strBaseFileName = Module1.Current.Aoi.StationTriplet.Replace(':', '_') + "_Watershed-Report.pdf";
                                outputPath = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\" + strBaseFileName;
                            }
                            success = GeneralTools.PublishFullPdfDocument(outputPath, ReportType.Watershed, sitesAppendixCount);    // Put it all together into a single pdf document
                            if (success != BA_ReturnCode.Success)
                            {
                                errorCount++;
                            }
                            if (success != BA_ReturnCode.Success)
                            {
                                errorCount++;
                            }
                            else if (ArchiveChecked)
                            {
                                string reportName = Path.GetFileName(outputPath);
                                // Copy final watershed analysis report to a central location
                                if (File.Exists(outputPath))
                                {
                                    string targetPath = ParentFolder + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\" + reportName;
                                    if (!targetPath.Equals(outputPath))
                                    {
                                        File.Copy(outputPath, targetPath, true);
                                        strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Copied report to " +
                                            ParentFolder + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\" + reportName + "\r\n";
                                        File.AppendAllText(_strLogFile, strLogEntry);
                                    }
                                }
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
                            Module1.Current.ModuleLogManager.LogError(nameof(RunImplAsync),
                                e.StackTrace);
                            strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Batch PDF export failed for " +
                                oAoi.Name + ". Check for errors in the logs at " + oAoi.FilePath + "\\" + Constants.FOLDER_LOGS + "!\r\n";
                            File.AppendAllText(_strLogFile, strLogEntry);
                            Names[idxRow].AoiBatchStateText = AoiBatchState.Failed.ToString();
                        }
                    }
                }
            }   // Move on to next AOI

            // Concluding log entry
            strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Batch tool finished!! \r\n";
            using (StreamWriter sw = File.AppendText(_strLogFile))
            {
                sw.WriteLine(strLogEntry);
            }
            MessageBox.Show("Done!");
        }

        private async void RunSnodasImplAsync(object param)
        {
            string snodasLog = ParentFolder + "\\" + Constants.FOLDER_SNODAS_GEOJSON + "\\" + Constants.FILE_SNODAS_GEOJSON_LOG;
            // Make sure the maps_publish folder exists under the selected folder
            if (!Directory.Exists(Path.GetDirectoryName(snodasLog)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(snodasLog));
            }

            // Create initial log entry
            string strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Starting snodas geojson export to publish in " +
                Path.GetDirectoryName(snodasLog) + "\r\n";
            File.WriteAllText(snodasLog, strLogEntry);    // overwrite file if it exists

            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
            {
                if (Names[idxRow].AoiBatchIsSelected)
                {
                    int errorCount = 0; // keep track of any non-fatal errors
                    string aoiFolder = Names[idxRow].FilePath;
                    Names[idxRow].AoiBatchStateText = AoiBatchState.Started.ToString();  // update gui
                    strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Starting geojson export for " +
                        Names[idxRow].Name + "\r\n";
                    File.AppendAllText(snodasLog, strLogEntry);       // append

                    // generate the file(s)
                    string pointOutputPath = Path.GetTempPath() + "pourpoint.geojson";
                    string polygonOutputPath = Path.GetTempPath() + "polygon.geojson";
                    string strAoiFolder = GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Aoi);
                    // Validate and process pourpoint file
                    string strPointPath = strAoiFolder + "\\" + Constants.FILE_POURPOINT;
                    BA_ReturnCode success = BA_ReturnCode.UnknownError;
                    if (await GeodatabaseTools.FeatureClassExistsAsync(new Uri(strAoiFolder), Constants.FILE_POURPOINT))
                    {
                        if (await GeodatabaseTools.CountFeaturesAsync(new Uri(strAoiFolder), Constants.FILE_POURPOINT) == 1)
                        {
                            // Query for station information
                            string stationTriplet = "";
                            string[] arrValues = await AnalysisTools.GetStationValues(aoiFolder);
                            if (arrValues.Length == 2)
                            {
                                stationTriplet = arrValues[0];
                            }
                            if (string.IsNullOrEmpty(stationTriplet))
                            {
                                strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "ERROR:Station triplet cannot be determined. Skipping this AOI! \r\n";
                                File.AppendAllText(snodasLog, strLogEntry);       // append
                                errorCount++;
                            }
                            else
                            {
                                success = await GeoprocessingTools.FeaturesToSnodasGeoJsonAsync(strPointPath, pointOutputPath, true);
                                if (success == BA_ReturnCode.Success)
                                {
                                    strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Pourpoint geoJson exported to temp directory \r\n";
                                    File.AppendAllText(snodasLog, strLogEntry);       // append
                                }
                            }
                        }
                        else
                        {
                            strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Pourpoint file does not have one and only one feature. Skipping this AOI!! \r\n";
                            File.AppendAllText(snodasLog, strLogEntry);       // append
                            errorCount++;
                        }
                    }
                    else
                    {
                        strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Unable to locate pourpoint file for AOI. Skipping this AOI!! \r\n";
                        File.AppendAllText(snodasLog, strLogEntry);       // append
                        errorCount++;
                    }
                    // Validate and process aoi_v file
                    if (success == BA_ReturnCode.Success)
                    {
                        string strPolyPath = strAoiFolder + "\\" + Constants.FILE_AOI_VECTOR;
                        if (await GeodatabaseTools.FeatureClassExistsAsync(new Uri(strAoiFolder), Constants.FILE_AOI_VECTOR))
                        {
                            string strFcPath = strPolyPath;
                            success = await GeoprocessingTools.FeaturesToSnodasGeoJsonAsync(strFcPath, polygonOutputPath, true);
                            if (success == BA_ReturnCode.Success)
                            {
                                strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "AOI polygon geoJson exported to temp directory \r\n";
                                File.AppendAllText(snodasLog, strLogEntry);       // append
                            }
                            else
                            {
                                strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "An error occurred while processing the polygon. Skipping this AOI! \r\n";
                                File.AppendAllText(snodasLog, strLogEntry);       // append
                            }
                            if (success == BA_ReturnCode.Success)
                            {
                                Webservices ws = new Webservices();
                                string errorMessage = ws.GenerateSnodasGeoJson(pointOutputPath, polygonOutputPath,
                                    ParentFolder + "\\" + Constants.FOLDER_SNODAS_GEOJSON);
                                if (!string.IsNullOrEmpty(errorMessage))
                                {
                                    strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + errorMessage + " \r\n";
                                    File.AppendAllText(snodasLog, strLogEntry);       // append
                                    errorCount++;
                                }
                                else
                                {
                                    strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Geojson exported for " + Names[idxRow].Name + " \r\n";
                                    File.AppendAllText(snodasLog, strLogEntry);       // append
                                }
                            }
                        }
                        else
                        {
                            strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Unable to locate aoi_v file for AOI. Skipping this AOI!! \r\n";
                            File.AppendAllText(snodasLog, strLogEntry);       // append
                            errorCount++;
                        }
                    }
                    if (success == BA_ReturnCode.Success && errorCount == 0)
                    {
                        Names[idxRow].AoiBatchStateText = AoiBatchState.Completed.ToString();  // update gui
                    }
                    else
                    {
                        Names[idxRow].AoiBatchStateText = AoiBatchState.Failed.ToString();
                    }

                }
            }
            strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "GeoJson exports complete!! \r\n";
            File.AppendAllText(snodasLog, strLogEntry);       // append
            MessageBox.Show("Generated GeoJson files are available in " + ParentFolder + "\\" + Constants.FOLDER_SNODAS_GEOJSON, "BAGIS-PRO");
        }

        private async void RunForecastImplAsync(object param)
        {
            const string UPDATED_NEAR = "Updated-Near";
            const string NO_CHANGE_MATCH = "No change-Match";
            const string NOT_A_FORECAST = "Not A Forecast";
            string log = ParentFolder + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\" + Constants.FILE_FORECAST_STATION_LOG;
            // Create initial log entry
            try
            {
                FileInfo file = new FileInfo(log);
                if (file.Exists)
                {
                    using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        stream.Close();
                    }
                }
            }
            catch (IOException)
            {
                MessageBox.Show($@"Unable to write to log file {log}! Please close the .csv file and try again.", "BAGIS-PRO");
                return;
            }
            string strLogEntry = "Date,Time,AOI File Path,Old Triplet,New Triplet,Remarks\r\n";
            File.WriteAllText(log, strLogEntry);    // overwrite file if it exists
            strLogEntry = CreateLogEntry(ParentFolder, "", "", $@"Starting forecast station updates");
            File.AppendAllText(log, strLogEntry);
            BA_ReturnCode success = BA_ReturnCode.Success;
            Webservices ws = new Webservices();
            IList<string> lstMergeFeatures = new List<string>();

            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
            {
                if (Names[idxRow].AoiBatchIsSelected)
                {
                    int errorCount = 0; // keep track of any non-fatal errors
                    string aoiFolder = Names[idxRow].FilePath;
                    Names[idxRow].AoiBatchStateText = AoiBatchState.Started.ToString();  // update gui
                    strLogEntry = CreateLogEntry(aoiFolder, "", "", $@"Starting forecast station update");
                    File.AppendAllText(log, strLogEntry);       // append

                    Uri ppUri = new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Aoi));
                    string strTriplet = "";
                    string strStationName = "";
                    string strAwdbId = "";
                    string strNearTriplet = "";
                    string strNearStationName = "";
                    string strNearAwdbId = "";
                    string strRemark = "";

                    // GET THE STATION TRIPLET FROM THE POURPOINT
                    IDictionary<string, string> dictEdits = new Dictionary<string, string>()
                    { { Constants.FIELD_STATION_TRIPLET, ""},
                      { Constants.FIELD_STATION_NAME, ""},
                      { Constants.FIELD_AWDB_ID, ""}
                    }; 
                    QueryFilter queryFilter = new QueryFilter();
                    if (await GeodatabaseTools.FeatureClassExistsAsync(ppUri, Constants.FILE_POURPOINT))
                    {
                        string[] arrFields = new string[] { Constants.FIELD_STATION_TRIPLET, Constants.FIELD_STATION_NAME, Constants.FIELD_AWDB_ID };
                        foreach (string strField in arrFields)
                        {
                            // Check for the field, if it exists query the value
                            if (await GeodatabaseTools.AttributeExistsAsync(ppUri, Constants.FILE_POURPOINT, strField))
                            {
                                string strValue = await GeodatabaseTools.QueryTableForSingleValueAsync(ppUri, Constants.FILE_POURPOINT,
                                    strField, queryFilter);
                                switch (strField)
                                {
                                    case Constants.FIELD_STATION_TRIPLET:
                                        strTriplet = strValue;
                                        dictEdits[Constants.FIELD_STATION_TRIPLET] = strTriplet;
                                        break;
                                    case Constants.FIELD_STATION_NAME:
                                        strStationName = strValue;
                                        dictEdits[Constants.FIELD_STATION_NAME] = strStationName;
                                        break;
                                    case Constants.FIELD_AWDB_ID:
                                        strAwdbId = strValue;
                                        dictEdits[Constants.FIELD_AWDB_ID] = strAwdbId;
                                        break;
                                }
                            }
                            // Add the field if it is missing
                            else
                            {
                                success = await GeoprocessingTools.AddFieldAsync(ppUri.LocalPath + "\\" + Constants.FILE_POURPOINT, strField, "TEXT");
                            }
                        }
                    }
                    else
                    {
                        strLogEntry = CreateLogEntry(aoiFolder, "", "", $@"ERROR: Unable to open {ppUri.LocalPath + "\\" + Constants.FILE_POURPOINT}");
                        File.AppendAllText(log, strLogEntry);       // append
                        success = BA_ReturnCode.ReadError;
                        errorCount++;
                    }

                    // QUERY THE MASTER LIST FOR THE STATION TRIPLET
                    string[] arrSearch = { Constants.FIELD_STATION_TRIPLET, Constants.FIELD_AWDB_ID, Constants.FIELD_NWCCNAME };
                    string[] arrFound = new string[arrSearch.Length];
                    string strWsUri = (string)Module1.Current.BatchToolSettings.MasterAoiList + "/0";  // Append layer ordinal to the uri
                    if (!AlwaysNearChecked)
                    {
                        queryFilter = new QueryFilter();
                        queryFilter.WhereClause = Constants.FIELD_STATION_TRIPLET + " = '" + strTriplet + "'";
                        arrFound = await ws.QueryServiceForValuesAsync(new Uri((string)Module1.Current.BatchToolSettings.MasterAoiList), "0", arrSearch, queryFilter);
                        if (arrFound != null && arrFound.Length == arrSearch.Length && arrFound[0] != null)
                        {
                            strRemark = NO_CHANGE_MATCH;
                        }
                    }

                    if (!NO_CHANGE_MATCH.Equals(strRemark))
                    {
                        // Use the NEAR tool to find the closest Pourpoint
                        success = await GeoprocessingTools.NearAsync(ppUri.LocalPath + "\\" + Constants.FILE_POURPOINT, strWsUri, Constants.VALUE_FORECAST_STATION_SEARCH_RADIUS);
                        if (success == BA_ReturnCode.Success)
                        {
                            queryFilter = new QueryFilter();
                            arrFound = new string[arrSearch.Length];
                            string strNearId = await GeodatabaseTools.QueryTableForSingleValueAsync(ppUri, Constants.FILE_POURPOINT,
                                Constants.FIELD_NEAR_ID, queryFilter);
                            if (!String.IsNullOrEmpty(strNearId))
                            {
                                queryFilter.WhereClause = Constants.FIELD_OBJECT_ID + " = " + strNearId;
                                arrFound = await ws.QueryServiceForValuesAsync(new Uri((string)Module1.Current.BatchToolSettings.MasterAoiList), "0", arrSearch, queryFilter);
                                if (arrFound != null && arrFound.Length == 3 && arrFound[0] != null)
                                {
                                    strNearTriplet = arrFound[0];
                                    strNearAwdbId = arrFound[1].Trim('"');
                                    strNearStationName = arrFound[2];
                                    if (!string.IsNullOrEmpty(strNearTriplet))
                                    {
                                        strRemark = UPDATED_NEAR;
                                    }
                                }
                                else
                                {
                                    strRemark = NOT_A_FORECAST;
                                }
                            }
                            else
                            {
                                strRemark = NOT_A_FORECAST;
                            }
                            //Delete fields added by NEAR process: NEAR_DIST and NEAR_ID
                            string[] arrFieldsToDelete = new string[] { Constants.FIELD_NEAR_ID, Constants.FIELD_NEAR_DIST };
                            success = await GeoprocessingTools.DeleteFeatureClassFieldsAsync(ppUri.LocalPath + "\\" + Constants.FILE_POURPOINT, arrFieldsToDelete);
                        }
                    }
                    switch (strRemark)
                    {
                        case UPDATED_NEAR:
                            dictEdits[Constants.FIELD_AWDB_ID] = strNearAwdbId;
                            dictEdits[Constants.FIELD_STATION_TRIPLET] = strNearTriplet;
                            dictEdits[Constants.FIELD_STATION_NAME] = strNearStationName;
                            success = await GeodatabaseTools.UpdateFeatureAttributesAsync(ppUri, Constants.FILE_POURPOINT,
                                new QueryFilter(), dictEdits);
                            lstMergeFeatures.Add(ppUri.LocalPath + "\\" + Constants.FILE_AOI_VECTOR);
                            break;
                        case NO_CHANGE_MATCH:
                            lstMergeFeatures.Add(ppUri.LocalPath + "\\" + Constants.FILE_AOI_VECTOR);
                            break;
                    }
                    // DO WE NEED TO UPDATE AOI_V?
                    string strAoiVPath = ppUri.LocalPath + "\\" + Constants.FILE_AOI_VECTOR;
                    string[] arrPpFields = { Constants.FIELD_STATION_TRIPLET, Constants.FIELD_STATION_NAME, Constants.FIELD_AWDB_ID };
                    foreach (var strField in arrPpFields)
                    {
                        if (!await GeodatabaseTools.AttributeExistsAsync(ppUri, Constants.FILE_AOI_VECTOR, strField))
                        {
                            success = await GeoprocessingTools.AddFieldAsync(strAoiVPath, strField, "TEXT");
                        }
                    }
                    if (success == BA_ReturnCode.Success)
                    {
                        string strValue = await GeodatabaseTools.QueryTableForSingleValueAsync(ppUri, Constants.FILE_AOI_VECTOR,
                            Constants.FIELD_STATION_TRIPLET, queryFilter);
                        if (!dictEdits[Constants.FIELD_STATION_TRIPLET].Equals(strValue))
                        {
                            // UPDATE VALUES ON AOI_V
                            success = await GeodatabaseTools.UpdateFeatureAttributesAsync(ppUri, Constants.FILE_AOI_VECTOR,
                                new QueryFilter(), dictEdits);
                        }
                    }
                    else
                    {
                        errorCount++;
                        success = BA_ReturnCode.ReadError;
                    }

                    strLogEntry = CreateLogEntry(aoiFolder, strTriplet, strNearTriplet, strRemark);
                    File.AppendAllText(log, strLogEntry);       // append
                    if (success == BA_ReturnCode.Success && errorCount == 0)
                    {
                        Names[idxRow].AoiBatchStateText = AoiBatchState.Completed.ToString();  // update gui
                    }
                    else
                    {
                        Names[idxRow].AoiBatchStateText = AoiBatchState.Failed.ToString();
                    }
                }
            }   // MOVE ON TO NEXT AOI
            strLogEntry = CreateLogEntry(ParentFolder, "", "", $@"Forecast station updates complete!!");
            File.AppendAllText(log, strLogEntry);       // append
            string strMergeMessage = "";
            if (MergeAoiVChecked)
            {
                success = await MergeAoiVectorsAsync(lstMergeFeatures, log);
                if (success == BA_ReturnCode.Success)
                {
                    strLogEntry = CreateLogEntry(ParentFolder, "", "", $@"aoi_v feature class merge complete!!");
                    strMergeMessage = $@" aoi_v polygons have been merged to {ParentFolder}\{Constants.FOLDER_MAP_PACKAGE}\{Constants.FILE_MERGE_GDB}\{Constants.FILE_MERGED_AOI_POLYS}";
                }
                else
                {
                    strLogEntry = CreateLogEntry(ParentFolder, "", "", $@"ERROR: aoi_v feature class merge encountered 1 or more errors!!");
                }
                File.AppendAllText(log, strLogEntry);       // append
            }
            MessageBox.Show($@"Forecast station updates done!!{strMergeMessage}");
        }

        private string CreateLogEntry(string strAoiPath, string strOldTriplet, string strNewTriplet, string strRemarks)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($@"{DateTime.Now.ToString("MM/dd/yy")},{DateTime.Now.ToString("H:mm:ss")},");
            sb.Append($@"{strAoiPath},{strOldTriplet},{strNewTriplet},");
            sb.Append($@"{strRemarks}{Environment.NewLine}");
            return sb.ToString();
        }

        private async Task<BA_ReturnCode> MergeAoiVectorsAsync(IList<string> lstMergeFeatures, string strLog)
        {
            BA_ReturnCode success = BA_ReturnCode.WriteError;
            string mergeGdbPath = $@"{ParentFolder}\{Constants.FOLDER_MAP_PACKAGE}\{Constants.FILE_MERGE_GDB}";
            var environments = Geoprocessing.MakeEnvironmentArray(workspace: ParentFolder);
            if (!Directory.Exists(mergeGdbPath))
            {
                var parameters = Geoprocessing.MakeValueArray($@"{ParentFolder}\{Constants.FOLDER_MAP_PACKAGE}", Constants.FILE_MERGE_GDB);
                var gpResult = await Geoprocessing.ExecuteToolAsync("CreateFileGDB_management", parameters, environments,
                                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    return success;
                }
            }
            string strOutputPath = $@"{mergeGdbPath}\{Constants.FILE_MERGED_AOI_POLYS}";
            StringBuilder sb = new StringBuilder();
            int intError = 0;
            string mapAwdbId = "$@\"awdb_id \"\"awdb_id\"\" true true false 30 Text 0 0,First,#,\"";
            string mapStationTriplet = "$@\"stationTriplet \"\"stationTriplet\"\" true true false 255 Text 0 0,First,#,\"";
            string mapStationName = $@"stationName ""stationName"" true true false 255 Text 0 0,First,#,";

            if (lstMergeFeatures.Count > 0)
            {
                success = await GeoprocessingTools.CopyFeaturesAsync(ParentFolder, lstMergeFeatures[0], strOutputPath);
                if (success == BA_ReturnCode.Success)
                {
                    for (int i = 1; i < lstMergeFeatures.Count; i++)
                    {
                        sb.Append(lstMergeFeatures[i]);
                        sb.Append(";");
                        //StringBuilder sbAoiName = new StringBuilder($@"AOINAME ""AOINAME"" true true false 40 Text 0 0,First,#,");
                        //sbAoiName.Append($@"{fc},AOINAME,0,40, ");
                        StringBuilder sbAwdbId = new StringBuilder(mapAwdbId);
                        sbAwdbId.Append($@"{lstMergeFeatures[i]},0,30, ");
                        StringBuilder sbStationTriplet = new StringBuilder(mapStationTriplet);   
                        sbStationTriplet.Append($@"{lstMergeFeatures[i]},stationTriplet,0,255, ");
                        StringBuilder sbStationName = new StringBuilder(mapStationName);
                        sbStationName.Append($@"{lstMergeFeatures[i]},stationName,0,255, ");
                        string[] arrFieldMapping = new string[3];
                        //arrFieldMapping[0] = sbAoiName.ToString().TrimEnd(',');
                        arrFieldMapping[0] = sbAwdbId.ToString().TrimEnd(',');
                        //arrFieldMapping[2] = sbBasin.ToString().TrimEnd(',');
                        arrFieldMapping[1] = sbStationTriplet.ToString().TrimEnd(',');
                        arrFieldMapping[2] = sbStationName.ToString().TrimEnd(',');

                        var parameters = Geoprocessing.MakeValueArray(lstMergeFeatures[i], strOutputPath, "NO_TEST", arrFieldMapping);
                        var gpResult = await Geoprocessing.ExecuteToolAsync("Append_management", parameters, environments,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        if (gpResult.IsFailed)
                        {
                            string strLogEntry = CreateLogEntry(ParentFolder, "", "", $@"An error occurred while appending the {lstMergeFeatures[i]} file! {gpResult.ReturnValue}");
                            File.AppendAllText(strLog, strLogEntry);       // append
                            intError++;
                        }
                    }
                }
            }
            if (intError == 0)
            {
                success = BA_ReturnCode.Success;
            }
            return success;
        }

    }

  /// <summary>
  /// Button implementation to show the DockPane.
  /// </summary>
	internal class DockBatchTools_ShowButton : Button
	{
		protected override void OnClick()
		{
            try
            {
                DockBatchToolsViewModel.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to show Batch Tools pane! " + ex.Message, "BAGIS-PRO");
                MessageBox.Show("Stack trace" + ex.StackTrace, "BAGIS-PRO");
            }
        }
  }	
}
