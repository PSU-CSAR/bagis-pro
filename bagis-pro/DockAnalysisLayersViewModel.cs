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

        public void ResetView()
        {
            RepresentedArea_Checked = false;
            PrismZones_Checked = false;
        }

        public ICommand CmdGenerateLayers
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    // Create from template
                    await GenerateLayersAsync(RepresentedArea_Checked, PrismZones_Checked);
                });
            }
        }

        private async Task GenerateLayersAsync(bool calculateRepresented, bool calculatePrism)
        {
            try
            {
                if (String.IsNullOrEmpty(Module1.Current.Aoi.Name))
                {
                    MessageBox.Show("No AOI selected for analysis !!", "BAGIS-PRO");
                    return;
                }

                if (calculateRepresented == false && calculatePrism == false)
                {
                    MessageBox.Show("No layers selected to generate !!", "BAGIS-PRO");
                    return;
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
                    string strLayer = System.IO.Path.GetFileName(Module1.Current.Settings.m_precipFile);
                    success = await AnalysisTools.CalculatePrismZonesAsync(Module1.Current.Aoi.FilePath, strLayer);
                    if (success == BA_ReturnCode.Success)
                    {
                        layersPane.RepresentedArea_Checked = false;
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
