using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using bagis_pro.BA_Objects;
using ExtensionMethod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace bagis_pro.AoiTools
{
    internal class WinAoiInfoModel : PropertyChangedBase
    {
        WinAoiInfo _view = null;
        double _minElevMeters;
        double _maxElevMeters;
        double _areaSqKm;
        double _areaAcre;
        double _areaSqMiles;
        double _aoiRefArea;
        string _aoiRefUnits = "N/A";
        private bool _prism_Checked = false;
        private string _prismBufferDistance = "";
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
        public WinAoiInfoModel(WinAoiInfo view)
        {
            _view = view;
        }

        public double MinElevMeters
        {
            get => _minElevMeters;
            set
            {
                if (_minElevMeters != value)
                {
                    _minElevMeters = value;
                    NotifyPropertyChanged(nameof(MinElevMeters));
                    NotifyPropertyChanged(nameof(ElevRangeMeters)); // Also notify the calculated property                    
                }
            }
        }
        public double MaxElevMeters
        {
            get => _maxElevMeters;
            set
            {
                if (_maxElevMeters != value)
                {
                    _maxElevMeters = value;
                    NotifyPropertyChanged(nameof(MaxElevMeters));
                    NotifyPropertyChanged(nameof(ElevRangeMeters)); // Also notify the calculated property                    
                }
            }
        }
        public double AreaSqKm
        {
            get => _areaSqKm;
            set => SetProperty(ref _areaSqKm, value);
        }

        public double AreaAcre
        {
            get => _areaAcre;
            set => SetProperty(ref _areaAcre, value);
        }
        public double AreaSqMile
        {
            get => _areaSqMiles;
            set => SetProperty(ref _areaSqMiles, value);
        }
        public double ElevRangeMeters
        {
            get => Math.Round(MaxElevMeters - MinElevMeters, 2);
        }
        public double AoiRefArea
        {
            get => _aoiRefArea;
            set => SetProperty(ref _aoiRefArea, value);
        }

        public string AoiRefUnits
        {
            get => _aoiRefUnits;
            set => SetProperty(ref _aoiRefUnits, value);
        }
        public bool Prism_Checked
        {
            get { return _prism_Checked; }
            set
            {
                SetProperty(ref _prism_Checked, value, () => Prism_Checked);
            }
        }
        public string PrismBufferDistance
        {
            get { return _prismBufferDistance; }
            set
            {
                SetProperty(ref _prismBufferDistance, value, () => PrismBufferDistance);
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
        private RelayCommand _setAoiCommand;
        public ICommand CmdSetAoi
        {
            get
            {
                if (_setAoiCommand == null)
                    _setAoiCommand = new RelayCommand(SetAoiImplAsync, () => true);
                return _setAoiCommand;
            }
        }
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
        private async void SetAoiImplAsync(object param)
        {
            try
            {
                OpenItemDialog selectAoiDialog = new OpenItemDialog()
                {
                    Title = "Select AOI Folder",
                    MultiSelect = false,
                    Filter = ItemFilters.Folders
                };
                if (selectAoiDialog.ShowDialog() == true)
                {
                    Module1.DeactivateState("Aoi_Selected_State");
                    IEnumerable<Item> selectedItems = selectAoiDialog.Items;
                    var e = selectedItems.FirstOrDefault();
                    Aoi oAoi = await GeneralTools.SetAoiAsync(e.Path, null);
                    if (oAoi != null)
                    {
                        Module1.Current.CboCurrentAoi.SetAoiName(oAoi.Name);
                        _view.Title = $@"AOI: {oAoi.Name}";
                        string sMask = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_VECTOR;
                        IList<double> lstResult = await GeoprocessingTools.GetDemStatsAsync(oAoi.FilePath, sMask, 0.005);
                        if (lstResult.Count == 2)   // We expect the min and max values in that order
                        {
                            MinElevMeters = Math.Round(lstResult[0],2);
                            MaxElevMeters = Math.Round(lstResult[1],2);
                        }
                        var result = await GeodatabaseTools.CalculateAoiAreaSqMetersAsync(oAoi.FilePath, -1);
                        double dblAreaSqM = result.Item1;
                        if (dblAreaSqM > 0)
                        {
                            AreaSqKm = Math.Round(ArcGIS.Core.Geometry.AreaUnit.SquareMeters.ConvertTo(dblAreaSqM,
                                ArcGIS.Core.Geometry.AreaUnit.SquareKilometers), 2);
                            AreaAcre = Math.Round(ArcGIS.Core.Geometry.AreaUnit.SquareMeters.ConvertTo(dblAreaSqM,
                                ArcGIS.Core.Geometry.AreaUnit.Acres), 2);
                            AreaSqMile = Math.Round(ArcGIS.Core.Geometry.AreaUnit.SquareMeters.ConvertTo(dblAreaSqM,
                                ArcGIS.Core.Geometry.AreaUnit.SquareMiles), 2);
                        }

                        string[] arrReference = new string[] { Constants.FIELD_AOIREFAREA, Constants.FIELD_AOIREFUNIT };
                        Uri uriAoi = new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi));
                        if (await GeodatabaseTools.AttributeExistsAsync(uriAoi, Constants.FILE_POURPOINT, arrReference[0]))
                        {
                            for (int i = 0; i < arrReference.Length; i++)
                            {
                                string retVal = await GeodatabaseTools.QueryTableForSingleValueAsync(uriAoi, Constants.FILE_POURPOINT, arrReference[i], new ArcGIS.Core.Data.QueryFilter());
                                if (!string.IsNullOrEmpty(retVal))
                                {
                                    switch (i)
                                    {
                                        case 0:
                                            double dblVal = Convert.ToDouble(retVal);
                                            AoiRefArea = Math.Round(dblVal,2);
                                            break;
                                        case 1:
                                            AoiRefUnits = retVal;
                                            break;
                                    }
                                }
                            }
                        }
                        // Set reference to layers pane to get information that was extracted during setAoi() function
                        var layersPane = (DockpaneLayersViewModel)FrameworkApplication.DockPaneManager.Find("bagis_pro_DockpaneLayers");
                        if (layersPane != null)
                        {
                            Prism_Checked = layersPane.Prism_Checked;
                            PrismBufferUnits = layersPane.PrismBufferUnits;
                            PrismBufferDistance = layersPane.PrismBufferDistance;
                            SNOTEL_Checked = layersPane.SNOTEL_Checked;
                            SnotelBufferUnits = layersPane.SnotelBufferUnits;
                            SnotelBufferDistance = layersPane.SnotelBufferDistance;
                            SnowCos_Checked = layersPane.SnowCos_Checked;
                            SnowCosBufferUnits = layersPane.SnowCosBufferUnits;
                            SnowCosBufferDistance = layersPane.SnowCosBufferDistance;
                            SnowCos_Checked = layersPane.SnowCos_Checked;
                        }
                        if (oAoi.PrismDepthUnits.Equals(LinearUnit.Inches.ToString()))
                        {
                            PrismInchesChecked = true;
                        }
                        else
                        {
                            PrismMmChecked = true;
                        }

                        if (!oAoi.ValidForecastData)
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.Append("This AOI does not have the required forecast station information. ");
                            sb.Append("Use the Batch Tools menu to run the 'Forecast Station Data' tool to update ");
                            sb.Append("the forecast station data. Next use the Batch Tools menu to run the ");
                            sb.Append("'Generate AOI Reports' tool to create the analysis layers required by BAGIS-Pro.");
                            MessageBox.Show(sb.ToString(), "BAGIS PRO");
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
                                MessageBox.Show(sb.ToString(), "BAGIS PRO");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to set the AOI!! " + e.Message, "BAGIS PRO");
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
                // Apply default buffer if left null
                if (string.IsNullOrEmpty(PrismBufferDistance))
                {
                    PrismBufferDistance = (string)Module1.Current.BagisSettings.PrecipBufferDistance;
                    PrismBufferUnits = (string)Module1.Current.BagisSettings.PrecipBufferUnits;
                }

                if (clipPrism)
                {
                    if (!await GeodatabaseTools.FeatureClassExistsAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi)), Constants.FILE_AOI_PRISM_VECTOR))
                    {
                        string strInputFeatures = $@"{GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi)}\{Constants.FILE_AOI_VECTOR}";
                        string strOutputFeatures = $@"{GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi)}\{Constants.FILE_AOI_PRISM_VECTOR}";
                        string strDistance = $@"{PrismBufferDistance} {PrismBufferUnits}";
                        success = await GeoprocessingTools.BufferAsync(strInputFeatures, strOutputFeatures, strDistance, "ALL", CancelableProgressor.None);
                    }
                    if (success == BA_ReturnCode.Success)
                    {
                        success = await AnalysisTools.ClipLayersAsync(Module1.Current.Aoi.FilePath, BA_Objects.DataSource.GetPrecipitationKey,
                            PrismBufferDistance, PrismBufferUnits, PrismBufferDistance, PrismBufferUnits);
                        if (success == BA_ReturnCode.Success)
                        {
                            success = await AnalysisTools.UpdateSitesPropertiesAsync(Module1.Current.Aoi.FilePath, SiteProperties.Precipitation);
                        }
                        if (success == BA_ReturnCode.Success)
                        {
                            ReclipPrism_Checked = false;
                            Prism_Checked = true;
                        }
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

    }
    }
