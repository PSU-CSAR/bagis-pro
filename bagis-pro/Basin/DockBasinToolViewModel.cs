﻿using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.UtilityNetwork.Trace;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Core.Utilities;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using bagis_pro.BA_Objects;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Button = ArcGIS.Desktop.Framework.Contracts.Button;

namespace bagis_pro.Basin
{
    internal class DockBasinToolViewModel : DockPane
    {
        private const string _dockPaneID = "bagis_pro_Basin_DockBasinTool";

        protected DockBasinToolViewModel() 
        {
            Subfolders = new ObservableCollection<FolderEntry>();
            Subfolders.CollectionChanged += ContentCollectionChanged;


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

        private string _parentFolder;
        private string _basinStatus;
        private string _aoiStatus;
        private bool _cmdViewDemEnabled;
        private bool _cmdViewLayersEnabled;
        private bool _cmdSelectBasinEnabled;
        private WinViewDemLayers _winViewDemLayers = null;

        public string ParentFolder
        {
            get => _parentFolder;
            set => SetProperty(ref _parentFolder, value);
        }
        public string BasinStatus
        {
            get { return _basinStatus; }
            set
            {
                SetProperty(ref _basinStatus, value, () => BasinStatus);
            }
        }
        public string AoiStatus
        {
            get { return _aoiStatus; }
            set
            {
                SetProperty(ref _aoiStatus, value, () => AoiStatus);
            }
        }
        public bool CmdViewDemEnabled
        {
            get { return _cmdViewDemEnabled; }
            set
            {
                SetProperty(ref _cmdViewDemEnabled, value, () => CmdViewDemEnabled);
            }
        }

        public bool CmdViewLayersEnabled
        {
            get { return _cmdViewLayersEnabled; }
            set
            {
                SetProperty(ref _cmdViewLayersEnabled, value, () => CmdViewLayersEnabled);
            }
        }
        public bool CmdSelectBasinEnabled
        {
            get { return _cmdSelectBasinEnabled; }
            set
            {
                SetProperty(ref _cmdSelectBasinEnabled, value, () => CmdSelectBasinEnabled);
            }
        }

        public ObservableCollection<FolderEntry> Subfolders { get; set; }

        // Assigns the propertyChanged event handler to each AOI item
        public void ContentCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (FolderEntry item in e.OldItems)
                {
                    //Removed items
                    item.PropertyChanged -= FolderEntryPropertyChanged;
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (FolderEntry item in e.NewItems)
                {
                    //Added items
                    item.PropertyChanged += FolderEntryPropertyChanged;
                }
            }
        }
        public void FolderEntryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            //This will get called when the property of an object inside the collection changes
            bool bOneSelected = false;
            for (int idxRow = 0; idxRow < Subfolders.Count; idxRow++)
            {
                if (Subfolders[idxRow].BasinStatus.Length > 1)
                {
                    bOneSelected = true;
                    break;
                }
            }
        }
        public System.Windows.Input.ICommand CmdFolder
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    //ParentFolder = "C:\\Docs\\AOIs\\testbasin";
                    //IsBasin = "No";
                    //IsAoi = "No";
                    //FolderEntry oEntry = new FolderEntry(".. <Parent Folder>",
                    //    "", "", "", "");
                    //Subfolders.Add(oEntry);
                    //FolderEntry oEntry1 = new FolderEntry("13309220_ID_USGS_Mf_Salmon_R_at_Mf_Lodge",
                    //    "22.15 meters resolution", "13309220:ID:USGS", "Mf Salmon R at Mf Lodge","17");
                    //Subfolders.Add(oEntry1);
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

                    Subfolders.Clear();
                    // Display the first entry
                    string strParent = ".. <Parent Folder>";
                    FolderEntry parentEntry = new FolderEntry();
                    parentEntry.Name = strParent;
                    Subfolders.Add(parentEntry);
                    // @ToDo: Not sure when we use the following
                    string strRoot = "..<Root Folder - No Parent folder>";
                    if (!String.IsNullOrEmpty(ParentFolder))
                    {
                        string[] arrSubDirectories = Directory.GetDirectories(ParentFolder);
                        for (int i = 0; i < arrSubDirectories.Length; i++)
                        {
                            FolderEntry nextEntry = new FolderEntry();
                            nextEntry.Name = Path.GetFileName(arrSubDirectories[i]);
                            FolderType fType = await GeodatabaseTools.GetAoiFolderTypeAsync(arrSubDirectories[i]);
                            Uri uriSurfaces = new Uri(GeodatabaseTools.GetGeodatabasePath(arrSubDirectories[i], GeodatabaseNames.Surfaces));
                            string strDemPath = $@"{uriSurfaces.LocalPath}\{Constants.FILE_DEM_FILLED}";
                            if (fType != FolderType.AOI)
                            {
                                if (await GeodatabaseTools.RasterDatasetExistsAsync(uriSurfaces, Constants.FILE_DEM_FILLED))
                                {
                                    // This subfolder contains a dem
                                    double cellSize = await GeodatabaseTools.GetCellSizeAsync(new Uri(strDemPath), WorkspaceType.Geodatabase);
                                    nextEntry.BasinStatus = Convert.ToString(Math.Round(cellSize, 2)) + " meters resolution";
                                }
                                else
                                {
                                    nextEntry.BasinStatus = "No";
                                }
                                nextEntry.PourpointId = "N/A";
                                nextEntry.PourpointName = "N/A";
                                nextEntry.Huc2 = "N/A";
                            }
                            else
                            {
                                double cellSize = await GeodatabaseTools.GetCellSizeAsync(new Uri(strDemPath), WorkspaceType.Geodatabase);
                                if (cellSize > 0)
                                {
                                    nextEntry.BasinStatus = Convert.ToString(Math.Round(cellSize, 2)) + " meters resolution";
                                    nextEntry.PourpointId = "Missing";
                                    nextEntry.PourpointName = "Missing";
                                    nextEntry.Huc2 = "Missing";
                                    string[] arrValues = await AnalysisTools.QueryLocalStationValues(arrSubDirectories[i]);
                                    if (arrValues.Length == 3)
                                    {
                                        if (!string.IsNullOrEmpty(arrValues[0]))
                                        {
                                            nextEntry.PourpointId = arrValues[0];
                                        }
                                        if (!string.IsNullOrEmpty(arrValues[1]))
                                        {
                                            nextEntry.PourpointName = arrValues[1];
                                        }
                                        if (!string.IsNullOrEmpty(arrValues[2]))
                                        {
                                            nextEntry.Huc2 = arrValues[2];
                                        }
                                    }
                                }
                                else
                                {
                                    nextEntry.BasinStatus = "No";
                                    nextEntry.PourpointId = "N/A";
                                    nextEntry.PourpointName = "N/A";
                                    nextEntry.Huc2 = "N/A";
                                }
                            }
                            Subfolders.Add(nextEntry);
                        }
                        CheckSelectedFolderStatus(ParentFolder);
                    }
                });
            }
        }

        public System.Windows.Input.ICommand CmdViewDem
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    ArcGIS.Desktop.Layouts.Layout layout = await MapTools.GetDefaultLayoutAsync(Constants.MAPS_DEFAULT_LAYOUT_NAME);
                    Map oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
                    BA_ReturnCode success = await MapTools.SetDefaultMapFrameDimensionAsync(Constants.MAPS_DEFAULT_MAP_FRAME_NAME, layout, oMap,
                        0.5, 2.5, 8.0, 10.5);
                    Uri uriAoi = new Uri(GeodatabaseTools.GetGeodatabasePath(ParentFolder, GeodatabaseNames.Aoi));
                    if (await GeodatabaseTools.FeatureClassExistsAsync(uriAoi, Constants.FILE_AOI_VECTOR))
                    {
                        //remove layer if it's there already
                        await QueuedTask.Run(() =>
                        {
                            Layer oLayer =
                            oMap.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(Constants.MAPS_BASIN_BOUNDARY, StringComparison.CurrentCultureIgnoreCase));
                            if (oLayer != null)
                            {
                                oMap.RemoveLayer(oLayer);
                            }
                        });

                        //add aoi boundary to map
                        string strPath = GeodatabaseTools.GetGeodatabasePath(ParentFolder, GeodatabaseNames.Aoi, true) +
                                         Constants.FILE_AOI_VECTOR;
                        Uri aoiUri = new Uri(strPath);
                        success = await MapTools.AddAoiBoundaryToMapAsync(aoiUri, ColorFactory.Instance.RedRGB, Constants.MAPS_DEFAULT_MAP_NAME, Constants.MAPS_BASIN_BOUNDARY);
                    }
                });
            }
        }
        public System.Windows.Input.ICommand CmdViewLayers
        {
            get
            {
                return new RelayCommand( () =>
                {
                    //already open?
                    if (_winViewDemLayers != null)
                    {
                        _winViewDemLayers.FolderPath = ParentFolder;    
                        if (AoiStatus.Equals("No"))
                        {
                            _winViewDemLayers.ckPourpoint.IsEnabled = false;
                        }
                        else
                        {
                            _winViewDemLayers.ckPourpoint.IsEnabled = true;
                        }
                        return;
                    }
                        
                    _winViewDemLayers = new WinViewDemLayers();
                    _winViewDemLayers.Owner = FrameworkApplication.Current.MainWindow;  // Required for modeless dialog
                    _winViewDemLayers.Closed += (o, e) => { _winViewDemLayers = null; };
                    //_winexportpdf.Show();
                    //uncomment for modal
                    _winViewDemLayers.FolderPath = ParentFolder;
                    if (AoiStatus.Equals("No"))
                    {
                        _winViewDemLayers.ckPourpoint.IsEnabled = false;
                    }
                    else
                    {
                        _winViewDemLayers.ckPourpoint.IsEnabled = true;
                    }
                    var result = _winViewDemLayers.ShowDialog();
                });
            }
        }
        public System.Windows.Input.ICommand CmdSelectBasin
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    //SetDefaultProjection also initializes the Layout and the MapFrame dimensions
                    //01-APR-2025: We decided not to set a default project for now so other projections could be supported
                    //Initialize the layout and map separately instead
                    //BA_ReturnCode success = await MapTools.SetDefaultProjection();
                    ArcGIS.Desktop.Layouts.Layout layout = await MapTools.GetDefaultLayoutAsync(Constants.MAPS_DEFAULT_LAYOUT_NAME);
                    Map oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
                    BA_ReturnCode success = await MapTools.SetDefaultMapFrameDimensionAsync(Constants.MAPS_DEFAULT_MAP_FRAME_NAME, layout, oMap,
                        0.5, 2.5, 8.0, 10.5);
                    IList<Aoi> lstAois = await GeneralTools.GetAoiFoldersAsync(ParentFolder, "");
                    bool bNeedToClipDem = false;
                    if (BasinStatus.ToUpper().IndexOf("RESOLUTION") > -1)   // FGDB exists
                    {
                        if (AoiStatus.ToUpper().Equals("YES") || lstAois.Count > 0)
                        {
                            MessageBox.Show("The selected folder is an AOI or contains AOIs. You cannot alter its DEM data.", "BAGIS-Pro");
                        }
                        else
                        {
                            DialogResult res = MessageBox.Show("Reuse existing DEM?", "BAGIS-Pro", MessageBoxButtons.YesNo);
                            switch (res)
                            {
                                case DialogResult.Cancel:
                                    return;
                                case DialogResult.Yes:
                                    // Do nothing. bNeedToClipDem is already false
                                    break;
                                case DialogResult.No:
                                    DialogResult confirm = 
                                        MessageBox.Show("Are you sure? \r\n WARNING!!! Existing DEM layers will be deleted if you continue. Click NO to cancel the action.", "BAGIS-Pro", MessageBoxButtons.YesNo);
                                    if (confirm == DialogResult.No)
                                    {
                                        return;
                                    }
                                    // Delete the Surfaces Geodatabase
                                    string surfacesGdbPath = GeodatabaseTools.GetGeodatabasePath(ParentFolder, GeodatabaseNames.Surfaces); ;
                                    string aoiGdbPath = GeodatabaseTools.GetGeodatabasePath(ParentFolder, GeodatabaseNames.Aoi);
                                    if (Directory.Exists(surfacesGdbPath))
                                    {
                                        int retVal = await MapTools.RemoveLayersInFolderAsync(surfacesGdbPath);
                                        success = await GeoprocessingTools.DeleteDatasetAsync(surfacesGdbPath);
                                    }
                                    // Delete the Aoi Geodatabase
                                    if (Directory.Exists(aoiGdbPath))
                                    {
                                        int retVal = await MapTools.RemoveLayersInFolderAsync(aoiGdbPath);
                                        success = await GeoprocessingTools.DeleteDatasetAsync(aoiGdbPath);
                                    }
                                    // verify the gdbs were removed
                                    if (Directory.Exists(surfacesGdbPath) || Directory.Exists(aoiGdbPath))
                                    {
                                        MessageBox.Show("Unable to clear BASIN's internal file geodatabase! Please restart ArcGIS Pro and try again.", "BAGIS-Pro", MessageBoxButtons.YesNo);
                                    }
                                    bNeedToClipDem = true;
                                    break;
                            }
                        }
                    }
                    // @ToDo: BAGIS V3 has code to set BasinFolderBase to ParentFolder
                    // Check for local settings file
                    success = GeneralTools.LoadBagisSettings();
                    if (success != BA_ReturnCode.Success)
                    {
                        MessageBox.Show("Unable to get critical system settings information. System stopped!", "BAGIS-Pro", MessageBoxButtons.YesNo);
                        return;
                    }
                    // reset aoi information
                    GeneralTools.ResetAoiFlags();
                    GeneralTools.ResetAoi();

                    // These are gp environment variables to be used for gp tools in this function; There is no way to set this at the session/project level
                    var env = Geoprocessing.MakeEnvironmentArray(workspace: ParentFolder);

                    if (bNeedToClipDem)
                    {
                        DemInfo demInfo = null;
                        if (Module1.Current.DemDimension == null || (Module1.Current.DemDimension.x_CellSize * Module1.Current.DemDimension.y_CellSize == 0))
                        {
                            string strSourceDem = (string)Module1.Current.BagisSettings.DemUri;
                            WorkspaceType wType = await GeneralTools.GetRasterWorkspaceType(strSourceDem);
                            if (wType == WorkspaceType.None)
                            {
                                System.Windows.MessageBox.Show("Invalid DEM. AOI cannot be created!", "BAGIS-Pro", System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Error);
                                return;
                            }
                            else if (wType == WorkspaceType.ImageServer || wType == WorkspaceType.Geodatabase || wType == WorkspaceType.Raster)
                            {
                                System.Windows.MessageBox.Show("The Basin DEM extent boundaries will be snapped to the DEM raster cells.\r\n The activation might take a few seconds.", "BAGIS-Pro");
                                demInfo = await GeneralTools.GetRasterDimensionsAsync(strSourceDem);
                            }
                        }
                    }

                });
            }
        }
        protected async void CheckSelectedFolderStatus(string strFolderPath)
        {
            FolderType fType = await GeodatabaseTools.GetAoiFolderTypeAsync(strFolderPath);
            Uri uriSurfaces = new Uri(GeodatabaseTools.GetGeodatabasePath(strFolderPath, GeodatabaseNames.Surfaces));
            string strFullDemPath = $@"{uriSurfaces.LocalPath}\{Constants.FILE_DEM_FILLED}";
            if (fType != FolderType.AOI)
            {
                //then check if dem_filled exists
                if (await GeodatabaseTools.RasterDatasetExistsAsync(uriSurfaces, Constants.FILE_DEM_FILLED))
                {
                    double cellSize = await GeodatabaseTools.GetCellSizeAsync(new Uri(strFullDemPath), WorkspaceType.Geodatabase);                    
                    BasinStatus = Convert.ToString(Math.Round(cellSize, 2)) + " meters resolution";
                    CmdViewLayersEnabled = true;
                }
                else
                {
                    BasinStatus = "No";
                    CmdViewLayersEnabled = false;
                }
                AoiStatus = "No";
            }
            else
            {
                double cellSize = -1;
                if (await GeodatabaseTools.RasterDatasetExistsAsync(uriSurfaces, Constants.FILE_DEM_FILLED))
                {
                    cellSize = await GeodatabaseTools.GetCellSizeAsync(new Uri(strFullDemPath), WorkspaceType.Geodatabase);
                }
                if (cellSize <= 0)
                {
                    BasinStatus = "No";
                    AoiStatus = "No";
                    CmdViewLayersEnabled = false;
                }
                else
                {
                    BasinStatus = Convert.ToString(Math.Round(cellSize, 2)) + " meters resolution";
                    AoiStatus = "Yes";
                    CmdViewLayersEnabled = true;
                }
            }
            //allow user to view the DEM extent when the selected folder is a BASIN or an AOI
            if (BasinStatus.Equals("No") && AoiStatus.Equals("No"))
            {
                CmdViewDemEnabled = false;
            }
            else
            {
                CmdViewDemEnabled = true;
            }
            CmdSelectBasinEnabled = true;
        }
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class DockBasinTool_ShowButton : Button
    {
        protected override void OnClick()
        {
            DockBasinToolViewModel.Show();
        }
    }

    internal class FolderEntry : INotifyPropertyChanged
    {
        string _name;
        string _basinStatus;
        string _pourpointId;
        string _pourpointName;
        string _huc2;

        internal FolderEntry(string strName, string strBasinStatus, string pourpointId, string pourpointName, string strHuc2)
        {
            _name = strName;
            _basinStatus = strBasinStatus;
            _pourpointId = pourpointId;
            _pourpointName = pourpointName;
            _huc2 = strHuc2;
        }
        internal FolderEntry()
        {

        }
        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
            }
        }
        public string BasinStatus
        {
            get { return _basinStatus; }
            set
            {
                _basinStatus = value;
            }
        }
        public string PourpointId
        {
            get { return _pourpointId; }
            set
            {
                _pourpointId = value;
            }
        }
        public string PourpointName
        {
            get { return _pourpointName; }
            set
            {
                _pourpointName = value;
            }
        }
        public string Huc2
        {
            get { return _huc2; }
            set
            {
                _huc2 = value;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property.
        // The CallerMemberName attribute that is applied to the optional propertyName
        // parameter causes the property name of the caller to be substituted as an argument.
        private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
