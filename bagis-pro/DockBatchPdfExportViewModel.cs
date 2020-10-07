using System;
using System.Collections.Generic;
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
        private bool _archiveChecked = false;
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

        public bool ArchiveChecked
        {
            get { return _archiveChecked; }
            set
            {
                SetProperty(ref _archiveChecked, value, () => ArchiveChecked);
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
                });
            }
        }

        public ICommand CmdRun
        {
            get
            {
                return new RelayCommand(() =>
                {
 
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
