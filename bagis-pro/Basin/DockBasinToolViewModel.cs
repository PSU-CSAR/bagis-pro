using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Utilities;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using bagis_pro.BA_Objects;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        private string _isBasin;
        private string _isAoi;
        private bool _cmdViewDemEnabled;
        private bool _cmdViewLayersEnabled;

        public string ParentFolder
        {
            get => _parentFolder;
            set => SetProperty(ref _parentFolder, value);
        }
        public string IsBasin
        {
            get { return _isBasin; }
            set
            {
                SetProperty(ref _isBasin, value, () => IsBasin);
            }
        }
        public string IsAoi
        {
            get { return _isAoi; }
            set
            {
                SetProperty(ref _isAoi, value, () => IsAoi);
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
                                    double cellSize = await GeodatabaseTools.GetCellSizeAsync(uriSurfaces, Constants.FILE_DEM_FILLED, WorkspaceType.Raster);
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
                                double cellSize = await GeodatabaseTools.GetCellSizeAsync(uriSurfaces, Constants.FILE_DEM_FILLED, WorkspaceType.Raster);
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
                    }


                    //IList<BA_Objects.Aoi> lstAois = await GeneralTools.GetAoiFoldersAsync(ParentFolder, _strLogFile);
                    //foreach (var pAoi in lstAois)
                    //{
                    //    string[] arrValues = await AnalysisTools.QueryLocalStationValues(pAoi.FilePath);
                    //    if (arrValues.Length == 3)
                    //    {
                    //        pAoi.StationTriplet = arrValues[0];
                    //        pAoi.StationName = arrValues[1];
                    //        pAoi.Huc2 = Convert.ToInt16(arrValues[2]);
                    //        if (!pAoi.ValidForecastData)
                    //        {
                    //            pAoi.AoiBatchStateText = AoiBatchState.NotReady.ToString();
                    //        }
                    //    }
                    //    Names.Add(pAoi);
                    //}
                    //if (Names.Count > 0)
                    //{
                    //    CmdRunEnabled = true;
                    //    CmdSnodasEnabled = true;
                    //    CmdForecastEnabled = true;
                    //    CmdToggleEnabled = true;
                    //    TasksEnabled = true;
                    //    CmdGenStatisticsEnabled = true;
                    //    CmdFireReportEnabled = true;
                    //}
                    //else
                    //{
                    //    MessageBox.Show("No valid AOIs were found in the selected folder!", "BAGIS-PRO");
                    //    Module1.Current.ModuleLogManager.LogDebug(nameof(CmdAoiFolder),
                    //        "No valid AOIs were found in the selected folder!");
                    //    CmdRunEnabled = false;
                    //    CmdSnodasEnabled = false;
                    //    CmdForecastEnabled = false;
                    //    CmdToggleEnabled = false;
                    //    TasksEnabled = false;
                    //    FireTasksEnabled = false;
                    //    CmdGenStatisticsEnabled = false;
                    //    CmdFireReportEnabled = false;
                    //}

                });
            }
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
