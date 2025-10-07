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
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace bagis_pro.Basin
{
    internal class WinClipDemModel : PropertyChangedBase
    {
        WinClipDem _view = null;
        bool _demExtentChecked = true;
        bool _filledDemChecked = true;
        bool _flowDirChecked = true;
        bool _flowAccChecked = true;
        bool _slopeChecked = true;
        bool _aspectChecked = true;
        bool _hillshadeChecked = true;
        bool _cmdClipEnabled = true;
        double _zFactor = 1;
        private bool _smoothDemChecked;
        private int _filterCellHeight = 3;
        private int _filterCellWidth = 7;
        private string _basinFolder = "";

        public WinClipDemModel(WinClipDem view)
        {
            _view = view;
        }

        public bool FilledDemChecked
        {
            get => _filledDemChecked;
            set => SetProperty(ref _filledDemChecked, value);
        }
        public bool FlowDirChecked
        {
            get => _flowDirChecked;
            set => SetProperty(ref _flowDirChecked, value);
        }
        public bool FlowAccChecked
        {
            get => _flowAccChecked;
            set => SetProperty(ref _flowAccChecked, value);
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
        public bool DemExtentChecked
        {
            get => _demExtentChecked;
            set => SetProperty(ref _demExtentChecked, value);
        }
        public bool CmdClipEnabled
        {
            get => _cmdClipEnabled;
            set => SetProperty(ref _cmdClipEnabled, value);
        }
        public double ZFactor
        {
            get => _zFactor;
            set => SetProperty(ref _zFactor, value);
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
        public ICommand CmdAll => new RelayCommand(() =>
        {
            SetCheckedValues(true);
        });

        public ICommand CmdNone => new RelayCommand(() =>
        {
            SetCheckedValues(false);
        });

        protected void SetCheckedValues(bool checkAll)
        {
            DemExtentChecked = checkAll;
            FilledDemChecked = checkAll;
            FlowDirChecked = checkAll;
            FlowAccChecked = checkAll;
            SlopeChecked = checkAll;
            AspectChecked = checkAll;
            HillshadeChecked = checkAll;
        }

        private RelayCommand _runClipCommand;
        public ICommand CmdClip
        {
            get
            {
                if (_runClipCommand == null)
                    _runClipCommand = new RelayCommand(RunClipImplAsync, () => true);
                return _runClipCommand;
            }
        }

        private async void RunClipImplAsync(object param)
        {
            // verify filter size parameters
            if (SmoothDemChecked)
            {
                if (FilterCellHeight <= 0 || FilterCellWidth <= 0)
                {
                    MessageBox.Show("Invalid filter size! Please re-enter.", "BAGIS-Pro");
                    return;
                }
            }

            // verify dem is available
            string strSourceDem = Module1.Current.DataSources[DataSource.GetDemKey].uri;
            WorkspaceType wType = await GeneralTools.GetRasterWorkspaceType(strSourceDem);
            if (wType == WorkspaceType.None)
            {
                MessageBox.Show("Invalid DEM. AOI cannot be created!", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Get output path from parent form
            var pane = (DockBasinToolViewModel)FrameworkApplication.DockPaneManager.Find("bagis_pro_Basin_DockBasinTool");;
            _basinFolder = pane.ParentFolder;

            uint nStep = 7;
            int intWait = 500;
            var progress = new ProgressDialog("Processing ...", "Cancel", 100, false);
            var status = new CancelableProgressorSource(progress);
            status.Max = 100;
            progress.Show();
            CmdClipEnabled = false;
            IList<string> lstExistingGdb = GeodatabaseTools.CheckForBasinGdb(_basinFolder);
            for (int i = 0; i < lstExistingGdb.Count; i++)
            {
                int layersRemoved = await MapTools.RemoveLayersInFolderAsync(lstExistingGdb[i]);
                System.IO.Directory.Delete(lstExistingGdb[i], true);
            }
            BA_ReturnCode success = await GeodatabaseTools.CreateGeodatabaseFoldersAsync(_basinFolder, FolderType.BASIN, status.Progressor);
            if (success != BA_ReturnCode.Success)
            {
                MessageBox.Show("Unable to create basin file geodatabases. Check permissions and disk space and try again.", "BAGIS-Pro");
                progress.Hide();
                progress.Dispose();
                CmdClipEnabled = true;
                return;
            }
            
            string gdbAoi = GeodatabaseTools.GetGeodatabasePath(_basinFolder, GeodatabaseNames.Aoi);
            await QueuedTask.Run(() =>
            {
                status.Progressor.Value = 0;    // reset the progressor's value back to 0 between GP tasks
                status.Progressor.Message = $@"Clipping DEM to Basin Folder ...";
                //block the CIM for a second
                Task.Delay(intWait).Wait();
            }, status.Progressor);

            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                var parameters = Geoprocessing.MakeValueArray(Constants.MAPS_CLIP_DEM_LAYER, "POLYGON", $@"{gdbAoi}\{Constants.FILE_AOI_VECTOR}",
                    "DELETE_GRAPHICS");
                return Geoprocessing.ExecuteToolAsync("GraphicsToFeatures_conversion", parameters, null,
                            status.Progressor, GPExecuteToolFlags.AddToHistory);
            });

            Map oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);

            if (gpResult.IsFailed)
            {
                MessageBox.Show("Unable to save the DEM extent file!", "BAGIS-Pro");
                progress.Hide();
                progress.Dispose();
                CmdClipEnabled = true;
                return;
            }

            string[] arrAddFields = new string[] { Constants.FIELD_BASIN };     // A basin doesnt need station_name or station_triplet
            string[] arrNewFieldTypes = new string[] { "TEXT" };
            string basinName = System.IO.Path.GetFileName(_basinFolder);
            success = await GeoprocessingTools.AddFieldAsync($@"{gdbAoi}\{Constants.FILE_AOI_VECTOR}", arrAddFields[0],
                arrNewFieldTypes[0], status);
            if (success == BA_ReturnCode.Success && !string.IsNullOrEmpty(basinName))
            {
                IDictionary<string, string> dictUpdate = new Dictionary<string, string>();
                dictUpdate.Add(arrAddFields[0], basinName);
                success = await GeodatabaseTools.UpdateFeatureAttributesAsync(new System.Uri(gdbAoi), Constants.FILE_AOI_VECTOR, 
                    new ArcGIS.Core.Data.QueryFilter(), dictUpdate);
            }

            if (DemExtentChecked)
            {
                success = await MapTools.AddAoiBoundaryToMapAsync(new System.Uri($@"{gdbAoi}\{Constants.FILE_AOI_VECTOR}"), ColorFactory.Instance.RedRGB, 
                    Constants.MAPS_DEFAULT_MAP_NAME, Constants.MAPS_BASIN_BOUNDARY);
                if (success != BA_ReturnCode.Success)
                {
                    MessageBox.Show("Unable to add the extent layer", "BAGIS-Pro");
                }
            }

            await QueuedTask.Run(() =>
            {
                // clear graphic container
                var graphicsLayer = oMap.GetLayersAsFlattenedList().OfType<GraphicsLayer>().Where(f =>
                    f.Name == Constants.MAPS_CLIP_DEM_LAYER).FirstOrDefault();
                if (graphicsLayer != null)
                {
                    oMap.RemoveLayer(graphicsLayer);
                }
            });

            // clip DEM then save it
            await QueuedTask.Run(() =>
            {
                status.Progressor.Value = 0;    // reset the progressor's value back to 0 between GP tasks
                status.Progressor.Message = $@"Clipping DEM to Basin Folder ... (step 1 of {nStep})";
                //block the CIM for a second
                Task.Delay(intWait).Wait();
            }, status.Progressor);

            string surfacesGdbPath = GeodatabaseTools.GetGeodatabasePath(_basinFolder, GeodatabaseNames.Surfaces);
            string strDem = $@"{surfacesGdbPath}\{Constants.FILE_DEM}";
            IReadOnlyList<string> parameters = null;
            IReadOnlyList<KeyValuePair<string, string>> environments = null;
            if (success == BA_ReturnCode.Success)
            {
                string tempDem = "originaldem";
                string tempOutput = $@"{surfacesGdbPath}\{tempDem}";
                success = await AnalysisTools.ClipRasterLayerNoBufferAsync(_basinFolder, $@"{gdbAoi}\{Constants.FILE_AOI_VECTOR}", 
                    strSourceDem, tempOutput, strSourceDem, status.Progressor);

                if (SmoothDemChecked && success == BA_ReturnCode.Success)
                {
                    string envExtent = await GeodatabaseTools.GetEnvelope(gdbAoi, Constants.FILE_AOI_VECTOR);
                    string neighborhood = "Rectangle " + FilterCellWidth + " " + FilterCellHeight + " CELL";
                    parameters = Geoprocessing.MakeValueArray(tempOutput, strDem, neighborhood, "MEAN", "DATA");
                    environments = Geoprocessing.MakeEnvironmentArray(workspace: _basinFolder, extent: envExtent, mask: $@"{surfacesGdbPath}\{tempDem}", snapRaster: strSourceDem);
                    gpResult = await Geoprocessing.ExecuteToolAsync("FocalStatistics_sa", parameters, environments,
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
                else if (success == BA_ReturnCode.Success)
                {
                    parameters = Geoprocessing.MakeValueArray(tempOutput, strDem);
                    gpResult = await Geoprocessing.ExecuteToolAsync("Rename_management", parameters, null,
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
            if (success != BA_ReturnCode.Success)
            {
                int retVal = await AbandonClipDEMAsync(_basinFolder, progress, status.Progressor);
                return;
            }

            await QueuedTask.Run(() =>
            {
                status.Progressor.Value = 0;    // reset the progressor's value back to 0 between GP tasks
                status.Progressor.Message = $@"Filling DEM... (step 2 of {nStep})";
                //block the CIM for a second
                Task.Delay(intWait).Wait();
            }, status.Progressor);

            string filledDemPath = $@"{surfacesGdbPath}\{Constants.FILE_DEM_FILLED}";
            parameters = Geoprocessing.MakeValueArray(strDem, filledDemPath);
            environments = Geoprocessing.MakeEnvironmentArray(workspace: _basinFolder, mask: $@"{strDem}", snapRaster: strSourceDem);
            gpResult = await Geoprocessing.ExecuteToolAsync("Fill_sa", parameters, environments,
                status.Progressor, GPExecuteToolFlags.AddToHistory);
            StringBuilder sbDem = new StringBuilder();
            if (gpResult.IsFailed)
            {
                int retVal = await AbandonClipDEMAsync(_basinFolder, progress, status.Progressor);
                return;
            }
            else
            {                
                //We need to add a new tag at "/metadata/dataIdInfo/searchKeys/keyword"
                sbDem.Append(Constants.META_TAG_PREFIX);
                // Elevation Units
                sbDem.Append(Constants.META_TAG_ZUNIT_CATEGORY + MeasurementUnitType.Elevation + "; ");
                sbDem.Append(Constants.META_TAG_ZUNIT_VALUE + (string)Module1.Current.BagisSettings.DemUnits + "; ");
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
                if (FilledDemChecked)
                {
                    await MapTools.DisplayRasterStretchSymbolAsync(Constants.MAPS_DEFAULT_MAP_NAME, new Uri(filledDemPath), Constants.FILE_DEM_FILLED,
                        "ArcGIS Colors", "Black to White", 0);
                }
            }

            await QueuedTask.Run(() =>
            {
                status.Progressor.Value = 0;    // reset the progressor's value back to 0 between GP tasks
                status.Progressor.Message = $@"Calculating Slope... (step 3 of {nStep})";
                //block the CIM for a second
                Task.Delay(intWait).Wait();
            }, status.Progressor);

            double zFactor = 1;
            string demUnits = (string)Module1.Current.BagisSettings.DemUnits;
            if (!demUnits.Equals("Meters"))
            {
                zFactor = 0.3048;
            }
            parameters = Geoprocessing.MakeValueArray(filledDemPath, $@"{surfacesGdbPath}\{Constants.FILE_SLOPE}",
                "PERCENT_RISE", zFactor);
            environments = Geoprocessing.MakeEnvironmentArray(workspace: _basinFolder, snapRaster: strSourceDem);
            gpResult = await Geoprocessing.ExecuteToolAsync("Slope_sa", parameters, environments,
                status.Progressor, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                int retVal = await AbandonClipDEMAsync(_basinFolder, progress, status.Progressor);
                return;
            }
            SlopeUnit defaultSlope = SlopeUnit.PctSlope; //BAGIS generates Slope in Degree
            sbDem.Clear();
            //We need to add a new tag at "/metadata/dataIdInfo/searchKeys/keyword"
            sbDem.Append(Constants.META_TAG_PREFIX);
            // Elevation Units
            sbDem.Append(Constants.META_TAG_ZUNIT_CATEGORY + MeasurementUnitType.Slope + "; ");
            sbDem.Append(Constants.META_TAG_ZUNIT_VALUE + defaultSlope.GetEnumDescription() + "; ");
            sbDem.Append(Constants.META_TAG_SUFFIX);
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
            if (SlopeChecked)
            {
                await MapTools.DisplayRasterStretchSymbolAsync(Constants.MAPS_DEFAULT_MAP_NAME, new Uri($@"{surfacesGdbPath}\{Constants.FILE_SLOPE}"),
                    Constants.FILE_SLOPE, "ArcGIS Colors", "Slope", 0);
            }

            await QueuedTask.Run(() =>
            {
                status.Progressor.Value = 0;    // reset the progressor's value back to 0 between GP tasks
                status.Progressor.Message = $@"Calculating Aspect... (step 4 of {nStep})";
                //block the CIM for a second
                Task.Delay(intWait).Wait();
            }, status.Progressor);

            parameters = Geoprocessing.MakeValueArray(filledDemPath, $@"{surfacesGdbPath}\{Constants.FILE_ASPECT}");
            environments = Geoprocessing.MakeEnvironmentArray(workspace: _basinFolder, snapRaster: strSourceDem);
            gpResult = await Geoprocessing.ExecuteToolAsync("Aspect_sa", parameters, environments,
                status.Progressor, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                int retVal = await AbandonClipDEMAsync(_basinFolder, progress, status.Progressor);
                return;
            }
            if (AspectChecked)
            {
                await MapTools.DisplayRasterStretchSymbolAsync(Constants.MAPS_DEFAULT_MAP_NAME, new Uri($@"{surfacesGdbPath}\{Constants.FILE_ASPECT}"), Constants.FILE_ASPECT,
                    "ArcGIS Colors", "Aspect", 0);
            }

            await QueuedTask.Run(() =>
            {
                status.Progressor.Value = 0;    // reset the progressor's value back to 0 between GP tasks
                status.Progressor.Message = $@"Calculating Flow Direction... (step 5 of {nStep})";
                //block the CIM for a second
                Task.Delay(intWait).Wait();
            }, status.Progressor);

            parameters = Geoprocessing.MakeValueArray(filledDemPath, $@"{surfacesGdbPath}\{Constants.FILE_FLOW_DIRECTION}");
            environments = Geoprocessing.MakeEnvironmentArray(workspace: _basinFolder, snapRaster: strSourceDem);
            gpResult = await Geoprocessing.ExecuteToolAsync("FlowDirection_sa", parameters, environments,
                status.Progressor, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                int retVal = await AbandonClipDEMAsync(_basinFolder, progress, status.Progressor);
                return;
            }
            if (FlowDirChecked)
            {
                await MapTools.DisplayRasterStretchSymbolAsync(Constants.MAPS_DEFAULT_MAP_NAME, new Uri($@"{surfacesGdbPath}\{Constants.FILE_FLOW_DIRECTION}"), Constants.FILE_FLOW_DIRECTION,
                    "ArcGIS Colors", "Black to White", 0);
            }

            await QueuedTask.Run(() =>
            {
                status.Progressor.Value = 0;    // reset the progressor's value back to 0 between GP tasks
                status.Progressor.Message = $@"Calculating Flow Accumulation... (step 6 of {nStep})";
                //block the CIM for a second
                Task.Delay(intWait).Wait();
            }, status.Progressor);

            parameters = Geoprocessing.MakeValueArray($@"{surfacesGdbPath}\{Constants.FILE_FLOW_DIRECTION}",
                $@"{surfacesGdbPath}\{Constants.FILE_FLOW_ACCUMULATION}");
            environments = Geoprocessing.MakeEnvironmentArray(workspace: _basinFolder, snapRaster: strSourceDem);
            gpResult = await Geoprocessing.ExecuteToolAsync("FlowAccumulation_sa", parameters, environments,
                status.Progressor, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                int retVal = await AbandonClipDEMAsync(_basinFolder, progress, status.Progressor);
                return;
            }
            if (FlowAccChecked)
            {
                await MapTools.DisplayRasterStretchSymbolAsync(Constants.MAPS_DEFAULT_MAP_NAME, new Uri($@"{surfacesGdbPath}\{Constants.FILE_FLOW_ACCUMULATION}"), 
                    Constants.FILE_FLOW_ACCUMULATION,
                    "ArcGIS Colors", "Black to White", 0);
            }

            await QueuedTask.Run(() =>
            {
                status.Progressor.Value = 0;    // reset the progressor's value back to 0 between GP tasks
                status.Progressor.Message = $@"Calculating Hillshade... (step 7 of {nStep})";
                //block the CIM for a second
                Task.Delay(intWait).Wait();
            }, status.Progressor);

            parameters = Geoprocessing.MakeValueArray($@"{surfacesGdbPath}\{Constants.FILE_DEM_FILLED}",
                $@"{surfacesGdbPath}\{Constants.FILE_HILLSHADE}", "", "", "", ZFactor);
            environments = Geoprocessing.MakeEnvironmentArray(workspace: _basinFolder, snapRaster: strSourceDem);
            gpResult = await Geoprocessing.ExecuteToolAsync("Hillshade_sa", parameters, environments,
                status.Progressor, GPExecuteToolFlags.AddToHistory);
            if (gpResult.IsFailed)
            {
                int retVal = await AbandonClipDEMAsync(_basinFolder, progress, status.Progressor);
                return;
            }
            if (HillshadeChecked)
            {
                await MapTools.DisplayRasterStretchSymbolAsync(Constants.MAPS_DEFAULT_MAP_NAME, new Uri($@"{surfacesGdbPath}\{Constants.FILE_HILLSHADE}"), Constants.MAPS_HILLSHADE,
                    "ArcGIS Colors", "Black to White", 0);
            }

            //if DEM is successfully clipped then disable the DEM clip tools and enable the AOI_tool
            //declare the property value of buttons in the main toolbar
            if (success == BA_ReturnCode.Success)
            {
                Module1.DeactivateState("bagis_pro_Buttons_SetBasinExtentTool_State");
                Module1.DeactivateState("bagis_pro_Buttons_BtnCreateBasin_State");
                Module1.ActivateState("bagis_pro_Buttons_BtnDefineAoi_State");
            }

            // Clean-up step progressor
            progress.Hide();
            progress.Dispose();
            CmdClipEnabled = true;
        }
        private async Task<int> AbandonClipDEMAsync(string aoiPath, ProgressDialog prog, CancelableProgressor status)
        {
            MessageBox.Show("An error has occurred while clipping. Process halted!", "BAGIS Pro");
            int layersRemoved = await MapTools.RemoveLayersInFolderAsync(GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Aoi));
            layersRemoved = await MapTools.RemoveLayersInFolderAsync(GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Surfaces));
            BA_ReturnCode success = await GeoprocessingTools.DeleteDatasetAsync(GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Aoi), status);
            success = await GeoprocessingTools.DeleteDatasetAsync(GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Surfaces), status);
            if (System.IO.Directory.Exists(GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Aoi)) ||
                System.IO.Directory.Exists(GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Surfaces)))                
            {
                MessageBox.Show("Unable to clear BASIN's internal file geodatabase. Please restart BAGIS Pro and try again.. Please restart ArcGIS Pro and try again!", "BAGIS Pro");
            }
            prog.Hide();
            prog.Dispose();
            return layersRemoved;
        }
    }
}
