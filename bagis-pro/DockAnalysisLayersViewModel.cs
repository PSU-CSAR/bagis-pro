﻿using System;
using System.Collections.Generic;
using System.IO;
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
        private string _heading = "Generate Analysis Layers";
        private bool _RepresentedArea_Checked = false;
        private bool _PrismZones_Checked = false;
        private bool _AspectZones_Checked = false;
        private bool _SlopeZones_Checked = false;
        private bool _ElevationZones_Checked = false;
        private bool _SitesZones_Checked = false;
        private bool _Roads_Checked = false;
        private bool _FederalLand_Checked = false;
        private bool _ForestedArea_Checked = false;
        private bool _ElevPrecipCorr_Checked = false;
        private bool _SWE_Delta_Checked = false;
        private bool _Precip_Contrib_Checked = false;
        private bool _Winter_Precip_Checked = false;
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

        public bool SitesZones_Checked
        {
            get { return _SitesZones_Checked; }
            set
            {
                SetProperty(ref _SitesZones_Checked, value, () => SitesZones_Checked);
            }
        }

        public bool Roads_Checked
        {
            get { return _Roads_Checked; }
            set
            {
                SetProperty(ref _Roads_Checked, value, () => Roads_Checked);
            }
        }

        public bool FederalLand_Checked
        {
            get { return _FederalLand_Checked; }
            set
            {
                SetProperty(ref _FederalLand_Checked, value, () => FederalLand_Checked);
            }
        }

        public bool ForestedArea_Checked
        {
            get { return _ForestedArea_Checked; }
            set
            {
                SetProperty(ref _ForestedArea_Checked, value, () => ForestedArea_Checked);
            }
        }

        public bool ElevPrecipCorr_Checked
        {
            get { return _ElevPrecipCorr_Checked; }
            set
            {
                SetProperty(ref _ElevPrecipCorr_Checked, value, () => ElevPrecipCorr_Checked);
            }
        }

        public bool SWE_Delta_Checked
        {
            get { return _SWE_Delta_Checked; }
            set
            {
                SetProperty(ref _SWE_Delta_Checked, value, () => SWE_Delta_Checked);
            }
        }

        public bool Precip_Contrib_Checked
        {
            get { return _Precip_Contrib_Checked; }
            set
            {
                SetProperty(ref _Precip_Contrib_Checked, value, () => Precip_Contrib_Checked);
            }
        }

        public bool Winter_Precip_Checked
        {
            get { return _Winter_Precip_Checked; }
            set
            {
                SetProperty(ref _Winter_Precip_Checked, value, () => Winter_Precip_Checked);
            }
        }

        public void ResetView()
        {
            RepresentedArea_Checked = false;
            PrismZones_Checked = false;
            AspectZones_Checked = false;
            SlopeZones_Checked = false;
            ElevationZones_Checked = false;
            SitesZones_Checked = false;
            Roads_Checked = false;
            FederalLand_Checked = false;
            ForestedArea_Checked = false;
            ElevPrecipCorr_Checked = false;
            SWE_Delta_Checked = false;
            Precip_Contrib_Checked = false;
            Winter_Precip_Checked = false;
        }

        public ICommand CmdGenerateLayers
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    // Create from template
                    await GenerateLayersAsync(RepresentedArea_Checked, PrismZones_Checked, AspectZones_Checked,
                        SlopeZones_Checked, ElevationZones_Checked, Roads_Checked, FederalLand_Checked, ForestedArea_Checked,
                        ElevPrecipCorr_Checked, SitesZones_Checked, SWE_Delta_Checked, Precip_Contrib_Checked,
                        Winter_Precip_Checked);
                });
            }
        }

        private async Task GenerateLayersAsync(bool calculateRepresented, bool calculatePrism, bool calculateAspect,
            bool calculateSlope, bool calculateElevation, bool bufferRoads, bool extractPublicLand,
            bool extractForestedArea, bool elevPrecipCorr, bool calculateSitesZones, bool calculateSweDelta,
            bool calculatePrecipContrib, bool winterPrecip)
        {
            try
            {
                if (String.IsNullOrEmpty(Module1.Current.Aoi.Name))
                {
                    MessageBox.Show("No AOI selected for analysis !!", "BAGIS-PRO");
                    return;
                }

                if (calculateRepresented == false && calculatePrism == false && calculateAspect == false
                    && calculateSlope == false && calculateElevation == false && bufferRoads == false
                    && extractPublicLand == false && extractForestedArea == false && elevPrecipCorr == false
                    && calculateSitesZones == false && calculateSweDelta == false && calculatePrecipContrib == false
                    && winterPrecip == false)
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
                    double siteBufferDistanceMiles = (double)Module1.Current.BagisSettings.SiteBufferDistMiles;
                    double siteElevRangeFeet = (double)Module1.Current.BagisSettings.SiteElevRangeFeet;
                    success = await AnalysisTools.GenerateSiteLayersAsync(siteBufferDistanceMiles, siteElevRangeFeet);
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.RepresentedArea_Checked = false;
                    }
                    else
                    {
                        MessageBox.Show("An error occurred while trying to calculate the sites represented area !!", "BAGIS-PRO");
                    }
                }

                if (calculatePrism)
                {
                    string strLayer = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Prism, true) +
                        Path.GetFileName((string)Module1.Current.BagisSettings.AoiPrecipFile);
                    string strZonesRaster = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_PRECIP_ZONE;
                    success = await AnalysisTools.CalculatePrecipitationZonesAsync(strLayer, strZonesRaster);
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.PrismZones_Checked = false;
                    }
                }

                if (calculateAspect)
                {
                    success = await AnalysisTools.CalculateAspectZonesAsync();
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
                    string strMaskPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_BUFFERED_VECTOR;
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
                    success = await AnalysisTools.CalculateElevationZonesAsync(Module1.Current.Aoi.FilePath);
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.ElevationZones_Checked = false;
                    }
                }

                if (calculateSitesZones)
                {
                    Uri uri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers));
                    string[] arrSitesLayers = new string[] { Constants.FILE_SNOTEL, Constants.FILE_SNOW_COURSE, Constants.FILE_SNOLITE, Constants.FILE_COOP_PILLOW };
                    bool hasSnotel = false;
                    bool hasSnowCourse = false;
                    for (int i = 0; i < arrSitesLayers.Length; i++)
                    {
                        if (await GeodatabaseTools.CountFeaturesAsync(uri, arrSitesLayers[i]) > 0)
                        {
                            switch (i)
                            {
                                case 0:
                                    hasSnotel = true;
                                    break;
                                case 1:
                                    hasSnowCourse = true;
                                    break;
                                case 2:
                                    hasSnotel = true;
                                    break;
                                case 3:
                                    hasSnotel = true;
                                    break;
                            }
                        }
                    }
                    if (hasSnotel || hasSnowCourse)
                    {
                        success = await AnalysisTools.CalculateSitesZonesAsync(Module1.Current.Aoi.FilePath, hasSnotel, hasSnowCourse);
                    }
                    else
                    {
                        MessageBox.Show("No sites found to create sites zone layers!!", "BAGIS-PRO");
                    }

                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.SitesZones_Checked = false;
                    }
                }

                bool bSkipPotentialSites = false;
                if (bufferRoads)
                {
                    Uri uri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers));
                    bool bExists = await GeodatabaseTools.FeatureClassExistsAsync(uri, Constants.FILE_ROADS);
                    if (!bExists)
                    {
                        MessageBox.Show("The roads layer is missing. Clip the roads layer before creating the roads analysis layer!!", "BAGIS-PRO");
                        Module1.Current.ModuleLogManager.LogDebug(nameof(GenerateLayersAsync),
                            "Unable to buffer roads because fs_roads layer does not exist. Process stopped!!");
                        return;
                    }

                    // This could come from the UI eventually
                    string strDistance = Module1.Current.BagisSettings.RoadsAnalysisBufferDistance + " " +
                        Module1.Current.BagisSettings.RoadsAnalysisBufferUnits;
                    success = await AnalysisTools.GenerateProximityRoadsLayerAsync(uri, strDistance);
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.Roads_Checked = false;
                    }
                    else
                    {
                        bSkipPotentialSites = true;     // may skip combined potential sites because this layer couldn't be generated    
                    }
                }

                if (extractPublicLand)
                {
                    success = await AnalysisTools.GetFederalNonWildernessLandsAsync(Module1.Current.Aoi.FilePath);

                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.FederalLand_Checked = false;
                    }
                    else
                    {
                        bSkipPotentialSites = true;     // may skip combined potential sites because this layer couldn't be generated    
                    }
                }

                if (extractForestedArea)
                {

                    success = await AnalysisTools.ExtractForestedAreaAsync(Module1.Current.Aoi.FilePath);
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.ForestedArea_Checked = false;
                    }
                    else
                    {
                        bSkipPotentialSites = true;     // may skip combined potential sites because this layer couldn't be generated    
                    }
                }

                if (! bSkipPotentialSites)
                {
                    if (bufferRoads || extractPublicLand || extractForestedArea)
                    {
                        // if either of the underlying layers changed, we need to recalculate the
                        // potential sites layer
                        success = await AnalysisTools.CalculatePotentialSitesAreaAsync(Module1.Current.Aoi.FilePath);
                        if (success != BA_ReturnCode.Success)
                        {
                            MessageBox.Show("An error occurred while generating the potential sites layer!!", "BAGIS-PRO");
                        }
                    }
                }

                if (elevPrecipCorr == true)
                {
                    string strLayer = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Prism, true) +
                        Path.GetFileName((string)Module1.Current.BagisSettings.AoiPrecipFile);

                    Uri uriPrism = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Prism));
                    success = await AnalysisTools.CalculateElevPrecipCorrAsync(Module1.Current.Aoi.FilePath, uriPrism, 
                        Path.GetFileName((string)Module1.Current.BagisSettings.AoiPrecipFile));


                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.ElevPrecipCorr_Checked = false;
                    }
                }

                if (calculateSweDelta == true)
                {
                    success = await AnalysisTools.CalculateSWEDeltaAsync(Module1.Current.Aoi.FilePath);
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.SWE_Delta_Checked = false;
                    }
                }

                if (calculatePrecipContrib == true)
                {
                    success = await AnalysisTools.CalculatePrecipitationContributionAsync(Module1.Current.Aoi.FilePath, -1);
                    success = await AnalysisTools.CalculateQuarterlyPrecipitationAsync(Module1.Current.Aoi);
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.Precip_Contrib_Checked = false;
                    }
                }

                if (winterPrecip == true)
                {
                    success = await AnalysisTools.GenerateWinterPrecipitationLayerAsync(Module1.Current.Aoi);
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.Winter_Precip_Checked = false;
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
