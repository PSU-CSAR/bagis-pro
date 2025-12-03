using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
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
        int _aoiRefArea;
        string _aoiRefUnits = "N/A";
        private bool _prism_Checked = false;
        private string _prismBufferDistance = "";
        private string _prismBufferUnits = "Meters";
        private bool _reclipPrism_Checked = false;

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
        public int AoiRefArea
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
                                            // Reference area appears to be rounded to an integer in all of my sample data
                                            AoiRefArea = Convert.ToInt32(retVal);
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

    }


    }
