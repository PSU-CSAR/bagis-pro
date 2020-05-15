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
        private bool _reclipSwe_Checked = true;
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
