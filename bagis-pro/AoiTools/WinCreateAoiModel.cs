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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace bagis_pro.AoiTools
{
    internal class WinCreateAoiModel : ViewModelBase
    {
        WinCreateAoi _view = null;
        bool _snapPPChecked;
        double _snapDistance;
        bool _aoiBufferChecked;
        double _aoiBufferDistance;
        double _prismBufferDistance;
        string _demElevUnit = "";
        private bool _prism_Checked = false;
        private string _prismBufferUnits = "Meters";
        private bool _reclipPrism_Checked = false;
        private bool _prismInchesChecked;
        private bool _prismMmChecked = false;
        private bool _SNOTEL_Checked = false;
        private string _snotelBufferDistance = "";
        private string _snotelBufferUnits = "";
        private bool _reclipSnotel_Checked = false;
        private bool _snowCos_Checked = false;
        private string _snowCosBufferDistance = "";
        private string _snowCosBufferUnits = "";
        private bool _reclipSnowCos_Checked = false;
        private ObservableCollection<string> _lstUnits = new ObservableCollection<string>();
        private ObservableCollection<string> _rasterLayers = new ObservableCollection<string>();
        private ObservableCollection<string> _vectorLayers = new ObservableCollection<string>();
        private ObservableCollection<string> _selectedLayers = new ObservableCollection<string>();
        public WinCreateAoiModel(WinCreateAoi view)
        {
            _view = view;
            SnapPPChecked = true;
            SnapDistance = 15;
            AoiBufferChecked = true;
            AoiBufferDistance = Convert.ToDouble((string)Module1.Current.BagisSettings.AoiBufferDistance);
            PrismBufferDistance = 1000;
            DemElevUnit = (string)Module1.Current.BagisSettings.DemUnits;
        }
        public bool SnapPPChecked
        {
            get => _snapPPChecked;
            set
            {
                if (_snapPPChecked != value)
                {
                    _snapPPChecked = value;
                }
            }
        }
        public double SnapDistance
        {
            get => _snapDistance;
            set
            {
                if (_snapDistance != value)
                {
                    _snapDistance = value;                
                }
            }
        }
        public bool AoiBufferChecked
        {
            get => _aoiBufferChecked;
            set => SetProperty(ref _aoiBufferChecked, value);
        }

        public double AoiBufferDistance
        {
            get => _aoiBufferDistance;
            set => SetProperty(ref _aoiBufferDistance, value);
        }
        public double PrismBufferDistance
        {
            get => _prismBufferDistance;
            set => SetProperty(ref _prismBufferDistance, value);
        }
        public string DemElevUnit
        {
            get => _demElevUnit;
            set => SetProperty(ref _demElevUnit, value);
        }
        public bool Prism_Checked
        {
            get { return _prism_Checked; }
            set
            {
                SetProperty(ref _prism_Checked, value, () => Prism_Checked);
            }
        }
        public string PrismBufferUnits
        {
            get { return _prismBufferUnits; }
            set
            {
                SetProperty(ref _prismBufferUnits, value, () => PrismBufferUnits);
            }
        }
        public bool ReclipPrism_Checked
        {
            get { return _reclipPrism_Checked; }
            set
            {
                SetProperty(ref _reclipPrism_Checked, value, () => ReclipPrism_Checked);
            }
        }
        public bool PrismInchesChecked
        {
            get { return _prismInchesChecked; }
            set
            {
                SetProperty(ref _prismInchesChecked, value, () => PrismInchesChecked);
            }
        }
        public bool PrismMmChecked
        {
            get { return _prismMmChecked; }
            set
            {
                SetProperty(ref _prismMmChecked, value, () => PrismMmChecked);
            }
        }

        public bool SNOTEL_Checked
        {
            get { return _SNOTEL_Checked; }
            set
            {
                SetProperty(ref _SNOTEL_Checked, value, () => SNOTEL_Checked);
            }
        }

        public string SnotelBufferDistance
        {
            get { return _snotelBufferDistance; }
            set
            {
                SetProperty(ref _snotelBufferDistance, value, () => SnotelBufferDistance);
            }
        }

        public string SnotelBufferUnits
        {
            get { return _snotelBufferUnits; }
            set
            {
                SetProperty(ref _snotelBufferUnits, value, () => SnotelBufferUnits);
            }
        }

        public bool ReclipSNOTEL_Checked
        {
            get { return _reclipSnotel_Checked; }
            set
            {
                SetProperty(ref _reclipSnotel_Checked, value, () => ReclipSNOTEL_Checked);
            }
        }

        public bool SnowCos_Checked
        {
            get { return _snowCos_Checked; }
            set
            {
                SetProperty(ref _snowCos_Checked, value, () => SnowCos_Checked);
            }
        }
        public string SnowCosBufferDistance
        {
            get { return _snowCosBufferDistance; }
            set
            {
                SetProperty(ref _snowCosBufferDistance, value, () => SnowCosBufferDistance);
            }
        }

        public string SnowCosBufferUnits
        {
            get { return _snowCosBufferUnits; }
            set
            {
                SetProperty(ref _snowCosBufferUnits, value, () => SnowCosBufferUnits);
            }
        }
        public bool ReclipSnowCos_Checked
        {
            get { return _reclipSnowCos_Checked; }
            set
            {
                SetProperty(ref _reclipSnowCos_Checked, value, () => ReclipSnowCos_Checked);
            }
        }
        public ObservableCollection<string> LstUnits
        {
            get
            {
                if (_lstUnits.Count == 0)
                {
                    _lstUnits.Add("Meters");
                    _lstUnits.Add("Kilometers");
                }
                return _lstUnits;
            }
            set { _lstUnits = value; NotifyPropertyChanged("LstUnits"); }
        }
        public ObservableCollection<string> RasterLayers
        {
            get => _rasterLayers;
            set => SetProperty(ref _rasterLayers, value); // Utilizes ViewModelBase.SetProperty
        }

        public ObservableCollection<string> VectorLayers
        {
            get => _vectorLayers;
            set => SetProperty(ref _vectorLayers, value); // Utilizes ViewModelBase.SetProperty
        }

        public ObservableCollection<string> SelectedLayers
        {
            get => _selectedLayers;
            set => SetProperty(ref _selectedLayers, value); // Utilizes ViewModelBase.SetProperty
        }
        private RelayCommand _setAoiCommand;

        public ICommand CmdClipLayers
        {
            get
            {
                return new RelayCommand(async () => {
                    // Create from template
                    await ClipLayersAsync(ReclipPrism_Checked, ReclipSNOTEL_Checked,
                        ReclipSnowCos_Checked);
                });
            }
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

        private async Task ClipLayersAsync(bool clipPrism, bool clipSnotel, bool clipSnowCos)
        {
            try
            {
                if (clipPrism == false && clipSnotel == false && clipSnowCos == false)
                {
                    MessageBox.Show("No layers selected to clip !!", "BAGIS-PRO");
                    return;
                }

                var cmdShowHistory = FrameworkApplication.GetPlugInWrapper("esri_geoprocessing_showToolHistory") as ICommand;
                if (cmdShowHistory != null)
                {
                    if (cmdShowHistory.CanExecute(null))
                    {
                        cmdShowHistory.Execute(null);
                    }
                }

                BA_ReturnCode success = BA_ReturnCode.Success;

                if (clipPrism)
                {
                    if (!await GeodatabaseTools.FeatureClassExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi)), Constants.FILE_AOI_PRISM_VECTOR))
                    {
                        string strInputFeatures = $@"{GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi)}\{Constants.FILE_AOI_VECTOR}";
                        string strOutputFeatures = $@"{GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi)}\{Constants.FILE_AOI_PRISM_VECTOR}";
                        string strDistance = $@"{PrismBufferDistance} {PrismBufferUnits}";
                        success = await GeoprocessingTools.BufferAsync(strInputFeatures, strOutputFeatures, strDistance, "ALL", CancelableProgressor.None);
                    }

                }

                if (clipSnotel || clipSnowCos)
                {
                    string snotelBufferDistance = "";
                    string snowCosBufferDistance = "";
                    double dblDistance = -1;
                    bool isDouble = Double.TryParse(SnotelBufferDistance, out dblDistance);
                    if (clipSnotel && isDouble && dblDistance > 0)
                    {
                        snotelBufferDistance = SnotelBufferDistance + " " + SnotelBufferUnits;
                    }
                    isDouble = Double.TryParse(SnowCosBufferDistance, out dblDistance);
                    if (clipSnowCos && isDouble && dblDistance > 0)
                    {
                        snowCosBufferDistance = SnowCosBufferDistance + " " + SnowCosBufferUnits;
                    }

                    success = await AnalysisTools.ClipSnoLayersAsync(Module1.Current.Aoi.FilePath, clipSnotel, snotelBufferDistance,
                        clipSnowCos, snowCosBufferDistance);
                    if (success == BA_ReturnCode.Success)
                    {
                        Uri uriLayers = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers));
                        if (clipSnotel)
                        {
                            ReclipSNOTEL_Checked = false;
                            SNOTEL_Checked = await GeodatabaseTools.FeatureClassExistsAsync(uriLayers, Constants.FILE_SNOTEL);
                        }
                        if (clipSnowCos)
                        {
                            ReclipSnowCos_Checked = false;
                            SnowCos_Checked = await GeodatabaseTools.FeatureClassExistsAsync(uriLayers, Constants.FILE_SNOW_COURSE);
                        }
                    }
                }

                if (success != BA_ReturnCode.Success)
                {
                    MessageBox.Show("An error occurred while trying to clip the layers !!", "BAGIS-PRO");
                }
            }
            catch (Exception ex)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync),
                    "Exception: " + ex.Message);
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
