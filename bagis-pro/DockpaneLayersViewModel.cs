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
      private bool _clipIsRunning;
      private RelayCommand _clipLayersCommand;


      protected DockpaneLayersViewModel() {  }  

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
      /// Text shown near the top of the DockPane.
      /// </summary>
	  private string _heading = "Manage Layers";
      public string Heading
      {
        get { return _heading; }
        set
        {
          SetProperty(ref _heading, value, () => Heading);
        }
      }

      public RelayCommand CmdClipLayers
        {
            get
            {
                return _clipLayersCommand
                  ?? (_clipLayersCommand = new RelayCommand(
                    async () =>
                            {
                                if (_clipIsRunning)
                                {
                                    return;
                                }

                                _clipIsRunning = true;
                                CmdClipLayers.RaiseCanExecuteChanged();

                                if (String.IsNullOrEmpty(Module1.Current.Aoi.Name))
                                {
                                    MessageBox.Show("No AOI selected for analysis !!", "BAGIS-PRO");
                                    return;
                                }

                                
                                BA_ReturnCode success = await AnalysisTools.ClipSnotelSWELayersAsync();

                                if (success == BA_ReturnCode.Success)
                                {
                                    MessageBox.Show("Analysis layers clipped!!", "BAGIS-PRO");
                                }
                                else
                                {
                                    MessageBox.Show("An error occurred while trying to clip the layers !!", "BAGIS-PRO");
                                }

                                _clipIsRunning = false;
                                CmdClipLayers.RaiseCanExecuteChanged();
                            },
                            () => !_clipIsRunning));
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
