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
                    FolderType fType = await GeodatabaseTools.GetAoiFolderTypeAsync(AoiFolder);
                    if (fType != FolderType.AOI)
                    {
                        MessageBox.Show("This folder is not an AOI!");
                        return;
                    }

                    string strPublisher = (string)Module1.Current.BatchToolSettings.Publisher;
                    if (!Publisher.Trim().Equals(strPublisher))
                    {
                        Module1.Current.BatchToolSettings.Publisher = Publisher;
                        String json = JsonConvert.SerializeObject(Module1.Current.BatchToolSettings, Formatting.Indented);
                        File.WriteAllText(SettingsFile, json);
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
