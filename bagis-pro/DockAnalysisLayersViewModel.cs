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
    internal class DockAnalysisLayersViewModel : DockPane
    {
        private const string _dockPaneID = "bagis_pro_DockAnalysisLayers";

        protected DockAnalysisLayersViewModel() { }

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
        /// Hide the pane if there is no current AOI when Pro starts up
        /// </summary>
        /// <param name="isVisible"></param>
        protected override void OnShow(bool isVisible)
        {
            if (isVisible == true)
            {
                if (Module1.Current.CboCurrentAoi == null)
                {
                    this.Hide();
                }
            }
        }

        /// <summary>
        /// Text shown near the top of the DockPane.
        /// </summary>
        private string _heading = "Calculate Analysis Layers";
        private bool _RepresentedArea_Checked = false;
        private bool _PrismZones_Checked = false;
        private bool _AspectZones_Checked = false;
        private bool _SlopeZones_Checked = false;
        private bool _ElevationZones_Checked = false;
        public string Heading
        {
            get { return _heading; }
            set
            {
                SetProperty(ref _heading, value, () => Heading);
            }
        }

        public bool RepresentedArea_Checked
        {
            get { return _RepresentedArea_Checked; }
            set
            {
                SetProperty(ref _RepresentedArea_Checked, value, () => RepresentedArea_Checked);
            }
        }

        public bool PrismZones_Checked
        {
            get { return _PrismZones_Checked; }
            set
            {
                SetProperty(ref _PrismZones_Checked, value, () => PrismZones_Checked);
            }
        }

        public bool AspectZones_Checked
        {
            get { return _AspectZones_Checked; }
            set
            {
                SetProperty(ref _AspectZones_Checked, value, () => AspectZones_Checked);
            }
        }

        public bool SlopeZones_Checked
        {
            get { return _SlopeZones_Checked; }
            set
            {
                SetProperty(ref _SlopeZones_Checked, value, () => SlopeZones_Checked);
            }
        }

        public bool ElevationZones_Checked
        {
            get { return _ElevationZones_Checked; }
            set
            {
                SetProperty(ref _ElevationZones_Checked, value, () => ElevationZones_Checked);
            }
        }

        public void ResetView()
        {
            RepresentedArea_Checked = false;
            PrismZones_Checked = false;
            AspectZones_Checked = false;
            SlopeZones_Checked = false;
            ElevationZones_Checked = false;
        }

        public ICommand CmdGenerateLayers
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    // Create from template
                    await GenerateLayersAsync(RepresentedArea_Checked, PrismZones_Checked, AspectZones_Checked,
                        SlopeZones_Checked, ElevationZones_Checked);
                });
            }
        }

        private async Task GenerateLayersAsync(bool calculateRepresented, bool calculatePrism, bool calculateAspect,
            bool calculateSlope, bool calculateElevation)
        {
            try
            {
                if (String.IsNullOrEmpty(Module1.Current.Aoi.Name))
                {
                    MessageBox.Show("No AOI selected for analysis !!", "BAGIS-PRO");
                    return;
                }

                if (calculateRepresented == false && calculatePrism == false && calculateAspect == false
                    && calculateSlope == false && calculateElevation == false)
                {
                    MessageBox.Show("No layers selected to generate !!", "BAGIS-PRO");
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

                var layersPane = (DockAnalysisLayersViewModel)FrameworkApplication.DockPaneManager.Find("bagis_pro_DockAnalysisLayers");
                BA_ReturnCode success = BA_ReturnCode.Success;

                if (calculateRepresented)
                {
                    success = await AnalysisTools.GenerateSiteLayersAsync();
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.RepresentedArea_Checked = false;
                    }

                }

                if (calculatePrism)
                {
                    string strLayer = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Prism, true) +
                                      System.IO.Path.GetFileName(Module1.Current.Settings.m_precipFile);
                    string strZonesRaster = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_PRECIP_ZONE;
                    string strMaskPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_PRISM_VECTOR;
                    IList<BA_Objects.Interval> lstInterval = await AnalysisTools.GetPrismClassesAsync(Module1.Current.Aoi.FilePath,
                        strLayer, Module1.Current.Settings.m_precipZonesCount);
                    success = await AnalysisTools.CalculateZonesAsync(Module1.Current.Aoi.FilePath, strLayer,
                        lstInterval, strZonesRaster, strMaskPath, "PRISM");
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.PrismZones_Checked = false;
                    }
                }

                if (calculateAspect)
                {
                    string strLayer = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Surfaces, true) +
                        Constants.FILE_ASPECT;
                    string strZonesRaster = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_ASPECT_ZONE;
                    string strMaskPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_VECTOR;
                    IList<BA_Objects.Interval> lstInterval = AnalysisTools.GetAspectClasses(Module1.Current.Settings.m_aspectDirections);
                    success = await AnalysisTools.CalculateZonesAsync(Module1.Current.Aoi.FilePath, strLayer,
                        lstInterval, strZonesRaster, strMaskPath, "ASPECT");
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.AspectZones_Checked = false;
                    }
                }

                if (calculateSlope)
                {
                    string strLayer = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Surfaces, true) +
                        Constants.FILE_SLOPE;
                    string strZonesRaster = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SLOPE_ZONE;
                    string strMaskPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_VECTOR;
                    IList<BA_Objects.Interval> lstInterval = AnalysisTools.GetSlopeClasses();
                    success = await AnalysisTools.CalculateZonesAsync(Module1.Current.Aoi.FilePath, strLayer,
                        lstInterval, strZonesRaster, strMaskPath, "SLOPE");
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.SlopeZones_Checked = false;
                    }
                }

                if (calculateElevation)
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(GenerateLayersAsync),
                        "GetDemStatsAsync");
                    IList<double> lstResult = await GeoprocessingTools.GetDemStatsAsync(Module1.Current.Aoi.FilePath, "", 0.005);
                    double demElevMinMeters = -1;
                    double demElevMaxMeters = -1;
                    if (lstResult.Count == 2)   // We expect the min and max values in that order
                    {
                        demElevMinMeters = lstResult[0];
                        demElevMaxMeters = lstResult[1];
                    }
                    else
                    {
                        MessageBox.Show("Unable to read DEM. Elevation zones cannot be generated!!", "BAGIS-PRO");
                        Module1.Current.ModuleLogManager.LogDebug(nameof(GenerateLayersAsync),
                            "Unable to read min/max elevation from DEM");
                        return;
                    }

                    double aoiElevMin = demElevMinMeters;
                    double aoiElevMax = demElevMaxMeters;
                    if (! Module1.Current.Settings.m_demUnits.Equals(Module1.Current.Settings.m_demDisplayUnits))
                    {
                        if (Module1.Current.Settings.m_demDisplayUnits.Equals("Feet"))
                        {
                            aoiElevMin = LinearUnit.Meters.ConvertTo(demElevMinMeters, LinearUnit.Feet);
                            aoiElevMax = LinearUnit.Meters.ConvertTo(demElevMaxMeters, LinearUnit.Feet);
                        }
                        else if (Module1.Current.Settings.m_demUnits.Equals("Feet"))
                        {
                            aoiElevMin = LinearUnit.Feet.ConvertTo(demElevMinMeters, LinearUnit.Meters);
                            aoiElevMax = LinearUnit.Feet.ConvertTo(demElevMaxMeters, LinearUnit.Meters);
                        }
                    }

                    short[] arrTestIntervals = new short[] { 5000, 2500, 1000, 500, 250, 200, 100, 50 };
                    short bestInterval = 50;
                    var range = aoiElevMax - aoiElevMin;
                    foreach (var testInterval in arrTestIntervals)
                    {
                        double dblZoneCount = range / testInterval;
                        if (dblZoneCount >= Module1.Current.Settings.m_minElevZones)
                        {
                            bestInterval = testInterval;
                            break;
                        }
                    }
                    IList<BA_Objects.Interval> lstInterval = AnalysisTools.GetElevationClasses(aoiElevMin, aoiElevMax,
                        bestInterval, Module1.Current.Settings.m_demUnits, Module1.Current.Settings.m_demDisplayUnits);
                    string strLayer = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Surfaces, true) +
                        Constants.FILE_DEM_FILLED;
                    string strZonesRaster = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_ELEV_ZONE;
                    string strMaskPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_VECTOR;
                    success = await AnalysisTools.CalculateZonesAsync(Module1.Current.Aoi.FilePath, strLayer,
                        lstInterval, strZonesRaster, strMaskPath, "ELEVATION");
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.ElevationZones_Checked = false;
                    }
                }

                if (success == BA_ReturnCode.Success)
                {
                    MessageBox.Show("Analysis layers generated !!", "BAGIS-PRO");
                }
            }
            catch (Exception ex)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(GenerateLayersAsync),
                    "Exception: " + ex.Message);
            }
        }
    }

        /// <summary>
        /// Button implementation to show the DockPane.
        /// </summary>
        internal class DockAnalysisLayers_ShowButton : Button
        {
            protected override void OnClick()
            {
                DockAnalysisLayersViewModel.Show();
            }
        }
}
