using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System.Linq;
using ArcGIS.Core.CIM;

namespace bagis_pro
{
    public class WinViewDemLayersModel : PropertyChangedBase
    {
        WinViewDemLayers _view = null;
        bool _filledDemChecked = false;
        bool _flowDirChecked = false;
        bool _flowAccChecked = false;
        bool _slopeChecked = false;
        bool _aspectChecked = false;
        bool _hillshadeChecked = false;
        bool _pourpointChecked = false;
        public bool FilledDemChecked
        {
            get => _filledDemChecked;
            set => SetProperty(ref _filledDemChecked, value);
        }

        public bool FlowDirChecked
        {
            get => _flowDirChecked;
            set => SetProperty(ref _flowDirChecked, value);
        }
        public bool FlowAccChecked
        {
            get => _flowAccChecked;
            set => SetProperty(ref _flowAccChecked, value);
        }
        public bool SlopeChecked
        {
            get => _slopeChecked;
            set => SetProperty(ref _slopeChecked, value);
        }
        public bool AspectChecked
        {
            get => _aspectChecked;
            set => SetProperty(ref _aspectChecked, value);
        }
        public bool HillshadeChecked
        {
            get => _hillshadeChecked;
            set => SetProperty(ref _hillshadeChecked, value);
        }
        public bool PourpointChecked
        {
            get => _pourpointChecked;
            set => SetProperty(ref _pourpointChecked, value);
        }
        public WinViewDemLayersModel(WinViewDemLayers view)
        {
            _view = view;
        }
        protected void SetCheckedValues(bool checkAll)
        {
            FilledDemChecked = checkAll;
            FlowDirChecked = checkAll;
            FlowAccChecked = checkAll;
            SlopeChecked = checkAll;
            AspectChecked = checkAll;
            HillshadeChecked = checkAll;
            if (_view.ckPourpoint.IsEnabled)
            {
                PourpointChecked = checkAll;
            }            
        }

        public ICommand CmdAll => new RelayCommand(() =>
        {
            SetCheckedValues(true);
        });

        public ICommand CmdNone => new RelayCommand(() =>
        {
            SetCheckedValues(false);
        });
        public ICommand CmdCancel => new RelayCommand(() =>
        {
            _view.DialogResult = false;
            _view.Close();
        });

        public ICommand CmdDisplay => new RelayCommand( async () =>
        {
            string[] arrDemLayers = {Constants.FILE_DEM_FILLED, Constants.FILE_FLOW_DIRECTION, Constants.FILE_FLOW_ACCUMULATION,
                                    Constants.FILE_SLOPE, Constants.FILE_ASPECT, Constants.MAPS_HILLSHADE, Constants.MAPS_STREAM_GAGE};

            Layout layout = await MapTools.GetDefaultLayoutAsync(Constants.MAPS_DEFAULT_LAYOUT_NAME);
            Map oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
            BA_ReturnCode success = await MapTools.SetDefaultMapFrameDimensionAsync(Constants.MAPS_DEFAULT_MAP_FRAME_NAME, layout, oMap,
                0.5, 2.5, 8.0, 10.5);

            await QueuedTask.Run(() =>
            {
                foreach (string strName in arrDemLayers)
                {
                    Layer oLayer =
                        oMap.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(strName, StringComparison.CurrentCultureIgnoreCase));
                    if (oLayer != null)
                    {
                        oMap.RemoveLayer(oLayer);
                    }
                }
            });
            string surfacesGdb = GeodatabaseTools.GetGeodatabasePath(_view.FolderPath, GeodatabaseNames.Surfaces);
            string strPath = $@"{surfacesGdb}\{Constants.FILE_DEM_FILLED}";
            if (FilledDemChecked)
            {
                await MapTools.DisplayRasterStretchSymbolAsync(Constants.MAPS_DEFAULT_MAP_NAME, new Uri(strPath),Constants.FILE_DEM_FILLED,
                    "ArcGIS Colors", "Black to White", 0);
            }
            if (FlowAccChecked)
            {
                strPath = $@"{surfacesGdb}\{Constants.FILE_FLOW_ACCUMULATION}";
                await MapTools.DisplayRasterStretchSymbolAsync(Constants.MAPS_DEFAULT_MAP_NAME, new Uri(strPath), Constants.FILE_FLOW_ACCUMULATION,
                    "ArcGIS Colors", "Black to White", 0);
            }
            if (FlowDirChecked)
            {
                strPath = $@"{surfacesGdb}\{Constants.FILE_FLOW_DIRECTION}";
                await MapTools.DisplayRasterStretchSymbolAsync(Constants.MAPS_DEFAULT_MAP_NAME, new Uri(strPath), Constants.FILE_FLOW_DIRECTION,
                    "ArcGIS Colors", "Black to White", 0);
            }
            if (SlopeChecked)
            {
                strPath = $@"{surfacesGdb}\{Constants.FILE_SLOPE}";
                await MapTools.DisplayRasterStretchSymbolAsync(Constants.MAPS_DEFAULT_MAP_NAME, new Uri(strPath), Constants.FILE_SLOPE,
                    "ArcGIS Colors", "Slope", 0);
            }
            if (AspectChecked)
            {
                strPath = $@"{surfacesGdb}\{Constants.FILE_ASPECT}";
                await MapTools.DisplayRasterStretchSymbolAsync(Constants.MAPS_DEFAULT_MAP_NAME, new Uri(strPath), Constants.FILE_ASPECT,
                    "ArcGIS Colors", "Aspect", 0);
            }
            if (HillshadeChecked)
            {
                strPath = $@"{surfacesGdb}\{Constants.FILE_HILLSHADE}";
                await MapTools.DisplayRasterStretchSymbolAsync(Constants.MAPS_DEFAULT_MAP_NAME, new Uri(strPath), Constants.MAPS_HILLSHADE,
                    "ArcGIS Colors", "Black to White", 0);
            }
            if(PourpointChecked)
            {
                strPath = GeodatabaseTools.GetGeodatabasePath(_view.FolderPath, GeodatabaseNames.Aoi, true) +
                    Constants.FILE_POURPOINT;
                success = await MapTools.AddPointMarkersAsync(Constants.MAPS_DEFAULT_MAP_NAME, new Uri(strPath), Constants.MAPS_STREAM_GAGE, CIMColor.CreateRGBColor(255, 165, 0),
                    SimpleMarkerStyle.Circle, 8, "", MaplexPointPlacementMethod.NorthEastOfPoint);
            }

        });

    }
}
