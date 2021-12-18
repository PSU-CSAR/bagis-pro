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
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;


namespace bagis_pro
{
    internal class DockpaneLayersViewModel : DockPane
    {   
      private const string _dockPaneID = "bagis_pro_DockpaneLayers";

        protected DockpaneLayersViewModel() {}

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
        private bool _SWE_Checked = false;
        private string _SWEBufferDistance = "";
        private string _SWEBufferUnits = "";
        private bool _reclipSwe_Checked = false;
        private bool _prism_Checked = false;
        private string _prismBufferDistance = "";
        private string _prismBufferUnits = "";
        private bool _reclipPrism_Checked = false;
        private bool _SNOTEL_Checked = false;
        private string _snotelBufferDistance = "";
        private string _snotelBufferUnits = "";
        private bool _reclipSnotel_Checked = false;
        private bool _snowCos_Checked = false;
        private string _snowCosBufferDistance = "";
        private string _snowCosBufferUnits = "";
        private bool _reclipSnowCos_Checked = false;
        private bool _roads_Checked = false;
        private string _roadsBufferDistance = "";
        private string _roadsBufferUnits = "";
        private bool _reclipRoads_Checked = false;
        private bool _publicLands_Checked = false;
        private string _publicLandsBufferDistance = "";
        private string _publicLandsBufferUnits = "";
        private bool _reclipPublicLands_Checked = false;
        private bool _vegetation_Checked = false;
        private string _vegetationBufferDistance = "";
        private string _vegetationBufferUnits = "";
        private bool _reclipVegetation_Checked = false;
        private bool _landCover_Checked = false;
        private string _landCoverBufferDistance = "";
        private string _landCoverBufferUnits = "";
        private bool _reclipLandCover_Checked = false;


        public bool ReclipSwe_Checked
        {
            get { return _reclipSwe_Checked; }
            set
            {
                SetProperty(ref _reclipSwe_Checked, value, () => ReclipSwe_Checked);
            }
        }

        public bool SWE_Checked
        {
            get { return _SWE_Checked; }
            set
            {
                SetProperty(ref _SWE_Checked, value, () => SWE_Checked);
            }
        }

        public string SWEBufferDistance
        {
            get { return _SWEBufferDistance; }
            set
            {
                SetProperty(ref _SWEBufferDistance, value, () => SWEBufferDistance);
            }
        }

        public string SWEBufferUnits
        {
            get { return _SWEBufferUnits; }
            set
            {
                SetProperty(ref _SWEBufferUnits, value, () => SWEBufferUnits);
            }
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

        public bool Roads_Checked
        {
            get { return _roads_Checked; }
            set
            {
                SetProperty(ref _roads_Checked, value, () => Roads_Checked);
            }
        }

        public string RoadsBufferDistance
        {
            get { return _roadsBufferDistance; }
            set
            {
                SetProperty(ref _roadsBufferDistance, value, () => RoadsBufferDistance);
            }
        }

        public string RoadsBufferUnits
        {
            get { return _roadsBufferUnits; }
            set
            {
                SetProperty(ref _roadsBufferUnits, value, () => RoadsBufferUnits);
            }
        }

        public bool ReclipRoads_Checked
        {
            get { return _reclipRoads_Checked; }
            set
            {
                SetProperty(ref _reclipRoads_Checked, value, () => ReclipRoads_Checked);
            }
        }

        public bool PublicLands_Checked
        {
            get { return _publicLands_Checked; }
            set
            {
                SetProperty(ref _publicLands_Checked, value, () => PublicLands_Checked);
            }
        }

        public string PublicLandsBufferDistance
        {
            get { return _publicLandsBufferDistance; }
            set
            {
                SetProperty(ref _publicLandsBufferDistance, value, () => PublicLandsBufferDistance);
            }
        }

        public string PublicLandsBufferUnits
        {
            get { return _publicLandsBufferUnits; }
            set
            {
                SetProperty(ref _publicLandsBufferUnits, value, () => PublicLandsBufferUnits);
            }
        }

        public bool ReclipPublicLands_Checked
        {
            get { return _reclipPublicLands_Checked; }
            set
            {
                SetProperty(ref _reclipPublicLands_Checked, value, () => ReclipPublicLands_Checked);
            }
        }
        public bool Vegetation_Checked
        {
            get { return _vegetation_Checked; }
            set
            {
                SetProperty(ref _vegetation_Checked, value, () => Vegetation_Checked);
            }
        }

        public string VegetationBufferDistance
        {
            get { return _vegetationBufferDistance; }
            set
            {
                SetProperty(ref _vegetationBufferDistance, value, () => VegetationBufferDistance);
            }
        }

        public string VegetationBufferUnits
        {
            get { return _vegetationBufferUnits; }
            set
            {
                SetProperty(ref _vegetationBufferUnits, value, () => VegetationBufferUnits);
            }
        }

        public bool ReclipVegetation_Checked
        {
            get { return _reclipVegetation_Checked; }
            set
            {
                SetProperty(ref _reclipVegetation_Checked, value, () => ReclipVegetation_Checked);
            }
        }

        public bool LandCover_Checked
        {
            get { return _landCover_Checked; }
            set
            {
                SetProperty(ref _landCover_Checked, value, () => LandCover_Checked);
            }
        }

        public string LandCoverBufferDistance
        {
            get { return _landCoverBufferDistance; }
            set
            {
                SetProperty(ref _landCoverBufferDistance, value, () => LandCoverBufferDistance);
            }
        }

        public string LandCoverBufferUnits
        {
            get { return _landCoverBufferUnits; }
            set
            {
                SetProperty(ref _landCoverBufferUnits, value, () => LandCoverBufferUnits);
            }
        }

        public bool ReclipLandCover_Checked
        {
            get { return _reclipLandCover_Checked; }
            set
            {
                SetProperty(ref _reclipLandCover_Checked, value, () => ReclipLandCover_Checked);
            }
        }

        public void ResetView()
        {
            Prism_Checked = false;
            PrismBufferDistance = (string) Module1.Current.BatchToolSettings.PrecipBufferDistance;
            PrismBufferUnits = (string) Module1.Current.BatchToolSettings.PrecipBufferUnits;
            ReclipPrism_Checked = false;
            SWE_Checked = false;
            SWEBufferDistance = (string) Module1.Current.BatchToolSettings.PrecipBufferDistance;
            SWEBufferUnits = (string) Module1.Current.BatchToolSettings.PrecipBufferUnits;
            ReclipSwe_Checked = false;
            SNOTEL_Checked = false;
            SnotelBufferDistance = "";
            SnotelBufferUnits = (string) Module1.Current.BatchToolSettings.SnotelBufferUnits;
            ReclipSNOTEL_Checked = false;
            SnowCos_Checked = false;
            SnowCosBufferDistance = "";
            SnowCosBufferUnits = (string) Module1.Current.BatchToolSettings.SnotelBufferUnits;
            ReclipSnowCos_Checked = false;
            Roads_Checked = false;
            RoadsBufferDistance = "";
            RoadsBufferUnits = (string) Module1.Current.BatchToolSettings.RoadsBufferUnits;
            ReclipRoads_Checked = false;
            PublicLands_Checked = false;
            PublicLandsBufferDistance = "";
            PublicLandsBufferUnits = (string) Module1.Current.BatchToolSettings.RoadsBufferUnits;
            ReclipPublicLands_Checked = false;
            Vegetation_Checked = false;
            VegetationBufferDistance = "";
            VegetationBufferUnits = (string)Module1.Current.BatchToolSettings.VegetationBufferUnits;
            ReclipVegetation_Checked = false;
            ReclipLandCover_Checked = false;

        }

        public ICommand CmdClipLayers
        {
            get
            {
                return new RelayCommand(async () => {
                    // Create from template
                    await ClipLayersAsync(ReclipSwe_Checked, ReclipPrism_Checked, ReclipSNOTEL_Checked, 
                        ReclipSnowCos_Checked, ReclipRoads_Checked, _reclipPublicLands_Checked, _reclipVegetation_Checked,
                        _reclipLandCover_Checked);
                });
            }
        }

        private async Task ClipLayersAsync(bool clipSwe, bool clipPrism, bool clipSnotel, bool clipSnowCos,
            bool clipRoads, bool clipPublicLands, bool clipVegetation, bool clipLandcover)
        {
            try
            {
                if (String.IsNullOrEmpty(Module1.Current.Aoi.Name))
                {
                    MessageBox.Show("No AOI selected for analysis !!", "BAGIS-PRO");
                    return;
                }

                if (clipSwe == false && clipPrism == false && 
                    clipSnotel == false && clipSnowCos == false && clipRoads == false &&
                    clipPublicLands == false && clipVegetation == false && clipLandcover == false)
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

                var layersPane = (DockpaneLayersViewModel)FrameworkApplication.DockPaneManager.Find("bagis_pro_DockpaneLayers");
                BA_ReturnCode success = BA_ReturnCode.Success;

                // Check for PRISM units
                string strPrismPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Prism, true) 
                    + PrismFile.Annual.ToString();
                string pBufferDistance = "";
                string pBufferUnits = "";
                string strBagisTag = await GeneralTools.GetBagisTagAsync(strPrismPath, Constants.META_TAG_XPATH);
                if (!string.IsNullOrEmpty(strBagisTag))
                {
                    pBufferDistance = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_BUFFER_DISTANCE, ';');
                    pBufferUnits = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_XUNIT_VALUE, ';');
                }
                // Apply default buffer if left null
                if (string.IsNullOrEmpty(PrismBufferDistance))
                {
                    PrismBufferDistance = (string)Module1.Current.BatchToolSettings.PrecipBufferDistance;
                    PrismBufferUnits = (string)Module1.Current.BatchToolSettings.PrecipBufferUnits;
                }

                if (clipPrism)
                {
                    success = await AnalysisTools.ClipLayersAsync(Module1.Current.Aoi.FilePath, Constants.DATA_TYPE_PRECIPITATION,
                        pBufferDistance, pBufferUnits, PrismBufferDistance, PrismBufferUnits);
                    if (success == BA_ReturnCode.Success)
                    {
                        success = await AnalysisTools.UpdateSitesPropertiesAsync(Module1.Current.Aoi.FilePath, SiteProperties.Precipitation);
                    }
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.ReclipPrism_Checked = false;
                        layersPane.Prism_Checked = true;
                    }
                }
                if (clipSwe)
                {
                    success = await AnalysisTools.ClipSweLayersAsync(pBufferDistance, pBufferUnits, 
                        SWEBufferDistance, SWEBufferUnits);
                        if (success == BA_ReturnCode.Success)
                        {
                            layersPane.ReclipSwe_Checked = false;
                            layersPane.SWE_Checked = true;
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
                        if (clipSnotel)
                        {
                            layersPane.ReclipSNOTEL_Checked = false;
                            layersPane.SNOTEL_Checked = true;
                        }
                        if (clipSnowCos)
                        {
                            layersPane.ReclipSnowCos_Checked = false;
                            layersPane.SnowCos_Checked = true;
                        }
                    }
                }

                if (clipRoads)
                {
                    string strOutputFc = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true)
                        + Constants.FILE_ROADS;
                    success = await AnalysisTools.ClipFeatureLayerAsync(Module1.Current.Aoi.FilePath, strOutputFc, Constants.DATA_TYPE_ROADS,
                        RoadsBufferDistance, RoadsBufferUnits);
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.ReclipRoads_Checked = false;
                        layersPane.Roads_Checked = true;
                    }
                    else
                    {
                        MessageBox.Show("An error occurred while clipping the roads. Check the log file!!", "BAGIS-PRO");
                    }
                }

                if (clipPublicLands)
                {
                    string strOutputFc = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true)
                        + Constants.FILE_PUBLIC_LAND;
                    success = await AnalysisTools.ClipFeatureLayerAsync(Module1.Current.Aoi.FilePath, strOutputFc, Constants.DATA_TYPE_PUBLIC_LAND,
                        PublicLandsBufferDistance, PublicLandsBufferUnits);
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.ReclipPublicLands_Checked = false;
                        layersPane.PublicLands_Checked = true;
                    }
                    else
                    {
                        MessageBox.Show("An error occurred while clipping the public lands. Check the log file!!", "BAGIS-PRO");
                    }
                }

                if (clipVegetation)
                {
                    string strOutputRaster = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true)
                        + Constants.FILE_VEGETATION_EVT;
                    success = await AnalysisTools.ClipRasterLayerAsync(Module1.Current.Aoi.FilePath, strOutputRaster, Constants.DATA_TYPE_VEGETATION,
                        VegetationBufferDistance, VegetationBufferUnits);
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.ReclipVegetation_Checked = false;
                        layersPane.Vegetation_Checked = true;
                    }
                    else
                    {
                        MessageBox.Show("An error occurred while clipping the vegetation layer. Check the log file!!", "BAGIS-PRO");
                    }
                }

                if (clipLandcover)
                {
                    string strOutputRaster = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true)
                        + Constants.FILE_LAND_COVER;
                    success = await AnalysisTools.ClipRasterLayerAsync(Module1.Current.Aoi.FilePath, strOutputRaster, Constants.DATA_TYPE_LAND_COVER,
                        LandCoverBufferDistance, LandCoverBufferUnits);
                    string strNullOutput = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true)
                        + "tmpNull";
                    if (success == BA_ReturnCode.Success)
                    {
                        string strConstant = "11";  // water bodies
                        string strWaterbodies = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true)
                            + Constants.FILE_WATER_BODIES;
                        string strWhere = "Value <> 11";
                        success = await GeoprocessingTools.SetNullAsync(strOutputRaster, strConstant, strNullOutput, strWhere);
                    }
                    if (success == BA_ReturnCode.Success)
                    {
                        // Create vector representation of waterbodies for map display
                        string strWaterbodies = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true)
                            + Constants.FILE_WATER_BODIES;
                        var parameters = Geoprocessing.MakeValueArray(strNullOutput, strWaterbodies);
                        var gpResult = await Geoprocessing.ExecuteToolAsync("RasterToPolygon_conversion", parameters, null,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        if (gpResult.IsFailed)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync),
                                "Failed to create vector representation of waterbodies!");
                            success = BA_ReturnCode.UnknownError;
                        }
                        else
                        {
                            // Delete temp null raster
                            success = await GeoprocessingTools.DeleteDatasetAsync(strNullOutput);
                        }
                    }
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.ReclipLandCover_Checked = false;
                        layersPane.LandCover_Checked = true;
                    }
                    else
                    {
                        MessageBox.Show("An error occurred while clipping the land cover layer. Check the log file!!", "BAGIS-PRO");
                    }
                }

                if (success == BA_ReturnCode.Success)
                {
                    MessageBox.Show("Analysis layers clipped!!", "BAGIS-PRO");
                }
                else
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

  /// <summary>
  /// Button implementation to show the DockPane.
  /// </summary>
	internal class DockpaneLayers_ShowButton : Button
	{
		protected override void OnClick()
		{
            DockpaneLayersViewModel.Show();

        }
    }	
}
