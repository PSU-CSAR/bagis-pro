using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using bagis_pro.BA_Objects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;

namespace bagis_pro.AoiTools
{
    internal class WinDefineAoiModel : ViewModelBase
    {
        WinDefineAoi _view = null;
        string _basinName = "";
        private ObservableCollection<string> _aoiFolders = new ObservableCollection<string>();
        string _selectedAoi = "";
        bool _btnBoundaryEnabled = false;
        bool _btnSelectEnabled = false;
        bool _btnDeleteEnabled = false;
        public WinDefineAoiModel(WinDefineAoi view)
        {
            _view = view;
            _view.Title = "Define AOI";
            BasinName = Convert.ToString(Module1.Current.CboCurrentBasin.SelectedItem);
        }

        public async Task InitializeAsync()
        {            
            ObservableCollection<string> tmpList = new ObservableCollection<string>();
            IList<Aoi> lstAois = await GeneralTools.GetAoiFoldersAsync(Module1.Current.BasinFolderBase, "");
            for (int i = 0; i < lstAois.Count; i++)
            {
                Aoi oAoi = lstAois[i];
                tmpList.Add(Path.GetFileName(oAoi.FilePath));
            }
            AoiFolders = tmpList;
            if (AoiFolders.Count != 0)
            {
                BtnBoundaryEnabled = true;
                BtnSelectEnabled = true;
                BtnDeleteEnabled = true;
            }
            else
            {
                BtnBoundaryEnabled = false;
                BtnSelectEnabled = false;
                BtnDeleteEnabled = false;
            }
        }
        public string BasinName
        {
            get => _basinName;
            set => SetProperty(ref _basinName, value);
        }
        public ObservableCollection<string> AoiFolders
        {
            get => _aoiFolders;
            set => SetProperty(ref _aoiFolders, value); // Utilizes ViewModelBase.SetProperty
        }
        public string SelectedAoi
        {
            get => _selectedAoi;
            set
            {
                if (_selectedAoi != value)
                {
                    _selectedAoi = value;
                }
            }
        }
        public bool BtnBoundaryEnabled
        {
            get => _btnBoundaryEnabled;
            set => SetProperty(ref _btnBoundaryEnabled, value);
        }
        public bool BtnSelectEnabled
        {
            get => _btnSelectEnabled;
            set => SetProperty(ref _btnSelectEnabled, value);
        }
        public bool BtnDeleteEnabled
        {
            get => _btnDeleteEnabled;
            set => SetProperty(ref _btnDeleteEnabled, value);
        }
        public ICommand CmdClose
        {
            get
            {
                return new RelayCommand( () => {
                    _view.Close();
                });
            }
        }

        private RelayCommand _selectAoiCommand;
        public ICommand CmdSelectAoi
        {
            get
            {
                if (_selectAoiCommand == null)
                    _selectAoiCommand = new RelayCommand(SelectAoiImplAsync, () => true);
                return _selectAoiCommand;
            }
        }
        private async void SelectAoiImplAsync(object param)
        {
            string aoiFolder = $@"{Module1.Current.BasinFolderBase}\{SelectedAoi}";
            Aoi oAoi = await GeneralTools.SetAoiAsync(aoiFolder, null);
            if (oAoi != null)
            {
                Module1.Current.CboCurrentAoi.SetAoiName(oAoi.Name);
                if (!oAoi.ValidForecastData)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("This AOI does not have the required forecast station information. ");
                    sb.Append("Use the Batch Tools menu to run the 'Forecast Station Data' tool to update ");
                    sb.Append("the forecast station data. Next use the Batch Tools menu to run the ");
                    sb.Append("'Generate AOI Reports' tool to create the analysis layers required by BAGIS-Pro.");
                    System.Windows.MessageBox.Show(sb.ToString(), "BAGIS PRO");
                }
                else
                {
                    string[] arrButtonNames = { "bagis_pro_Menus_MnuMaps_BtnMapLoad", "bagis_pro_Buttons_BtnExcelTables",
                                "bagis_pro_WinExportPdf"};
                    int intButtonsDisabled = 0;
                    for (int i = 0; i < arrButtonNames.Length; i++)
                    {
                        var plugin = FrameworkApplication.GetPlugInWrapper(arrButtonNames[i]);
                        if (plugin == null)
                        {
                            intButtonsDisabled++;
                        }
                        else if (!plugin.Enabled)
                        {
                            intButtonsDisabled++;
                        }
                    }
                    if (intButtonsDisabled > 0)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.Append("This AOI is missing at least one required layer. Use the Batch Tools menu to run the ");
                        sb.Append("'Generate AOI Reports' tool to create the analysis layers required by BAGIS-Pro.");
                        System.Windows.MessageBox.Show(sb.ToString(), "BAGIS PRO");
                    }
                }
                _view.Close();
            }
        }

        private RelayCommand _displayBoundaryCommand;
        public ICommand CmdBoundary
        {
            get
            {
                if (_displayBoundaryCommand == null)
                    _displayBoundaryCommand = new RelayCommand(DisplayBoundaryImplAsync, () => true);
                return _displayBoundaryCommand;
            }
        }
        private async void DisplayBoundaryImplAsync(object param)
        {
            //add aoi boundary to map
            string aoiFolder = $@"{Module1.Current.BasinFolderBase}\{SelectedAoi}";
            string strPath = GeodatabaseTools.GetGeodatabasePath(aoiFolder, GeodatabaseNames.Aoi, true) +
                             Constants.FILE_AOI_VECTOR;
            Uri aoiUri = new Uri(strPath);
            BA_ReturnCode success = await MapTools.AddAoiBoundaryToMapAsync(aoiUri, ColorFactory.Instance.RedRGB, Constants.MAPS_DEFAULT_MAP_NAME, $@"{SelectedAoi}");
        }

        private RelayCommand _deleteAoiCommand;
        public ICommand CmdDeleteAoi
        {
            get
            {
                if (_deleteAoiCommand == null)
                    _deleteAoiCommand = new RelayCommand(DeleteAoiImplAsync, () => true);
                return _deleteAoiCommand;
            }
        }
        private async void DeleteAoiImplAsync(object param)
        {
            string aoiFolder = $@"{Module1.Current.BasinFolderBase}\{SelectedAoi}";
            DialogResult res = System.Windows.Forms.MessageBox.Show($@"Delete {SelectedAoi} ?", "BAGIS-Pro",
                MessageBoxButtons.YesNo);
            switch (res)
            {
                case DialogResult.Cancel:
                    return;
                case DialogResult.Yes:
                    // Continue to delete aoi
                    break;
                case DialogResult.No:
                    return;
            }

            // If the selected aoi is contained by the aoi to be deleted, reset aoi
            if (Module1.Current.Aoi != null && !String.IsNullOrEmpty(Module1.Current.Aoi.FilePath))
            {
                // Doing this because Aoi.name is the stationName, Not necessarily same as folder name
                string aoiFolderName = Path.GetFileName(Module1.Current.Aoi.FilePath);
                if (aoiFolderName.Equals(SelectedAoi))
                {
                    GeneralTools.ResetAoi();
                }
            }

            AoiFolders.Clear();
            int layerCount = await MapTools.RemoveLayersInFolderAsync(aoiFolder);
            BA_ReturnCode success = await GeoprocessingTools.ClearWorkspaceCacheAsync(aoiFolder);
            if (success == BA_ReturnCode.Success)
            {
                success = await GeoprocessingTools.DeleteDatasetAsync(aoiFolder);
            }            
            if (success != BA_ReturnCode.Success || Directory.Exists(aoiFolder))
            {
                System.Windows.MessageBox.Show("Cannot delete the AOI! It is probably caused by a file lock in the Geodatabase. Please restart ArcGIS and repeat the process.");
            }
            else 
            {
                await this.InitializeAsync();
            }                
        }

    }
}
