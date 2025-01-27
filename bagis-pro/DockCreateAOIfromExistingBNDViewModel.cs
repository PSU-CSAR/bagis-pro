using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using bagis_pro.BA_Objects;
using ExtensionMethod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace bagis_pro
{
    internal class DockCreateAOIfromExistingBNDViewModel : DockPane
    {
        private const string _dockPaneID = "bagis_pro_DockCreateAOIfromExistingBND";

        protected DockCreateAOIfromExistingBNDViewModel() 
        {
            BA_ReturnCode success = GeneralTools.LoadBagisSettings();
            BufferDistance = Convert.ToDouble((string) Module1.Current.BagisSettings.AoiBufferDistance);
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
        private bool _smoothDemChecked;
        private int _filterCellHeight = 3;
        private int _filterCellWidth = 7;
        private bool _bufferAoiChecked = true;
        private int _zFactor = 1;
        private double _bufferDistance;
        private string _slopeUnitDescr;
        private string _demElevUnit;
        private string _aoiBufferUnits;

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

        public string AoiBufferUnits
        {
            get => _aoiBufferUnits;
            set => SetProperty(ref _aoiBufferUnits, value);
        }

        public System.Windows.Input.ICommand CmdOutputWorkspace
        {
            get
            {
                return new RelayCommand( () =>
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
            uint nStep= 8;
            int intWait = 500;
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

            // verify dem is available
            Webservices ws = new Webservices();
            string strSourceDem = (string)Module1.Current.BagisSettings.DemUri;
            bool bDemImageService = false;
            bool bDemRaster = false;
            if (!string.IsNullOrEmpty(strSourceDem))
            {
                bDemImageService = await Webservices.ValidateImageService(strSourceDem);
                if (!bDemImageService)
                {
                    string strDirectory = Path.GetDirectoryName(strSourceDem);
                    string strRaster = Path.GetFileName(strSourceDem);
                    if (!string.IsNullOrEmpty(strDirectory) && !string.IsNullOrEmpty(strRaster))
                    {
                        Uri gdbUri = new Uri(strDirectory);
                        bDemRaster = await GeodatabaseTools.RasterDatasetExistsAsync(gdbUri, strRaster);
                    }                    
                }
            }
            if (bDemImageService == false && bDemRaster == false)
            {
                System.Windows.MessageBox.Show("Invalid DEM. AOI cannot be created!", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            //verify AOI buffer distance
            if (BufferAoiChecked)
            {               
                if (BufferDistance <= 0)
                {
                    // Switch back to default
                    BufferDistance = Convert.ToDouble((string)Module1.Current.BagisSettings.AoiBufferDistance);
                }
            }

            // Start populating aoi object
            Map oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
            Aoi oAoi = new Aoi();
            oAoi.FilePath = $@"{OutputWorkspace}\{AoiName}";
            try
            {
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
                    else
                    {
                        return;
                    }
                }
                else
                {
                    Directory.CreateDirectory(oAoi.FilePath);
                }
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show("An unknown error occurred while trying to create the AOI directory!", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;

            }
            
            var progress = new ProgressDialog("Processing ...", "Cancel", 100, false);
            var status = new CancelableProgressorSource(progress);
            status.Max = 100;
            progress.Show();
            await QueuedTask.Run(() =>
            {
                status.Progressor.Value = 0;    // reset the progressor's value back to 0 between GP tasks
                status.Progressor.Message = $@"Generating AOI boundary... (step 1 of {nStep})";
                //block the CIM for a second
                Task.Delay(intWait).Wait();

            }, status.Progressor);

            BA_ReturnCode success = await GeodatabaseTools.CreateGeodatabaseFoldersAsync(oAoi.FilePath, FolderType.AOI, status.Progressor);
            if (success != BA_ReturnCode.Success)
            {
                System.Windows.MessageBox.Show("Unable to create GDBs! Please check disk space", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            double cellSize = -1;
            if (bDemImageService)
            {
                cellSize = await GeodatabaseTools.GetCellSizeAsync(new Uri(strSourceDem), "", WorkspaceType.ImageServer);
            }
            else
            {
                string strGdb = Path.GetDirectoryName(strSourceDem);
                cellSize = await GeodatabaseTools.GetCellSizeAsync(new Uri(strGdb), Path.GetFileName(strSourceDem), WorkspaceType.Raster);
            }
            // If DEM CellSize could not be calculated, the DEM is likely invalid
            if (cellSize <= 0)
            {
                System.Windows.MessageBox.Show($@"{strSourceDem} is invalid and cannot be used as the DEM layer. Check your BAGIS settings.", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                progress.Hide();
                return;
            }

            //create a raster version of the AOI boundary
            string fieldRasterId = "RASTERID";
            if (!await GeodatabaseTools.AttributeExistsShapefileAsync(new Uri(Path.GetDirectoryName(SourceFile)), 
                Path.GetFileName(SourceFile),fieldRasterId))
            {
                success = await GeoprocessingTools.AddFieldAsync(SourceFile, fieldRasterId, "INTEGER", status);
                if (success == BA_ReturnCode.Success)
                {
                    success = await GeodatabaseTools.UpdateFeatureAttributeNumericAsync(new Uri(Path.GetDirectoryName(SourceFile)),
                        Path.GetFileName(SourceFile), new QueryFilter(), fieldRasterId, 1);
                    if (success != BA_ReturnCode.Success)
                    {
                        System.Windows.MessageBox.Show("Input shapefile does not contain valid polygons! Please visually inspect the file.", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                        progress.Hide();
                        return;
                    }
                }
            }
  
            string aoiRasterPath = $@"{GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi, true)}{Constants.FILE_AOI_RASTER}";
            if (success == BA_ReturnCode.Success)
            {
                IGPResult gpResult = await QueuedTask.Run(() =>
                {
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath, snapRaster: strSourceDem);
                    var parameters = Geoprocessing.MakeValueArray(SourceFile, fieldRasterId, aoiRasterPath, cellSize);
                    return Geoprocessing.ExecuteToolAsync("FeatureToRaster_conversion", parameters, environments,
                                status.Progressor, GPExecuteToolFlags.AddToHistory);
                });
                if (gpResult.IsFailed)
                {
                    System.Windows.MessageBox.Show("Unable to convert input polygon to raster.", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                    success = BA_ReturnCode.WriteError;
                    progress.Hide();
                    return;
                }
            }

            //rasterize the raster to vector
            if (success == BA_ReturnCode.Success)
            {
                string aoiVectorPath = $@"{GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi, true)}{Constants.FILE_AOI_VECTOR}";
                IGPResult gpResult = await QueuedTask.Run(() =>
                {
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath);
                    var parameters = Geoprocessing.MakeValueArray(aoiRasterPath, aoiVectorPath, "NO_SIMPLIFY");
                    return Geoprocessing.ExecuteToolAsync("RasterToPolygon_conversion", parameters, environments,
                                status.Progressor, GPExecuteToolFlags.AddToHistory);
                });
                if (gpResult.IsFailed)
                {
                    System.Windows.MessageBox.Show("Unable to convert input raster to polygon.", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                    success = BA_ReturnCode.WriteError;
                    progress.Hide();
                    return;
                }
                else
                {
                    success = await GeodatabaseTools.AddAOIVectorAttributesAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi)), AoiName, status);
                    if (success != BA_ReturnCode.Success)
                    {
                        System.Windows.MessageBox.Show("Unable to add or populate fields to aoi_v", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                        progress.Hide();
                        return;
                    }
                }  
            }

            // clip DEM then save it
            string strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi, true) +
                         Constants.FILE_AOI_VECTOR;
            await QueuedTask.Run(() =>
            {
                status.Progressor.Value = 0;
                status.Progressor.Message = $@"Clipping DEM... (step 2 of {nStep})";
                //block the CIM for a second
                Task.Delay(intWait).Wait();

            }, status.Progressor);

            string aoiGdbPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi);
            string strOutputFeatures = "";
            string aoiBufferDistance = Convert.ToString(BufferDistance); // Default buffer distance is meters
            if (success == BA_ReturnCode.Success)
            {
                if (!BufferAoiChecked)
                {
                    aoiBufferDistance = "1"; //one meter buffer to dissolve polygons connected at a point
                }
                strOutputFeatures = $@"{aoiGdbPath}\{Constants.FILE_AOI_BUFFERED_VECTOR}";
                success = await GeoprocessingTools.BufferAsync(strPath, strOutputFeatures, $@"{aoiBufferDistance} {Constants.UNITS_METERS}", "ALL", status.Progressor);                
            }

            string surfacesGdbPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Surfaces);
            if (success == BA_ReturnCode.Success)
            {
                string tempDem = "originaldem";
                string tempOutput = $@"{surfacesGdbPath}\{tempDem}";
                success = await AnalysisTools.ClipRasterLayerNoBufferAsync(oAoi.FilePath, strOutputFeatures, Constants.FILE_AOI_BUFFERED_VECTOR,
                    strSourceDem, tempOutput, strSourceDem, status.Progressor);

                string strDem = $@"{surfacesGdbPath}\{Constants.FILE_DEM}";
                if (success == BA_ReturnCode.Success)
                {
                    if (SmoothDemChecked)
                    {
                        string envExtent = await GeodatabaseTools.GetEnvelope(aoiGdbPath, Constants.FILE_AOI_BUFFERED_VECTOR);
                        string neighborhood = "Rectangle " + FilterCellWidth + " " + FilterCellHeight + " CELL";
                        var parameters = Geoprocessing.MakeValueArray(tempOutput, strDem, neighborhood, "MEAN", "DATA");
                        var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath, extent: envExtent, mask: $@"{surfacesGdbPath}\{tempDem}", snapRaster: strSourceDem);
                        var gpResult = await Geoprocessing.ExecuteToolAsync("FocalStatistics_sa", parameters, environments,
                            status.Progressor, GPExecuteToolFlags.AddToHistory);
                        if (gpResult.IsFailed)
                        {
                            success = BA_ReturnCode.UnknownError;
                        }
                        else
                        {
                            success = BA_ReturnCode.Success;
                            // delete original dem
                            success = await GeoprocessingTools.DeleteDatasetAsync(tempOutput, status.Progressor);
                        }
                    }
                    else
                    {
                        var parameters = Geoprocessing.MakeValueArray(tempOutput, strDem);
                        var gpResult = await Geoprocessing.ExecuteToolAsync("Rename_management", parameters, null,
                            status.Progressor, GPExecuteToolFlags.AddToHistory);
                        if (gpResult.IsFailed)
                        {
                            success = BA_ReturnCode.UnknownError;
                        }
                        else
                        {
                            success = BA_ReturnCode.Success;
                        }
                    }
                }
                Uri uri = null;
                string filledDemPath = $@"{surfacesGdbPath}\{Constants.FILE_DEM_FILLED}";
                await QueuedTask.Run(() =>
                {
                    status.Progressor.Value = 0;
                    status.Progressor.Message = $@"Filling DEM... (step 3 of {nStep})";
                    //block the CIM for a second
                    Task.Delay(intWait).Wait();

                }, status.Progressor);
                if (success == BA_ReturnCode.Success)
                {
                    var parameters = Geoprocessing.MakeValueArray(strDem, filledDemPath);
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath, mask: $@"{strDem}", snapRaster: strSourceDem);
                    var gpResult = await Geoprocessing.ExecuteToolAsync("Fill_sa", parameters, environments,
                        status.Progressor, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        success = BA_ReturnCode.UnknownError;
                        progress.Hide();
                        return;
                    }
                    else
                    {
                        StringBuilder sbDem = new StringBuilder();
                        //We need to add a new tag at "/metadata/dataIdInfo/searchKeys/keyword"
                        sbDem.Append(Constants.META_TAG_PREFIX);
                        // Elevation Units
                        sbDem.Append(Constants.META_TAG_ZUNIT_CATEGORY + MeasurementUnitType.Elevation + "; ");
                        sbDem.Append(Constants.META_TAG_ZUNIT_VALUE + DemElevUnit + "; ");
                        // Buffer Distance
                        sbDem.Append(Constants.META_TAG_BUFFER_DISTANCE + aoiBufferDistance + "; ");
                        // X Units
                        sbDem.Append(Constants.META_TAG_XUNIT_VALUE + AoiBufferUnits + "; ");
                        sbDem.Append(Constants.META_TAG_SUFFIX);
                        if (success == BA_ReturnCode.Success)
                        {
                            //Update the metadata
                            await QueuedTask.Run(() =>
                            {
                                var fc = ItemFactory.Instance.Create(filledDemPath,
                                ItemFactory.ItemType.PathItem);
                                if (fc != null)
                                {
                                    string strXml = string.Empty;
                                    strXml = fc.GetXml();
                                    System.Xml.XmlDocument xmlDocument = GeneralTools.UpdateMetadata(strXml, Constants.META_TAG_XPATH, sbDem.ToString(),
                                        Constants.META_TAG_PREFIX.Length);
                                    fc.SetXml(xmlDocument.OuterXml);
                                }
                            });
                        }
                        success = BA_ReturnCode.Success;
                    }
                }
                await QueuedTask.Run(() =>
                {
                    status.Progressor.Value = 0;
                    status.Progressor.Message = $@"Calculating Slope... (step 4 of {nStep})";
                    //block the CIM for a second
                    Task.Delay(intWait).Wait();
                }, status.Progressor);

                if (success == BA_ReturnCode.Success)
                {
                    double zFactor = 1;
                    if (!DemElevUnit.Equals("Meters"))
                    {
                        zFactor = 0.3048;
                    }
                    var parameters = Geoprocessing.MakeValueArray(filledDemPath, $@"{surfacesGdbPath}\{Constants.FILE_SLOPE}",
                        "PERCENT_RISE", zFactor);
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath, snapRaster: strSourceDem);
                    var gpResult = await Geoprocessing.ExecuteToolAsync("Slope_sa", parameters, environments,
                        status.Progressor, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        success = BA_ReturnCode.UnknownError;
                        progress.Hide();
                        return;
                    }
                    StringBuilder sbDem = new StringBuilder();
                    //We need to add a new tag at "/metadata/dataIdInfo/searchKeys/keyword"
                    sbDem.Append(Constants.META_TAG_PREFIX);
                    // Elevation Units
                    sbDem.Append(Constants.META_TAG_ZUNIT_CATEGORY + MeasurementUnitType.Slope + "; ");
                    sbDem.Append(Constants.META_TAG_ZUNIT_VALUE + SlopeUnitDescr + "; ");
                    sbDem.Append(Constants.META_TAG_SUFFIX);
                    if (success == BA_ReturnCode.Success)
                    {
                        //Update the metadata
                        await QueuedTask.Run(() =>
                        {
                            var fc = ItemFactory.Instance.Create($@"{surfacesGdbPath}\{Constants.FILE_SLOPE}",
                            ItemFactory.ItemType.PathItem);
                            if (fc != null)
                            {
                                string strXml = string.Empty;
                                strXml = fc.GetXml();
                                System.Xml.XmlDocument xmlDocument = GeneralTools.UpdateMetadata(strXml, Constants.META_TAG_XPATH, sbDem.ToString(),
                                    Constants.META_TAG_PREFIX.Length);
                                fc.SetXml(xmlDocument.OuterXml);
                            }
                        });

                    }
                }

                await QueuedTask.Run(() =>
                {
                    status.Progressor.Value = 0;
                    status.Progressor.Message = $@"Calculating Aspect... (step 5 of {nStep})";
                    //block the CIM for a second
                    Task.Delay(intWait).Wait();

                }, status.Progressor);

                if (success == BA_ReturnCode.Success)
                {
                    var parameters = Geoprocessing.MakeValueArray(filledDemPath,
                        $@"{surfacesGdbPath}\{Constants.FILE_ASPECT}");
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath, snapRaster: strSourceDem);
                    var gpResult = await Geoprocessing.ExecuteToolAsync("Aspect_sa", parameters, environments,
                        status.Progressor, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        success = BA_ReturnCode.UnknownError;
                        progress.Hide();
                        return;
                    }
                }

                await QueuedTask.Run(() =>
                {
                    status.Progressor.Value = 0;
                    status.Progressor.Message = $@"Calculating Flow Direction... (step 6 of {nStep})";
                    //block the CIM for a second
                    Task.Delay(intWait).Wait();

                }, status.Progressor);
                if (success == BA_ReturnCode.Success)
                {
                    var parameters = Geoprocessing.MakeValueArray(filledDemPath,
                        $@"{surfacesGdbPath}\{Constants.FILE_FLOW_DIRECTION}");
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath, snapRaster: strSourceDem);
                    var gpResult = await Geoprocessing.ExecuteToolAsync("FlowDirection_sa", parameters, environments,
                        status.Progressor, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        success = BA_ReturnCode.UnknownError;
                        progress.Hide();
                        return;
                    }
                }
                await QueuedTask.Run(() =>
                {
                    status.Progressor.Value = 0;
                    status.Progressor.Message = $@"Calculating Flow Accumulation... (step 7 of {nStep})";
                    //block the CIM for a second
                    Task.Delay(intWait).Wait();
                }, status.Progressor);
                if (success == BA_ReturnCode.Success)
                {
                    var parameters = Geoprocessing.MakeValueArray($@"{surfacesGdbPath}\{Constants.FILE_FLOW_DIRECTION}",
                        $@"{surfacesGdbPath}\{ Constants.FILE_FLOW_ACCUMULATION}");
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath, snapRaster: strSourceDem);
                    var gpResult = await Geoprocessing.ExecuteToolAsync("FlowAccumulation_sa", parameters, environments,
                        status.Progressor, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        success = BA_ReturnCode.UnknownError;
                        progress.Hide();
                        return;
                    }
                    //if (FlowAccumChecked)
                    //{
                    //    uri = new Uri($@"{surfacesGdbPath}\{Constants.FILE_FLOW_ACCUMULATION}");
                    //    await MapTools.DisplayRasterStretchSymbolAsync(Constants.MAPS_DEFAULT_MAP_NAME, uri, "Flow Accumulation", "ArcGIS Colors", "Black to White", 0);
                    //    await QueuedTask.Run(() =>
                    //    {
                    //        var rasterLayer = oMap.GetLayersAsFlattenedList().OfType<RasterLayer>().Where(f =>
                    //            f.Name == "Flow Accumulation").FirstOrDefault();                            
                    //        CIMRasterColorizer rColorizer = rasterLayer.GetColorizer();
                    //        // Check if the colorizer is an RGB colorizer.
                    //        if (rColorizer is CIMRasterStretchColorizer stretchColorizer)
                    //        {
                    //            // Update RGB colorizer properties.
                    //            stretchColorizer.StretchType = RasterStretchType.HistogramEqualize;
                    //            stretchColorizer.StatsType = RasterStretchStatsType.AreaOfView;
                    //            // Update the raster layer with the changed colorizer.
                    //            rasterLayer.SetColorizer(stretchColorizer);
                    //        }
                    //    });
                    //}
                }
                //create pourpoint using the max of flow_acc value within the AOI
                if (success == BA_ReturnCode.Success)
                {
                    //get the max of flow acc
                    double dblMax = -1;
                    var parameters = Geoprocessing.MakeValueArray($@"{surfacesGdbPath}\{Constants.FILE_FLOW_ACCUMULATION}", "MAXIMUM");
                    IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, null,
                        status.Progressor, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        success = BA_ReturnCode.UnknownError;
                        progress.Hide();
                        return;
                    }
                    else
                    {
                        bool bSuccess = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMax);
                        string ppRaster = "ppraster";
                        if (bSuccess)
                        {                            
                            success = await GeoprocessingTools.ConAsync($@"{surfacesGdbPath}\{Constants.FILE_FLOW_ACCUMULATION}", "1",
                                $@"{surfacesGdbPath}\{ppRaster}", dblMax, status.Progressor);
                            if (success == BA_ReturnCode.Success)
                            {
                                success = await GeoprocessingTools.RasterToPointAsync($@"{surfacesGdbPath}\{ppRaster}", Constants.FIELD_VALUE,
                                   $@"{aoiGdbPath}\{Constants.FILE_POURPOINT}", status.Progressor);
                            }
                        }
                        if (success == BA_ReturnCode.Success)
                        {
                            success = await GeodatabaseTools.AddPourpointAttributesAsync(oAoi.FilePath, AoiName, Constants.VALUE_NOT_SPECIFIED, "", status);
                        }
                        if (await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(surfacesGdbPath),ppRaster))
                        {
                            success = await GeoprocessingTools.DeleteDatasetAsync($@"{surfacesGdbPath}\{ppRaster}", status.Progressor);
                        }
                    }
                }
                await QueuedTask.Run(() =>
                {
                    status.Progressor.Value = 0;
                    status.Progressor.Message = $@"Calculating Hillshade... (step 8 of {nStep})";
                    //block the CIM for a second
                    Task.Delay(intWait).Wait();

                }, status.Progressor);
                // Create Hillshade layer
                if (success == BA_ReturnCode.Success)
                {
                    var parameters = Geoprocessing.MakeValueArray($@"{surfacesGdbPath}\{Constants.FILE_DEM_FILLED}",
                        $@"{surfacesGdbPath}\{Constants.FILE_HILLSHADE}", "","","", ZFactor);
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath, snapRaster: strSourceDem);
                    var gpResult = await Geoprocessing.ExecuteToolAsync("Hillshade_sa", parameters, environments,
                        status.Progressor, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        success = BA_ReturnCode.UnknownError;
                        progress.Hide();
                        return;
                    }

                    // Display DEM Extent layer - aoi_v            
                    if (oMap != null)
                    {
                        strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi, true) +
                         Constants.FILE_AOI_VECTOR;
                        Uri aoiUri = new Uri(strPath);
                        success = await MapTools.AddAoiBoundaryToMapAsync(aoiUri, ColorFactory.Instance.RedRGB, Constants.MAPS_DEFAULT_MAP_NAME, Constants.MAPS_BASIN_BOUNDARY);
                        if (success != BA_ReturnCode.Success)
                        {
                            System.Windows.MessageBox.Show("Unable to add the extent layer", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        uri = new Uri($@"{surfacesGdbPath}\{Constants.FILE_HILLSHADE}");
                        await MapTools.DisplayRasterStretchSymbolAsync(Constants.MAPS_DEFAULT_MAP_NAME, uri, Constants.MAPS_HILLSHADE, "ArcGIS Colors", "Black to White", 0);

                    }
                }
            }
            System.Windows.Forms.MessageBox.Show("AOI was created!", "BAGIS-Pro");
        }

    }
}
