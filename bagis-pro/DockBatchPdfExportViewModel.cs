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
                    success = await AnalysisTools.ClipSweLayersAsync(pBufferDistance, pBufferUnits,
                        strDefaultBufferDistance, strDefaultBufferUnits);




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
