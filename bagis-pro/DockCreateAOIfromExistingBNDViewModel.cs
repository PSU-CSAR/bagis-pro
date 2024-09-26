using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using bagis_pro.BA_Objects;
using ExtensionMethod;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace bagis_pro
{
    internal class DockCreateAOIfromExistingBNDViewModel : DockPane
    {
        private const string _dockPaneID = "bagis_pro_DockCreateAOIfromExistingBND";

        protected DockCreateAOIfromExistingBNDViewModel() 
        {
            BA_ReturnCode success = GeneralTools.LoadBatchToolSettings();
            BufferDistance = Convert.ToDouble((string) Module1.Current.BatchToolSettings.AoiBufferDistance);
            string prismBufferUnits = (string)Module1.Current.BatchToolSettings.PrecipBufferUnits;
            double prismBufferDist = (double)Module1.Current.BatchToolSettings.PrecipBufferDistance;
            if (!string.IsNullOrEmpty(prismBufferUnits) && prismBufferUnits.Equals("Kilometers"))
            {
                PrismBufferDist = LinearUnit.Kilometers.ConvertTo(prismBufferDist, LinearUnit.Meters);
            }
            else
            {
                PrismBufferDist = prismBufferDist;
            }
            SlopeUnit defaultSlope = SlopeUnit.PctSlope; //BAGIS generates Slope in Degree
            SlopeUnitDescr = defaultSlope.GetEnumDescription();
            // DemElevUnit value is set before we get here in the BtnClick method because it is async
        }

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
        /// Hide the pane if SourceFile is empty. Means it wasn't triggered by the button
        /// </summary>
        /// <param name="isVisible"></param>
        protected override void OnShow(bool isVisible)
        {
            if (isVisible == true)
            {
                if (string.IsNullOrEmpty(SourceFile) == true)
                {
                    this.Hide();
                }
            }
        }

        /// <summary>
        /// Text shown near the top of the DockPane.
        /// </summary>
        private string _heading = "";
        private string _sourceFile = "";
        private string _outputWorkspace = "";
        private string _aoiName = "";
        private bool _dem10Checked;
        private bool _dem30Checked;
        private bool _smoothDemChecked;
        private int _filterCellHeight = 3;
        private int _filterCellWidth = 7;
        private bool _demExtentChecked = true;
        private bool _filledDemChecked = true;
        private bool _flowDirectChecked = true;
        private bool _flowAccumChecked = true;
        private bool _slopeChecked = true;
        private bool _aspectChecked = true;
        private bool _hillshadeChecked = true;
        private bool _bufferAoiChecked = true;
        private int _zFactor = 1;
        private double _bufferDistance;
        private double _prismBufferDist;
        private bool _inchesChecked = true;
        private bool _mmChecked;
        private string _slopeUnitDescr;
        private string _demElevUnit;

        public string Heading
        {
            get => _heading;
            set => SetProperty(ref _heading, value);
        }
        public string SourceFile
        {
            get => _sourceFile;
            set => SetProperty(ref _sourceFile, value);
        }
        public string OutputWorkspace
        {
            get => _outputWorkspace;
            set => SetProperty(ref _outputWorkspace, value);
        }
        public string AoiName
        {
            get => _aoiName;
            set => SetProperty(ref _aoiName, value);
        }
        public bool Dem10Checked
        {
            get => _dem10Checked;
            set => SetProperty(ref _dem10Checked, value);
        }
        public bool Dem30Checked
        {
            get => _dem30Checked;
            set => SetProperty(ref _dem30Checked, value);
        }
        public bool SmoothDemChecked
        {
            get => _smoothDemChecked;
            set => SetProperty(ref _smoothDemChecked, value);
        }
        public int FilterCellHeight
        {
            get => _filterCellHeight;
            set => SetProperty(ref _filterCellHeight, value);
        }
        public int FilterCellWidth
        {
            get => _filterCellWidth;
            set => SetProperty(ref _filterCellWidth, value);
        }
        public bool DemExtentChecked
        {
            get => _demExtentChecked;
            set => SetProperty(ref _demExtentChecked, value);
        }
        public bool FilledDemChecked
        {
            get => _filledDemChecked;
            set => SetProperty(ref _filledDemChecked, value);
        }
        public bool FlowDirectChecked
        {
            get => _flowDirectChecked;
            set => SetProperty(ref _flowDirectChecked, value);
        }
        public bool FlowAccumChecked
        {
            get => _flowAccumChecked;
            set => SetProperty(ref _flowAccumChecked, value);
        }
        public bool SlopeChecked
        {
            get => _slopeChecked;
            set => SetProperty(ref _slopeChecked, value);
        }
        public bool AspectChecked
        {
            get => _aspectChecked;
            set => SetProperty(ref _aspectChecked, value);
        }
        public bool HillshadeChecked
        {
            get => _hillshadeChecked;
            set => SetProperty(ref _hillshadeChecked, value);
        }
        public int ZFactor
        {
            get => _zFactor;
            set => SetProperty(ref _zFactor, value);
        }
        public bool BufferAoiChecked
        {
            get => _bufferAoiChecked;
            set => SetProperty(ref _bufferAoiChecked, value);
        }
        public double BufferDistance
        {
            get => _bufferDistance;
            set => SetProperty(ref _bufferDistance, value);
        }
        public double PrismBufferDist
        {
            get => _prismBufferDist;
            set => SetProperty(ref _prismBufferDist, value);
        }
        public bool InchesChecked
        {
            get => _inchesChecked;
            set => SetProperty(ref _inchesChecked, value);
        }
        public bool MmChecked
        {
            get => _mmChecked;
            set => SetProperty(ref _mmChecked, value);
        }

        public string SlopeUnitDescr
        {
            get => _slopeUnitDescr;
            set => SetProperty(ref _slopeUnitDescr, value);
        }
        public string DemElevUnit
        {
            get => _demElevUnit;
            set => SetProperty(ref _demElevUnit, value);
        }

        public System.Windows.Input.ICommand CmdOutputWorkspace
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    //Display the filter in an Open Item dialog
                    OpenItemDialog aNewFilter = new OpenItemDialog
                    {
                        Title = "Select basin folder",
                        MultiSelect = false,
                        Filter = ItemFilters.Folders
                    };
                    bool? ok = aNewFilter.ShowDialog();
                    bool bOk = ok ?? false;
                    if (bOk)
                    {
                        OutputWorkspace = "";
                        var arrFileNames = aNewFilter.Items;
                        foreach (var item in arrFileNames)
                        {
                            OutputWorkspace = item.Path;
                        }
                    }
                });
            }
        }
        public System.Windows.Input.ICommand CmdSelectAll
        {
            get
            {
                return new RelayCommand(() =>
                {
                    DemExtentChecked = true; 
                    FilledDemChecked = true;
                    FlowAccumChecked = true;
                    FlowDirectChecked = true;
                    SlopeChecked = true;
                    AspectChecked = true;
                    HillshadeChecked = true;
                });
            }
        }
        public System.Windows.Input.ICommand CmdSelectNone
        {
            get
            {
                return new RelayCommand(() =>
                {
                    DemExtentChecked = false;
                    FilledDemChecked = false;
                    FlowAccumChecked = false;
                    FlowDirectChecked = false;
                    SlopeChecked = false;
                    AspectChecked = false;
                    HillshadeChecked = false;
                });
            }
        }

        private RelayCommand _runGenerateAoiCommand;
        public ICommand CmdGenerateAoi
        {
            get
            {
                if (_runGenerateAoiCommand == null)
                    _runGenerateAoiCommand = new RelayCommand(RunGenerateAoiImplAsync, () => true);
                return _runGenerateAoiCommand;
            }
        }

        private async void RunGenerateAoiImplAsync(object param)
        {
            uint nStep;
            // Validation
            if (string.IsNullOrEmpty(OutputWorkspace) || string.IsNullOrEmpty(AoiName))
            {
                System.Windows.MessageBox.Show("Missing output workspace or AOI name!", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!System.IO.Directory.Exists(OutputWorkspace))
            {
                System.Windows.MessageBox.Show("Output workspace does not exist!", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            //verify filter size parameters
            if (SmoothDemChecked)
            {
                if (FilterCellHeight <= 0 || FilterCellWidth <= 0)
                {
                    System.Windows.MessageBox.Show("Invalid filter size! Please reenter.", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            //verify AOI buffer distance
            if (BufferAoiChecked)
            {               
                if (BufferDistance <= 0)
                {
                    // Switch back to default
                    BufferDistance = Convert.ToDouble((string)Module1.Current.BatchToolSettings.AoiBufferDistance);
                }
                if (PrismBufferDist <= 0)
                {
                    // Switch back to default
                    string prismBufferUnits = (string)Module1.Current.BatchToolSettings.PrecipBufferUnits;
                    double prismBufferDist = (double)Module1.Current.BatchToolSettings.PrecipBufferDistance;
                    if (!string.IsNullOrEmpty(prismBufferUnits) && prismBufferUnits.Equals("Kilometers"))
                    {
                        PrismBufferDist = LinearUnit.Kilometers.ConvertTo(prismBufferDist, LinearUnit.Meters);
                    }
                    else
                    {
                        PrismBufferDist = prismBufferDist;
                    }
                }
            }

            // Start populating aoi object
            Aoi oAoi = new Aoi();
            oAoi.FilePath = $@"{OutputWorkspace}\{AoiName}";
            //@ToDo: checking to see if we need to support GenerateAOIOnly; This also affects display of prism buffer in load method
            uint internalLayerCount = 32;
            nStep = internalLayerCount; // step counter for frmmessage

            Webservices ws = new Webservices();
            string strDem = await ws.GetDemUriAsync(Dem10Checked);
            if (Directory.Exists(oAoi.FilePath))
            {
                MessageBoxResult res = 
                    System.Windows.MessageBox.Show($@"{oAoi.FilePath} folder already exists. Overwrite?", "BAGIS-Pro", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    int layersRemoved = await MapTools.RemoveLayersInFolderAsync(oAoi.FilePath);
                    Directory.Delete(oAoi.FilePath, true);  // recursive delete removes everything in directory
                    Directory.CreateDirectory(oAoi.FilePath);
                }
            }
            else
            {
                Directory.CreateDirectory(oAoi.FilePath);
            }

            BA_ReturnCode success = await GeodatabaseTools.CreateGeodatabaseFoldersAsync(oAoi.FilePath, FolderType.AOI);
            if (success != BA_ReturnCode.Success)
            {
                System.Windows.MessageBox.Show("Unable to create GDBs! Please check disk space", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var progress = new ProgressDialog("Clipping DEM to AOI Folder", "Cancel", nStep + 2, true);
            var status = new CancelableProgressorSource(progress);
            status.Max = nStep + 2;
            progress.Show();
            await QueuedTask.Run(() =>
            {
                status.Progressor.Value += 1;
                status.Progressor.Status = (status.Progressor.Value * 100 / status.Progressor.Max) + @" % Completed";
            }, status.Progressor);

            double cellSize = await GeodatabaseTools.GetCellSizeAsync(new Uri(strDem), "", WorkspaceType.ImageServer);
            // If DEMCellSize could not be calculated, the DEM is likely invalid
            if (cellSize <= 0)
            {
                System.Windows.MessageBox.Show($@"{strDem} is invalid and cannot be used as the DEM layer. Check your BAGIS settings.", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                progress.Hide();
                return;
            }

            //create a raster version of the AOI boundary
            success = await GeoprocessingTools.AddFieldAsync(SourceFile, "RASTERID", "INTEGER");
            if (success == BA_ReturnCode.Success)
            {
                success = await GeodatabaseTools.UpdateFeatureAttributeNumericAsync(new Uri(Path.GetDirectoryName(SourceFile)), 
                    Path.GetFileName(SourceFile), new QueryFilter(), "RASTERID", 1);
            }


        }

    }
}
