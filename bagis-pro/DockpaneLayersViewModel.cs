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
    internal class DockpaneLayersViewModel : DockPane
    {   
      private const string _dockPaneID = "bagis_pro_DockpaneLayers";

        protected DockpaneLayersViewModel()
        {

        }

        /// <summary>
        /// Show the DockPane.
        /// </summary>
        internal static void Show()
      {        
        DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
        if (pane == null)
          return;

            // Don't show if aoi condition not enabled
            if (!FrameworkApplication.State.Contains("Aoi_Selected_State"))
            {
                return;
            }
            pane.Activate();
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
            SnotelBufferDistance = Convert.ToString(Module1.Current.Settings.m_snotelBufferDistance);
            SnotelBufferUnits = Convert.ToString(Module1.Current.Settings.m_snotelBufferUnits);
            ReclipSNOTEL_Checked = false;
            SnowCos_Checked = false;
            SnowCosBufferDistance = Convert.ToString(Module1.Current.Settings.m_snotelBufferDistance);
            SnowCosBufferUnits = Convert.ToString(Module1.Current.Settings.m_snotelBufferUnits);
            ReclipSnowCos_Checked = false;
        }

        public ICommand CmdClipLayers
        {
            get
            {
                return new RelayCommand(async () => {
                    // Create from template
                    await ClipLayersAsync(_reclipSwe_Checked);
                });
            }
        }

        private async Task ClipLayersAsync(bool clipSwe)
        {
            try
            {
                if (String.IsNullOrEmpty(Module1.Current.Aoi.Name))
                {
                    MessageBox.Show("No AOI selected for analysis !!", "BAGIS-PRO");
                    return;
                }

                BA_ReturnCode success = BA_ReturnCode.Success;
                if (clipSwe)
                {
                    success = await AnalysisTools.ClipSnotelSWELayersAsync(Module1.Current.Aoi.FilePath);
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
