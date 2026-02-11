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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace bagis_pro.AoiTools
{
    internal class WinDefineAoiModel : ViewModelBase
    {
        WinDefineAoi _view = null;
        string _basinName = "";
        public WinDefineAoiModel(WinDefineAoi view)
        {
            _view = view;
            _view.Title = "Define AOI";
            BasinName = Convert.ToString(Module1.Current.CboCurrentBasin.SelectedItem);
        }
        public string BasinName
        {
            get => _basinName;
            set => SetProperty(ref _basinName, value);
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

        private RelayCommand _addLayersCommand;
        public ICommand CmdAddLayers
        {
            get
            {
                if (_addLayersCommand == null)
                    _addLayersCommand = new RelayCommand(AddLayersImplAsync, () => true);
                return _addLayersCommand;
            }
        }
        private async void AddLayersImplAsync(object param)
        {
            IList lstBothLists = (IList)param;
            if (lstBothLists != null && lstBothLists.Count == 2)
            {
                BA_ReturnCode success = BA_ReturnCode.UnknownError;
                IList lstRasters = (IList) lstBothLists[0];
                IList lstFeatureClasses = (IList)lstBothLists[1];
                string strGdbPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers);
                for (int i = 0; i < lstRasters.Count; i++)
                {
                    Uri uri = new Uri($@"{strGdbPath}\{Convert.ToString(lstRasters[i])}");
                    success = await MapTools.DisplayRasterLayerAsync(Constants.MAPS_DEFAULT_MAP_NAME, uri, Convert.ToString(lstRasters[i]),true);
                }
                Map oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
                for (int i = 0; i < lstFeatureClasses.Count; i++)
                {
                    Uri uri = new Uri($@"{strGdbPath}\{Convert.ToString(lstFeatureClasses[i])}");
                    await QueuedTask.Run(() =>
                    {
                        //Define some of the Feature Layer's parameters
                        var flyrCreatnParam = new FeatureLayerCreationParams(uri)
                        {
                            Name = Convert.ToString(lstFeatureClasses[i]),
                            IsVisible = true,
                        };
                        FeatureLayer fLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flyrCreatnParam, oMap);
                    });
                }
            }             
        }
    }
}
