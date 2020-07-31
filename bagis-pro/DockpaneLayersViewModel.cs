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
        private string _heading = "Manage Layers";
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


        public string Heading
      {
        get { return _heading; }
        set
        {
          SetProperty(ref _heading, value, () => Heading);
        }
      }

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

        public void ResetView()
        {
            Prism_Checked = false;
            PrismBufferDistance = Convert.ToString(Module1.Current.Settings.m_prismBufferDistance);
            PrismBufferUnits = Convert.ToString(Module1.Current.Settings.m_prismBufferUnits);
            ReclipPrism_Checked = false;
            SWE_Checked = false;
            SWEBufferDistance = Convert.ToString(Module1.Current.Settings.m_prismBufferDistance);
            SWEBufferUnits = Convert.ToString(Module1.Current.Settings.m_prismBufferUnits);
            ReclipSwe_Checked = false;
            SNOTEL_Checked = false;
            SnotelBufferDistance = "";
            SnotelBufferUnits = Convert.ToString(Module1.Current.Settings.m_snotelBufferUnits);
            ReclipSNOTEL_Checked = false;
            SnowCos_Checked = false;
            SnowCosBufferDistance = "";
            SnowCosBufferUnits = Convert.ToString(Module1.Current.Settings.m_snotelBufferUnits);
            ReclipSnowCos_Checked = false;
            Roads_Checked = false;
            RoadsBufferDistance = "";
            RoadsBufferUnits = Convert.ToString(Module1.Current.Settings.m_roadsBufferUnits);
            ReclipRoads_Checked = false;
        }

        public ICommand CmdClipLayers
        {
            get
            {
                return new RelayCommand(async () => {
                    // Create from template
                    await ClipLayersAsync(ReclipSwe_Checked, ReclipPrism_Checked, ReclipSNOTEL_Checked, 
                        ReclipSnowCos_Checked, ReclipRoads_Checked);
                });
            }
        }

        private async Task ClipLayersAsync(bool clipSwe, bool clipPrism, bool clipSnotel, bool clipSnowCos,
            bool clipRoads)
        {
            try
            {
                if (String.IsNullOrEmpty(Module1.Current.Aoi.Name))
                {
                    MessageBox.Show("No AOI selected for analysis !!", "BAGIS-PRO");
                    return;
                }

                if (clipSwe == false && clipPrism == false && 
                    clipSnotel == false && clipSnowCos == false && clipRoads == false)
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
                var fc = ItemFactory.Instance.Create(strPrismPath, ItemFactory.ItemType.PathItem);
                string pBufferDistance = "";
                string pBufferUnits = "";

                await QueuedTask.Run(() =>
                {
                    if (fc != null)
                    {
                        string strXml = string.Empty;
                        strXml = fc.GetXml();
                        //check metadata was returned
                        string strBagisTag = GeneralTools.GetBagisTag(strXml);
                        if (!string.IsNullOrEmpty(strBagisTag))
                        {
                            pBufferDistance = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_BUFFER_DISTANCE, ';');
                            pBufferUnits = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_XUNIT_VALUE, ';');
                        }
                    }   
                });

                if (clipPrism)
                {
                    success = await AnalysisTools.ClipLayersAsync(Module1.Current.Aoi.FilePath, Constants.DATA_TYPE_PRECIPITATION,
                        pBufferDistance, pBufferUnits, PrismBufferDistance, PrismBufferUnits);
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.ReclipPrism_Checked = false;
                        layersPane.Prism_Checked = true;
                    }
                }
                if (clipSwe)
                {
                    success = await AnalysisTools.ClipLayersAsync(Module1.Current.Aoi.FilePath, Constants.DATA_TYPE_SWE,
                        pBufferDistance, pBufferUnits, SWEBufferDistance, SWEBufferUnits);
                    // Calculate and record overall min and max for symbology
                    if (success == BA_ReturnCode.Success)
                    {
                        double dblOverallMin = 9999;
                        double dblOverallMax = -9999;
                        string strLayersGdb = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true);
                        foreach (var fName in Constants.FILES_SNODAS_SWE)
                        {
                            string strOutputPath = strLayersGdb + fName;
                            double dblMin = -1;
                            var parameters = Geoprocessing.MakeValueArray(strOutputPath, "MINIMUM");
                            var environments = Geoprocessing.MakeEnvironmentArray(workspace: Module1.Current.Aoi.FilePath);
                            IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                            bool isDouble = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMin);
                            if (isDouble && dblMin < dblOverallMin)
                            {
                                dblOverallMin = dblMin;
                                Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
                                    "Updated overall SWE minimum to " + dblOverallMin);
                            }
                            double dblMax = -1;
                            parameters = Geoprocessing.MakeValueArray(strOutputPath, "MAXIMUM");
                            gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                            isDouble = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMax);
                            if (isDouble && dblMax > dblOverallMax)
                            {
                                dblOverallMax = dblMax;
                                Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
                                    "Updated overall SWE maximum to " + dblOverallMax);
                            }
                        }
                        // Save overall min and max in metadata
                        IDictionary<string, BA_Objects.DataSource> dictLocalDataSources = GeneralTools.QueryLocalDataSources();
                        if (dictLocalDataSources.ContainsKey(Constants.DATA_TYPE_SWE))
                        {
                            BA_Objects.DataSource dataSource = dictLocalDataSources[Constants.DATA_TYPE_SWE];
                            dataSource.minValue = dblOverallMin;
                            dataSource.maxValue = dblOverallMax;
                            success = GeneralTools.SaveDataSourcesToFile(dictLocalDataSources);
                            Module1.Current.ModuleLogManager.LogDebug(nameof(ClipLayersAsync),
                                "Updated settings overall min and max metadata for SWE");
                        }
                        else
                        {
                            MessageBox.Show("An error occurred while trying to update the SWE layer metadata!!");
                            Module1.Current.ModuleLogManager.LogError(nameof(ClipLayersAsync),
                                "Unable to locate SWE metadata entry to update");
                        }
                        success = GeneralTools.SaveDataSourcesToFile(dictLocalDataSources);

                        if (success == BA_ReturnCode.Success)
                        {
                            layersPane.ReclipSwe_Checked = false;
                            layersPane.SWE_Checked = true;
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
                        layersPane.ReclipPrism_Checked = false;
                        layersPane.Prism_Checked = true;
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
