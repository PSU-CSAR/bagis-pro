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
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace bagis_pro
{
    internal class DockAdminToolsViewModel : DockPane
    {
        private const string _dockPaneID = "bagis_pro_DockAdminTools";

        protected DockAdminToolsViewModel() 
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

        /// <summary>
        /// Text shown near the top of the DockPane.
        /// </summary>
        private string _heading = "Batch Report Tools";
        private string _parentFolder;
        private string _settingsFile;
        private string _snodasFolder;
        private string _publisher;
        private string _comments;
        private bool _archiveChecked = false;
        private bool _siteAnalysisChecked = true;
        private string _strLogFile;
        private bool _cmdRunEnabled = false;
        private bool _cmdForecastEnabled = false;
        private bool _cmdSnodasEnabled = false;
        private bool _cmdLogEnabled = false;


        public string Heading
        {
            get => _heading;
            set => SetProperty(ref _heading, value);
        }

        public string ParentFolder
        {
            get { return _parentFolder; }
            set
            {
                SetProperty(ref _parentFolder, value, () => ParentFolder);
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

        public bool CmdRunEnabled
        {
            get { return _cmdRunEnabled; }
            set
            {
                SetProperty(ref _cmdRunEnabled, value, () => CmdRunEnabled);
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

        public string SnodasFolder
        {
            get { return _snodasFolder; }
            set
            {
                SetProperty(ref _snodasFolder, value, () => SnodasFolder);
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

        public ObservableCollection<BA_Objects.Aoi> Names { get; set; }

        public System.Windows.Input.ICommand CmdAoiFolder
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
                    SnodasFolder = ParentFolder + "\\" + Constants.FOLDER_SNODAS_GEOJSON;
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

            // Reset batch states
            ResetAoiBatchStateText();

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
                            success = await GeoprocessingTools.FeaturesToSnodasGeoJsonAsync(strPointPath, pointOutputPath, true);
                            if (success == BA_ReturnCode.Success)
                            {
                                strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Pourpoint geoJson exported to temp directory \r\n";
                                File.AppendAllText(snodasLog, strLogEntry);       // append
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

        private void ResetAoiBatchStateText()
        {
            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
            {
                Names[idxRow].AoiBatchStateText = AoiBatchState.Waiting.ToString();
            }
        }
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class DockAdminTools_ShowButton : Button
    {
        protected override void OnClick()
        {
            DockAdminToolsViewModel.Show();
        }
    }
}
