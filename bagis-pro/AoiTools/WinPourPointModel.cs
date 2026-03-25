using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Data.UtilityNetwork.Trace;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
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
using System.Windows.Input;

namespace bagis_pro.AoiTools
{
    internal class WinPourPointModel : ViewModelBase
    {
        WinPourPoint _view = null;
        private ObservableCollection<string> _pourPoints = new ObservableCollection<string>();
        string _selectedPourPoint = "";
        bool _btnSelectEnabled = false;
        bool _hasTempPourPoint = false;
        public WinPourPointModel(WinPourPoint view)
        {
            _view = view;
            _view.Title = "Pour Points";
        }

        public async Task InitializeAsync()
        {
            _hasTempPourPoint = false;
            BtnSelectEnabled = false;

            // Load flow accumulation raster as a reference
            string[] arrFlow = { Constants.FILE_FLOW_ACCUMULATION };
            await MapTools.RemoveLayersfromMapFrame(Constants.MAPS_DEFAULT_MAP_NAME, arrFlow);
            string surfacesGdbPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.BasinFolderBase, GeodatabaseNames.Surfaces);
            await MapTools.DisplayRasterStretchSymbolAsync(Constants.MAPS_DEFAULT_MAP_NAME, new Uri($@"{surfacesGdbPath}\{Constants.FILE_FLOW_ACCUMULATION}"),
                Constants.FILE_FLOW_ACCUMULATION,
                "ArcGIS Colors", "Black to White", 0);

            // Load gauge station layer if it doesn't already exist
            bool bExists = false;
            await QueuedTask.Run(() =>
            {
                //Finding the first project item with name matches with mapName
                Map map = null;
                MapProjectItem mpi =
                    Project.Current.GetItems<MapProjectItem>()
                    .FirstOrDefault(m => m.Name.Equals(Constants.MAPS_DEFAULT_MAP_NAME, StringComparison.CurrentCultureIgnoreCase));
                if (mpi != null)
                {
                    map = mpi.GetMap();
                    Layer oLayer =
                        map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(Constants.MAPS_GAUGE_STATIONS, StringComparison.CurrentCultureIgnoreCase));
                    if (oLayer != null)
                    {
                        bExists = true;
                    }
                }
            });

            if (!bExists)
            {
                string strGaugeStationsUri = (string)Module1.Current.BagisSettings.GaugeStationUri;
                BA_ReturnCode success = await MapTools.DisplayGaugeStationsLayerAsync(strGaugeStationsUri);
            }

            bool bPointLayer = false;
            FeatureLayer oFeatureLayer = null;
            await QueuedTask.Run(() =>
            {
                //Finding the first project item with name matches with mapName
                Map map = null;
                MapProjectItem mpi =
                    Project.Current.GetItems<MapProjectItem>()
                    .FirstOrDefault(m => m.Name.Equals(Constants.MAPS_DEFAULT_MAP_NAME, StringComparison.CurrentCultureIgnoreCase));
                if (mpi != null)
                {
                    map = mpi.GetMap();
                    Layer oLayer =
                        map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(Constants.MAPS_GAUGE_STATIONS, StringComparison.CurrentCultureIgnoreCase));
                    if (oLayer != null)
                    {
                        oFeatureLayer = (FeatureLayer)oLayer;
                        if (oFeatureLayer.ShapeType == ArcGIS.Core.CIM.esriGeometryType.esriGeometryPoint)
                        {
                            bPointLayer = true;
                        }
                    }
                }
            });
            if (!bPointLayer)
            {
                MessageBox.Show("Gauge station layer specified in the settings is invalid!", "BAGIS-Pro");
                return;
            }

            SpatialQueryFilter spatialQueryFilter = null;
            Uri polyFeatureGdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.BasinFolderBase, GeodatabaseNames.Aoi));
            Geometry polyGeometry = null;
            await QueuedTask.Run(() =>
            {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(polyFeatureGdbUri)))
                {
                    using (Table table = geodatabase.OpenDataset<Table>(Constants.FILE_AOI_VECTOR))
                    {
                        QueryFilter queryFilter = new QueryFilter();
                        double maxArea = -1;    // We will report the points in the largest polygon if > 1
                        using (RowCursor cursor = table.Search(queryFilter, false))
                        {
                            while (cursor.MoveNext())
                            {
                                using (Feature feature = (Feature)cursor.Current)
                                {
                                    Geometry areaGeo = feature.GetShape();
                                    var area = GeometryEngine.Instance.Area(areaGeo);
                                    if (area > maxArea)
                                    {
                                        maxArea = area;
                                        polyGeometry = feature.GetShape();
                                    }
                                }
                            }
                        }
                    }
                }
                spatialQueryFilter = new SpatialQueryFilter
                {
                    FilterGeometry = polyGeometry,
                    SpatialRelationship = SpatialRelationship.Contains
                };
            });

            ObservableCollection<string> tmpList = new ObservableCollection<string>();
            await QueuedTask.Run(() =>
            {
                using (RowCursor cursor = oFeatureLayer.Search(spatialQueryFilter))
                {
                    while (cursor.MoveNext())
                    {
                        if (cursor.Current is Feature feature)
                        {
                            int idx = feature.FindField(Constants.FIELD_NAME);
                            if (idx > -1)
                            {
                                tmpList.Add(Convert.ToString(feature[idx]));
                            }
                        }
                    }
                }
            });

            PourPoints = tmpList;
            if (PourPoints.Count != 0)
            {
                BtnSelectEnabled = true;
            }
            else
            {
                BtnSelectEnabled = false;
            }
        }
        public ObservableCollection<string> PourPoints
        {
            get => _pourPoints;
            set => SetProperty(ref _pourPoints, value); // Utilizes ViewModelBase.SetProperty
        }
        public string SelectedPourPoint
        {
            get => _selectedPourPoint;
            set
            {
                if (_selectedPourPoint != value)
                {
                    _selectedPourPoint = value;
                }
            }
        }
        public bool BtnSelectEnabled
        {
            get => _btnSelectEnabled;
            set => SetProperty(ref _btnSelectEnabled, value);
        }
        public ICommand CmdCancel
        {
            get
            {
                return new RelayCommand( () => {
                    _view.Close();
                });
            }
        }
        private RelayCommand _selectCommand;
        public ICommand CmdSelect
        {
            get
            {
                if (_selectCommand == null)
                    _selectCommand = new RelayCommand(SelectImplAsync, () => true);
                return _selectCommand;
            }
        }

        private async void SelectImplAsync(object param)
        {
            if (String.IsNullOrEmpty(SelectedPourPoint))
            {
                MessageBox.Show("Please add a new pourpoint!", "BAGIS-Pro");
                return;
            }
            // Query the stationTriplet for the selected pourpoint
            //Finding the first project item with name matches with mapName
            string stationTriplet = null;
            await QueuedTask.Run(() =>
            {
                FeatureLayer oFeatureLayer = null;
                Map map = null;
                MapProjectItem mpi =
                    Project.Current.GetItems<MapProjectItem>()
                    .FirstOrDefault(m => m.Name.Equals(Constants.MAPS_DEFAULT_MAP_NAME, StringComparison.CurrentCultureIgnoreCase));
                if (mpi != null)
                {
                    map = mpi.GetMap();
                    Layer oLayer =
                     map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(Constants.MAPS_GAUGE_STATIONS, StringComparison.CurrentCultureIgnoreCase));
                    if (oLayer != null)
                    {
                        oFeatureLayer = (FeatureLayer)oLayer;
                    }
                }
                if (oFeatureLayer != null)
                {
                    QueryFilter filter = new QueryFilter();
                    filter.WhereClause = $@"{Constants.FIELD_NAME} = '{SelectedPourPoint.Trim()}'";
                    using (RowCursor cursor = oFeatureLayer.Search(filter))
                    {
                        while (cursor.MoveNext())
                        {
                            if (cursor.Current is Feature feature)
                            {
                                int idx = feature.FindField(Constants.FIELD_STATION_TRIPLET);
                                if (idx > -1)
                                {
                                    
                                    stationTriplet = Convert.ToString(feature[idx]);
                                }
                            }
                        }
                    }
                }
            });
            string str1 = SelectedPourPoint.Replace(' ', '_');
            string str2 = str1.Replace(",", "-");
            string defaultInput = str2.Replace("'", "-");
            defaultInput = $@"{defaultInput}_{DateTime.Now.ToString("MMddyyyy")}";
            var inputDialog = new InputWindow("AOI Name", "Please enter the name of the AOI:", defaultInput);
            // ShowDialog() blocks the current thread until the window is closed
            string aoiName = "";
            if (inputDialog.ShowDialog() == true)
            {
                aoiName = inputDialog.UserInput;
            }
            else
            {
                return;
            }
            if (aoiName.IndexOf(' ') > -1)
            {
                MessageBox.Show("Space not allowed in the AOI name!", "BAGIS-Pro");
            }
            _view.Close();
            string tempAOIFolderName = $@"{Module1.Current.BasinFolderBase}\{aoiName}";
            if (Directory.Exists(tempAOIFolderName))
            {
                MessageBoxResult res = MessageBox.Show($@"{aoiName} folder already exists. Overwrite?", "BAGIS-Pro", MessageBoxButton.YesNo);
                if (res == MessageBoxResult.Yes)
                {
                    int layersRemoved = await MapTools.RemoveLayersInFolderAsync(tempAOIFolderName);
                    BA_ReturnCode success = await GeoprocessingTools.DeleteDatasetAsync(tempAOIFolderName);
                    if (success != BA_ReturnCode.Success || Directory.Exists(tempAOIFolderName))
                    {
                        MessageBox.Show("Unable to remove the folder. Program stopped.", "BAGIS-Pro");
                        return;
                    }
                }
            }
            try
            {
                DirectoryInfo di = Directory.CreateDirectory(tempAOIFolderName);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to create new AOI folder. Program stopped", "BAGIS-Pro");
                return;
            }

            // reset aoi tools when a new aoi is selected
            GeneralTools.ResetAoiFlags();
            GeneralTools.ResetAoi();

            // SelectAOI_Flag = True 'still allow user to select a different AOI
            Module1.ActivateState("bagis_pro_Buttons_BtnDefineAoi_State");
            Module1.Current.CboCurrentAoi.SetAoiName(aoiName);
            Module1.ActivateState("bagis_pro_Buttons_SetPourpointTool_State");
            Module1.ActivateState("bagis_pro_Buttons_BtnCreateAoi_State");

            MessageBox.Show($@"Please select a pourpoint location and then create the AOI! ID: {stationTriplet}", "BAGIS-Pro");
        }


      private RelayCommand _viewBasinCommand;
        public ICommand CmdViewBasin
        {
            get
            {
                if (_viewBasinCommand == null)
                    _viewBasinCommand = new RelayCommand(ViewBasinImplAsync, () => true);
                return _viewBasinCommand;
            }
        }
        private async void ViewBasinImplAsync(object param)
        {
            //zoom to basin boundary
            string strPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.BasinFolderBase, GeodatabaseNames.Aoi);
            Uri aoiUri = new Uri(strPath);
            await QueuedTask.Run(() =>
            {
                using (Geodatabase geodatabase =
                        new Geodatabase(new FileGeodatabaseConnectionPath(aoiUri)))
                {
                    // Use the geodatabase.
                    using (FeatureClassDefinition fcDefinition = geodatabase.GetDefinition<FeatureClassDefinition>(Constants.FILE_AOI_VECTOR))
                    {
                        var extent = fcDefinition.GetExtent();
                        //expandedExtent = extent.Expand(-0.5, -0.5, true);
                        MapView.Active.ZoomToAsync(extent);
                    }
                }
            });
        }

    }
}
