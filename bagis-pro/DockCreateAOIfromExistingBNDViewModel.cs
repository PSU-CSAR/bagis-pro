﻿using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Data.UtilityNetwork.Trace;
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
using ArcGIS.Desktop.Internal.GeoProcessing;
using ArcGIS.Desktop.Internal.Mapping.Controls.Histogram;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using bagis_pro.BA_Objects;
using ExtensionMethod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
            Aoi oAoi = new Aoi();
            oAoi.FilePath = $@"{OutputWorkspace}\{AoiName}";
            uint internalLayerCount = 32;
            nStep = internalLayerCount; // step counter for frmmessage

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
                success = await GeoprocessingTools.AddFieldAsync(SourceFile, fieldRasterId, "INTEGER");
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
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath);
                    var parameters = Geoprocessing.MakeValueArray(SourceFile, fieldRasterId, aoiRasterPath, cellSize);
                    return Geoprocessing.ExecuteToolAsync("FeatureToRaster_conversion", parameters, environments,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
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
                    var parameters = Geoprocessing.MakeValueArray(aoiRasterPath, aoiVectorPath);
                    return Geoprocessing.ExecuteToolAsync("RasterToPolygon_conversion", parameters, environments,
                                CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
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
                    success = await GeodatabaseTools.AddAOIVectorAttributesAsync(new Uri(GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi)), AoiName);
                    if (success != BA_ReturnCode.Success)
                    {
                        System.Windows.MessageBox.Show("Unable to add or populate fields to aoi_v", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                        progress.Hide();
                        return;
                    }
                }  
            }

            // Display DEM Extent layer - aoi_v            
            string strPath = "";
            Map oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
            if (oMap != null && DemExtentChecked)
            {
                strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi, true) +
                 Constants.FILE_AOI_VECTOR;
                Uri aoiUri = new Uri(strPath);
                success = await MapTools.AddAoiBoundaryToMapAsync(aoiUri, ColorFactory.Instance.BlackRGB, Constants.MAPS_DEFAULT_MAP_NAME, Constants.MAPS_BASIN_BOUNDARY);
                if (success != BA_ReturnCode.Success)
                {
                    System.Windows.MessageBox.Show("Unable to add the extent layer", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                    progress.Hide();
                    return;
                }
            }

            // clip DEM then save it
            await QueuedTask.Run(() =>
            {
                status.Progressor.Value += 1;
                status.Progressor.Message = $@"Clipping DEM... (step 1 of {nStep})";
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
                success = await GeoprocessingTools.BufferAsync(strPath, strOutputFeatures, $@"{aoiBufferDistance} {Constants.UNITS_METERS}", "ALL");                
            }

            string surfacesGdbPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Surfaces);
            if (success == BA_ReturnCode.Success)
            {
                string tempDem = "originaldem";
                string tempOutput = $@"{surfacesGdbPath}\{tempDem}";
                if (bDemImageService)
                {
                    success = await AnalysisTools.ClipRasterLayerNoBufferAsync(oAoi.FilePath, strOutputFeatures, Constants.FILE_AOI_BUFFERED_VECTOR,
                        strSourceDem, tempOutput);
                }
                else
                {
                    success = await AnalysisTools.ClipRasterLayerNoBufferAsync(oAoi.FilePath, strOutputFeatures, Constants.FILE_AOI_BUFFERED_VECTOR,
                        strSourceDem, tempOutput);
                }
                string strDem = $@"{surfacesGdbPath}\{Constants.FILE_DEM}";
                if (success == BA_ReturnCode.Success)
                {
                    if (SmoothDemChecked)
                    {
                        string envExtent = await GeodatabaseTools.GetEnvelope(aoiGdbPath, Constants.FILE_AOI_BUFFERED_VECTOR);
                        string neighborhood = "Rectangle " + FilterCellWidth + " " + FilterCellHeight + " CELL";
                        var parameters = Geoprocessing.MakeValueArray(tempOutput, strDem, neighborhood, "MEAN", "DATA");
                        var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath, extent: envExtent, mask: $@"{surfacesGdbPath}\{tempDem}");
                        var gpResult = await Geoprocessing.ExecuteToolAsync("FocalStatistics_sa", parameters, environments,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        if (gpResult.IsFailed)
                        {
                            success = BA_ReturnCode.UnknownError;
                        }
                        else
                        {
                            success = BA_ReturnCode.Success;
                            // delete original dem
                            success = await GeoprocessingTools.DeleteDatasetAsync(tempOutput);
                        }
                    }
                    else
                    {
                        var parameters = Geoprocessing.MakeValueArray(tempOutput, strDem);
                        var gpResult = await Geoprocessing.ExecuteToolAsync("Rename_management", parameters, null,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        if (gpResult.IsFailed)
                        {
                            success = BA_ReturnCode.UnknownError;
                        }
                        else
                        {
                            success = BA_ReturnCode.Success;
                        }
                    }

                    StringBuilder sbDem = new StringBuilder();
                    //Update the metadata if there is a custom buffer
                    //We need to add a new tag at "/metadata/dataIdInfo/searchKeys/keyword"
                    sbDem.Append(Constants.META_TAG_PREFIX);
                    // Buffer Distance
                    sbDem.Append(Constants.META_TAG_BUFFER_DISTANCE + aoiBufferDistance + "; ");
                    // X Units
                    sbDem.Append(Constants.META_TAG_XUNIT_VALUE + Constants.UNITS_METERS + "; ");
                    sbDem.Append(Constants.META_TAG_SUFFIX);
                    if (!String.IsNullOrEmpty(aoiBufferDistance) && success == BA_ReturnCode.Success)
                    {
                        //Update the metadata
                        await QueuedTask.Run(() =>
                        {
                            var fc = ItemFactory.Instance.Create(strDem,
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
                Uri uri = null;
                if (success == BA_ReturnCode.Success)
                {
                    var parameters = Geoprocessing.MakeValueArray(strDem, $@"{surfacesGdbPath}\{Constants.FILE_DEM_FILLED}");
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath, mask: $@"{strDem}");
                    var gpResult = await Geoprocessing.ExecuteToolAsync("Fill_sa", parameters, environments,
                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        success = BA_ReturnCode.UnknownError;
                        return;
                    }
                    else
                    {
                        success = BA_ReturnCode.Success;
                    }
                    if (FilledDemChecked)
                    {
                        uri = new Uri($@"{surfacesGdbPath}\{Constants.FILE_DEM_FILLED}");
                        await MapTools.DisplayRasterStretchSymbolAsync(Constants.MAPS_DEFAULT_MAP_NAME, uri, "Filled DEM", "ArcGIS Colors", "Black to White", 0);
                    }
                }

            }

        }

    }
}