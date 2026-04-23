using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Data.UtilityNetwork.Trace;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Core.Utilities;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using bagis_pro.BA_Objects;
using bagis_pro.Buttons;
using ExtensionMethod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace bagis_pro.AoiTools
{
    internal class WinCreateAoiModel : ViewModelBase
    {
        WinCreateAoi _view = null;
        bool _snapPPChecked;
        double _snapDistance;
        bool _aoiBufferChecked;
        double _aoiBufferDistance;
        string _demElevUnit = "";
        private string _slopeUnitDescr;

        public WinCreateAoiModel(WinCreateAoi view)
        {
            _view = view;
            SnapPPChecked = true;
            SnapDistance = 15;
            AoiBufferChecked = true;
            AoiBufferDistance = Convert.ToDouble((string)Module1.Current.BagisSettings.AoiBufferDistance);
            DemElevUnit = (string)Module1.Current.BagisSettings.DemUnits;
            SlopeUnit defaultSlope = SlopeUnit.PctSlope; //BAGIS generates Slope in Degree
            SlopeUnitDescr = defaultSlope.GetEnumDescription();


        }
        public bool SnapPPChecked
        {
            get => _snapPPChecked;
            set
            {
                if (_snapPPChecked != value)
                {
                    _snapPPChecked = value;
                }
            }
        }
        public double SnapDistance
        {
            get => _snapDistance;
            set
            {
                if (_snapDistance != value)
                {
                    _snapDistance = value;                
                }
            }
        }
        public bool AoiBufferChecked
        {
            get => _aoiBufferChecked;
            set => SetProperty(ref _aoiBufferChecked, value);
        }

        public double AoiBufferDistance
        {
            get => _aoiBufferDistance;
            set => SetProperty(ref _aoiBufferDistance, value);
        }
        public string DemElevUnit
        {
            get => _demElevUnit;
            set => SetProperty(ref _demElevUnit, value);
        }
        public string SlopeUnitDescr
        {
            get => _slopeUnitDescr;
            set => SetProperty(ref _slopeUnitDescr, value);
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
            uint nStep = 10;
            int intWait = 500;
            // Validation
            Aoi oAoi = Module1.Current.Aoi;
            if (string.IsNullOrEmpty(oAoi.FilePath) || string.IsNullOrEmpty(oAoi.Name))
            {
                System.Windows.MessageBox.Show("Missing output workspace or AOI name!", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!Directory.Exists(oAoi.FilePath))
            {
                MessageBox.Show("Output workspace does not exist!", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // verify dem is available
            string strSourceDem = Module1.Current.DataSources[DataSource.GetDemKey].uri;
            WorkspaceType wType = await GeneralTools.GetRasterWorkspaceType(strSourceDem);
            if (wType == WorkspaceType.None)
            {
                System.Windows.MessageBox.Show("Invalid DEM. AOI cannot be created!", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            //verify AOI buffer distance
            if (AoiBufferChecked)
            {
                if (AoiBufferDistance <= 0)
                {
                    // Switch back to default
                    AoiBufferDistance = Convert.ToDouble((string)Module1.Current.BagisSettings.AoiBufferDistance);
                }
            }

            // Start populating aoi object
            Map oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);

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

            // Create maps folder
            string strMapsFolder = $@"{oAoi.FilePath}\{Constants.FOLDER_MAPS}";
            if (!Directory.Exists(strMapsFolder))
            {
                DirectoryInfo dirInfo = Directory.CreateDirectory(strMapsFolder);
                if (dirInfo == null)
                {
                    MessageBox.Show("Unable to create maps folder in " + oAoi.FilePath + "! Process stopped.", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                    return ;
                }
            }

            // set pourpoint filename and save pourpoint as a feature
            string aoiGdb = $@"{GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi)}";
            string surfacesGdbPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Surfaces);
            string unsnappedppname = Constants.FILE_UNSNAPPED_POURPOINT;
            if (!SnapPPChecked)
            {
                unsnappedppname = Constants.FILE_POURPOINT;
            }

            IGPResult oGpResult = await QueuedTask.Run(() =>
            {
                var parameters = Geoprocessing.MakeValueArray(Constants.MAPS_POURPOINT_LAYER, "POINT", $@"{aoiGdb}\{unsnappedppname}",
                    "DELETE_GRAPHICS");
                return Geoprocessing.ExecuteToolAsync("GraphicsToFeatures_conversion", parameters, null,
                            status.Progressor, GPExecuteToolFlags.AddToHistory);
            });
            if (oGpResult.IsFailed)
            {
                MessageBox.Show("Unable to save Pour Point!", "BAGIS-Pro");
            }
            // Delete graphics layer
            string[] arrRemove = { Constants.MAPS_POURPOINT_LAYER };
            await MapTools.RemoveLayersfromMapFrame(Constants.MAPS_DEFAULT_MAP_NAME, arrRemove);
                await QueuedTask.Run(() =>
                {
                    status.Progressor.Value = 0;
                    status.Progressor.Message = $@"Delineating AOI Boundaries... (step 2 of {nStep})";
                    //block the CIM for a second
                    Task.Delay(intWait).Wait();

                }, status.Progressor);

            if (SnapPPChecked)
            {
                string snapFileName = "tmpSnap";
                string extractFileName = "tmpExtract";
                oGpResult = await QueuedTask.Run(() =>
                {
                    var parameters = Geoprocessing.MakeValueArray($@"{aoiGdb}\{unsnappedppname}", 
                        $@"{GeodatabaseTools.GetGeodatabasePath(Module1.Current.BasinFolderBase, GeodatabaseNames.Surfaces, true)}{Constants.FILE_FLOW_ACCUMULATION}",
                        $@"{aoiGdb}\{snapFileName}", SnapDistance, Constants.FIELD_OID);
                    return Geoprocessing.ExecuteToolAsync("SnapPourPoint_sa", parameters, null,
                                status.Progressor, GPExecuteToolFlags.AddToHistory);
                });
                if (oGpResult.IsFailed)
                {
                    MessageBox.Show("Unable to snap Pour Point!", "BAGIS-Pro");
                }
                else
                {
                    //Query the Previous Raster to Include only the PP location
                    //Set where_clause to > -1 (Pour Point Value)
                    oGpResult = await QueuedTask.Run(() =>
                    {
                        var parameters = Geoprocessing.MakeValueArray($@"{aoiGdb}\{snapFileName}",
                            $@"{Constants.FIELD_VALUE} > -1", $@"{aoiGdb}\{extractFileName}");

                        return Geoprocessing.ExecuteToolAsync("ExtractByAttributes_sa", parameters, null,
                                    status.Progressor, GPExecuteToolFlags.AddToHistory);
                    });
                    if (oGpResult.IsFailed)
                    {
                        MessageBox.Show("Unable to extract Pour Point location!", "BAGIS-Pro");
                    }
                    else
                    {
                        GPExecuteToolFlags oFlags = GPExecuteToolFlags.None | GPExecuteToolFlags.AddToHistory; 
                        success = await GeoprocessingTools.RasterToPointAsync($@"{aoiGdb}\{extractFileName}", Constants.FIELD_VALUE,
                            $@"{aoiGdb}\{Constants.FILE_POURPOINT}", oFlags, status.Progressor);
                    }
                    if (success == BA_ReturnCode.Success)
                    {
                        oGpResult = await QueuedTask.Run(() =>
                        {
                            var parameters = Geoprocessing.MakeValueArray($@"{GeodatabaseTools.GetGeodatabasePath(Module1.Current.BasinFolderBase, GeodatabaseNames.Surfaces, true)}{Constants.FILE_FLOW_DIRECTION}",
                                $@"{aoiGdb}\{Constants.FILE_POURPOINT}", $@"{aoiGdb}\{Constants.FILE_AOI_RASTER}");

                            return Geoprocessing.ExecuteToolAsync("Watershed_sa", parameters, null,
                                        status.Progressor, GPExecuteToolFlags.AddToHistory);
                        });
                        if (oGpResult.IsFailed)
                        {
                            MessageBox.Show("Unable to create AOI boundary!", "BAGIS-Pro");
                            progress.Hide();
                            return;
                        }
                    }
                    if (await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(aoiGdb), snapFileName))
                    {
                        success = await GeoprocessingTools.DeleteDatasetAsync($@"{aoiGdb}\{snapFileName}");
;                   }
                    if (await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(aoiGdb), extractFileName))
                    {
                        success = await GeoprocessingTools.DeleteDatasetAsync($@"{aoiGdb}\{extractFileName}");
                    }
                }
            }
            else
            {
                oGpResult = await QueuedTask.Run(() =>
                {
                    var parameters = Geoprocessing.MakeValueArray($@"{GeodatabaseTools.GetGeodatabasePath(Module1.Current.BasinFolderBase, GeodatabaseNames.Surfaces, true)}{Constants.FILE_FLOW_DIRECTION}",
                        $@"{aoiGdb}\{Constants.FILE_POURPOINT}", $@"{aoiGdb}\{Constants.FILE_AOI_RASTER}");

                    return Geoprocessing.ExecuteToolAsync("Watershed_sa", parameters, null,
                                status.Progressor, GPExecuteToolFlags.AddToHistory);
                });
                if (oGpResult.IsFailed)
                {
                    MessageBox.Show("Unable to create AOI boundary!", "BAGIS-Pro");
                    progress.Hide();
                    return;
                }
            }

            // add pourpoint layer to map
            Uri uri = new Uri(aoiGdb + "\\" + Constants.FILE_POURPOINT);
            success = await MapTools.AddPointMarkersAsync(Constants.MAPS_DEFAULT_MAP_NAME, uri, Constants.MAPS_STREAM_GAGE, CIMColor.CreateRGBColor(255, 165, 0),
                SimpleMarkerStyle.Circle, 8, "", MaplexPointPlacementMethod.NorthEastOfPoint);

            string aoiVectorPath = $@"{aoiGdb}\{Constants.FILE_AOI_VECTOR}";
            if (success == BA_ReturnCode.Success)
            {                
                IGPResult gpResult = await QueuedTask.Run(() =>
                {
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath);
                    var parameters = Geoprocessing.MakeValueArray($@"{aoiGdb}\{Constants.FILE_AOI_RASTER}", aoiVectorPath, "NO_SIMPLIFY");
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
            }

            //Add attributes after aoi_v is created
            if (success == BA_ReturnCode.Success)
            {
                string stationName = "";
                if (!string.IsNullOrEmpty(oAoi.StationName))
                {
                    stationName = oAoi.StationName;
                }
                string stationTriplet = Constants.VALUE_NOT_SPECIFIED;
                if (!string.IsNullOrEmpty(oAoi.StationTriplet))
                {
                    stationTriplet = oAoi.StationTriplet;
                }
                string basinName = Convert.ToString(Module1.Current.CboCurrentBasin.SelectedItem);
                success = await GeodatabaseTools.AddPourpointAttributesAsync(oAoi.FilePath, stationName, stationTriplet, basinName, status);
                success = await GeodatabaseTools.AddAOIVectorAttributesAsync(new Uri(aoiGdb), stationName, stationTriplet, basinName, status);
            }

            if (success == BA_ReturnCode.Success)
            {
                success = await MapTools.AddAoiBoundaryToMapAsync(new Uri(aoiVectorPath), 
                    ColorFactory.Instance.RedRGB, Constants.MAPS_DEFAULT_MAP_NAME, $@"AOI {oAoi.StationName}");
            }
            else
            {
                System.Windows.MessageBox.Show("Unable to append attributes to aoi layers.", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                success = BA_ReturnCode.WriteError;
                progress.Hide();
                return;
            }

            if (success == BA_ReturnCode.Success)
            {
                double aoiMinArea = 0.0036; //Sq Km - 4 pixels of 30 meter DEM
                var result = await GeodatabaseTools.CalculateAoiAreaSqMetersAsync(oAoi.FilePath, -1);
                double aoiAreaSqMeters = result.Item1;
                double areaSqKm = AreaUnit.SquareMeters.ConvertTo(aoiAreaSqMeters, AreaUnit.SquareKilometers);
                if (areaSqKm <= 0)
                {
                    System.Windows.MessageBox.Show("Unable to get the area of the AOI! Program stopped.", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                    success = BA_ReturnCode.WriteError;
                    progress.Hide();
                    return;
                }
                else if (areaSqKm < aoiMinArea)
                {
                    System.Windows.MessageBox.Show("The size of the AOI is too small! \r\nPlease select a new pour point location or use the auto snapping option.", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
                    success = BA_ReturnCode.WriteError;
                    progress.Hide();
                    return;
                }
                else
                {
                    string areaMessage = "The area of AOI is:\r\n";
                    areaMessage = areaMessage + string.Format("{0,8:N2} ", areaSqKm) + "Square Km\r\n";
                    double acres = AreaUnit.SquareKilometers.ConvertTo(areaSqKm, AreaUnit.Acres);
                    areaMessage = areaMessage + string.Format("{0,8:N2} ", acres) + "Acre\r\n";
                    double sqMiles = AreaUnit.SquareKilometers.ConvertTo(areaSqKm, AreaUnit.SquareMiles);
                    areaMessage = areaMessage + $"{sqMiles,8:N2} " + "Square Miles\r\n\r\n";
                    areaMessage = areaMessage + "Do you want to use this AOI boundary?";
                    MessageBoxResult res = MessageBox.Show(areaMessage, "BAGIS-Pro", MessageBoxButton.YesNo);
                    if (res == MessageBoxResult.No)
                    {
                        _view.Close();
                    }
                }
            }

            if (!AoiBufferChecked)
            {
                // one meter buffer to dissolve polygons connected at a point
                AoiBufferDistance = 1;
            }
            success = await GeoprocessingTools.BufferAsync(aoiVectorPath,$@"{aoiGdb}\{Constants.FILE_AOI_BUFFERED_VECTOR}", 
                $@"{AoiBufferDistance} {Constants.UNITS_METERS}", "ALL", status.Progressor);

            string snapRaster = $@"{GeodatabaseTools.GetGeodatabasePath(Module1.Current.BasinFolderBase, GeodatabaseNames.Surfaces, true)}{Constants.FILE_DEM_FILLED}";
            double cellSize = await GeodatabaseTools.GetCellSizeAsync(new Uri(snapRaster), await GeneralTools.GetRasterWorkspaceType(snapRaster));
            if (success == BA_ReturnCode.Success)
            {
                string aoiRasterPath = $@"{surfacesGdbPath}\{Constants.FILE_AOI_BUFFERED_RASTER}";
                IGPResult gpResult = await QueuedTask.Run(() =>
                {
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath, snapRaster: snapRaster);
                    var parameters = Geoprocessing.MakeValueArray($@"{aoiGdb}\{Constants.FILE_AOI_BUFFERED_VECTOR}", Constants.FIELD_OBJECT_ID, 
                        aoiRasterPath, cellSize);
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

            //=========================
            //start the clipping preparation
            //=========================
            await QueuedTask.Run(() =>
            {
                status.Progressor.Value = 0;
                status.Progressor.Message = $@"Clipping DEM layer... (step 3 of {nStep})";
                //block the CIM for a second
                Task.Delay(intWait).Wait();

            }, status.Progressor);

            // Open and Clip DEM
            string clipEnvelope = null;
            string outputRaster = $@"{surfacesGdbPath}\{Constants.FILE_DEM}";
            await QueuedTask.Run(() =>
            {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(aoiGdb))))
                using (FeatureClass fClass = geodatabase.OpenDataset<FeatureClass>(Constants.FILE_AOI_BUFFERED_VECTOR))
                {
                    Envelope env = fClass.GetExtent();
                    clipEnvelope = env.Extent.XMin + " " + env.Extent.YMin + " " + env.Extent.XMax + " " + env.Extent.YMax;
                }
            });
            success = await GeoprocessingTools.ClipRasterAsync($@"{GeodatabaseTools.GetGeodatabasePath(Module1.Current.BasinFolderBase, GeodatabaseNames.Surfaces, true)}{Constants.FILE_DEM}", 
                clipEnvelope, outputRaster, $@"{aoiGdb}\{Constants.FILE_AOI_BUFFERED_VECTOR}", null, true, 
                oAoi.FilePath, snapRaster);
            if (success != BA_ReturnCode.Success)
            {
                MessageBox.Show("Clipping DEM failed!", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            outputRaster = $@"{surfacesGdbPath}\{Constants.FILE_DEM_FILLED}";
            success = await GeoprocessingTools.ClipRasterAsync($@"{GeodatabaseTools.GetGeodatabasePath(Module1.Current.BasinFolderBase, GeodatabaseNames.Surfaces, true)}{Constants.FILE_DEM_FILLED}",
                clipEnvelope, outputRaster, $@"{aoiGdb}\{Constants.FILE_AOI_BUFFERED_VECTOR}", null, true,
                oAoi.FilePath, snapRaster);
            if (success != BA_ReturnCode.Success)
            {
                MessageBox.Show("Clipping FILLED DEM failed!", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            //=========================
            //Clip and save ASPECT
            //=========================
            await QueuedTask.Run(() =>
            {
                status.Progressor.Value = 0;
                status.Progressor.Message = $@"Clipping ASPECT... (step 4 of {nStep})";
                //block the CIM for a second
                Task.Delay(intWait).Wait();

            }, status.Progressor);

            outputRaster = $@"{surfacesGdbPath}\{Constants.FILE_ASPECT}";
            success = await GeoprocessingTools.ClipRasterAsync($@"{GeodatabaseTools.GetGeodatabasePath(Module1.Current.BasinFolderBase, GeodatabaseNames.Surfaces, true)}{Constants.FILE_ASPECT}",
                clipEnvelope, outputRaster, $@"{aoiGdb}\{Constants.FILE_AOI_BUFFERED_VECTOR}", null, true,
                oAoi.FilePath, snapRaster);
            if (success != BA_ReturnCode.Success)
            {
                MessageBox.Show("Clipping ASPECT failed!", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            //=========================
            //Clip and save SlOPE
            //=========================
            await QueuedTask.Run(() =>
            {
                status.Progressor.Value = 0;
                status.Progressor.Message = $@"Clipping SLOPE... (step 5 of {nStep})";
                //block the CIM for a second
                Task.Delay(intWait).Wait();

            }, status.Progressor);

            outputRaster = $@"{surfacesGdbPath}\{Constants.FILE_SLOPE}";
            success = await GeoprocessingTools.ClipRasterAsync($@"{GeodatabaseTools.GetGeodatabasePath(Module1.Current.BasinFolderBase, GeodatabaseNames.Surfaces, true)}{Constants.FILE_SLOPE}",
                clipEnvelope, outputRaster, $@"{aoiGdb}\{Constants.FILE_AOI_BUFFERED_VECTOR}", null, true,
                oAoi.FilePath, snapRaster);
            if (success != BA_ReturnCode.Success)
            {
                MessageBox.Show("Clipping ASPECT failed!", "BAGIS-Pro", MessageBoxButton.OK, MessageBoxImage.Error);
            }


            return;

           
       
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
            string aoiBufferDistance = Convert.ToString(AoiBufferDistance); // Default buffer distance is meters
            if (success == BA_ReturnCode.Success)
            {
                if (!AoiBufferChecked)
                {
                    aoiBufferDistance = "1"; //one meter buffer to dissolve polygons connected at a point
                }
                strOutputFeatures = $@"{aoiGdbPath}\{Constants.FILE_AOI_BUFFERED_VECTOR}";
                success = await GeoprocessingTools.BufferAsync(strPath, strOutputFeatures, $@"{aoiBufferDistance} {Constants.UNITS_METERS}", "ALL", status.Progressor);
            }

            if (success == BA_ReturnCode.Success)
            {
                string tempDem = "originaldem";
                string tempOutput = $@"{surfacesGdbPath}\{tempDem}";
                success = await AnalysisTools.ClipRasterLayerNoBufferAsync(oAoi.FilePath, strOutputFeatures,
                    strSourceDem, tempOutput, strSourceDem, status.Progressor);

                string strDem = $@"{surfacesGdbPath}\{Constants.FILE_DEM}";
                if (success == BA_ReturnCode.Success)
                {

                }
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
                        sbDem.Append(Constants.META_TAG_XUNIT_VALUE + Constants.UNITS_METERS + "; ");
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
                        $@"{surfacesGdbPath}\{Constants.FILE_FLOW_ACCUMULATION}");
                    var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath, snapRaster: strSourceDem);
                    var gpResult = await Geoprocessing.ExecuteToolAsync("FlowAccumulation_sa", parameters, environments,
                        status.Progressor, GPExecuteToolFlags.AddToHistory);
                    if (gpResult.IsFailed)
                    {
                        success = BA_ReturnCode.UnknownError;
                        progress.Hide();
                        return;
                    }
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
                                   $@"{aoiGdbPath}\{Constants.FILE_POURPOINT}", GPExecuteToolFlags.AddToHistory, status.Progressor);
                            }
                        }
                        if (await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(surfacesGdbPath), ppRaster))
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
                    //@ToDo: Clip Hillshade

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
