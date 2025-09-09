using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using bagis_pro.BA_Objects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace bagis_pro
{
    internal class DockAdminToolsViewModel : DockPane
    {
        private const string _dockPaneID = "bagis_pro_DockAdminTools";
        private const string _include = "Include";

        protected DockAdminToolsViewModel() 
        {
            try
            {
                BA_ReturnCode success = GeneralTools.LoadBagisSettings();
                if (success == BA_ReturnCode.Success && Module1.Current.BagisSettings != null)
                {
                    Publisher = (string)Module1.Current.BagisSettings.Publisher;
                    FireIncrementYears = (int)Module1.Current.BagisSettings.FireIncrementYears;
                    NifcMinYear = (int)Module1.Current.BagisSettings.FireNifcMinYear;
                    MtbsMinYear = (int)Module1.Current.BagisSettings.FireMtbsMinYear;
                    FireDataClipYears = (int)Module1.Current.BagisSettings.FireDataClipYears;
                    SettingsFile = $@"{GeneralTools.GetBagisSettingsPath()}\{Constants.FOLDER_SETTINGS}\{Constants.FILE_BAGIS_SETTINGS}";
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred loading the batch tools! " + e.StackTrace);
            }
            Names = new ObservableCollection<BA_Objects.Aoi>();
            Names.CollectionChanged += ContentCollectionChanged;
            ArchiveChecked = true;
            TasksEnabled = false;
            FireDataEnabled = false;
            FireReportEnabled = false;
            Clip_Nifc_Checked = true;
            Clip_Mtbs_Checked = true;
            FilterFireStatus = new ObservableCollection<string>();
            FilterFireStatus.Add(_include);
            FilterFireStatus.Add(AoiBatchState.MissingFireData.ToString());
            FilterFireStatus.Add(AoiBatchState.HasReport.ToString());
            FilterFireStatus.Add(AoiBatchState.MissingReport.ToString());
            FilterFireStatus.Add(AoiBatchState.NoFire.ToString());
            SelectedFireStatus = _include;
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
        private string _snodasFolder;
        private string _statisticsFolder;
        private string _publisher;
        private string _comments;
        private bool _archiveChecked = false;
        private string _strLogFile;
        private string _strForecastLogFile;
        private string _strSnodasLogFile;
        private string _strGenStatisticsLogFile;
        private string _strFireDataLogFile;
        private string _strFireReportLogFile;
        private string _strFireMapsLogFile;
        private bool _cmdRunEnabled = false;
        private bool _cmdForecastEnabled = false;
        private bool _cmdSnodasEnabled = false;
        private bool _cmdToggleEnabled = false;
        private bool _cmdGenStatisticsEnabled = false;
        private bool _cmdLogEnabled = false;
        private bool _cmdForecastLogEnabled = false;
        private bool _cmdSnodasLogEnabled = false;
        private bool _cmdGenStatisticsLogEnabled = false;
        private bool _tasksEnabled = false;
        private bool _alwaysNearChecked = false;
        private bool _mergeAoiVChecked = true;
        private bool _updateStationDataChecked = false;
        private bool _fireDataEnabled = false;
        private bool _fireReportEnabled = false;
        private string _fireReportFolder;
        private bool _cmdFireReportLogEnabled = false;
        private bool _cmdFireReportEnabled = false;
        private bool _cmdFireDataLogEnabled = false;
        private int _intAnnualEndYear;
        private int _intIncrementalEndYear;
        private int _intMinYear;
        private int _intFireIncrementYears;
        private bool _bAllTimeChecked = true;   //Default
        private bool _bSelectedTimeChecked = false;
        private bool _bAnnualDataChecked = false;
        private bool _bIncrementDataChecked = false;
        private int _intSelectedMinYear;
        private int _intSelectedMaxYear;
        private int _intFireTimePeriodCount;
        private int _intNifcMaxYear;
        private int _intNifcMinYear;
        private int _intMtbsMaxYear;
        private int _intMtbsMinYear;
        private int _intFireDataClipYears;
        private string _strNifcDataDescr;
        private string _strMtbsDataDescr;
        private bool _bClip_Nifc_Checked;
        private bool _bClip_Mtbs_Checked;
        private bool _Reclip_MTBS_Checked = false;  //@ToDo: Change to true before production
        private string _strSettingsFile;
        private string _strSelectedFireStatus;
        private const string _separator = ",";

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

        public bool CmdForecastLogEnabled
        {
            get { return _cmdForecastLogEnabled; }
            set
            {
                SetProperty(ref _cmdForecastLogEnabled, value, () => CmdForecastLogEnabled);
            }
        }

        public bool CmdSnodasLogEnabled
        {
            get { return _cmdSnodasLogEnabled; }
            set
            {
                SetProperty(ref _cmdSnodasLogEnabled, value, () => CmdSnodasLogEnabled);
            }
        }
        public bool CmdGenStatisticsLogEnabled
        {
            get { return _cmdGenStatisticsLogEnabled; }
            set
            {
                SetProperty(ref _cmdGenStatisticsLogEnabled, value, () => CmdGenStatisticsLogEnabled);
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

        public bool CmdToggleEnabled
        {
            get { return _cmdToggleEnabled; }
            set
            {
                SetProperty(ref _cmdToggleEnabled, value, () => CmdToggleEnabled);
            }
        }

        public bool CmdGenStatisticsEnabled
        {
            get { return _cmdGenStatisticsEnabled; }
            set
            {
                SetProperty(ref _cmdGenStatisticsEnabled, value, () => CmdGenStatisticsEnabled);
            }
        }

        public bool TasksEnabled
        {
            get { return _tasksEnabled; }
            set
            {
                SetProperty(ref _tasksEnabled, value, () => TasksEnabled);
            }
        }

        public bool FireDataEnabled
        {
            get { return _fireDataEnabled; }
            set
            {
                SetProperty(ref _fireDataEnabled, value, () => FireDataEnabled);
            }
        }

        public bool FireReportEnabled
        {
            get { return _fireReportEnabled; }
            set
            {
                SetProperty(ref _fireReportEnabled, value, () => FireReportEnabled);
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

        public string SnodasFolder
        {
            get { return _snodasFolder; }
            set
            {
                SetProperty(ref _snodasFolder, value, () => SnodasFolder);
            }
        }

        public string StatisticsFolder
        {
            get { return _statisticsFolder; }
            set
            {
                SetProperty(ref _statisticsFolder, value, () => StatisticsFolder);
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

        public bool UpdateStationDataChecked
        {
            get { return _updateStationDataChecked; }
            set
            {
                SetProperty(ref _updateStationDataChecked, value, () => UpdateStationDataChecked);
            }
        }

        public string FireReportFolder
        {
            get { return _fireReportFolder; }
            set
            {
                SetProperty(ref _fireReportFolder, value, () => FireReportFolder);
            }
        }

        public bool CmdFireReportLogEnabled
        {
            get { return _cmdFireReportLogEnabled; }
            set
            {
                SetProperty(ref _cmdFireReportLogEnabled, value, () => CmdFireReportLogEnabled);
            }
        }

        public bool CmdFireDataLogEnabled
        {
            get { return _cmdFireDataLogEnabled; }
            set
            {
                SetProperty(ref _cmdFireDataLogEnabled, value, () => CmdFireDataLogEnabled);
            }
        }

        public bool CmdFireReportEnabled
        {
            get { return _cmdFireReportEnabled; }
            set
            {
                SetProperty(ref _cmdFireReportEnabled, value, () => CmdFireReportEnabled);
            }
        }
        public int AnnualEndYear
        {
            get { return _intAnnualEndYear; }
            set
            {
                SetProperty(ref _intAnnualEndYear, value, () => AnnualEndYear);
            }
        }
        public int IncrementalEndYear
        {
            get { return _intIncrementalEndYear; }
            set
            {
                SetProperty(ref _intIncrementalEndYear, value, () => IncrementalEndYear);
            }
        }

        public int FireIncrementYears
        {
            get { return _intFireIncrementYears; }
            set
            {
                SetProperty(ref _intFireIncrementYears, value, () => FireIncrementYears);
            }
        }

        public bool AllTimeChecked
        {
            get { return _bAllTimeChecked; }
            set
            {
                SetProperty(ref _bAllTimeChecked, value, () => AllTimeChecked);
            }
        }

        public bool SelectedTimeChecked
        {
            get { return _bSelectedTimeChecked; }
            set
            {
                SetProperty(ref _bSelectedTimeChecked, value, () => SelectedTimeChecked);
            }
        }

        public bool AnnualDataChecked
        {
            get { return _bAnnualDataChecked; }
            set
            {
                SetProperty(ref _bAnnualDataChecked, value, () => AnnualDataChecked);
            }
        }

        public bool IncrementDataChecked
        {
            get { return _bIncrementDataChecked; }
            set
            {
                SetProperty(ref _bIncrementDataChecked, value, () => IncrementDataChecked);
            }
        }

        public int SelectedMinYear
        {
            get { return _intSelectedMinYear; }
            set
            {
                SetProperty(ref _intSelectedMinYear, value, () => SelectedMinYear);
            }
        }

        public int SelectedMaxYear
        {
            get { return _intSelectedMaxYear; }
            set
            {
                SetProperty(ref _intSelectedMaxYear, value, () => SelectedMaxYear);
            }
        }

        public int FireTimePeriodCount
        {
            get { return _intFireTimePeriodCount; }
            set
            {
                SetProperty(ref _intFireTimePeriodCount, value, () => FireTimePeriodCount);
            }
        }
        public int NifcMinYear
        {
            get { return _intNifcMinYear; }
            set
            {
                SetProperty(ref _intNifcMinYear, value, () => NifcMinYear);
            }
        }
        public int MtbsMinYear
        {
            get { return _intMtbsMinYear; }
            set
            {
                SetProperty(ref _intMtbsMinYear, value, () => MtbsMinYear);
            }
        }
        public int FireDataClipYears
        {
            get { return _intFireDataClipYears; }
            set
            {
                SetProperty(ref _intFireDataClipYears, value, () => FireDataClipYears);
            }
        }
        public string NifcDataDescr
        {
            get { return _strNifcDataDescr; }
            set
            {
                SetProperty(ref _strNifcDataDescr, value, () => NifcDataDescr);
            }
        }
        public string MtbsDataDescr
        {
            get { return _strMtbsDataDescr; }
            set
            {
                SetProperty(ref _strMtbsDataDescr, value, () => MtbsDataDescr);
            }
        }

        public string SelectedFireStatus
        {
            get { return _strSelectedFireStatus; }
            set
            {
                SetProperty(ref _strSelectedFireStatus, value, () => SelectedFireStatus);
            }
        }

        public bool Reclip_MTBS_Checked
        {
            get { return _Reclip_MTBS_Checked; }
            set
            {
                SetProperty(ref _Reclip_MTBS_Checked, value, () => Reclip_MTBS_Checked);
            }
        }
        public string FireDataClipDescr
        {
            get { return $@"From the previous {_intFireDataClipYears} years"; }
        }
        public bool Clip_Nifc_Checked
        {
            get { return _bClip_Nifc_Checked; }
            set
            {
                SetProperty(ref _bClip_Nifc_Checked, value, () => Clip_Nifc_Checked);
            }
        }
        public bool Clip_Mtbs_Checked
        {
            get { return _bClip_Mtbs_Checked; }
            set
            {
                SetProperty(ref _bClip_Mtbs_Checked, value, () => Clip_Mtbs_Checked);
            }
        }
        public string SettingsFile
        {
            get { return _strSettingsFile; }
            set 
            {
                SetProperty(ref _strSettingsFile, value, () => SettingsFile);
            }
        }

        public ObservableCollection<BA_Objects.Aoi> Names { get; set; }
        public ObservableCollection<string> FilterFireStatus { get; set; }

        // Assigns the propertyChanged event handler to each AOI item
        public void ContentCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (BA_Objects.Aoi item in e.OldItems)
                {
                    //Removed items
                    item.PropertyChanged -= AoiPropertyChanged;
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (BA_Objects.Aoi item in e.NewItems)
                {
                    //Added items
                    item.PropertyChanged += AoiPropertyChanged;
                }
            }
        }

        public void AoiPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            //This will get called when the property of an object inside the collection changes
            bool bOneSelected = false;
            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
            {
                if (Names[idxRow].AoiBatchIsSelected)
                {
                    bOneSelected = true;
                    break;
                }
            }
            TasksEnabled = bOneSelected;    
        }

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
                    StatisticsFolder = $@"{ParentFolder}\{Constants.FOLDER_MAP_PACKAGE}";
                    FireReportFolder = StatisticsFolder;
                    // Make sure the maps_publish folder exists under the selected folder
                    if (!Directory.Exists(Path.GetDirectoryName(_strLogFile)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(_strLogFile));
                    }
                    // Enable the view batch tool log button if log exists
                    CmdLogEnabled = File.Exists(_strLogFile);

                    _strForecastLogFile = $@"{ParentFolder}\{Constants.FOLDER_MAP_PACKAGE}\{Constants.FILE_FORECAST_STATION_LOG}";
                    CmdForecastLogEnabled = File.Exists(_strForecastLogFile);

                    _strSnodasLogFile = $@"{ParentFolder}\{Constants.FOLDER_SNODAS_GEOJSON}\{Constants.FILE_SNODAS_GEOJSON_LOG}";
                    CmdSnodasLogEnabled = File.Exists(_strSnodasLogFile);

                    _strGenStatisticsLogFile = $@"{ParentFolder}\{Constants.FOLDER_MAP_PACKAGE}\{Constants.FILE_GEN_STATISTICS_LOG}";
                    CmdGenStatisticsLogEnabled = File.Exists(_strGenStatisticsLogFile);

                    _strFireDataLogFile = $@"{ParentFolder}\{Constants.FOLDER_MAP_PACKAGE}\{Constants.FOLDER_FIRE_STATISTICS}\{Constants.FILE_FIRE_DATA_LOG}";
                    CmdFireDataLogEnabled = File.Exists(_strFireDataLogFile);

                    _strFireReportLogFile = $@"{ParentFolder}\{Constants.FOLDER_MAP_PACKAGE}\{Constants.FOLDER_FIRE_STATISTICS}\{Constants.FILE_FIRE_REPORT_LOG}";
                    _strFireMapsLogFile = $@"{ParentFolder}\{Constants.FOLDER_MAP_PACKAGE}\{Constants.FOLDER_FIRE_STATISTICS}\{Constants.FILE_FIRE_REPORT_LOG}";
                    CmdFireReportLogEnabled = File.Exists(_strFireReportLogFile);


                    Names.Clear();
                    IList<BA_Objects.Aoi> lstAois = await GeneralTools.GetAoiFoldersAsync(ParentFolder, _strLogFile);
                    foreach (var pAoi in lstAois)
                    {
                        string[] arrValues = await AnalysisTools.QueryLocalStationValues(pAoi.FilePath);
                        if (arrValues.Length == 3)
                        {
                            pAoi.StationTriplet = arrValues[0];
                            pAoi.StationName = arrValues[1];
                            pAoi.Huc2 = Convert.ToInt16(arrValues[2]);
                            if (pAoi.ValidForecastData)
                            {
                                pAoi.AoiBatchStateText = AoiBatchState.Ready.ToString();
                            }
                        }
                        Names.Add(pAoi);
                    }
                    if (Names.Count > 0)
                    {
                        CmdRunEnabled = true;
                        CmdSnodasEnabled = true;
                        CmdForecastEnabled = true;
                        CmdToggleEnabled = true;
                        TasksEnabled = true;
                        CmdGenStatisticsEnabled = true;
                        CmdFireReportEnabled = true;
                    }
                    else
                    {
                        MessageBox.Show("No valid AOIs were found in the selected folder!", "BAGIS-PRO");
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CmdAoiFolder),
                            "No valid AOIs were found in the selected folder!");
                        CmdRunEnabled = false;
                        CmdSnodasEnabled = false;
                        CmdForecastEnabled = false;
                        CmdToggleEnabled = false;
                        TasksEnabled = false;
                        FireDataEnabled = false;
                        FireReportEnabled = false;
                        CmdGenStatisticsEnabled = false;
                        CmdFireReportEnabled = false;
                    }

                    // Query minimum available fire data year from webservice; It's here because it's an async call
                    if (AnnualEndYear < 1)   // Only do this the first time an AOI folder is selected
                    {
                        Webservices ws = new Webservices();
                        _intNifcMaxYear = await ws.QueryNifcMinYearAsync(Module1.Current.DataSources, Constants.DATA_TYPE_FIRE_CURRENT);
                        NifcDataDescr = $@"NIFC data available from {NifcMinYear} to {_intNifcMaxYear}";
                        AnnualEndYear = _intNifcMaxYear;
                        SelectedMaxYear = AnnualEndYear;
                        _intMinYear = (DateTime.Now.Year - FireDataClipYears) + 1;
                        SelectedMinYear = _intMinYear;
                        _intMtbsMaxYear = await this.QueryMtbsMaxYearAsync(Module1.Current.DataSources, Constants.DATA_TYPE_FIRE_BURN_SEVERITY);
                        if (_intMtbsMaxYear > 0)
                        {
                            MtbsDataDescr = $@"MTBS data available from {MtbsMinYear} to {_intMtbsMaxYear}";
                            FireDataEnabled = true; 
                            IncrementalEndYear = _intMtbsMaxYear;
                        }
                        else
                        {
                            IncrementalEndYear = _intNifcMaxYear;
                            MtbsDataDescr = $@"MTBS web services are currently unavailable. Data retrieval is disabled.";
                        }
                        int intIncrementPeriods;
                        IList<Interval> lstInterval = GeneralTools.GetFireStatisticsIntervals(IncrementalEndYear, FireDataClipYears, FireIncrementYears, false, 0, out intIncrementPeriods);
                        FireTimePeriodCount = intIncrementPeriods;
                        FireReportEnabled = true;
                    }
                });
            }
        }

        public System.Windows.Input.ICommand CmdToggle
        {
            get
            {
                return new RelayCommand( () =>
                {
                    switch (SelectedFireStatus)
                    {
                        case _include:
                            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
                            {
                                Names[idxRow].AoiBatchIsSelected = !Names[idxRow].AoiBatchIsSelected;
                            }
                            break;
                        case nameof(AoiBatchState.MissingFireData):
                            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
                            {
                                if (Names[idxRow].AoiBatchStateText.Equals(AoiBatchState.MissingFireData.ToString()))
                                {
                                    Names[idxRow].AoiBatchIsSelected = true;
                                }
                                else
                                {
                                    Names[idxRow].AoiBatchIsSelected = false;
                                }                                
                            }
                            break;
                        case nameof(AoiBatchState.HasReport):
                            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
                            {
                                if (Names[idxRow].AoiBatchStateText.Equals(AoiBatchState.HasReport.ToString()))
                                {
                                    Names[idxRow].AoiBatchIsSelected = true;
                                }
                                else
                                {
                                    Names[idxRow].AoiBatchIsSelected = false;
                                }
                            }
                            break;
                        case nameof(AoiBatchState.MissingReport):
                            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
                            {
                                if (Names[idxRow].AoiBatchStateText.Equals(AoiBatchState.MissingReport.ToString()))
                                {
                                    Names[idxRow].AoiBatchIsSelected = true;
                                }
                                else
                                {
                                    Names[idxRow].AoiBatchIsSelected = false;
                                }
                            }
                            break;
                        case nameof(AoiBatchState.NoFire):
                            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
                            {
                                if (Names[idxRow].AoiBatchStateText.Equals(AoiBatchState.NoFire.ToString()))
                                {
                                    Names[idxRow].AoiBatchIsSelected = true;
                                }
                                else
                                {
                                    Names[idxRow].AoiBatchIsSelected = false;
                                }
                            }
                            break;
                        default:
                            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
                            {
                                Names[idxRow].AoiBatchIsSelected = !Names[idxRow].AoiBatchIsSelected;
                            }
                            break;

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

        private RelayCommand _runFireDataCommand;
        public ICommand CmdFireData
        {
            get
            {
                if (_runFireDataCommand == null)
                    _runFireDataCommand = new RelayCommand(RunFireDataImplAsync, () => true);
                return _runFireDataCommand;
            }
        }
        private RelayCommand _runFireReport;
        public ICommand CmdFireReport
        {
            get
            {
                if (_runFireReport == null)
                    _runFireReport = new RelayCommand(RunFireReportImplAsync, () => true);
                return _runFireReport;
            }
        }
        private RelayCommand _runFireMaps;
        public ICommand CmdFireMaps
        {
            get
            {
                if (_runFireMaps == null)
                    _runFireMaps = new RelayCommand(RunFireMapsImplAsync, () => true);
                return _runFireMaps;
            }
        }

        private async void RunSnodasImplAsync(object param)
        {
            // Make sure the maps_publish folder exists under the selected folder
            if (!Directory.Exists(Path.GetDirectoryName(_strSnodasLogFile)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_strSnodasLogFile));
            }

            // Create initial log entry
            string strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Starting snodas geojson export to publish in " +
                Path.GetDirectoryName(_strSnodasLogFile) + "\r\n";
            File.WriteAllText(_strSnodasLogFile, strLogEntry);    // overwrite file if it exists

            // Reset batch states
            ResetAoiBatchStateText();
            // Set status to waiting for selected AOIs
            int intReturn = SetAoiWaitingText();

            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
            {
                if (Names[idxRow].AoiBatchIsSelected)
                {
                    int errorCount = 0; // keep track of any non-fatal errors
                    string aoiFolder = Names[idxRow].FilePath;
                    if (!Names[idxRow].AoiBatchStateText.Equals(AoiBatchState.Waiting.ToString()))
                    {
                        strLogEntry = $@"{DateTime.Now.ToString("MM/dd/yy H:mm:ss")} Skipping export for {Names[idxRow].FilePath}. Required station information is missing.{System.Environment.NewLine}";
                        File.AppendAllText(_strSnodasLogFile, strLogEntry);
                        continue;
                    }
                    else
                    {
                        Names[idxRow].AoiBatchStateText = AoiBatchState.Started.ToString();  // update gui
                        strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Starting geojson export for " +
                            Names[idxRow].Name + "\r\n";
                        File.AppendAllText(_strSnodasLogFile, strLogEntry);       // append

                    }

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
                                File.AppendAllText(_strSnodasLogFile, strLogEntry);       // append
                            }
                        }
                        else
                        {
                            strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Pourpoint file does not have one and only one feature. Skipping this AOI!! \r\n";
                            File.AppendAllText(_strSnodasLogFile, strLogEntry);       // append
                            errorCount++;
                        }
                    }
                    else
                    {
                        strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Unable to locate pourpoint file for AOI. Skipping this AOI!! \r\n";
                        File.AppendAllText(_strSnodasLogFile, strLogEntry);       // append
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
                                File.AppendAllText(_strSnodasLogFile, strLogEntry);       // append
                            }
                            else
                            {
                                strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "An error occurred while processing the polygon. Skipping this AOI! \r\n";
                                File.AppendAllText(_strSnodasLogFile, strLogEntry);       // append
                            }
                            if (success == BA_ReturnCode.Success)
                            {
                                Webservices ws = new Webservices();
                                string errorMessage = ws.GenerateSnodasGeoJson(pointOutputPath, polygonOutputPath,
                                    ParentFolder + "\\" + Constants.FOLDER_SNODAS_GEOJSON);
                                if (!string.IsNullOrEmpty(errorMessage))
                                {
                                    strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + errorMessage + " \r\n";
                                    File.AppendAllText(_strSnodasLogFile, strLogEntry);       // append
                                    errorCount++;
                                }
                                else
                                {
                                    strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Geojson exported for " + Names[idxRow].Name + " \r\n";
                                    File.AppendAllText(_strSnodasLogFile, strLogEntry);       // append
                                }
                            }
                        }
                        else
                        {
                            strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Unable to locate aoi_v file for AOI. Skipping this AOI!! \r\n";
                            File.AppendAllText(_strSnodasLogFile, strLogEntry);       // append
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
            File.AppendAllText(_strSnodasLogFile, strLogEntry);       // append
            CmdSnodasLogEnabled = true; 
            MessageBox.Show("Generated GeoJson files are available in " + ParentFolder + "\\" + Constants.FOLDER_SNODAS_GEOJSON, "BAGIS-PRO");
        }

        private RelayCommand _runGenStatisticsCommand;
        public ICommand CmdStatistics
        {
            get
            {
                if (_runGenStatisticsCommand == null)
                    _runGenStatisticsCommand = new RelayCommand(RunGenStatisticsImplAsync, () => true);
                return _runGenStatisticsCommand;
            }
        }

        private async void RunGenStatisticsImplAsync(object param)
        {
            // Make sure the maps_publish folder exists under the selected folder
            if (!Directory.Exists(Path.GetDirectoryName(_strGenStatisticsLogFile)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_strGenStatisticsLogFile));
            }

            // Create initial log entry
            string strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Starting generate statistics export to publish in " +
                Path.GetDirectoryName(_strGenStatisticsLogFile) + "\r\n";
            File.WriteAllText(_strGenStatisticsLogFile, strLogEntry);    // overwrite file if it exists

            // Reset batch states
            ResetAoiBatchStateText();
            // Set status to waiting for selected AOIs
            int intReturn = SetAoiWaitingText();

            // Download the runoff csv file from the NRCS Portal                
            string documentId = (string)Module1.Current.BagisSettings.AnnualRunoffItemId;
            string annualRunoffDataDescr = (string)Module1.Current.BagisSettings.AnnualRunoffDataDescr;
            string annualRunoffDataYear = (string)Module1.Current.BagisSettings.AnnualRunoffDataYear;
            Webservices ws = new Webservices();
            var runOffSuccess = await ws.GetPortalFile(BA_Objects.AGSPortalProperties.PORTAL_ORGANIZATION, documentId, Module1.Current.SettingsPath + "\\" + Constants.FOLDER_SETTINGS +
                "\\" + Constants.FILE_ANNUAL_RUNOFF_CSV);
            int errorCount = 0; // keep track of any non-fatal errors
            if (runOffSuccess != BA_ReturnCode.Success)
            {
                errorCount++;
            }

            // Structures to manage data
            string strCsvFile =$@"{Path.GetDirectoryName(_strGenStatisticsLogFile)}\{Constants.FILE_AOI_STATISTICS}";
            string separator = ",";
            StringBuilder output = new StringBuilder();
            String[] headings = { "stationTriplet","stationName","report_end_year","aoiArea_SqMeters", "aoiArea_SqMiles", "ann_runoff_ratio_pct",
                "centroid_x_dd","centroid_y_dd","state_codes","elev_min_ft","elev_max_ft","elev_range_ft","elev_median_ft",
                "auto_sites_buffer","scos_sites_buffer","snotel_sites_all","snolite_sites_all","scos_sites_all","coop_sites_all","snotel_sites_inside",
                "snolite_sites_inside","scos_sites_inside","coop_sites_inside","snotel_sites_outside","snolite_sites_outside",
                "scos_sites_outside","coop_sites_outside","auto_rep_area_pct","scos_rep_area_pct","forested_area_pct",
                "aspect_zones_def","aspect_area_pct", "elev_zones_def","elev_zones_area_pct","auto_site_count_elev_zone","scos_site_count_elev_zone",
                "critical_precip_zones_def","critical_precip_zones_pct","wilderness_area_pct","public_non_wild_area_pct",
                "air_area_pct","all_site_density","auto_site_density","scos_site_density","area_outside_usa_pct","slope_zones_def","slope_area_pct"};
            output.AppendLine(string.Join(separator, headings));

            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
            {
                if (Names[idxRow].AoiBatchIsSelected)
                {
                    string aoiFolder = Names[idxRow].FilePath;
                    if (!Names[idxRow].AoiBatchStateText.Equals(AoiBatchState.Waiting.ToString()))
                    {
                        strLogEntry = $@"{DateTime.Now.ToString("MM/dd/yy H:mm:ss")} Skipping export for {Names[idxRow].FilePath}. Required station information is missing.{System.Environment.NewLine}";
                        File.AppendAllText(_strGenStatisticsLogFile, strLogEntry);
                        continue;
                    }
                    else
                    {
                        Names[idxRow].AoiBatchStateText = AoiBatchState.Started.ToString();  // update gui
                        strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Starting generate statistics export for " +
                            Names[idxRow].Name + "\r\n";
                        File.AppendAllText(_strGenStatisticsLogFile, strLogEntry);       // append
                    }

                    // Set current AOI
                    BA_Objects.Aoi oAoi = await GeneralTools.SetAoiAsync(aoiFolder, Names[idxRow]);
                    if (Module1.Current.CboCurrentAoi != null)
                    {
                        FrameworkApplication.Current.Dispatcher.Invoke(() =>
                        {
                            // Do something on the GUI thread
                            Module1.Current.CboCurrentAoi.SetAoiName(oAoi.Name);
                        });
                    }

                    IList<string> lstElements = await AnalysisTools.GenerateForecastStatisticsList(oAoi, _strGenStatisticsLogFile, runOffSuccess);
                    output.AppendLine(string.Join(separator, lstElements));

                    Names[idxRow].AoiBatchStateText = AoiBatchState.Completed.ToString();  // update gui
                    strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Finished generate statistics export for " +
                        Names[idxRow].Name + "\r\n";
                    File.AppendAllText(_strGenStatisticsLogFile, strLogEntry);       // append
                }
            }
            try
            {
                // Overwrites file with new content
                File.WriteAllText(strCsvFile, output.ToString());
            }
            catch (Exception ex)
            {
                strLogEntry = $@"{DateTime.Now.ToString("MM/dd/yy H:mm:ss")} Data could not be written to the CSV file!{System.Environment.NewLine}";
                File.AppendAllText(_strGenStatisticsLogFile, strLogEntry);
                MessageBox.Show("Data could not be written to the CSV file!", "BAGIS-PRO");
                return;
            }
            strLogEntry = $@"{DateTime.Now.ToString("MM/dd/yy H:mm:ss")} The data has been successfully saved to the CSV file.{System.Environment.NewLine}";
            File.AppendAllText(_strGenStatisticsLogFile, strLogEntry);
            MessageBox.Show("The data has been successfully saved to the CSV file.", "BAGIS-PRO");

        }

        private void ResetAoiBatchStateText()
        {
            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
            {
                Aoi oAoi = Names[idxRow];
                if (oAoi.StationTriplet.Equals(Constants.VALUE_MISSING) ||
                    oAoi.StationName.Equals(Constants.VALUE_MISSING) ||
                    oAoi.Huc2 == -1)
                {
                    oAoi.AoiBatchStateText = AoiBatchState.NotReady.ToString();
                }
                else
                {
                   oAoi.AoiBatchStateText = AoiBatchState.Ready.ToString();
                }
            }
        }
        private int SetAoiWaitingText()
        {
            int retVal = 0;
            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
            {
                Aoi oAoi = Names[idxRow];
                if (Names[idxRow].AoiBatchIsSelected && oAoi.AoiBatchStateText == AoiBatchState.Ready.ToString())
                {
                    oAoi.AoiBatchStateText = AoiBatchState.Waiting.ToString();
                    retVal++;
                }
            }
            return retVal;
        }

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

        public ICommand CmdLog
        {
            get
            {
                return new RelayCommand(() =>
                {
                    if (File.Exists(_strLogFile))
                    {
                        var p = new Process();
                        p.StartInfo = new ProcessStartInfo(_strLogFile)
                        {
                            UseShellExecute = true
                        };
                        p.Start();
                    }
                    else
                    {
                        MessageBox.Show("Could not find log file!", "BAGIS-PRO");
                        CmdLogEnabled = false;
                    }
                });
            }
        }

        public ICommand CmdForecastLog
        {
            get
            {
                return new RelayCommand(() =>
                {
                    if (File.Exists(_strForecastLogFile))
                    {
                        var p = new Process();
                        p.StartInfo = new ProcessStartInfo(_strForecastLogFile)
                        {
                            UseShellExecute = true
                        };
                        p.Start();
                    }
                    else
                    {
                        MessageBox.Show("Could not find forecast log file!", "BAGIS-PRO");
                        CmdForecastLogEnabled = false;
                    }
                });
            }
        }

        public ICommand CmdSnodasLog
        {
            get
            {
                return new RelayCommand(() =>
                {
                    if (File.Exists(_strSnodasLogFile))
                    {
                        var p = new Process();
                        p.StartInfo = new ProcessStartInfo(_strSnodasLogFile)
                        {
                            UseShellExecute = true
                        };
                        p.Start();
                    }
                    else
                    {
                        MessageBox.Show("Could not find snodas log file!", "BAGIS-PRO");
                        CmdSnodasLogEnabled = false;
                    }
                });
            }
        }

        public ICommand CmdStatisticsLog
        {
            get
            {
                return new RelayCommand(() =>
                {
                    if (File.Exists(_strGenStatisticsLogFile))
                    {
                        var p = new Process();
                        p.StartInfo = new ProcessStartInfo(_strGenStatisticsLogFile)
                        {
                            UseShellExecute = true
                        };
                        p.Start();
                    }
                    else
                    {
                        MessageBox.Show("Could not find Generate Statistics log file!", "BAGIS-PRO");
                        CmdGenStatisticsLogEnabled = false;
                    }
                });
            }
        }

        public ICommand CmdFireReportLog
        {
            get
            {
                return new RelayCommand(() =>
                {
                    if (File.Exists(_strFireReportLogFile))
                    {
                        var p = new Process();
                        p.StartInfo = new ProcessStartInfo(_strFireReportLogFile)
                        {
                            UseShellExecute = true
                        };
                        p.Start();
                    }
                    else
                    {
                        MessageBox.Show("Could not find fire report log file!", "BAGIS-PRO");
                        CmdFireReportLogEnabled = false;
                    }
                });
            }
        }

        public ICommand CmdVerifyForFire
        {
            get
            {
                return new RelayCommand( async () =>
                {
                    ResetAoiBatchStateText();
                    for (int idxRow = 0; idxRow < Names.Count; idxRow++)
                    {
                        Aoi oAoi = Names[idxRow];
                        // Check for fire settings before continuing
                        bool bMissingFireSettings = false;
                        string strFireSettingsPath = $@"{oAoi.FilePath}\{Constants.FOLDER_MAPS}\{Constants.FILE_FIRE_SETTINGS}";
                        dynamic oFireSettings = null;
                        if (File.Exists(strFireSettingsPath))
                        {
                            oFireSettings = GeneralTools.GetFireSettings(oAoi.FilePath);
                            if (oFireSettings.DataSources == null)
                            {
                                bMissingFireSettings = true;
                            }
                        }
                        else
                        {
                            bMissingFireSettings = true;
                        }
                        if (bMissingFireSettings)
                        {
                            Names[idxRow].AoiBatchStateText = AoiBatchState.MissingFireData.ToString();  // update gui
                            continue;
                        }
                        // Check for fire.gdb and data before continuing
                        string strFireGdbPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Fire);
                        bool bHasFireData = false;
                        if (Directory.Exists(strFireGdbPath))
                        {
                            if (await GeodatabaseTools.FeatureClassExistsAsync(new Uri(strFireGdbPath), Constants.FILE_NIFC_FIRE))
                            {
                                bHasFireData = true;
                            }
                            if (bHasFireData)
                            {
                                // Check for mtbs layers
                                int intMtbs = 0;
                                await QueuedTask.Run(() =>
                                {
                                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFireGdbPath))))
                                    {
                                        IReadOnlyList<RasterDatasetDefinition> definitions = geodatabase.GetDefinitions<RasterDatasetDefinition>();
                                        foreach (RasterDatasetDefinition def in definitions)
                                        {
                                            if (def.GetName().IndexOf("mtbs") > -1)
                                            {
                                                intMtbs = 1;
                                                break;
                                            }
                                        }
                                    }
                                });
                                if (intMtbs == 0)
                                {
                                    bHasFireData = false;
                                }
                            }
                        }
                        if (!bHasFireData)
                        {
                            Names[idxRow].AoiBatchStateText = AoiBatchState.MissingFireData.ToString();  // update gui
                            continue;
                        }
                        // Fire data existence has been validated; Check no fire status
                        bool bZeroFires = false;
                        if (oFireSettings != null && bHasFireData)
                        {
                            long nifcFireCount = await GeodatabaseTools.CountFeaturesAsync(new Uri(strFireGdbPath), Constants.FILE_NIFC_FIRE);
                            // Check for mtbs layers
                            // mtbs_CONUS_1996_NODATA
                            int mtbsWithData = 0;
                            await QueuedTask.Run(() =>
                            {
                                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFireGdbPath))))
                                {
                                    IReadOnlyList<RasterDatasetDefinition> definitions = geodatabase.GetDefinitions<RasterDatasetDefinition>();
                                    foreach (RasterDatasetDefinition def in definitions)
                                    {
                                        if (def.GetName().IndexOf("mtbs") > -1 && def.GetName().IndexOf($@"_{Constants.VALUE_NO_DATA.ToUpper()}") == -1)
                                        {
                                            mtbsWithData++;
                                            break;
                                        }
                                    }
                                }
                            });
                            if (nifcFireCount == 0 && mtbsWithData == 0)
                            {
                                bZeroFires = true;
                                Names[idxRow].AoiBatchStateText = AoiBatchState.NoFire.ToString();  // update gui
                                continue;
                            }
                        }
                        // Fire data with fires has been validated; Check fire report status
                        bool bHasIncrementReports = false;
                        if (bHasFireData && !bZeroFires)
                        {
                            if (oFireSettings != null && oFireSettings.incrementReportEndYear != null)
                            {
                                long incrementReportEndYear = -1;
                                string strYear = Convert.ToString(oFireSettings.incrementReportEndYear);
                                bHasIncrementReports = long.TryParse(strYear, out incrementReportEndYear);
                                if (bHasIncrementReports)
                                {
                                    Names[idxRow].AoiBatchStateText = AoiBatchState.HasReport.ToString();  // update gui
                                }
                                else
                                {
                                    Names[idxRow].AoiBatchStateText = AoiBatchState.MissingReport.ToString();  // update gui
                                }
                            }
                            else
                            {
                                Names[idxRow].AoiBatchStateText = AoiBatchState.MissingReport.ToString();  // update gui
                            }
                        }
                    }
                });
            }
        }

        public ICommand CmdFireDataLog
        {
            get
            {
                return new RelayCommand(() =>
                {
                    if (File.Exists(_strFireDataLogFile))
                    {
                        var p = new Process();
                        p.StartInfo = new ProcessStartInfo(_strFireDataLogFile)
                        {
                            UseShellExecute = true
                        };
                        p.Start();
                    }
                    else
                    {
                        MessageBox.Show("Could not find fire data file!", "BAGIS-PRO");
                        CmdFireDataLogEnabled = false;
                    }
                });
            }
        }

        private async void RunImplAsync(object param)
        {
            // Create initial log entry
            string strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Starting batch tool to publish in " +
                Path.GetDirectoryName(_strLogFile) + "\r\n";
            File.WriteAllText(_strLogFile, strLogEntry);    // overwrite file if it exists

            // Reset AOI status
            ResetAoiBatchStateText();

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
            string strPublisher = (string)Module1.Current.BagisSettings.Publisher;
            if (!Publisher.Trim().Equals(strPublisher))
            {
                Module1.Current.BagisSettings.Publisher = Publisher;
                String json = JsonConvert.SerializeObject(Module1.Current.BagisSettings, Formatting.Indented);
                string strSettingsPath = GeneralTools.GetBagisSettingsPath();
                File.WriteAllText(strSettingsPath, json);
            }

            // Set status to waiting for selected AOIs
            int intReturn = SetAoiWaitingText();
            
            // Make directory for required folders if they don't exist
            // Make sure that maps and maps_publish folders exist
            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
            {
                if (Names[idxRow].AoiBatchIsSelected)
                {
                    int errorCount = 0; // keep track of any non-fatal errors
                    string aoiFolder = Names[idxRow].FilePath;
                    if (!Names[idxRow].AoiBatchStateText.Equals(AoiBatchState.Waiting.ToString()))
                    {
                        strLogEntry = $@"Skipping report for {Names[idxRow].FilePath}. Required station information is missing.{System.Environment.NewLine}";
                        File.AppendAllText(_strLogFile, strLogEntry);
                        continue;
                    }
                    else
                    {
                        Names[idxRow].AoiBatchStateText = AoiBatchState.Started.ToString();  // update gui
                    }
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
                    BA_Objects.Aoi oAoi = await GeneralTools.SetAoiAsync(aoiFolder, Names[idxRow]);
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
                        string strDefaultBufferDistance = (string)Module1.Current.BagisSettings.PrecipBufferDistance;
                        string strDefaultBufferUnits = (string)Module1.Current.BagisSettings.PrecipBufferUnits;
                        if (! await GeodatabaseTools.FeatureClassExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi)),Constants.FILE_AOI_PRISM_VECTOR))  
                        {
                            string strInputFeatures = $@"{GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi)}\{Constants.FILE_AOI_VECTOR}";
                            string strOutputFeatures = $@"{GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi)}\{Constants.FILE_AOI_PRISM_VECTOR}";
                            string strDistance = $@"{strDefaultBufferDistance} {strDefaultBufferUnits}";
                            success =  await GeoprocessingTools.BufferAsync(strInputFeatures, strOutputFeatures, strDistance, "ALL", CancelableProgressor.None);
                            if (success == BA_ReturnCode.Success)
                            {
                                pBufferDistance = strDefaultBufferDistance;
                                pBufferUnits = strDefaultBufferUnits;
                            }
                        }   
                        success = await AnalysisTools.ClipLayersAsync(aoiFolder, BA_Objects.DataSource.GetPrecipitationKey,
                            pBufferDistance, pBufferUnits, strDefaultBufferDistance, strDefaultBufferUnits);
                        if (success != BA_ReturnCode.Success)
                        {
                            errorCount++;
                        }

                        // PRISM Zones
                        strLayer = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Prism, true) +
                            Path.GetFileName((string)Module1.Current.BagisSettings.AoiPrecipFile);
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
                        bool isDouble = Double.TryParse((string)Module1.Current.BagisSettings.SnotelBufferDistance, out dblDistance);
                        if (!isDouble)
                        {
                            dblDistance = 0;
                        }
                        Module1.Current.ModuleLogManager.LogDebug(nameof(RunImplAsync), "Buffer distance from settings: " + dblDistance);
                        string snoBufferDistance = dblDistance + " " + (string)Module1.Current.BagisSettings.SnotelBufferUnits;
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
                            double siteBufferDistanceMiles = (double)Module1.Current.BagisSettings.SiteBufferDistMiles;
                            double siteElevRangeFeet = (double)Module1.Current.BagisSettings.SiteElevRangeFeet;
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
                                Module1.Current.ModuleLogManager.LogError(nameof(RunImplAsync),
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
                        string unmanagedBufferDistance = arrUnmanagedBufferInfo[0];
                        string unmanagedBufferUnits = arrUnmanagedBufferInfo[1];

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
                                    Module1.Current.ModuleLogManager.LogDebug(nameof(RunImplAsync),
                                        "Unable to buffer roads because fs_roads layer does not exist. Process stopped!!");
                                }
                                else
                                {
                                    string strDistance = Module1.Current.BagisSettings.RoadsAnalysisBufferDistance + " " +
                                        Module1.Current.BagisSettings.RoadsAnalysisBufferUnits;
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

                        // Generate Elevation Precipitation Correlation layer
                        strLayer = GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Prism, true) +
                            Path.GetFileName((string)Module1.Current.BagisSettings.AoiPrecipFile);
                        Uri uriPrism = new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Prism));
                        success = await AnalysisTools.CalculateElevPrecipCorrAsync(aoiFolder, uriPrism,
                            Path.GetFileName((string)Module1.Current.BagisSettings.AoiPrecipFile));
                        if (success != BA_ReturnCode.Success)
                        {
                            errorCount++;
                        }
                        if (success == BA_ReturnCode.Success)
                        {
                            Module1.Current.ModuleLogManager.LogDebug(nameof(RunImplAsync),
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
                            if (Module1.Current.BagisSettings.PdfExportResolution != null)
                            {
                                pdfExportResolution = (int)Module1.Current.BagisSettings.PdfExportResolution;
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
                                outputPath = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\" + "Not_Specified_Watershed-Report.pdf";
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
                            CmdLogEnabled = true;
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

        private async void RunForecastImplAsync(object param)
        {
            const string UPDATED_NEAR = "Updated-Near";
            const string NO_CHANGE_MATCH = "No change-Match";
            const string NOT_A_FORECAST = "Not A Forecast";
            const string UPDATED_STATION_DATA = "Updated-Station Data";
            // Create initial _strForecastLogFile entry
            try
            {
                FileInfo file = new FileInfo(_strForecastLogFile);
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
                MessageBox.Show($@"Unable to write to _strForecastLogFile file {_strForecastLogFile}! Please close the .csv file and try again.", "BAGIS-PRO");
                return;
            }
            string strLogFileEntry = "Date,Time,AOI File Path,Old Triplet,New Triplet,Remarks\r\n";
            File.WriteAllText(_strForecastLogFile, strLogFileEntry);    // overwrite file if it exists
            strLogFileEntry = CreateLogEntry(ParentFolder, "", "", $@"Starting forecast station updates");
            File.AppendAllText(_strForecastLogFile, strLogFileEntry);

            // Reset AOI status
            ResetAoiBatchStateText();
            // Set AOI status to waiting if box is checked
            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
            {
                Aoi oAoi = Names[idxRow];
                if (Names[idxRow].AoiBatchIsSelected)
                {
                    oAoi.AoiBatchStateText = AoiBatchState.Waiting.ToString();
                }
            }
            Webservices ws = new Webservices();
            IList<string> lstMergeFeatures = new List<string>();
            BA_ReturnCode success = BA_ReturnCode.Success;
            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
            {
                if (Names[idxRow].AoiBatchStateText == AoiBatchState.Waiting.ToString())
                {
                    int errorCount = 0; // keep track of any non-fatal errors
                    success = BA_ReturnCode.Success;    // Re-initialize success variable
                    string aoiFolder = Names[idxRow].FilePath;
                    Names[idxRow].AoiBatchStateText = AoiBatchState.Started.ToString();  // update gui
                    strLogFileEntry = CreateLogEntry(aoiFolder, "", "", $@"Starting forecast station update");
                    File.AppendAllText(_strForecastLogFile, strLogFileEntry);       // append

                    Uri ppUri = new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Aoi));
                    string strTriplet = "";
                    string strStationName = "";
                    string strNearTriplet = "";
                    string strNearStationName = "";
                    int intNearHuc2 = -1;
                    int intHuc2 = -1;   
                    string strRemark = "";

                    // GET THE STATION TRIPLET FROM THE POURPOINT
                    IDictionary<string, string> dictEdits = new Dictionary<string, string>()
                    { { Constants.FIELD_STATION_TRIPLET, ""},
                      { Constants.FIELD_STATION_NAME, ""}
                    };
                    QueryFilter queryFilter = new QueryFilter();
                    if (await GeodatabaseTools.FeatureClassExistsAsync(ppUri, Constants.FILE_POURPOINT))
                    {
                        string[] arrFields = new string[] { Constants.FIELD_STATION_TRIPLET, Constants.FIELD_STATION_NAME, Constants.FIELD_HUC2 };
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
                                }
                            }
                            // Add the field if it is missing
                            else
                            {
                                if (strField.Equals(Constants.FIELD_HUC2))
                                {
                                    success = await GeoprocessingTools.AddFieldAsync(ppUri.LocalPath + "\\" + Constants.FILE_POURPOINT, strField, "INTEGER", null);
                                }
                                else
                                {
                                    success = await GeoprocessingTools.AddFieldAsync(ppUri.LocalPath + "\\" + Constants.FILE_POURPOINT, strField, "TEXT", null);
                                }
                            }
                        }
                    }
                    else
                    {
                        strLogFileEntry = CreateLogEntry(aoiFolder, "", "", $@"ERROR: Unable to open {ppUri.LocalPath + "\\" + Constants.FILE_POURPOINT}");
                        File.AppendAllText(_strForecastLogFile, strLogFileEntry);       // append
                        success = BA_ReturnCode.ReadError;
                        errorCount++;
                    }

                    // QUERY THE MASTER LIST FOR THE STATION TRIPLET
                    string[] arrSearch = { Constants.FIELD_STATION_TRIPLET, Constants.FIELD_NAME, Constants.FIELD_HUC2 };
                    string[] arrFound = new string[arrSearch.Length];
                    string strWsUri = (string)Module1.Current.BagisSettings.GaugeStationUri;

                    string strWsUriTrimmed = "";
                    string layerId = "";
                    if (!string.IsNullOrEmpty(strWsUri))
                    {
                        layerId = strWsUri[strWsUri.Length - 1].ToString();
                        strWsUriTrimmed = strWsUri.Substring(0, strWsUri.Length - 2);
                    }
                    
                    if (!AlwaysNearChecked)
                    {
                        queryFilter = new QueryFilter();
                        queryFilter.WhereClause = Constants.FIELD_STATION_TRIPLET + " = '" + strTriplet + "'";
                        arrFound = await ws.QueryServiceForValuesAsync(new Uri(strWsUriTrimmed), layerId, arrSearch, queryFilter);
                        if (arrFound != null && arrFound.Length == arrSearch.Length && strTriplet.Equals(arrFound[0]))
                        {
                            strRemark = NO_CHANGE_MATCH;
                            if (UpdateStationDataChecked)
                            {
                                strRemark = UPDATED_STATION_DATA;
                                strStationName = arrFound[1];
                                intHuc2 = Convert.ToInt16(arrFound[2]);
                            }
                        }
                    }

                    string[] arrMatched = {NO_CHANGE_MATCH, UPDATED_STATION_DATA };
                    if (!arrMatched.Contains(strRemark))
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
                                arrFound = await ws.QueryServiceForValuesAsync(new Uri(strWsUriTrimmed), layerId, arrSearch, queryFilter);
                                if (arrFound != null && arrFound.Length == arrSearch.Length && arrFound[0] != null)
                                {
                                    strNearTriplet = arrFound[0];
                                    strNearStationName = arrFound[1];
                                    intNearHuc2 = Convert.ToInt16(arrFound[2]);
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
                            dictEdits[Constants.FIELD_STATION_TRIPLET] = strNearTriplet;
                            dictEdits[Constants.FIELD_STATION_NAME] = strNearStationName;
                            success = await GeodatabaseTools.UpdateFeatureAttributesAsync(ppUri, Constants.FILE_POURPOINT,
                                new QueryFilter(), dictEdits);
                            success = await GeodatabaseTools.UpdateFeatureAttributeNumericAsync(ppUri, Constants.FILE_POURPOINT, new QueryFilter(), Constants.FIELD_HUC2, intNearHuc2);
                            lstMergeFeatures.Add(ppUri.LocalPath + "\\" + Constants.FILE_AOI_VECTOR);
                            Names[idxRow].StationTriplet = strNearTriplet;
                            Names[idxRow].StationName = strNearStationName;
                            Names[idxRow].Huc2 = intNearHuc2;   
                            break;
                        case UPDATED_STATION_DATA:
                                dictEdits[Constants.FIELD_STATION_NAME] = strStationName;
                            success = await GeodatabaseTools.UpdateFeatureAttributesAsync(ppUri, Constants.FILE_POURPOINT,
                                new QueryFilter(), dictEdits);
                            success = await GeodatabaseTools.UpdateFeatureAttributeNumericAsync(ppUri, Constants.FILE_POURPOINT, new QueryFilter(), Constants.FIELD_HUC2, intHuc2);
                            lstMergeFeatures.Add(ppUri.LocalPath + "\\" + Constants.FILE_AOI_VECTOR);
                            Names[idxRow].StationName = strStationName;
                            Names[idxRow].Huc2 = intHuc2;
                            break;
                        case NO_CHANGE_MATCH:
                            lstMergeFeatures.Add(ppUri.LocalPath + "\\" + Constants.FILE_AOI_VECTOR);
                            break;
                    }
                    // DO WE NEED TO UPDATE AOI_V?
                    string strAoiVPath = ppUri.LocalPath + "\\" + Constants.FILE_AOI_VECTOR;
                    string[] arrPpFields = { Constants.FIELD_STATION_TRIPLET, Constants.FIELD_STATION_NAME };
                    foreach (var strField in arrPpFields)
                    {
                        if (!await GeodatabaseTools.AttributeExistsAsync(ppUri, Constants.FILE_AOI_VECTOR, strField))
                        {
                            success = await GeoprocessingTools.AddFieldAsync(strAoiVPath, strField, "TEXT", null);
                        }
                    }
                    if (success == BA_ReturnCode.Success)
                    {
                        string strValue = await GeodatabaseTools.QueryTableForSingleValueAsync(ppUri, Constants.FILE_AOI_VECTOR,
                            Constants.FIELD_STATION_TRIPLET, queryFilter);
                        if (!dictEdits[Constants.FIELD_STATION_TRIPLET].Equals(strValue) || strRemark.Equals(UPDATED_STATION_DATA))
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

                    if (!strRemark.Equals(NO_CHANGE_MATCH))
                    {
                        strLogFileEntry = CreateLogEntry(aoiFolder, strTriplet, strNearTriplet, strRemark);
                        File.AppendAllText(_strForecastLogFile, strLogFileEntry);       // append
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
            }   // MOVE ON TO NEXT AOI
            strLogFileEntry = CreateLogEntry(ParentFolder, "", "", $@"Forecast station updates complete!!");
            File.AppendAllText(_strForecastLogFile, strLogFileEntry);       // append
            CmdForecastLogEnabled = true;   
            string strMergeMessage = "";
            if (MergeAoiVChecked)
            {
                success = await MergeAoiVectorsAsync(lstMergeFeatures, _strForecastLogFile);
                if (success == BA_ReturnCode.Success)
                {
                    strLogFileEntry = CreateLogEntry(ParentFolder, "", "", $@"aoi_v feature class merge complete!!");
                    strMergeMessage = $@" aoi_v polygons have been merged to {ParentFolder}\{Constants.FOLDER_MAP_PACKAGE}\{Constants.FILE_MERGE_GDB}\{Constants.FILE_MERGED_AOI_POLYS}";
                }
                else
                {
                    strLogFileEntry = CreateLogEntry(ParentFolder, "", "", $@"ERROR: aoi_v feature class merge encountered 1 or more errors!!");
                }
                File.AppendAllText(_strForecastLogFile, strLogFileEntry);       // append
            }
            MessageBox.Show($@"Forecast station updates done!!{strMergeMessage}");
        }

        private async void RunFireDataImplAsync(object param)
        {
            if (!Clip_Nifc_Checked && !Clip_Mtbs_Checked)
            {
                MessageBox.Show("No layer types selected to clip. Process stopped!", "BAGIS-Pro");
                return;
            }
            
            // Make sure the maps_publish\fire_statistics folder exists under the selected folder
            string strParentPath = Path.GetDirectoryName(Path.GetDirectoryName(_strFireDataLogFile));
            if (!Directory.Exists(strParentPath))
            {
                Directory.CreateDirectory(strParentPath);
            }
            if (!Directory.Exists(Path.GetDirectoryName(_strFireDataLogFile)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_strFireDataLogFile));
            }

            // Create initial log entry
            string strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Starting fire data " + "\r\n";
            File.WriteAllText(_strFireDataLogFile, strLogEntry);    // overwrite file if it exists
            var oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
            int maxYearClip = DateTime.Now.Year;
            int minYearClip = maxYearClip - FireDataClipYears;

            // Reset batch states
            ResetAoiBatchStateText();
            // Set status to waiting for selected AOIs
            int intReturn = SetAoiWaitingText();

            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
            {
                if (Names[idxRow].AoiBatchIsSelected)
                {
                    int errorCount = 0; // keep track of any non-fatal errors
                    string aoiFolder = Names[idxRow].FilePath;
                    if (!Names[idxRow].AoiBatchStateText.Equals(AoiBatchState.Waiting.ToString()))
                    {
                        strLogEntry = $@"{DateTime.Now.ToString("MM/dd/yy H:mm:ss")} Skipping fire data for {Names[idxRow].FilePath}. Required station information is missing.{System.Environment.NewLine}";
                        File.AppendAllText(_strFireDataLogFile, strLogEntry);
                        continue;
                    }
                    else
                    {
                        Names[idxRow].AoiBatchStateText = AoiBatchState.Started.ToString();  // update gui
                        strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Starting fire data for " +
                            Names[idxRow].Name + "\r\n";
                        File.AppendAllText(_strFireDataLogFile, strLogEntry);       // append
                    }
                    string strAoiFolder = GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Aoi);
                    // Set current AOI
                    Aoi oAoi = await GeneralTools.SetAoiAsync(aoiFolder, Names[idxRow]);
                    if (Module1.Current.CboCurrentAoi != null)
                    {
                        FrameworkApplication.Current.Dispatcher.Invoke(() =>
                        {
                            // Do something on the GUI thread
                            Module1.Current.CboCurrentAoi.SetAoiName(oAoi.Name);
                        });
                    }

                    // Make sure fire.gdb exists
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: ParentFolder);
                    BA_ReturnCode success = BA_ReturnCode.UnknownError;
                    if (!Directory.Exists(GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Fire)))
                    {
                        var parameters = Geoprocessing.MakeValueArray(aoiFolder, GeodatabaseNames.Fire.Value);
                        var gpResult = await Geoprocessing.ExecuteToolAsync("CreateFileGDB_management", parameters, environments,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        if (gpResult.IsFailed)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(RunFireDataImplAsync),
                                $@"Unable to create {GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Fire)}. Skipping AOI!");
                            return;
                        }
                    }

                    // Used this when clipping to aoi_b_v
                    //string[] arrUnmanagedBufferInfo = await GeneralTools.QueryBufferDistanceAsync(aoiFolder, GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Aoi),
                    //    Constants.FILE_AOI_BUFFERED_VECTOR, false);
                    //string unmanagedBufferDistance = arrUnmanagedBufferInfo[0];
                    //string unmanagedBufferUnits = arrUnmanagedBufferInfo[1];

                    //Clip nifc fire layers
                    string strWsUri = Module1.Current.DataSources[Constants.DATA_TYPE_FIRE_HISTORY].uri;
                    dynamic oFireSettings = GeneralTools.GetFireSettings(aoiFolder);

                    if (Clip_Nifc_Checked)
                    {
                        FeatureLayer lyrHistory = null;
                        string strOutputFc = GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Layers, true)
                            + Constants.FILE_FIRE_HISTORY;
                        string strClipFile = GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Aoi, true)
                            + Constants.FILE_AOI_VECTOR;
                        //success = await AnalysisTools.ClipFeatureLayerAsync(aoiFolder, strOutputFc, Constants.DATA_TYPE_FIRE_HISTORY,
                        //    "0", "Meters");
                        // Delete nifc-sourced layers before starting clip
                        string[] arrLayersToDelete = {$"{GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Fire, true)}{Constants.FILE_NIFC_FIRE}",
                                                      $"{GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Layers, true)}{Constants.FILE_FIRE_HISTORY}",
                                                      $"{GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Layers, true)}{Constants.FILE_FIRE_CURRENT}"};
                        for (int i = 0; i < arrLayersToDelete.Length; i++)
                        {
                            Uri uri = new Uri(Path.GetDirectoryName(arrLayersToDelete[i]));
                            string fcName = Path.GetFileName(arrLayersToDelete[i]);
                            if (await GeodatabaseTools.FeatureClassExistsAsync(uri, fcName))
                            {
                                success = await GeoprocessingTools.DeleteDatasetAsync(arrLayersToDelete[i]);
                            }
                        }
                        // Remove nifc-related parameters before starting clip
                        oFireSettings.Remove("lastNifcYear");
                        oFireSettings.Remove("dataBeginYear");
                        oFireSettings.Remove("fireDataClipYears");
                        oFireSettings.Remove("annualReportEndYear");
                        oFireSettings.Remove("incrementReportEndYear");
                        oFireSettings.Remove("increment");
                        // Remove nifc data sources before starting clip
                        if (oFireSettings.DataSources != null)
                        {
                            JArray oDataSources = oFireSettings.DataSources;
                            if (oDataSources.Count > 0)
                            {
                                var foundDs = oDataSources.FirstOrDefault(a => a["layerType"].ToString().Equals(Constants.DATA_TYPE_FIRE_HISTORY));
                                if (foundDs != null) 
                                {
                                    oDataSources.Remove(foundDs);
                                }
                                foundDs = oDataSources.FirstOrDefault(a => a["layerType"].ToString().Equals(Constants.DATA_TYPE_FIRE_CURRENT));
                                if (foundDs != null)
                                {
                                    oDataSources.Remove(foundDs);
                                }
                            }
                        }

                        var environmentsClip = Geoprocessing.MakeEnvironmentArray(workspace: aoiFolder, extent:strClipFile );
                        var parametersClip = Geoprocessing.MakeValueArray(strWsUri, strClipFile, strOutputFc, "");
                        var gpResultClip = await Geoprocessing.ExecuteToolAsync("Clip_analysis", parametersClip, environmentsClip,
                                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        if (!gpResultClip.IsFailed)
                        {
                            if (gpResultClip.HasWarnings)
                            {
                                Module1.Current.ModuleLogManager.LogWarn(nameof(RunFireDataImplAsync),
                                    Constants.FILE_FIRE_HISTORY + " completed with the following warnings: ");
                                foreach (var msg in gpResultClip.Messages)
                                {
                                    if (msg.Type.Equals(GPMessageType.Warning))
                                    {
                                        Module1.Current.ModuleLogManager.LogWarn(nameof(RunFireDataImplAsync), msg.Text);
                                    }
                                }
                                strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + Constants.FILE_FIRE_HISTORY + 
                                    " clipping completed with WARNINGS. Check the AOI-level log for details \r\n";
                                File.AppendAllText(_strFireDataLogFile, strLogEntry);       // append
                            }
                            Uri uriLayers = new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Layers));
                            if (!await GeodatabaseTools.AttributeExistsAsync(uriLayers, Constants.FILE_FIRE_HISTORY, Constants.FIELD_YEAR))
                            {
                                success = await GeoprocessingTools.AddFieldAsync(strOutputFc, Constants.FIELD_YEAR, "SHORT", null);
                                if (success == BA_ReturnCode.Success)
                                {
                                    var parameters = Geoprocessing.MakeValueArray(strOutputFc, Constants.FIELD_YEAR,
                                        "!FIRE_YEAR_INT!", "PYTHON3", "", "SHORT");
                                    IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("management.CalculateField", parameters, null,
                                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                                    if (gpResult.IsFailed)
                                    {
                                        Module1.Current.ModuleLogManager.LogError(nameof(RunFireDataImplAsync),
                                            "CalculateField tool failed to update YEAR field. Error code: " + gpResult.ErrorCode);
                                        success = BA_ReturnCode.UnknownError;
                                    }
                                }
                            }
                            if (success == BA_ReturnCode.Success)
                            {
                                    await QueuedTask.Run(() =>
                                    {
                                        var historyParams = new FeatureLayerCreationParams(new Uri(strOutputFc))
                                        {
                                            Name = "Selection Layer",
                                            IsVisible = false,
                                            MapMemberIndex = 0,
                                            MapMemberPosition = 0,
                                        };
                                        lyrHistory = LayerFactory.Instance.CreateLayer<FeatureLayer>(historyParams, oMap);
                                        lyrHistory.SetDefinitionQuery($@"{Constants.FIELD_YEAR} >= {minYearClip}");
                                    });
                            }
                            GeneralTools.UpdateFireDataSourceSettings(ref oFireSettings, aoiFolder, Module1.Current.DataSources, Constants.DATA_TYPE_FIRE_HISTORY, false);
                        }
                        else
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(RunFireDataImplAsync),
                                "An error occurred while clipping " + Constants.FILE_FIRE_HISTORY + "!");
                            foreach (var msg in gpResultClip.Messages)
                            {
                                if (msg.Type.Equals(GPMessageType.Error))
                                {
                                    Module1.Current.ModuleLogManager.LogError(nameof(RunFireDataImplAsync), msg.Text);
                                }
                            }
                            strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "An error occurred while clipping " +
                                Constants.FILE_FIRE_HISTORY + ". Check the AOI-level log for details \r\n";
                            File.AppendAllText(_strFireDataLogFile, strLogEntry);       // append
                            success = BA_ReturnCode.WriteError;
                        }
                        strWsUri = Module1.Current.DataSources[Constants.DATA_TYPE_FIRE_CURRENT].uri;
                        strOutputFc = GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Layers, true)
                            + Constants.FILE_FIRE_CURRENT;
                        parametersClip = Geoprocessing.MakeValueArray(strWsUri, strClipFile, strOutputFc, "");
                        gpResultClip = await Geoprocessing.ExecuteToolAsync("Clip_analysis", parametersClip, environmentsClip,
                                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        if (!gpResultClip.IsFailed)
                        {
                            if (gpResultClip.HasWarnings)
                            {
                                Module1.Current.ModuleLogManager.LogWarn(nameof(RunFireDataImplAsync),
                                    Constants.FILE_FIRE_CURRENT + " completed with the following warnings: ");
                                foreach (var msg in gpResultClip.Messages)
                                {
                                    if (msg.Type.Equals(GPMessageType.Warning))
                                    {
                                        Module1.Current.ModuleLogManager.LogWarn(nameof(RunFireDataImplAsync), msg.Text);
                                    }
                                }
                                strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + Constants.FILE_FIRE_CURRENT +
                                    " clipping completed with WARNINGS. Check the AOI-level log for details \r\n";
                                File.AppendAllText(_strFireDataLogFile, strLogEntry);       // append
                            }
                            GeneralTools.UpdateFireDataSourceSettings(ref oFireSettings, aoiFolder, Module1.Current.DataSources, Constants.DATA_TYPE_FIRE_CURRENT, false);
                            success = await GeoprocessingTools.AddFieldAsync(strOutputFc, Constants.FIELD_YEAR, "SHORT", null);
                            if (success == BA_ReturnCode.Success)
                            {
                                var parameters = Geoprocessing.MakeValueArray(strOutputFc, Constants.FIELD_YEAR,
                                    $@"Year($feature.{Constants.FIELD_FIRECURRENT_DATE})", "Arcade", "", "SHORT");
                                IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("management.CalculateField", parameters, null,
                                    CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                                if (gpResult.IsFailed)
                                {
                                    Module1.Current.ModuleLogManager.LogError(nameof(RunFireDataImplAsync),
                                        "CalculateField tool failed to update YEAR field. Error code: " + gpResult.ErrorCode);
                                    success = BA_ReturnCode.UnknownError;
                                }
                            }
                        }
                        else
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(RunFireDataImplAsync),
                                "An error occurred while clipping " + Constants.FILE_FIRE_CURRENT + "!");
                            foreach (var msg in gpResultClip.Messages)
                            {
                                if (msg.Type.Equals(GPMessageType.Error))
                                {
                                    Module1.Current.ModuleLogManager.LogError(nameof(RunFireDataImplAsync), msg.Text);
                                }
                            }
                            strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "An error occurred while clipping " +
                                Constants.FILE_FIRE_CURRENT + ". Check the AOI-level log for details \r\n";
                            File.AppendAllText(_strFireDataLogFile, strLogEntry);       // append
                            success = BA_ReturnCode.WriteError;
                        }


                        // Merge features
                        string strCurrentId = "poly_SourceOID"; // Used to identify records that come from fire_current
                        string strMergeFc = "";
                        if (success == BA_ReturnCode.Success && lyrHistory != null)
                        {
                            string strInputDatasets = $@"{lyrHistory.Name};{strOutputFc}";
                            strMergeFc = GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Fire, true)
                                + Constants.FILE_NIFC_FIRE;
                            string strIrwinIdMap = $@"{Constants.FIELD_IRWIN_ID} ""{Constants.FIELD_IRWIN_ID}"" true true false 50 Text 0 0,First,#,{lyrHistory.Name},{Constants.FIELD_IRWIN_ID},0,50,{strOutputFc},poly_IRWINID,0,38;";
                            string strIncidentMap = $@"{Constants.FIELD_INCIDENT} ""{Constants.FIELD_INCIDENT}"" true true false 50 Text 0 0,First,#,{lyrHistory.Name},{Constants.FIELD_INCIDENT},0,50,{strOutputFc},{Constants.FIELD_FIRECURRENT_INCIDENT},0,50;";
                            string strYearMap = $@"{Constants.FIELD_YEAR} ""{Constants.FIELD_YEAR}"" true true false 2 Short 0 0,First,#,{lyrHistory.Name},{Constants.FIELD_YEAR},-1,-1,{strOutputFc},{Constants.FIELD_YEAR},-1,-1;";
                            string strFieldMap = $@"{strIrwinIdMap}{strIncidentMap}{strYearMap}{strCurrentId} ""{strCurrentId}"" true true false 4 Long 0 0,First,#,{strOutputFc},{strCurrentId},-1,-1";
                            var parameters = Geoprocessing.MakeValueArray(strInputDatasets, strMergeFc, strFieldMap, "NO_SOURCE_INFO");
                            IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("Merge_management", parameters, null,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                            if (gpResult.IsFailed)
                            {
                                Module1.Current.ModuleLogManager.LogError(nameof(RunFireDataImplAsync),
                                     "Unable to merge features. Error code: " + gpResult.ErrorCode);
                                success = BA_ReturnCode.UnknownError;
                            }
                        }
                        else
                        {
                            success = BA_ReturnCode.ReadError;
                        }

                        if (success == BA_ReturnCode.Success)
                        {
                            string strWhere1 = $@"{strCurrentId} > 0";    //Identifies features that came from firecurrent
                            string strNotWhere1 = $@"{strCurrentId} IS NULL";
                            success = await AnalysisTools.DeleteDuplicatesAsync(strMergeFc, strWhere1, Constants.FIELD_IRWIN_ID, strNotWhere1);
                        }
                        if (success == BA_ReturnCode.Success)
                        {
                            success = await AnalysisTools.DeleteIrwinDuplicatesAsync(aoiFolder);
                        }

                        if (success == BA_ReturnCode.Success)
                        {
                            success = await AnalysisTools.DissolveIncidentDuplicatesAsync(aoiFolder);
                        }

                        if (success == BA_ReturnCode.Success)
                        {
                            // Modify fire settings in memory
                            oFireSettings.lastNifcYear = _intNifcMaxYear;
                            oFireSettings.dataBeginYear = _intMinYear;
                            oFireSettings.fireDataClipYears = FireDataClipYears;
                        }

                        // Recalculate area due to bug in Pro
                        string strAreaProperties = Constants.FIELD_RECALC_AREA + " AREA_GEODESIC";
                        success = await GeoprocessingTools.CalculateGeometryAsync(strMergeFc, strAreaProperties, "SQUARE_METERS");
                        // Remove temporary layer
                        await QueuedTask.Run(() =>
                        {
                            oMap.RemoveLayer(lyrHistory);
                        });
                    }

                    if (Clip_Mtbs_Checked)
                    {
                        List<string> lstMtbsImageServices = new List<string>();
                        List<string> lstMtbsLayerNames = new List<string>();
                        DataSource dsFire = new DataSource(Module1.Current.DataSources[DataSource.GetFireBurnSeverityKey]);
                        for (int i = minYearClip; i <= _intMtbsMaxYear; i++)
                        {
                            lstMtbsImageServices.Add($@"{dsFire.uri}{GeneralTools.GetMtbsLayerFileName(i)}/{Constants.URI_IMAGE_SERVER}");
                            lstMtbsLayerNames.Add(GeneralTools.GetMtbsLayerFileName(i));
                        }
                        if (lstMtbsImageServices.Count > 0)
                        {
                            int intRetVal = await AnalysisTools.ClipMtbsLayersAsync(aoiFolder, Constants.FILE_AOI_VECTOR, lstMtbsImageServices, lstMtbsLayerNames,
                                _intMtbsMaxYear, Reclip_MTBS_Checked);
                            if (intRetVal > 0)
                            {
                                // Clean out old settings before trying to add new ones
                                oFireSettings.Remove("annualReportEndYear");
                                oFireSettings.Remove("incrementReportEndYear");
                                oFireSettings.Remove("increment");
                                // Remove mtbs data source
                                if (oFireSettings.DataSources != null)
                                {
                                    JArray oDataSources = oFireSettings.DataSources;
                                    if (oDataSources.Count > 0)
                                    {

                                        var foundDs = oDataSources.FirstOrDefault(a => a["layerType"].ToString().Equals(DataSource.GetFireBurnSeverityKey));
                                        if (foundDs != null)
                                        {
                                            oDataSources.Remove(foundDs);
                                        }
                                    }
                                }

                                // Update settings file in memory
                                if (oFireSettings.DataSources != null)
                                {
                                    // We know the file exists
                                    oFireSettings.mtbsLegend = Module1.Current.BagisSettings.MtbsLegend;
                                    oFireSettings.lastMtbsYear = _intMtbsMaxYear;
                                    oFireSettings.dataBeginYear = _intMinYear;
                                    oFireSettings.fireDataClipYears = FireDataClipYears;
                                 }
                                // Updates the MTBS data source and saves the file
                                GeneralTools.UpdateFireDataSourceSettings(ref oFireSettings, aoiFolder, Module1.Current.DataSources, Constants.DATA_TYPE_FIRE_BURN_SEVERITY, false);
                                success = BA_ReturnCode.Success;
                            }
                        }
                    }

                    bool bMissingFireHistory = await GeodatabaseTools.MissingFireHistory(aoiFolder);
                    if (bMissingFireHistory)
                    {
                        strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") +
                            "WARN: This AOI has mtbs severity data but no firehistory features! \r\n";
                        File.AppendAllText(_strFireDataLogFile, strLogEntry);       // append
                    }

                    // Save fire json settings to disk
                    try
                    {
                        // serialize JSON directly to a file
                        using (StreamWriter file = File.CreateText($@"{aoiFolder}\{Constants.FOLDER_MAPS}\{Constants.FILE_FIRE_SETTINGS}"))
                        {
                            JsonSerializer serializer = new JsonSerializer();
                            serializer.Formatting = Newtonsoft.Json.Formatting.Indented;
                            serializer.Serialize(file, oFireSettings);
                        }
                    }
                    catch (Exception)
                    {
                        //munch
                    }

                    if (success == BA_ReturnCode.Success && errorCount == 0)
                    {
                        Names[idxRow].AoiBatchStateText = AoiBatchState.Completed.ToString();  // update gui
                        strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Fire data complete for " +
                            Names[idxRow].Name + "\r\n";
                        File.AppendAllText(_strFireDataLogFile, strLogEntry);       // append
                    }
                    else
                    {
                        Names[idxRow].AoiBatchStateText = AoiBatchState.Failed.ToString();
                        strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Fire data FAILED for " +
                            Names[idxRow].Name + "\r\n";
                        File.AppendAllText(_strFireDataLogFile, strLogEntry);
                    }
                }
            }
            strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Fire data generation complete!! \r\n";
            File.AppendAllText(_strFireDataLogFile, strLogEntry);       // append
        }

        private async void RunFireReportImplAsync(object param)
        {
            // Make sure the maps_publish folder exists under the selected folder
            // Make sure the maps_publish\fire_statistics folder exists under the selected folder
            string strParentPath = Path.GetDirectoryName(Path.GetDirectoryName(_strFireDataLogFile));
            if (!Directory.Exists(strParentPath))
            {
                Directory.CreateDirectory(strParentPath);
            }
            if (!Directory.Exists(Path.GetDirectoryName(_strFireDataLogFile)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_strFireDataLogFile));
            }

            // Create initial log entry
            string strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Starting fire report " + "\r\n";
            File.WriteAllText(_strFireReportLogFile, strLogEntry);    // overwrite file if it exists

            int intIncrementPeriods = 0;
            IList<Interval> lstInterval = null;
            int overrideMinYear = _intMinYear;
            int overrideMaxYear = AnnualEndYear;
            bool bAnnualStatistics = true;
            bool bIncrementStatistics = true;
            // Only check this once
            if (SelectedTimeChecked)
            {
                overrideMinYear = SelectedMinYear;
                overrideMaxYear = SelectedMaxYear;
                if (!AnnualDataChecked)
                {
                    bAnnualStatistics = false;
                }
                if (!IncrementDataChecked)
                {
                    bIncrementStatistics = false;
                }
            }
            // Warn if there is existing output and give option to terminate
            if (bAnnualStatistics)
            {
                var dir = new DirectoryInfo($@"{Path.GetDirectoryName(_strFireReportLogFile)}");
                var fileEnum = dir.EnumerateFiles("*_annual_statistics.csv");
                if (fileEnum.Count() > 0)
                {
                    System.Windows.MessageBoxResult res = MessageBox.Show("At least 1 annual statistics file exists in the output folder. Do you want to continue and overwrite these files?", "BAGIS-Pro",
                        System.Windows.MessageBoxButton.YesNo);
                    if (res != System.Windows.MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
            }
            string strCsvIncrement = $@"{Path.GetDirectoryName(_strFireReportLogFile)}\increment_statistics.csv";
            if (bIncrementStatistics)
            {
                if (File.Exists(strCsvIncrement))
                {
                    System.Windows.MessageBoxResult res = MessageBox.Show("The increment_statistics.csv file exists in the output folder. Do you want to continue and overwrite this file?", "BAGIS-Pro",
                        System.Windows.MessageBoxButton.YesNo);
                    if (res != System.Windows.MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
            }

            if (bAnnualStatistics)
            {
                // Delete any annual statistics files we plan to overwrite
                for (int i = overrideMinYear; i <= overrideMaxYear; i++)
                {
                    string strCsvFile = $@"{Path.GetDirectoryName(_strFireReportLogFile)}\{i}_annual_statistics.csv";
                    if (File.Exists(strCsvFile))
                    {
                        try
                        {
                            File.Delete(strCsvFile);
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show($@"Unable to overwrite {strCsvFile}. Report process stopped!");
                            return;
                        }

                    }
                }
            }
            if (bIncrementStatistics)
            {
                if (File.Exists(strCsvIncrement))
                {
                    try
                    {
                        File.Delete(strCsvIncrement);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($@"Unable to overwrite {strCsvIncrement}. Report process stopped!");
                        return;
                    }
                }
            }
            string[] arrValidStatus = [AoiBatchState.HasReport.ToString(), AoiBatchState.MissingReport.ToString(), AoiBatchState.NoFire.ToString()];
            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
            {
                Aoi oAoi = Names[idxRow];
                if (Names[idxRow].AoiBatchIsSelected && arrValidStatus.Contains(oAoi.AoiBatchStateText))
                {
                    oAoi.AoiBatchStateText = AoiBatchState.Waiting.ToString();
                }
            }

            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
            {
                if (Names[idxRow].AoiBatchIsSelected)
                {
                    string aoiFolder = Names[idxRow].FilePath;
                    if (!Names[idxRow].AoiBatchStateText.Equals(AoiBatchState.Waiting.ToString()))
                    {
                        strLogEntry = $@"{DateTime.Now.ToString("MM/dd/yy H:mm:ss")} Skipping fire report for {Names[idxRow].FilePath}. AOI not ready for reports.{System.Environment.NewLine}";
                        File.AppendAllText(_strFireReportLogFile, strLogEntry);
                        continue;
                    }
                    else
                    {

                    }
                    // Set current AOI
                    Aoi oAoi = await GeneralTools.SetAoiAsync(aoiFolder, Names[idxRow]);
                    if (Module1.Current.CboCurrentAoi != null)
                    {
                        FrameworkApplication.Current.Dispatcher.Invoke(() =>
                        {
                            // Do something on the GUI thread
                            Module1.Current.CboCurrentAoi.SetAoiName(oAoi.Name);
                        });
                    }

                    Uri aoiUri = new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi));
                    Uri fireUri = new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Fire));
                    double aoiAreaSqMeters = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(aoiUri, Constants.FILE_AOI_VECTOR, null);
                    double cellSizeSqMeters = -1;
                    for (int i = overrideMinYear; i <= overrideMaxYear; i++)
                    {
                        string strRasterName = GeneralTools.GetMtbsLayerFileName(i);
                        Uri fullFireUri = new Uri($@"{fireUri.LocalPath}\{strRasterName}");
                        if (await GeodatabaseTools.RasterDatasetExistsAsync(fireUri, strRasterName))
                        {
                            double dblTest = await GeodatabaseTools.GetCellSizeAsync(fullFireUri, WorkspaceType.Geodatabase);
                            if (dblTest > 0)
                            {
                                cellSizeSqMeters = Math.Round(dblTest,2);
                                break;
                            }
                        }
                    }

                    IList<string> lstMissingMtbsYears = await GeodatabaseTools.QueryMissingMtbsRasters(oAoi.FilePath, overrideMinYear, overrideMaxYear);
                    if (bAnnualStatistics)
                    {
                        for (int i = overrideMinYear; i <= overrideMaxYear; i++)
                        {
                            BA_ReturnCode success = BA_ReturnCode.WriteError;
                            string strCsvFile = $@"{Path.GetDirectoryName(_strFireReportLogFile)}\{i}_annual_statistics.csv";
                            if (!File.Exists(strCsvFile))
                            {
                                success = this.InitAnnualCsv(i, strCsvFile);
                            }
                            else
                            {
                                success = BA_ReturnCode.Success;
                            }
                            if (success == BA_ReturnCode.Success)
                            {
                                IList<string> lstAnnualElements = await AnalysisTools.GenerateAnnualFireStatisticsList(oAoi, _strFireReportLogFile,
                                    aoiAreaSqMeters, cellSizeSqMeters, i, overrideMaxYear, lstMissingMtbsYears);
                                if (lstAnnualElements.Count > 0)
                                {
                                    try
                                    {
                                        // Adds new content to file
                                        StringBuilder sb = new StringBuilder();
                                        foreach (var item in lstAnnualElements)
                                        {
                                            sb.Append($@"{item}{_separator}");
                                        }
                                        using (StreamWriter sw = File.AppendText(strCsvFile))
                                        {
                                            sw.WriteLine(sb.ToString());
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        strLogEntry = $@"{DateTime.Now.ToString("MM/dd/yy H:mm:ss")} Data could not be written to the {strCsvFile} file!{System.Environment.NewLine}";
                                        File.AppendAllText(_strFireReportLogFile, strLogEntry);
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    if (bIncrementStatistics)
                    {
                        // Generating increment statistics
                        bool bRequestPeriods = false;
                        if (IncrementDataChecked)
                        {
                            bRequestPeriods = true;
                        }
                        BA_ReturnCode success = BA_ReturnCode.WriteError;
                        string strCsvFile = $@"{Path.GetDirectoryName(_strFireReportLogFile)}\increment_statistics.csv";
                        lstInterval = GeneralTools.GetFireStatisticsIntervals(IncrementalEndYear, FireDataClipYears, FireIncrementYears, bRequestPeriods, FireTimePeriodCount, out intIncrementPeriods);
                        if (!File.Exists(strCsvFile))
                        {
                            success = this.InitIncrementCsv(intIncrementPeriods, strCsvFile);
                        }
                        else
                        {
                            success = BA_ReturnCode.Success;
                        }
                        IList<string> lstOutput = await AnalysisTools.GenerateIncrementFireStatisticsList(oAoi, _strFireReportLogFile,
                            aoiAreaSqMeters, cellSizeSqMeters, lstInterval, lstMissingMtbsYears);
                        if (lstOutput.Count > 0)
                        {
                            try
                            {
                                // Adds new content to file
                                StringBuilder sb = new StringBuilder();
                                foreach (var item in lstOutput)
                                {
                                    sb.Append($@"{item}{_separator}");
                                }
                                using (StreamWriter sw = File.AppendText(strCsvFile))
                                {
                                    sw.WriteLine(sb.ToString());
                                }
                            }
                            catch (Exception ex)
                            {
                                strLogEntry = $@"{DateTime.Now.ToString("MM/dd/yy H:mm:ss")} Data could not be written to the {strCsvFile} file!{System.Environment.NewLine}";
                                File.AppendAllText(_strFireReportLogFile, strLogEntry);
                                return;
                            }
                        }

                    }

                    try
                    {
                        dynamic oFireSettings = GeneralTools.GetFireSettings(oAoi.FilePath);
                        if (oFireSettings.DataSources != null)
                        {
                            // We know the file exists
                            if (bAnnualStatistics)
                            {
                                oFireSettings.annualReportEndYear = overrideMaxYear;
                            }
                            else
                            {
                                oFireSettings.Remove("annualReportEndYear");
                            }
                            if (bIncrementStatistics)
                            {
                                oFireSettings.incrementReportEndYear = IncrementalEndYear;
                                oFireSettings.increment = FireIncrementYears;
                            }
                            else
                            {
                                oFireSettings.Remove("incrementReportEndYear");
                                oFireSettings.Remove("increment");
                            }

                                // serialize JSON directly to a file
                                using (StreamWriter file = File.CreateText($@"{oAoi.FilePath}\{Constants.FOLDER_MAPS}\{Constants.FILE_FIRE_SETTINGS}"))
                                {
                                    JsonSerializer serializer = new JsonSerializer();
                                    serializer.Formatting = Newtonsoft.Json.Formatting.Indented;
                                    serializer.Serialize(file, oFireSettings);
                                }
                        }
                    }
                    catch (Exception)
                    {
                        strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Unable to update fire_analysis.json for " +
                            Names[idxRow].Name + "\r\n";
                        File.AppendAllText(_strFireReportLogFile, strLogEntry);       // append
                    }

                    Names[idxRow].AoiBatchStateText = AoiBatchState.Completed.ToString();  // update gui
                    strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Finished generate fire statistics export for " +
                        Names[idxRow].Name + "\r\n";
                    File.AppendAllText(_strFireReportLogFile, strLogEntry);       // append
                }
            }
            MessageBox.Show("Fire statistics have been generated!");
        }

        private async void RunFireMapsImplAsync(object param)
        {
            // Make sure the maps_publish\fire_statistics folder exists under the selected folder
            if (!Directory.Exists(Path.GetDirectoryName(_strFireMapsLogFile)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_strFireMapsLogFile));
            }

            // Create initial log entry
            string strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Starting fire maps " + "\r\n";
            File.WriteAllText(_strFireMapsLogFile, strLogEntry);    // overwrite file if it exists

            int intIncrementPeriods = 0;
            IList<Interval> lstInterval = null;
            int overrideMinYear = _intMinYear;
            int overrideMaxYear = AnnualEndYear;
            bool bAnnualMaps = true;
            bool bIncrementMaps = true;
            // Only check this once
            if (SelectedTimeChecked)
            {
                overrideMinYear = SelectedMinYear;
                overrideMaxYear = SelectedMaxYear;
                if (!AnnualDataChecked)
                {
                    bAnnualMaps = false;
                }
                if (!IncrementDataChecked)
                {
                    bIncrementMaps = false;
                }
            }

            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
            {
                Aoi oAoi = Names[idxRow];
                if (Names[idxRow].AoiBatchIsSelected && oAoi.AoiBatchStateText == AoiBatchState.HasReport.ToString())
                {
                    oAoi.AoiBatchStateText = AoiBatchState.Waiting.ToString();
                }
            }

            for (int idxRow = 0; idxRow < Names.Count; idxRow++)
            {
                if (Names[idxRow].AoiBatchIsSelected)
                {
                    string aoiFolder = Names[idxRow].FilePath;
                    string strFireGdbPath = GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Fire);
                    if (!Names[idxRow].AoiBatchStateText.Equals(AoiBatchState.Waiting.ToString()))
                    {
                        strLogEntry = $@"{DateTime.Now.ToString("MM/dd/yy H:mm:ss")} Skipping fire map for {Names[idxRow].FilePath}. Required station information is missing.{System.Environment.NewLine}";
                        File.AppendAllText(_strFireMapsLogFile, strLogEntry);
                        continue;
                    }
                    else
                    {
                        Names[idxRow].AoiBatchStateText = AoiBatchState.Started.ToString();  // update gui
                        strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Starting fire maps for " +
                            Names[idxRow].Name + "\r\n";
                        File.AppendAllText(_strFireReportLogFile, strLogEntry);       // append
                    }
                    // Set current AOI
                    Aoi oAoi = await GeneralTools.SetAoiAsync(aoiFolder, Names[idxRow]);
                    if (Module1.Current.CboCurrentAoi != null)
                    {
                        FrameworkApplication.Current.Dispatcher.Invoke(() =>
                        {
                            // Do something on the GUI thread
                            Module1.Current.CboCurrentAoi.SetAoiName(oAoi.Name);
                        });
                    }

                    Uri aoiUri = new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi));
                    Uri fireUri = new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Fire));
                    string strExportPrefix = "";
                    if (!string.IsNullOrEmpty(Module1.Current.Aoi.StationTriplet))
                    {
                        strExportPrefix = Module1.Current.Aoi.StationTriplet.Replace(':', '_');
                    }
                    else
                    {
                        strExportPrefix = Constants.VALUE_NOT_SPECIFIED;
                        
                    }
                    string strFireReportPath = $@"{Module1.Current.Aoi.FilePath}\{Constants.FOLDER_MAP_PACKAGE}\{Constants.FOLDER_FIRE_STATISTICS}\{strExportPrefix}{Constants.FILE_REPORT_SUFFIX_PDF}";
                    // Make sure output directory exists
                    if (!Directory.Exists(Path.GetDirectoryName(strFireReportPath)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(strFireReportPath));
                    }
                    var result = await GeodatabaseTools.CalculateAoiAreaSqMetersAsync(Module1.Current.Aoi.FilePath, -1);
                    double aoiAreaSqMeters = result.Item1;
                    double areaSqKm = AreaUnit.SquareMeters.ConvertTo(aoiAreaSqMeters, AreaUnit.SquareKilometers);
                    IList<string> lstMissingMtbsYears = await GeodatabaseTools.QueryMissingMtbsRasters(oAoi.FilePath, overrideMinYear, overrideMaxYear);
                    string strMissingMtbsYears = "None";
                    if (lstMissingMtbsYears.Count > 0)
                    {
                        strMissingMtbsYears = string.Join(", ", lstMissingMtbsYears);
                    }

                    BA_ReturnCode success = GeneralTools.GenerateFireMapsTitlePage(areaSqKm, strMissingMtbsYears);
                    Layout oLayout = await MapTools.GetDefaultLayoutAsync(Constants.MAPS_FIRE_LAYOUT_NAME);
                    string strLayerFilePath = Module1.Current.SettingsPath + "\\" + Constants.FOLDER_SETTINGS + "\\" + Constants.LAYER_FILE_MTBS_FIRE;

                    // Always load the maps in case we are running through multiple Aois
                    success = await MapTools.DisplayFireMapsAsync(Module1.Current.Aoi.FilePath, oLayout);
                    if (success != BA_ReturnCode.Success)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(RunFireMapsImplAsync),
                            "An error occurred while trying to load the maps. The map package cannot be exported!");
                        Names[idxRow].AoiBatchStateText = AoiBatchState.Failed.ToString();
                        return;
                    }

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

                    // Legend
                    success = await MapTools.DisplayLegendAsync(Constants.MAPS_FIRE_MAP_FRAME_NAME, oLayout,
                        "ArcGIS Colors", "1.5 Point", true);

                    MapDefinition defaultMap = null;
                    if (oLayout != null)
                    {
                        if (success == BA_ReturnCode.Success)
                        {
                            defaultMap = MapTools.LoadMapDefinition(BagisMapType.FIRE);
                            success = await MapTools.UpdateLegendAsync(oLayout, defaultMap.LegendLayerList);
                        }
                    }
                    Map oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_FIRE_MAP_NAME);
                    var nifcLayer = oMap.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where(l => l.Name.Equals(Constants.MAPS_NIFC_PERIMETER, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    IList<string> lstExportedMaps = new List<string>();
                    if (bAnnualMaps)
                    {
                        for (int i = overrideMinYear; i <= overrideMaxYear; i++)
                        {
                            string strWhere = $"{Constants.FIELD_YEAR} = {i}";
                            long lngCount = await GeodatabaseTools.CountFeaturesWithFilterAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Fire)),
                                Constants.FILE_NIFC_FIRE, strWhere);
                            //@ToDo: if lngCount is 0, we don't try to produce a map with mtbs. Is this what we want?
                            Module1.Current.DisplayedMap = $@"{strExportPrefix}_{i}{Constants.FILE_MAP_SUFFIX_PDF}";
                            if (success == BA_ReturnCode.Success && lngCount > 0)
                            {
                                await QueuedTask.Run(() =>
                                {
                                    nifcLayer.SetDefinitionQuery($@"{Constants.FIELD_YEAR} = {i}");
                                    nifcLayer.SetVisibility(true);
                                });
                                if (!lstMissingMtbsYears.Contains(Convert.ToString(i)))
                                {
                                    Uri uriMtbs = new Uri($@"{GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Fire, true)}{GeneralTools.GetMtbsLayerFileName(i)}_RECL");
                                    success = await MapTools.DisplayUniqueValuesRasterFromLayerFileAsync(Constants.MAPS_FIRE_MAP_NAME, uriMtbs, this.GetMtbsMapName(i),
                                        strLayerFilePath, 25, true);
                                    // Put nifc layer on top; In order to move layerToMove, I need to know if the destination is a group layer and the zero based position it needs to move to.
                                    Tuple<GroupLayer, int> moveToLayerPosition = MapTools.FindLayerPosition(null, this.GetMtbsMapName(i), oMap);
                                    if (moveToLayerPosition.Item2 == -1)
                                    {
                                        Module1.Current.ModuleLogManager.LogError(nameof(RunFireMapsImplAsync), $"Layer {this.GetMtbsMapName(i)} not found ");
                                    }
                                    await QueuedTask.Run(() =>
                                    {
                                        // subtract 1 from moveToLayerPosition.Item2 because we want it ABOVE the found layer
                                        if (moveToLayerPosition.Item1 != null) //layer gets moved into the group
                                            moveToLayerPosition.Item1.MoveLayer(nifcLayer, moveToLayerPosition.Item2 - 1);
                                        else //Layer gets moved into the root
                                            oMap.MoveLayer(nifcLayer, moveToLayerPosition.Item2 - 1);
                                    });
                                }
                                defaultMap.Title = $@"FIRE DISTURBANCE {i}";
                                success = await MapTools.UpdateMapElementsAsync(Module1.Current.Aoi.StationName, Constants.MAPS_FIRE_LAYOUT_NAME, defaultMap);
                                success = await GeneralTools.ExportMapToPdfAsync(Constants.PDF_EXPORT_RESOLUTION);
                                var mtbsLayer = oMap.GetLayersAsFlattenedList().OfType<RasterLayer>().Where(l => l.Name.Equals(this.GetMtbsMapName(i), StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                                if (mtbsLayer != null)
                                {
                                    await QueuedTask.Run(() =>
                                    {
                                        mtbsLayer.SetVisibility(false);
                                    });
                                }
                            }
                            else
                            {
                                GeneralTools.GenerateBlankPage($"FIRE DISTURBANCE {i}", 
                                    GeneralTools.GetFullPdfFileName(Module1.Current.DisplayedMap));
                            }
                            lstExportedMaps.Add(GeneralTools.GetFullPdfFileName(Module1.Current.DisplayedMap));
                        }
                    }
                    if (bIncrementMaps)
                    {
                        // Generating increment maps
                        bool bRequestPeriods = false;
                        if (IncrementDataChecked)
                        {
                            bRequestPeriods = true;
                        }
                        lstInterval = GeneralTools.GetFireStatisticsIntervals(IncrementalEndYear, FireDataClipYears, FireIncrementYears, bRequestPeriods, FireTimePeriodCount, out intIncrementPeriods);
                        foreach (var oInterval in lstInterval)
                        {
                            string strFileName = $"Max_{oInterval.Value}";
                            string strLayerName = $"MTBS {oInterval.LowerBound}-{oInterval.UpperBound}";
                            Module1.Current.DisplayedMap = $@"{strExportPrefix}_{oInterval.LowerBound}-{oInterval.UpperBound}{Constants.FILE_MAP_SUFFIX_PDF}";
                            string strWhere = $"{Constants.FIELD_YEAR} >= {oInterval.LowerBound} And {Constants.FIELD_YEAR} <= {oInterval.UpperBound}";
                            long lngCount = await GeodatabaseTools.CountFeaturesWithFilterAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Fire)),
                                Constants.FILE_NIFC_FIRE, strWhere);
                            if (lngCount > 0)
                            {
                                //Set NIFC fire filter to the interval; Also count features within interval
                                await QueuedTask.Run(() =>
                                {
                                    nifcLayer.SetDefinitionQuery($@"{Constants.FIELD_YEAR} >= {oInterval.LowerBound} And {Constants.FIELD_YEAR} <= {oInterval.UpperBound}");
                                    nifcLayer.SetVisibility(true);
                                });
                                Uri uriMtbs = new Uri($@"{strFireGdbPath}\{strFileName}");
                                if (await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(strFireGdbPath), strFileName))
                                {
                                    success = await MapTools.DisplayUniqueValuesRasterFromLayerFileAsync(Constants.MAPS_FIRE_MAP_NAME, uriMtbs, strLayerName,
                                        strLayerFilePath, 25, true);
                                    // Put nifc layer on top; In order to move layerToMove, I need to know if the destination is a group layer and the zero based position it needs to move to.
                                    Tuple<GroupLayer, int> moveToLayerPosition = MapTools.FindLayerPosition(null, strLayerName, oMap);
                                    if (moveToLayerPosition.Item2 == -1)
                                    {
                                        Module1.Current.ModuleLogManager.LogError(nameof(RunFireMapsImplAsync), $"Layer {strLayerName} not found ");
                                    }
                                    await QueuedTask.Run(() =>
                                    {
                                        // subtract 1 from moveToLayerPosition.Item2 because we want it ABOVE the found layer
                                        if (moveToLayerPosition.Item1 != null) //layer gets moved into the group
                                            moveToLayerPosition.Item1.MoveLayer(nifcLayer, moveToLayerPosition.Item2 - 1);
                                        else //Layer gets moved into the root
                                            oMap.MoveLayer(nifcLayer, moveToLayerPosition.Item2 - 1);
                                    });
                                }
                                defaultMap.Title = $@"FIRE DISTURBANCE {oInterval.LowerBound}-{oInterval.UpperBound}";
                                success = await MapTools.UpdateMapElementsAsync(Module1.Current.Aoi.StationName, Constants.MAPS_FIRE_LAYOUT_NAME, defaultMap);
                                success = await GeneralTools.ExportMapToPdfAsync(Constants.PDF_EXPORT_RESOLUTION);
                                var mtbsLayer = oMap.GetLayersAsFlattenedList().OfType<RasterLayer>().Where(l => l.Name.Equals(strLayerName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                                if (mtbsLayer != null)
                                {
                                    await QueuedTask.Run(() =>
                                    {
                                        mtbsLayer.SetVisibility(false);
                                    });
                                }
                            }
                            else
                            {
                                GeneralTools.GenerateBlankPage($"FIRE DISTURBANCE {oInterval.LowerBound}-{oInterval.UpperBound}",
                                    GeneralTools.GetFullPdfFileName(Module1.Current.DisplayedMap));
                            }
                            lstExportedMaps.Add(GeneralTools.GetFullPdfFileName(Module1.Current.DisplayedMap));
                        }
                    }
                    if (lstExportedMaps.Count > 0)
                    {
                        success = GeneralTools.PublishFirePdfDocument(strFireReportPath, lstExportedMaps);
                    }
                    if (success == BA_ReturnCode.Success)
                    {
                        if (!ParentFolder.Equals(Module1.Current.Aoi.FilePath))
                        {
                            string strTargetFolder = $@"{ParentFolder}\{Constants.FOLDER_MAP_PACKAGE}\{Constants.FOLDER_FIRE_STATISTICS}";
                            if (!Directory.Exists(strTargetFolder))
                            {
                                Directory.CreateDirectory(strTargetFolder);
                            }
                            try
                            {
                                File.Copy(strFireReportPath, $@"{strTargetFolder}\{strExportPrefix}{Constants.FILE_REPORT_SUFFIX_PDF}", true);
                            }
                            catch (Exception)
                            {
                                MessageBox.Show("Unable to copy maps to selected folder. Please make sure all map documents are closed!");
                            }
                        }
                    }

                    Names[idxRow].AoiBatchStateText = AoiBatchState.Completed.ToString();  // update gui
                    strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Finished generate maps export for " +
                        Names[idxRow].Name + "\r\n";
                    File.AppendAllText(_strFireMapsLogFile, strLogEntry);       // append
                }
            }
            strLogEntry = DateTime.Now.ToString("MM/dd/yy H:mm:ss ") + "Completed fire maps generation" + "\r\n";
            File.AppendAllText(_strFireMapsLogFile, strLogEntry);       // append
            MessageBox.Show("Fire maps have been generated!");
        }
        private string GetMtbsMapName(int year)
        {
            return $@"MTBS_{year}";
        }
        private BA_ReturnCode InitAnnualCsv(int intYear, string strCsvFile)
        {
            StringBuilder output = new StringBuilder();
            string strYear = Convert.ToString(intYear);
            string strYearPrefix = $@"YY{strYear.Substring(strYear.Length - 2)}_";
            String[] headings = { "stationTriplet", "stationName", "report_end_year", $@"{strYearPrefix}newfireno", $@"{strYearPrefix}nifc_burnedArea_SqMiles",
                $@"{strYearPrefix}mtbs_burnedArea_SqMiles", $@"{strYearPrefix}nifc_burnedArea_pct", $@"{strYearPrefix}mtbs_burnedArea_pct", $@"{strYearPrefix}burnedForestedArea_SqMiles",
                $@"{strYearPrefix}burnedForestedArea_pct",$@"{strYearPrefix}lowburnedSeverityForestedArea__SqMiles",$@"{strYearPrefix}lowburnedSeverityForestedArea_pct",
                $@"{strYearPrefix}mediumburnedSeverityForestedArea_SqMiles",$@"{strYearPrefix}mediumburnedSeverityForestedArea_pct",
                $@"{strYearPrefix}highburnedSeverityForestedArea_SqMiles",$@"{strYearPrefix}highburnedSeverityForestedArea_pct",$@"{strYearPrefix}lowburnedSeverityArea_SqMiles", $@"{strYearPrefix}lowburnedSeverityArea_pct",
                $@"{strYearPrefix}mediumburnedSeverityArea_SqMiles", $@"{strYearPrefix}mediumburnedSeverityArea_pct",$@"{strYearPrefix}highburnedSeverityArea_SqMiles", $@"{strYearPrefix}highburnedSeverityArea_pct" };
            output.AppendLine(string.Join(_separator, headings));
            try
            {
                // Overwrites file with new content
                File.WriteAllText(strCsvFile, output.ToString());
                return BA_ReturnCode.Success;
            }
            catch (Exception ex)
            {
                string strLogEntry = $@"{DateTime.Now.ToString("MM/dd/yy H:mm:ss")} Headings could not be written to an annual CSV file!{System.Environment.NewLine}";
                File.AppendAllText(_strFireReportLogFile, strLogEntry);
                MessageBox.Show("Headings could not be written to an annual CSV file!", "BAGIS-PRO");
                return BA_ReturnCode.WriteError;
            }
        }
        private BA_ReturnCode InitIncrementCsv(int intIncrementPeriods, string strCsvFile)
        {
            StringBuilder output = new StringBuilder();
            IList<string> lstHeadings = new List<string>() { "stationTriplet", "stationName", "report_end_year", "mtbs_missing_years" };
            string fmt = "00";
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_newfireno";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_period";
                lstHeadings.Add(strYearsLabel);
            }

            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_nifc_burnedArea_SqMiles";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_mtbs_burnedArea_SqMiles";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_nifc_burnedArea_pct";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_mtbs_burnedArea_pct";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_burnedForestedArea_SqMiles";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_burnedForestedArea_pct";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_lowburnedSeverityForestedArea_SqMiles";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_lowburnedSeverityForestedArea_pct";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_mediumburnedSeverityForestedArea_SqMiles";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_mediumburnedSeverityForestedArea_pct";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_highburnedSeverityForestedArea_SqMiles";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_highburnedSeverityForestedArea_pct";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_lowburnedSeverityArea_SqMiles";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_lowburnedSeverityArea_pct";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_mediumburnedSeverityArea_SqMiles";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_mediumburnedSeverityArea_pct";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_highburnedSeverityArea_SqMiles";
                lstHeadings.Add(strYearsLabel);
            }
            for (int i = 1; i <= intIncrementPeriods; i++)
            {
                int yearsLabel = i * FireIncrementYears;
                string strYearsLabel = $@"Last{yearsLabel.ToString(fmt)}_highburnedSeverityArea_pct";
                lstHeadings.Add(strYearsLabel);
            }

            String[] incrementHeadings = lstHeadings.ToArray();
            output.AppendLine(string.Join(_separator, incrementHeadings));
            try
            {
                // Overwrites file with new content
                File.WriteAllText(strCsvFile, output.ToString());
                return BA_ReturnCode.Success;
            }
            catch (Exception ex)
            {
                string strLogEntry = $@"{DateTime.Now.ToString("MM/dd/yy H:mm:ss")} Headings could not be written to the increment CSV file!{System.Environment.NewLine}";
                File.AppendAllText(_strFireReportLogFile, strLogEntry);
                MessageBox.Show("Headings could not be written to the increment CSV file!", "BAGIS-PRO");
                return BA_ReturnCode.WriteError;
            }
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
            string mapAwdbId = $@"awdb_id ""awdb_id"" true true false 30 Text 0 0,First,#,";
            string mapStationTriplet = $@"stationTriplet ""stationTriplet"" true true false 255 Text 0 0,First,#,";
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
                        sbAwdbId.Append($@"{lstMergeFeatures[i]},awdb_id,0,30,");
                        StringBuilder sbStationTriplet = new StringBuilder(mapStationTriplet);
                        sbStationTriplet.Append($@"{lstMergeFeatures[i]},stationTriplet,0,255,");
                        StringBuilder sbStationName = new StringBuilder(mapStationName);
                        sbStationName.Append($@"{lstMergeFeatures[i]},stationName,0,255,");
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

        private async Task<int> QueryMtbsMaxYearAsync(IDictionary<string, dynamic> dictDatasources, string strDataType)
        {
            Webservices ws = new Webservices();
            IList<string> lstMtbsServiceNames = await ws.QueryMtbsImageServiceNamesAsync(dictDatasources, strDataType);
            int testYear = -1;
            if (lstMtbsServiceNames.Count > 0)
            {
                // usgs_mtbs_conus/mtbs_CONUS_1990
                string sName = lstMtbsServiceNames.Last();
                string[] arrPieces = sName.Split(new char[] { '_' });
                int lastPos = arrPieces.Length - 1;
                testYear = Convert.ToInt16(arrPieces[lastPos]);
            }
            return testYear;
        }
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class DockAdminTools_ShowButton : ArcGIS.Desktop.Framework.Contracts.Button
    {
        protected override void OnClick()
        {
            DockAdminToolsViewModel.Show();
        }
    }
}
