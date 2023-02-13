using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private bool _landOwnership_Checked = false;
        private string _landOwnershipBufferDistance = "";
        private string _landOwnershipUnits = "";
        private bool _reclipLandOwnership_Checked = false;
        private bool _landCover_Checked = false;
        private string _landCoverBufferDistance = "";
        private string _landCoverBufferUnits = "";
        private bool _reclipLandCover_Checked = false;
        // This is buffer information for aoib_v; Not displayed anywhere on the form
        // but used for default buffer distances for everything except PRISM and SWE
        private string _unmanagedBufferDistance = "";
        private string _unmanagedBufferUnits = "";
        private ObservableCollection<string> _lstUnits = new ObservableCollection<string>();


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

        public bool LandOwnership_Checked
        {
            get { return _landOwnership_Checked; }
            set
            {
                SetProperty(ref _landOwnership_Checked, value, () => LandOwnership_Checked);
            }
        }

        public string LandOwnershipBufferDistance
        {
            get { return _landOwnershipBufferDistance; }
            set
            {
                SetProperty(ref _landOwnershipBufferDistance, value, () => LandOwnershipBufferDistance);
            }
        }

        public string LandOwnershipBufferUnits
        {
            get { return _landOwnershipUnits; }
            set
            {
                SetProperty(ref _landOwnershipUnits, value, () => LandOwnershipBufferUnits);
            }
        }

        public bool ReclipLandOwnership_Checked
        {
            get { return _reclipLandOwnership_Checked; }
            set
            {
                SetProperty(ref _reclipLandOwnership_Checked, value, () => ReclipLandOwnership_Checked);
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

        public string UnmanagedBufferDistance
        {
            get { return _unmanagedBufferDistance; }
            set
            {
                SetProperty(ref _unmanagedBufferDistance, value, () => UnmanagedBufferDistance);
            }
        }

        public string UnmanagedBufferBufferUnits
        {
            get { return _unmanagedBufferUnits; }
            set
            {
                SetProperty(ref _unmanagedBufferUnits, value, () => UnmanagedBufferBufferUnits);
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

        public void ResetView()
        {
            Prism_Checked = false;
            PrismBufferDistance = "";
            PrismBufferUnits = "";
            ReclipPrism_Checked = false;
            SWE_Checked = false;
            SWEBufferDistance = "";
            SWEBufferUnits = "";
            ReclipSwe_Checked = false;
            SNOTEL_Checked = false;
            SnotelBufferDistance = "";
            SnotelBufferUnits = "";
            ReclipSNOTEL_Checked = false;
            SnowCos_Checked = false;
            SnowCosBufferDistance = "";
            SnowCosBufferUnits = "";
            ReclipSnowCos_Checked = false;
            Roads_Checked = false;
            RoadsBufferDistance = "";
            ReclipRoads_Checked = false;
            LandOwnership_Checked = false;
            LandOwnershipBufferDistance = "";
            ReclipLandOwnership_Checked = false;
            LandCover_Checked = false;
            LandCoverBufferDistance = "";
            ReclipLandCover_Checked = false;
        }

        public ICommand CmdClipLayers
        {
            get
            {
                return new RelayCommand(async () => {
                    // Create from template
                    await ClipLayersAsync(ReclipSwe_Checked, ReclipPrism_Checked, ReclipSNOTEL_Checked, 
                        ReclipSnowCos_Checked, ReclipRoads_Checked, _reclipLandOwnership_Checked, _reclipLandCover_Checked);
                });
            }
        }

        private async Task ClipLayersAsync(bool clipSwe, bool clipPrism, bool clipSnotel, bool clipSnowCos,
            bool clipRoads, bool clipLandOwnership, bool clipLandcover)
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
                    clipLandOwnership == false && clipLandcover == false)
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
                    success = await AnalysisTools.ClipLayersAsync(Module1.Current.Aoi.FilePath, BA_Objects.DataSource.GetPrecipitationKey,
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
                        Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync), "An error occurred while clipping the roads!");
                    }
                }

                if (clipLandOwnership)
                {
                    string strOutputFc = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true)
                        + Constants.FILE_LAND_OWNERSHIP;
                    success = await AnalysisTools.ClipFeatureLayerAsync(Module1.Current.Aoi.FilePath, strOutputFc, Constants.DATA_TYPE_LAND_OWNERSHIP,
                        LandOwnershipBufferDistance, LandOwnershipBufferUnits);
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.ReclipLandOwnership_Checked = false;
                        layersPane.LandOwnership_Checked = true;
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync), "An error occurred while clipping the land ownership layer!");
                    }
                }

                if (clipLandcover)
                {
                    success = await AnalysisTools.ClipLandCoverAsync(Module1.Current.Aoi.FilePath, LandCoverBufferDistance, LandCoverBufferUnits);
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.ReclipLandCover_Checked = false;
                        layersPane.LandCover_Checked = true;
                    }
                    else
                    {
                         Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync), "An error occurred while clipping the land cover layer!");
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
