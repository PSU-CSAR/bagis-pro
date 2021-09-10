using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework;
using System.Windows.Input;

namespace bagis_pro
{
    public class MapTools
    {
        private static int IDX_STRETCH_MIN = 0;
        private static int IDX_STRETCH_MAX = 1;
        private static int IDX_LABEL_MIN = 2;
        private static int IDX_LABEL_MAX = 3;
        private static double ERROR_MIN = 9999;
        public static async Task<BA_ReturnCode> DisplayMaps(string strAoiPath, Layout layout, bool bInteractive)
        {
            BA_Objects.Aoi oAoi = Module1.Current.Aoi;
            if (String.IsNullOrEmpty(oAoi.Name))
            {
                if (System.IO.Directory.Exists(strAoiPath))
                {
                    // Initialize AOI object
                    oAoi = await GeneralTools.SetAoiAsync(strAoiPath);
                    if (oAoi != null)
                    {
                        Module1.Current.CboCurrentAoi.SetAoiName(oAoi.Name);
                        MessageBox.Show("AOI is set to " + oAoi.Name + "!", "BAGIS PRO");
                    }
                    else
                    {
                        MessageBox.Show("An error occurred while trying to set the AOI!!", "BAGIS PRO");
                    }
                }
                else
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("!!Please set an AOI before testing the maps", "BAGIS Pro");
                }
            }

            MapTools.DeactivateMapButtons();
            Map oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
            if (oMap != null)
            {
                if (bInteractive == true && oMap.Layers.Count() > 0)
                {
                    string strMessage = "Adding the maps to the display will overwrite the current arrangement of data layers. " +
                           "This action cannot be undone." + System.Environment.NewLine + "Do you wish to continue ?";
                    MessageBoxResult oRes = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(strMessage, "BAGIS", MessageBoxButton.YesNo);
                    if (oRes != MessageBoxResult.Yes)
                    {
                        return BA_ReturnCode.OtherError;
                    }
                }

                if (layout == null)
                {
                    MessageBox.Show("The Basin Analysis layout could not be located. Maps will not display!", "BAGIS-PRO");
                    Module1.Current.ModuleLogManager.LogError(nameof(DisplayMaps), "The Basin Analysis layout could not be located. Maps not displayed!");
                    return BA_ReturnCode.UnknownError;
                }
                else
                {
                    BA_ReturnCode success = await MapTools.SetDefaultMapFrameDimensionAsync(Constants.MAPS_DEFAULT_MAP_FRAME_NAME, layout, oMap,
                        1.0, 2.0, 7.5, 9.0);

                    //remove existing layers from map frame
                    await MapTools.RemoveLayersfromMapFrame();

                    //retrieve Analysis object
                    BA_Objects.Analysis oAnalysis = GeneralTools.GetAnalysisSettings(Module1.Current.Aoi.FilePath);

                    //add Land Ownership Layer
                    string strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                        Constants.FILE_PUBLIC_LAND;
                    Uri uri = new Uri(strPath);
                    success = await MapTools.AddPolygonLayerUniqueValuesAsync(uri, "ArcGIS Colors", "Basic Random",
                        new string[] { "AGBUR" }, false, false, 30.0F, Constants.MAPS_PUBLIC_LAND_OWNERSHIP);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnPublicLandOwnership_State");

                    //add aoi boundary to map
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi, true) +
                                     Constants.FILE_AOI_VECTOR;
                    Uri aoiUri = new Uri(strPath);
                    success = await MapTools.AddAoiBoundaryToMapAsync(aoiUri, Constants.MAPS_AOI_BOUNDARY);

                    //add subbasin contribution layer to map
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                                     Constants.FILE_PRECIP_CONTRIB_VECTOR;
                    success = await MapTools.AddAoiBoundaryToMapAsync(new Uri(strPath), Constants.MAPS_SUBBASIN_BOUNDARY, false);

                    //add Snotel Represented Area Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SNOTEL_REPRESENTED;
                    uri = new Uri(strPath);
                    CIMColor fillColor = CIMColor.CreateRGBColor(255, 0, 0, 50);    //Red with 30% transparency
                    success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Constants.MAPS_SNOTEL_REPRESENTED);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnSnotel_State");

                    //add Snow Course Represented Area Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SCOS_REPRESENTED;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Constants.MAPS_SNOW_COURSE_REPRESENTED);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnSnowCourse_State");

                    //add All Sites Represented Area Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SITES_REPRESENTED;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Constants.MAPS_ALL_SITES_REPRESENTED);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnSitesAll_State");

                    // add roads layer
                    Module1.Current.RoadsLayerLegend = "Within unknown distance of access road";
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_ROADS_ZONE;
                    // Get buffer units out of the metadata so we can set the layer name
                    string strBagisTag = await GeneralTools.GetBagisTagAsync(strPath, Constants.META_TAG_XPATH);
                    if (!String.IsNullOrEmpty(strBagisTag))
                    {
                        string strBufferDistance = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_BUFFER_DISTANCE, ';');
                        string strBufferUnits = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_XUNIT_VALUE, ';');
                        Module1.Current.RoadsLayerLegend = "Within " + strBufferDistance + " " + strBufferUnits + " of access road";
                    }
                    uri = new Uri(strPath);
                    success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Module1.Current.RoadsLayerLegend);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnRoads_State");

                    //add Public Land Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_PUBLIC_LAND_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Constants.MAPS_FEDERAL_PUBLIC_LAND_ZONES);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnPublicLandZones_State");

                    //add Below Treeline Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_BELOW_TREELINE_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Constants.MAPS_BELOW_TREELINE);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnBelowTreeline_State");

                    //add Potential Site Locations Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SITES_LOCATION_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Constants.MAPS_SITES_LOCATION);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnSitesLocationZone_State");

                    //add Critical Precipitation Zones Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_CRITICAL_PRECIP_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Constants.MAPS_CRITICAL_PRECIPITATION_ZONES);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnCriticalPrecipZone_State");

                    // add aoi streams layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                              Constants.FILE_STREAMS;
                    uri = new Uri(strPath);
                    await MapTools.AddLineLayerAsync(uri, Constants.MAPS_STREAMS, ColorFactory.Instance.BlueRGB);

                    // add Snotel Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                              Constants.FILE_SNOTEL;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPointMarkersAsync(uri, Constants.MAPS_SNOTEL, CIMColor.CreateRGBColor(0, 255, 255),
                        SimpleMarkerStyle.X, 10, Constants.FIELD_SITE_ID);
                    if (success == BA_ReturnCode.Success)
                        Module1.Current.Aoi.HasSnotel = true;

                    // add Snow Course Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                              Constants.FILE_SNOW_COURSE;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPointMarkersAsync(uri, Constants.MAPS_SNOW_COURSE, CIMColor.CreateRGBColor(0, 255, 255),
                        SimpleMarkerStyle.Star, 12, Constants.FIELD_SITE_ID);
                    if (success == BA_ReturnCode.Success)
                        Module1.Current.Aoi.HasSnowCourse = true;

                    // add hillshade layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Surfaces, true) +
                        Constants.FILE_HILLSHADE;
                    uri = new Uri(strPath);
                    await MapTools.DisplayRasterStretchSymbolAsync(uri, Constants.MAPS_HILLSHADE, "ArcGIS Colors", "Black to White", 0);

                    // add elev zones layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_ELEV_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.DisplayRasterWithSymbolAsync(uri, Constants.MAPS_ELEV_ZONE, "ArcGIS Colors",
                                "Elevation #2", "NAME", 30, true);
                    if (success == BA_ReturnCode.Success)
                        Module1.ActivateState("MapButtonPalette_BtnElevation_State");

                    // add slope zones layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SLOPE_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.DisplayRasterWithSymbolAsync(uri, Constants.MAPS_SLOPE_ZONE, "ArcGIS Colors",
                                "Slope", "NAME", 30, false);
                    if (success == BA_ReturnCode.Success)
                        Module1.ActivateState("MapButtonPalette_BtnSlope_State");

                    // add aspect zones layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_ASPECT_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.DisplayRasterWithSymbolAsync(uri, Constants.MAPS_ASPECT_ZONE, "ArcGIS Colors",
                                "Aspect", "NAME", 30, false);
                    if (success == BA_ReturnCode.Success)
                        Module1.ActivateState("MapButtonPalette_BtnAspect_State");

                    // add SNOTEL SWE layer
                    int idxDefaultMonth = 5;    // April
                    success = await DisplaySWEMapAsync(idxDefaultMonth);

                    // add SWE delta layer
                    success = await DisplaySWEDeltaMapAsync(0, 5);

                    // add quarterly seasonal precipitation layer; Default is Q1
                    success = await DisplaySeasonalPrecipContribMapAsync(0, oAnalysis.SeasonalPrecipMax);

                    // add Precipitation zones layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_PRECIP_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.DisplayRasterWithSymbolAsync(uri, Constants.MAPS_PRISM_ZONE, "ArcGIS Colors",
                               "Precipitation", "NAME", 30, false);
                    if (success == BA_ReturnCode.Success)
                        Module1.ActivateState("MapButtonPalette_BtnPrism_State");

                    // add winter precipitation layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_WINTER_PRECIPITATION;
                    uri = new Uri(strPath);
                    success = await MapTools.DisplayRasterWithClassifyAsync(uri, Constants.MAPS_WINTER_PRECIPITATION, "ArcGIS Colors",
                        "Precipitation", Constants.FIELD_VALUE, 30, ClassificationMethod.EqualInterval, 
                            Convert.ToInt16(oAnalysis.PrecipZonesIntervalCount) + 1, false);
                    if (success == BA_ReturnCode.Success)
                        Module1.ActivateState("MapButtonPalette_BtnWinterPrecipitation_State");

                    // add Precipitation Contribution layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_PRECIPITATION_CONTRIBUTION;
                    uri = new Uri(strPath);
                    success = await MapTools.DisplayRasterWithClassifyAsync(uri, Constants.MAPS_PRECIPITATION_CONTRIBUTION, "ColorBrewer Schemes (RGB)",
                               "Yellow-Green-Blue (Continuous)", Constants.FIELD_VOL_ACRE_FT, 30, ClassificationMethod.EqualInterval, 10, false);

                    if (success == BA_ReturnCode.Success)
                        Module1.ActivateState("MapButtonPalette_BtnPrecipContrib_State");

                    // create map elements
                    success = await MapTools.AddMapElements(Constants.MAPS_DEFAULT_LAYOUT_NAME);
                    success = await MapTools.DisplayNorthArrowAsync(layout, Constants.MAPS_DEFAULT_MAP_FRAME_NAME);
                    success = await MapTools.DisplayScaleBarAsync(layout, Constants.MAPS_DEFAULT_MAP_FRAME_NAME);

                    //zoom to aoi boundary layer
                    double bufferFactor = 1.1;
                    success = await MapTools.ZoomToExtentAsync(aoiUri, bufferFactor);
                    return success;
                }
            }
            return BA_ReturnCode.UnknownError;
        }

        public static async Task<Layout> GetDefaultLayoutAsync(string layoutName)
        {
            return await QueuedTask.Run(() =>
            {
                Layout layout = null;
                Project proj = Project.Current;

               //Finding the first project item with name matches with mapName
               LayoutProjectItem lytItem =
                   proj.GetItems<LayoutProjectItem>()
                       .FirstOrDefault(m => m.Name.Equals(layoutName, StringComparison.CurrentCultureIgnoreCase));
                if (lytItem != null)
                {
                    layout = lytItem.GetLayout();
                }
                else
                {
                    layout = LayoutFactory.Instance.CreateLayout(8.5, 11, LinearUnit.Inches);
                    layout.SetName(layoutName);
                }
                return layout;
            });
        }

        public static async Task<BA_ReturnCode> SetDefaultMapFrameDimensionAsync(string mapFrameName, Layout oLayout, Map oMap, double xMin,
                                                                  double yMin, double xMax, double yMax)
        {
            await QueuedTask.Run(() =>
           {
               //Finding the mapFrame with mapFrameName
               if (!(oLayout.FindElement(mapFrameName) is MapFrame mfElm))
               {
                   //Build 2D envelope geometry
                   Coordinate2D mf_ll = new Coordinate2D(xMin, yMin);
                   Coordinate2D mf_ur = new Coordinate2D(xMax, yMax);
                   Envelope mf_env = EnvelopeBuilder.CreateEnvelope(mf_ll, mf_ur);
                   mfElm = LayoutElementFactory.Instance.CreateMapFrame(oLayout, mf_env, oMap);
                   mfElm.SetName(mapFrameName);
               }
               // Remove border from map frame
               var mapFrameDefn = mfElm.GetDefinition() as CIMMapFrame;
               mapFrameDefn.GraphicFrame.BorderSymbol = new CIMSymbolReference
               {
                   Symbol = SymbolFactory.Instance.ConstructLineSymbol(ColorFactory.Instance.BlackRGB, 0, SimpleLineStyle.Null)
               };
               mfElm.SetDefinition(mapFrameDefn);
           });
            return BA_ReturnCode.Success;
        }

        public static async Task<Map> SetDefaultMapNameAsync(string mapName)
        {
            return await QueuedTask.Run(async () =>
            {
                Map map = null;
                Project proj = Project.Current;

                //Finding the first project item with name matches with mapName
                MapProjectItem mpi =
                    proj.GetItems<MapProjectItem>()
                        .FirstOrDefault(m => m.Name.Equals(mapName, StringComparison.CurrentCultureIgnoreCase));
                if (mpi != null)
                {
                    map = mpi.GetMap();
                }
                else
                {
                    map = MapFactory.Instance.CreateMap(mapName, basemap: Basemap.None);
                    await FrameworkApplication.Panes.CreateMapPaneAsync(map);
                }
                return map;
            });
        }

        public static async Task<BA_ReturnCode> AddAoiBoundaryToMapAsync(Uri aoiUri, string displayName = "", bool isVisible = true,
            double lineSymbolWidth = 1.0)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (aoiUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(aoiUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(aoiUri.LocalPath);
            }
            else
            {
                return BA_ReturnCode.UnknownError;
            }
            await QueuedTask.Run(() =>
            {
                FeatureClass fClass = null;
                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                {
                    // Use the geodatabase.
                    fClass = geodatabase.OpenDataset<FeatureClass>(strFileName);
                }
                if (String.IsNullOrEmpty(displayName))
                {
                    displayName = fClass.GetDefinition().GetAliasName();
                }
                // Create symbology for feature layer
                var flyrCreatnParam = new FeatureLayerCreationParams(fClass)
                {
                    Name = displayName,
                    IsVisible = isVisible,
                    RendererDefinition = new SimpleRendererDefinition()
                    {
                        SymbolTemplate = SymbolFactory.Instance.ConstructPolygonSymbol(
                        ColorFactory.Instance.BlackRGB, SimpleFillStyle.Null,
                        SymbolFactory.Instance.ConstructStroke(ColorFactory.Instance.BlackRGB, lineSymbolWidth, SimpleLineStyle.Solid))
                        .MakeSymbolReference()
                    }
                };

                FeatureLayer fLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flyrCreatnParam, MapView.Active.Map);
            });
            return BA_ReturnCode.Success;
        }

        public static async Task<BA_ReturnCode> AddPolygonLayerAsync(Uri uri, CIMColor fillColor, bool isVisible, string displayName = "")
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (uri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(uri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(uri.LocalPath);
            }

            Uri tempUri = new Uri(strFolderPath);
            bool polygonLayerExists = await GeodatabaseTools.FeatureClassExistsAsync(tempUri, strFileName);
            if (!polygonLayerExists)
            {
                return BA_ReturnCode.ReadError;
            }
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            await QueuedTask.Run(() =>
            {
                //Define a simple renderer to symbolize the feature class.
                var simpleRender = new SimpleRendererDefinition
                {
                    SymbolTemplate = SymbolFactory.Instance.ConstructPolygonSymbol(
                        fillColor, SimpleFillStyle.Solid,
                        SymbolFactory.Instance.ConstructStroke(ColorFactory.Instance.BlackRGB, 0))
                        .MakeSymbolReference()

                };
                //Define some of the Feature Layer's parameters
                var flyrCreatnParam = new FeatureLayerCreationParams(uri)
                {
                    Name = displayName,
                    IsVisible = isVisible,
                };

                FeatureLayer fLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flyrCreatnParam, MapView.Active.Map);
                // Create and apply the renderer
                CIMRenderer renderer = fLayer?.CreateRenderer(simpleRender);
                fLayer.SetRenderer(renderer);
                success = BA_ReturnCode.Success;
            });
            return success;
        }

        public static async Task<BA_ReturnCode> AddPolygonLayerUniqueValuesAsync(Uri uri, string styleCategory, string styleName,
                                                                                 string[] arrDisplayFields, bool isVisible, 
                                                                                 bool bAllOtherValues, double dblTransparency, string displayName = "")
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (uri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(uri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(uri.LocalPath);
            }

            Uri tempUri = new Uri(strFolderPath);
            bool polygonLayerExists = await GeodatabaseTools.FeatureClassExistsAsync(tempUri, strFileName);
            if (!polygonLayerExists)
            {
                return BA_ReturnCode.ReadError;
            }
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            await QueuedTask.Run(() =>
            {
                // Find the color ramp
                StyleProjectItem style =
                    Project.Current.GetItems<StyleProjectItem>().FirstOrDefault(s => s.Name == styleCategory);
                if (style == null) return;
                var colorRampList = style.SearchColorRamps(styleName);
                CIMColorRamp cimColorRamp = null;
                if (colorRampList.Count > 0)
                {
                    cimColorRamp = colorRampList[0].ColorRamp;
                }
                else
                {
                    return;
                }

                //construct unique value renderer definition 
                var oSymbolReference = SymbolFactory.Instance.ConstructPolygonSymbol(ColorFactory.Instance.RedRGB, SimpleFillStyle.Solid, null).MakeSymbolReference();
                UniqueValueRendererDefinition uvr = new
                   UniqueValueRendererDefinition()
                {
                    ValueFields = arrDisplayFields, //multiple fields in the array if needed.
                    ColorRamp = cimColorRamp, //Specify color ramp
                    SymbolTemplate = oSymbolReference,
                    UseDefaultSymbol = bAllOtherValues
                };

                //Define some of the Feature Layer's parameters
                var flyrCreatnParam = new FeatureLayerCreationParams(uri)
                {
                    Name = displayName,
                    IsVisible = isVisible,
                };
                FeatureLayer fLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flyrCreatnParam, MapView.Active.Map);
                if (dblTransparency > 0)
                {
                    fLayer.SetTransparency(dblTransparency);
                }

                //Creates a "Renderer"
                var cimRenderer = fLayer.CreateRenderer(uvr);
                
                //Sets the renderer to the feature layer
                fLayer.SetRenderer(cimRenderer);
                
                success = BA_ReturnCode.Success;
            });
            return success;
        }

        public static async Task AddLineLayerAsync(Uri aoiUri, string displayName, CIMColor lineColor)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (aoiUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(aoiUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(aoiUri.LocalPath);
            }
            await QueuedTask.Run(() =>
            {
                FeatureClass fClass = null;
                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                {
                    // Use the geodatabase.
                    try
                    {
                        fClass = geodatabase.OpenDataset<FeatureClass>(strFileName);
                    }
                    catch (GeodatabaseTableException e)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(AddLineLayerAsync),
                            "Unable to open feature class " + strFileName);
                        Module1.Current.ModuleLogManager.LogError(nameof(AddLineLayerAsync),
                            "Exception: " + e.Message);
                        return;
                    }
                }
                // Create symbology for feature layer
                var flyrCreatnParam = new FeatureLayerCreationParams(fClass)
                {
                    Name = displayName,
                    IsVisible = true,
                    RendererDefinition = new SimpleRendererDefinition()
                    {
                        SymbolTemplate = SymbolFactory.Instance.ConstructLineSymbol(lineColor)
                        .MakeSymbolReference()
                    }
                };

                FeatureLayer fLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flyrCreatnParam, MapView.Active.Map);
            });
        }

        public static async Task<BA_ReturnCode> AddPointMarkersAsync(Uri aoiUri, string displayName, CIMColor markerColor,
                                    SimpleMarkerStyle markerStyle, double markerSize, string labelField)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (aoiUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(aoiUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(aoiUri.LocalPath);
            }
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            await QueuedTask.Run(() =>
            {
                FeatureClass fClass = null;
                int idxLabelField = -1;
                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                {
                    // Use the geodatabase.
                    try
                    {
                        fClass = geodatabase.OpenDataset<FeatureClass>(strFileName);
                        FeatureClassDefinition featureClassDefinition = fClass.GetDefinition();
                        idxLabelField = featureClassDefinition.FindField(labelField);
                    }
                    catch (GeodatabaseTableException e)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(AddPointMarkersAsync),
                             "Unable to open feature class " + strFileName);
                        Module1.Current.ModuleLogManager.LogError(nameof(AddPointMarkersAsync),
                            "Exception: " + e.Message);
                        success = BA_ReturnCode.ReadError;
                        return;
                    }
                }
                // Create symbology for feature layer
                var flyrCreatnParam = new FeatureLayerCreationParams(fClass)
                {
                    Name = displayName,
                    IsVisible = true,
                    RendererDefinition = new SimpleRendererDefinition()
                    {
                        SymbolTemplate = SymbolFactory.Instance.ConstructPointSymbol(markerColor, markerSize, markerStyle)
                        .MakeSymbolReference()
                    }
                };

                FeatureLayer fLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flyrCreatnParam, MapView.Active.Map);

                if (fLayer != null && idxLabelField > -1)
                {
                    fLayer.SetLabelVisibility(true);   //set the label's visiblity
                    //Get the layer's definition
                    var lyrDefn = fLayer.GetDefinition() as CIMFeatureLayer;
                    //Get the label classes - we need the first one
                    var listLabelClasses = lyrDefn.LabelClasses.ToList();
                    var theLabelClass = listLabelClasses.FirstOrDefault();
                    //Select label field
                    theLabelClass.Expression = "return $feature." + labelField + ";";
                    //Modify label Placement 
                    //Check if the label engine is Maplex or standard.
                    CIMGeneralPlacementProperties labelEngine = MapView.Active.Map.GetDefinition().GeneralPlacementProperties;
                    if (labelEngine is CIMStandardGeneralPlacementProperties) //Current labeling engine is Standard labeling engine               
                        theLabelClass.StandardLabelPlacementProperties.PointPlacementMethod = StandardPointPlacementMethod.OnTopPoint;
                    else    //Current labeling engine is Maplex labeling engine            
                        theLabelClass.MaplexLabelPlacementProperties.PointPlacementMethod = MaplexPointPlacementMethod.NorthEastOfPoint;
                    //Gets the text symbol of the label class            
                    var textSymbol = listLabelClasses.FirstOrDefault().TextSymbol.Symbol as CIMTextSymbol; 
                    textSymbol.FontStyleName = "Bold"; //set font as bold
                    textSymbol.SetSize(14); //set font size 
                    lyrDefn.LabelClasses = listLabelClasses.ToArray(); //Set the labelClasses back
                    fLayer.SetDefinition(lyrDefn); //set the layer's definition
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(AddPointMarkersAsync),
                        "Field " + labelField + " is missing from the feature class and cannot be used as a label!");
                }
            });
            success = BA_ReturnCode.Success;
            return success;
        }

        public static async Task<BA_ReturnCode> ZoomToExtentAsync(Uri aoiUri, double bufferFactor = 1)
        {
            //Get the active map view.
            var mapView = MapView.Active;
            if (mapView == null)
                return BA_ReturnCode.UnknownError;
            string strFileName = null;
            string strFolderPath = null;
            if (aoiUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(aoiUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(aoiUri.LocalPath);
            }

            Envelope zoomEnv = await QueuedTask.Run<Envelope>(() =>
            {
                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (
                  Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                {
                    // Use the geodatabase.
                    FeatureClassDefinition fcDefinition = geodatabase.GetDefinition<FeatureClassDefinition>(strFileName);
                    var extent = fcDefinition.GetExtent();
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ZoomToExtentAsync), "extent XMin=" + extent.XMin);
                    var expandedExtent = extent.Expand(bufferFactor, bufferFactor, true);
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ZoomToExtentAsync), "expandedExtent XMin=" + expandedExtent.XMin);
                    return expandedExtent;
                }
            });

            //Zoom the view to a given extent.
            bool bSuccess = false;
            Module1.Current.ModuleLogManager.LogDebug(nameof(ZoomToExtentAsync), "zoomEnv XMin=" + zoomEnv.XMin);
            await FrameworkApplication.Current.Dispatcher.Invoke(async () =>
            {
                // Do something on the GUI thread
                bSuccess = await MapView.Active.ZoomToAsync(zoomEnv, null);
            });


            Module1.Current.ModuleLogManager.LogDebug(nameof(ZoomToExtentAsync), "Return value from ZoomToAsync=" + bSuccess);
            if (bSuccess)
            {
                return BA_ReturnCode.Success;
            }
            else
            {
                return BA_ReturnCode.UnknownError;
            }
             
        }

        public static async Task RemoveLayersfromMapFrame()
        {
            string[] arrLayerNames = new string[41];
            arrLayerNames[0] = Constants.MAPS_AOI_BOUNDARY;
            arrLayerNames[1] = Constants.MAPS_STREAMS;
            arrLayerNames[2] = Constants.MAPS_SNOTEL;
            arrLayerNames[3] = Constants.MAPS_SNOW_COURSE;
            arrLayerNames[4] = Constants.MAPS_HILLSHADE;
            arrLayerNames[5] = Constants.MAPS_ELEV_ZONE;
            arrLayerNames[6] = Constants.MAPS_SNOW_COURSE_REPRESENTED;
            arrLayerNames[7] = Constants.MAPS_SNOTEL_REPRESENTED;
            arrLayerNames[8] = Constants.MAPS_SLOPE_ZONE;
            arrLayerNames[9] = Constants.MAPS_ASPECT_ZONE;
            arrLayerNames[10] = Constants.MAPS_ALL_SITES_REPRESENTED;
            arrLayerNames[11] = Constants.MAPS_PRISM_ZONE;
            arrLayerNames[12] = Constants.MAPS_FEDERAL_PUBLIC_LAND_ZONES;
            arrLayerNames[13] = Constants.MAPS_BELOW_TREELINE;
            arrLayerNames[14] = Constants.MAPS_SITES_LOCATION;
            arrLayerNames[15] = Constants.MAPS_CRITICAL_PRECIPITATION_ZONES;
            arrLayerNames[16] = Constants.MAPS_PUBLIC_LAND_OWNERSHIP;
            arrLayerNames[17] = Constants.MAPS_PRECIPITATION_CONTRIBUTION;
            arrLayerNames[18] = Constants.MAPS_WINTER_PRECIPITATION;
            arrLayerNames[19] = Constants.MAPS_SUBBASIN_BOUNDARY;
            int idxLayerNames = 20;
            for (int i = 0; i < Constants.LAYER_NAMES_SNODAS_SWE.Length; i++)
            {
                arrLayerNames[idxLayerNames] = Constants.LAYER_NAMES_SNODAS_SWE[i];
                idxLayerNames++;
            }
            for (int i = 0; i < Constants.LAYER_NAMES_SWE_DELTA.Length; i++)
            {
                arrLayerNames[idxLayerNames] = Constants.LAYER_NAMES_SWE_DELTA[i];
                idxLayerNames++;
            }
            for (int i = 0; i < Constants.LAYER_NAMES_SEASON_PRECIP_CONTRIB.Length; i++)
            {
                arrLayerNames[idxLayerNames] = Constants.LAYER_NAMES_SEASON_PRECIP_CONTRIB[i];
                idxLayerNames++;
            }
            var map = MapView.Active.Map;
            await QueuedTask.Run(() =>
            {
                foreach (string strName in arrLayerNames)
                {
                    Layer oLayer =
                        map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(strName, StringComparison.CurrentCultureIgnoreCase));
                    if (oLayer != null)
                    {

                        map.RemoveLayer(oLayer);
                    }
                }


                //special handling for the roads zones layer because the name may be different between AOI's; It's based on the buffer distance
                var returnLayers =
                     map.Layers.Where(m => m.Name.Contains("Within"));
                IList<string> lstLayerNames = new List<string>();
                foreach (var item in returnLayers)
                {
                    lstLayerNames.Add(item.Name);
                }
                foreach (var strName in lstLayerNames)
                {
                    Layer oLayer =
                        map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(strName, StringComparison.CurrentCultureIgnoreCase));
                    if (oLayer != null)
                    {

                        map.RemoveLayer(oLayer);
                    }
                }
           });
        }

        public static async Task DisplayRasterStretchSymbolAsync(Uri rasterUri, string displayName, string styleCategory,
            string styleName, int transparency)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (rasterUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(rasterUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(rasterUri.LocalPath);
            }
            if (await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(strFolderPath), strFileName))
            {
                await QueuedTask.Run(() =>
                {
                    // Find the color ramp
                    StyleProjectItem style =
                        Project.Current.GetItems<StyleProjectItem>().FirstOrDefault(s => s.Name == styleCategory);
                    if (style == null) return;
                    var colorRampList = style.SearchColorRamps(styleName);
                    CIMColorRamp cimColorRamp = null;
                    foreach (var colorRamp in colorRampList)
                    {
                        if (colorRamp.Name.Equals(styleName))
                        {
                            cimColorRamp = colorRamp.ColorRamp;
                            break;
                        }
                    }
                    if (cimColorRamp == null)
                    {
                        return;
                    }

                    // Create a new Stretch Colorizer Definition supplying the color ramp
                    StretchColorizerDefinition stretchColorizerDef = new StretchColorizerDefinition(0, RasterStretchType.DefaultFromSource, 1.0, cimColorRamp);
                    int idxLayer = MapView.Active.Map.Layers.Count();
                    RasterLayer rasterLayer = (RasterLayer)LayerFactory.Instance.CreateRasterLayer(rasterUri, MapView.Active.Map, idxLayer,
                        displayName, stretchColorizerDef);
                    rasterLayer.SetTransparency(transparency);
                });
            }
            else
            {
                Module1.Current.ModuleLogManager.LogError(nameof(DisplayRasterStretchSymbolAsync),
                    rasterUri.LocalPath + " could not be found. Raster not displayed!!" );
            }
        }

        public static async Task<BA_ReturnCode> DisplayRasterWithSymbolAsync(Uri rasterUri, string displayName, string styleCategory, string styleName,
            string fieldName, int transparency, bool isVisible)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (rasterUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(rasterUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(rasterUri.LocalPath);
            }
            // Check to see if the raster exists before trying to add it
            bool bExists = await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(strFolderPath), strFileName);
            if (!bExists)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(DisplayRasterWithSymbolAsync),
                    "Unable to add locate raster!!");
                return BA_ReturnCode.ReadError;
            }
            // Open the requested raster so we know it exists; return if it doesn't
            await QueuedTask.Run(async () =>
            {
                RasterLayer rasterLayer = null;
                // Create the raster layer on the active map
                await QueuedTask.Run(() =>
                {
                    try
                    {
                        rasterLayer = (RasterLayer)LayerFactory.Instance.CreateLayer(rasterUri, MapView.Active.Map);
                    }
                    catch (Exception e)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(DisplayRasterWithSymbolAsync),
                            e.Message);
                    }
                });

                // Set raster layer transparency and name
                if (rasterLayer != null)
                {
                    rasterLayer.SetTransparency(transparency);
                    rasterLayer.SetName(displayName);
                    rasterLayer.SetVisibility(isVisible);
                    // Create and deploy the unique values renderer
                    await MapTools.SetToUniqueValueColorizer(displayName, styleCategory, styleName, fieldName);
                }
            });
            return BA_ReturnCode.Success;
        }

        public static async Task<BA_ReturnCode> DisplayRasterWithClassifyAsync(Uri rasterUri, string displayName, string styleCategory, 
            string styleName, string fieldName, int transparency, ClassificationMethod classificationMethod, int numClasses, bool isVisible)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (rasterUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(rasterUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(rasterUri.LocalPath);
            }
            // Check to see if the raster exists before trying to add it
            bool bExists = await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(strFolderPath), strFileName);
            if (!bExists)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(DisplayRasterWithClassifyAsync),
                    "Unable to add locate raster!!");
                return BA_ReturnCode.ReadError;
            }
            // Create the raster layer on the active map
            await QueuedTask.Run(() =>
            {
                try
                {
                    RasterLayer rasterLayer = (RasterLayer)LayerFactory.Instance.CreateLayer(rasterUri, MapView.Active.Map);
                    // Set raster layer transparency and name
                    if (rasterLayer != null)
                    {
                        rasterLayer.SetName(displayName);
                        rasterLayer.SetTransparency(transparency);
                        rasterLayer.SetVisibility(isVisible);
                    }
                }
                catch (Exception e)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(DisplayRasterWithClassifyAsync),
                        e.Message);
                }
            });

            // Create and deploy the classify renderer
            await MapTools.SetToClassifyRenderer(displayName, styleCategory, styleName, fieldName,
                classificationMethod, numClasses);
            success = BA_ReturnCode.Success;
            return success;
        }

        public static async Task<BA_ReturnCode> DisplayStretchRasterWithSymbolAsync(Uri rasterUri, string displayName, string styleCategory, string styleName,
            int transparency, bool isVisible, bool useCustomMinMax, double stretchMax, double stretchMin,
            double labelMax, double labelMin)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (rasterUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(rasterUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(rasterUri.LocalPath);
            }
            // Check to see if the raster exists before trying to add it
            bool bExists = await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(strFolderPath), strFileName);
            if (!bExists)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(DisplayStretchRasterWithSymbolAsync),
                    "Unable to add locate raster!!");
                return success;
            }
            // Open the requested raster so we know it exists; return if it doesn't
            await QueuedTask.Run(async () =>
            {
                RasterLayer rasterLayer = null;
                // Create the raster layer on the active map
                await QueuedTask.Run(() =>
                {
                    try
                    {
                        rasterLayer = (RasterLayer)LayerFactory.Instance.CreateLayer(rasterUri, MapView.Active.Map);
                    }
                    catch (Exception e)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(DisplayStretchRasterWithSymbolAsync),
                            e.Message);
                    }
                });

                // Set raster layer transparency and name
                if (rasterLayer != null)
                {
                    rasterLayer.SetTransparency(transparency);
                    rasterLayer.SetName(displayName);
                    rasterLayer.SetVisibility(isVisible);
                    // Create and deploy the unique values renderer
                    await MapTools.SetToStretchValueColorizer(displayName, styleCategory, styleName, useCustomMinMax,
                        stretchMin, stretchMax, labelMin, labelMax);
                    success = BA_ReturnCode.Success;
                }
            });
            return success;
        }

        public static async Task<BA_ReturnCode> DisplayRasterFromLayerFileAsync(Uri rasterUri, string displayName,
            string layerFilePath, int transparency, bool bIsVisible)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (rasterUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(rasterUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(rasterUri.LocalPath);
            }
            // Check to see if the raster exists before trying to add it
            bool bExists = await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(strFolderPath), strFileName);
            if (!bExists)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(DisplayRasterFromLayerFileAsync),
                    "Unable to add locate raster!!");
                return BA_ReturnCode.ReadError;
            }
            // Open the requested raster so we know it exists; return if it doesn't
            await QueuedTask.Run(async () =>
            {
                RasterLayer rasterLayer = null;
                // Create the raster layer on the active map
                await QueuedTask.Run(() =>
                {
                    rasterLayer = (RasterLayer)LayerFactory.Instance.CreateLayer(rasterUri, MapView.Active.Map);
                });

                // Set raster layer transparency and name
                if (rasterLayer != null)
                {
                    //Get the Layer Document from the lyrx file
                    var lyrDocFromLyrxFile = new LayerDocument(layerFilePath);
                    var cimLyrDoc = lyrDocFromLyrxFile.GetCIMLayerDocument();

                    //Get the colorizer from the layer file
                    var layerDefs = cimLyrDoc.LayerDefinitions;
                    var colorizerFromLayerFile = ((CIMRasterLayer)cimLyrDoc.LayerDefinitions[0]).Colorizer as CIMRasterStretchColorizer;

                    //Apply the colorizer to the raster layer
                    rasterLayer?.SetColorizer(colorizerFromLayerFile);

                    //Set the name and transparency
                    rasterLayer?.SetName(displayName);
                    rasterLayer?.SetTransparency(transparency);
                    rasterLayer?.SetVisibility(bIsVisible);

                    if (rasterLayer?.GetColorizer() is CIMRasterStretchColorizer)
                    {
                        // if the stretch renderer is used get the selected band index
                        var stretchColorizer = rasterLayer?.GetColorizer() as CIMRasterStretchColorizer;
                        RasterStretchType mine = stretchColorizer.StretchType;
                    }
                }
            });
            return BA_ReturnCode.Success;
        }

        public static async Task SetToUniqueValueColorizer(string layerName, string styleCategory,
            string styleName, string fieldName)
        {
            // Get the layer we want to symbolize from the map
            Layer oLayer =
                MapView.Active.Map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(layerName, StringComparison.CurrentCultureIgnoreCase));
            if (oLayer == null)
                return;
            RasterLayer rasterLayer = (RasterLayer)oLayer;

            StyleProjectItem style =
                Project.Current.GetItems<StyleProjectItem>().FirstOrDefault(s => s.Name == styleCategory);
            if (style == null) return;
            var colorRampList = await QueuedTask.Run(() => style.SearchColorRamps(styleName));
            if (colorRampList == null || colorRampList.Count == 0) return;
            CIMColorRamp cimColorRamp = colorRampList[0].ColorRamp;

            // Creates a new UV Colorizer Definition using the default constructor.
            UniqueValueColorizerDefinition UVColorizerDef = new UniqueValueColorizerDefinition(fieldName, cimColorRamp);

            // Creates a new UV colorizer using the colorizer definition created above.
            CIMRasterUniqueValueColorizer newColorizer = await rasterLayer.CreateColorizerAsync(UVColorizerDef) as CIMRasterUniqueValueColorizer;

            // Sets the newly created colorizer on the layer.
            await QueuedTask.Run(() =>
            {
                rasterLayer.SetColorizer(MapTools.RecalculateColorizer(newColorizer));
            });
        }

        public static async Task SetToClassifyRenderer(string layerName, string styleCategory,
            string styleName, string fieldName, ClassificationMethod classificationMethod,int numberofClasses)
        {
            // Get the layer we want to symbolize from the map
            Layer oLayer =
                MapView.Active.Map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(layerName, StringComparison.CurrentCultureIgnoreCase));
 
            BasicRasterLayer basicRasterLayer = null;
            if (oLayer != null && oLayer is BasicRasterLayer)
            {
                basicRasterLayer = (BasicRasterLayer)oLayer;
            }
            else
            {
                return;
            }

            StyleProjectItem style =
                Project.Current.GetItems<StyleProjectItem>().FirstOrDefault(s => s.Name == styleCategory);
            if (style == null) return;
            var colorRampList = await QueuedTask.Run(() => style.SearchColorRamps(styleName));
            if (colorRampList == null || colorRampList.Count == 0) return;
            CIMColorRamp cimColorRamp = colorRampList[0].ColorRamp;

            // Creates a new Classify Colorizer Definition using defined parameters.
            ClassifyColorizerDefinition classifyColorizerDef = 
                new ClassifyColorizerDefinition(fieldName, numberofClasses, classificationMethod, cimColorRamp);

            // Creates a new Classify colorizer using the colorizer definition created above.
            CIMRasterClassifyColorizer newColorizer = 
                await basicRasterLayer.CreateColorizerAsync(classifyColorizerDef) as CIMRasterClassifyColorizer;

            // Sets the newly created colorizer on the layer.
            await QueuedTask.Run(() =>
            {
                basicRasterLayer.SetColorizer(newColorizer);
            });
        }

        public static async Task SetToStretchValueColorizer(string layerName, string styleCategory, string styleName,
            bool useCustomMinMax, double stretchMax, double stretchMin, double labelMax, double labelMin)
        {
            // Get the layer we want to symbolize from the map
            Layer oLayer =
                MapView.Active.Map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(layerName, StringComparison.CurrentCultureIgnoreCase));
            if (oLayer == null)
                return;
            RasterLayer rasterLayer = (RasterLayer)oLayer;

            StyleProjectItem style =
                Project.Current.GetItems<StyleProjectItem>().FirstOrDefault(s => s.Name == styleCategory);
            if (style == null) return;
            var colorRampList = await QueuedTask.Run(() => style.SearchColorRamps(styleName));
            if (colorRampList == null || colorRampList.Count == 0) return;
            CIMColorRamp cimColorRamp = colorRampList[0].ColorRamp;

            // Create a new Stretch Colorizer Definition supplying the color ramp
            StretchColorizerDefinition stretchColorizerDef = new StretchColorizerDefinition(0, RasterStretchType.DefaultFromSource, 1.0, cimColorRamp);
            stretchColorizerDef.StretchType = RasterStretchType.PercentMinimumMaximum;
            //Create a new Stretch colorizer using the colorizer definition created above.
            CIMRasterStretchColorizer newStretchColorizer =
              await rasterLayer.CreateColorizerAsync(stretchColorizerDef) as CIMRasterStretchColorizer;

            if (useCustomMinMax == true)
            {
                //Customize min and max
                newStretchColorizer.StretchType = RasterStretchType.MinimumMaximum;
                newStretchColorizer.StatsType = RasterStretchStatsType.GlobalStats;
                StatsHistogram histo = newStretchColorizer.StretchStats;
                histo.max = stretchMax;
                histo.min = stretchMin;
                newStretchColorizer.StretchStats = histo;

                //Update labels
                string strLabelMin = Convert.ToString(Math.Round(stretchMin, 2));
                string strLabelMax = Convert.ToString(Math.Round(stretchMax, 2));
                if (stretchMin != labelMin)
                {
                    strLabelMin = Convert.ToString(Math.Round(labelMin, 2));
                }
                if (stretchMax != labelMax)
                {
                    strLabelMax = Convert.ToString(Math.Round(labelMax, 2));
                }
                CIMRasterStretchClass[] stretchClasses = newStretchColorizer.StretchClasses;
                if (stretchClasses.Length == 3)
                {
                    stretchClasses[0].Label = strLabelMin;  // The min values are in first position
                    stretchClasses[0].Value = labelMin;
                    stretchClasses[2].Label = strLabelMax;  // The max values are in last position
                    stretchClasses[2].Value = labelMax;
                }
            }

            // Set the new colorizer on the raster layer.
            rasterLayer.SetColorizer(newStretchColorizer);
        }

        // This method addresses the issue that the CreateColorizer does not systematically assign colors 
        // from the associated color ramp.
        // https://community.esri.com/message/867870-re-creating-unique-value-colorizer-for-raster
        public static CIMRasterColorizer RecalculateColorizer(CIMRasterColorizer colorizer)
        {
            if (colorizer is CIMRasterUniqueValueColorizer uvrColorizer)
            {
                var colorRamp = uvrColorizer.ColorRamp;
                if (colorRamp == null)
                    throw new InvalidOperationException("Colorizer must have a color ramp");

                //get the total number of colors to be assigned
                var total_colors = uvrColorizer.Groups.Select(g => g.Classes.Count()).Sum();
                var colors = ColorFactory.Instance.GenerateColorsFromColorRamp(colorRamp, total_colors);
                var c = 0;
                foreach (var uvr_group in uvrColorizer.Groups)
                {
                    foreach (var uvr_class in uvr_group.Classes)
                    {
                        //assign the generated colors to each class in turn
                        uvr_class.Color = colors[c++];
                    }
                }
            }
            else if (colorizer is CIMRasterClassifyColorizer classColorizer)
            {
                var colorRamp = classColorizer.ColorRamp;
                if (colorRamp == null)
                    throw new InvalidOperationException("Colorizer must have a color ramp");

                var total_colors = classColorizer.ClassBreaks.Count();
                var colors = ColorFactory.Instance.GenerateColorsFromColorRamp(colorRamp, total_colors);
                var c = 0;
                foreach (var cbreak in classColorizer.ClassBreaks)
                {
                    //assign the generated colors to each class break in turn
                    cbreak.Color = colors[c++];
                }
            }
            return colorizer;
        }

        public static async Task<BA_ReturnCode> AddMapElements(string layoutName)
        {
            //Finding the first project item with name matches with layoutName
            Layout layout = null;
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            await QueuedTask.Run(() =>
            {
                LayoutProjectItem lytItem =
                Project.Current.GetItems<LayoutProjectItem>()
                    .FirstOrDefault(m => m.Name.Equals(layoutName, StringComparison.CurrentCultureIgnoreCase));
                if (lytItem != null)
                {
                    layout = lytItem.GetLayout();
                }
            });
            if (layout != null)
            {
                // Delete all graphic elements except map frame
                await QueuedTask.Run(() =>
                {
                    layout.DeleteElements(item => !item.Name.Contains(Constants.MAPS_DEFAULT_MAP_FRAME_NAME));
                });
                // Map Title
                success = await MapTools.DisplayTextBoxAsync(layout, Constants.MAPS_TITLE, 4.0, 10.5, ColorFactory.Instance.BlackRGB, 20, "Times New Roman",
                    "Bold", "Title");
                // Map SubTitle
                success = await MapTools.DisplayTextBoxAsync(layout, Constants.MAPS_SUBTITLE, 4.0, 10.1, ColorFactory.Instance.BlackRGB, 20, "Times New Roman",
                    "Regular", "SubTitle");
                // (optional) textbox
                success = await MapTools.DisplayTextBoxAsync(layout, Constants.MAPS_TEXTBOX1, 5.0, 1.6, ColorFactory.Instance.BlackRGB, 12, "Times New Roman",
                    "Regular", "Text Box 1");
                // sites textbox
                success = await MapTools.DisplayTextBoxAsync(layout, Constants.MAPS_TEXTBOX2, 5.2, 0.35, ColorFactory.Instance.BlackRGB, 12, "Times New Roman",
                    "Regular", "See the Active Sites table for individual SNOTEL and Snow Course site descriptions");
            }
            return success;
        }

        public static async Task<BA_ReturnCode> DisplayTextBoxAsync(Layout layout, string elementName, double xPos, double yPos,
                                        CIMColor fontColor, double fontSize, string fontFamily, string fontStyle, string textBoxText)
        {
            await QueuedTask.Run(() =>
            {
                //Build 2D point geometry
                Coordinate2D coord2D = new Coordinate2D(5, 5);

                //Set symbolology, create and add element to layout
                CIMTextSymbol sym = SymbolFactory.Instance.ConstructTextSymbol(fontColor, fontSize, fontFamily, fontStyle);
                sym.HorizontalAlignment = ArcGIS.Core.CIM.HorizontalAlignment.Left;
                GraphicElement ptTxtElm = LayoutElementFactory.Instance.CreatePointTextGraphicElement(layout, coord2D, textBoxText, sym);
                ptTxtElm.SetName(elementName);
                ptTxtElm.SetAnchor(Anchor.CenterPoint);
                ptTxtElm.SetX(xPos);
                ptTxtElm.SetY(yPos);
            });

            return BA_ReturnCode.Success;
        }

        public static async Task<BA_ReturnCode> DisplayLegendAsync(Layout layout, string styleCategory, string styleName)
        {
            //Construct on the worker thread
            await QueuedTask.Run(() =>
           {
               //Build 2D envelope geometry
               Coordinate2D leg_ll = new Coordinate2D(0.5, 0.3);
               Coordinate2D leg_ur = new Coordinate2D(2.14, 2.57);
               Envelope leg_env = EnvelopeBuilder.CreateEnvelope(leg_ll, leg_ur);

               //Reference MF, create legend and add to layout
               MapFrame mapFrame = layout.FindElement(Constants.MAPS_DEFAULT_MAP_FRAME_NAME) as MapFrame;
               if (mapFrame == null)
               {
                   ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Map frame not found", "WARNING");
                   return;
               }
               Legend legendElm = LayoutElementFactory.Instance.CreateLegend(layout, leg_env, mapFrame);
               legendElm.SetName(Constants.MAPS_LEGEND);
               legendElm.SetAnchor(Anchor.BottomLeftCorner);

               // Turn off all of the layers to start
               CIMLegend cimLeg = legendElm.GetDefinition() as CIMLegend;
               foreach (CIMLegendItem legItem in cimLeg.Items)
               {
                   legItem.ShowHeading = false;
                   legItem.IsVisible = false;
               }

               // Format other elements in the legend
               cimLeg.GraphicFrame.BorderSymbol = new CIMSymbolReference
               {
                   Symbol = SymbolFactory.Instance.ConstructLineSymbol(ColorFactory.Instance.BlackRGB, 1.5, SimpleLineStyle.Solid)
               };
               cimLeg.GraphicFrame.BorderGapX = 3;
               cimLeg.GraphicFrame.BorderGapY = 3;
               // Apply the changes
               legendElm.SetDefinition(cimLeg);

           });
            return BA_ReturnCode.Success;
        }

        public async static Task<BA_ReturnCode> UpdateLegendAsync(Layout oLayout, IList<string> lstLegendLayer)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            await QueuedTask.Run(() =>
            {
                if (oLayout == null)
                {
                Project proj = Project.Current;
                //Get the default map layout
                LayoutProjectItem lytItem =
                   proj.GetItems<LayoutProjectItem>()
                       .FirstOrDefault(m => m.Name.Equals(Constants.MAPS_DEFAULT_LAYOUT_NAME,
                       StringComparison.CurrentCultureIgnoreCase));
                if (lytItem != null)
                {
                    oLayout = lytItem.GetLayout();
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(UpdateLegendAsync),
                        "Unable to find default layout!!");
                    MessageBox.Show("Unable to find default layout. Cannot update legend!");
                    return;
                }
              }

                //Get LayoutCIM and iterate through its elements
                var layoutDef = oLayout.GetDefinition();

                foreach (var elem in layoutDef.Elements)
                {
                    if (elem is CIMLegend)
                    {
                        var legend = elem as CIMLegend;
                        CIMLegendItem[] arrTempItems = new CIMLegendItem[legend.Items.Length];
                        int idx = 0;
                        //  Add items that we want to display to temporary array
                        foreach (var strName in lstLegendLayer)
                        {
                            foreach (var legendItem in legend.Items)
                            {
                                // Find the item in the existing array
                                if (legendItem.Name == strName)
                                {
                                    // Set the visibility
                                    legendItem.IsVisible = true;
                                    // Add the item to the temporary array
                                    arrTempItems[idx] = legendItem;
                                    // Increment the index
                                    idx++;
                                }
                            }
                        }
                        // Add the remaining items with visibility set to false in their original order
                        foreach (var legendItem in legend.Items)
                        {
                            if (!lstLegendLayer.Contains(legendItem.Name))
                            {
                                legendItem.IsVisible = false;
                                arrTempItems[idx] = legendItem;
                                // Increment the index
                                idx++;
                            }
                        }
                        // Set the legend items
                        legend.Items = arrTempItems;
                    }
                }
                //Apply the changes back to the layout
                oLayout.SetDefinition(layoutDef);
            });
            success = BA_ReturnCode.Success;
            return success;
        }

        public static async Task UpdateMapElementsAsync(string subTitleText, BA_Objects.MapDefinition mapDefinition)
        {
            await QueuedTask.Run(() =>
            {
                Project proj = Project.Current;

                //Get the default map layout
                LayoutProjectItem lytItem =
                   proj.GetItems<LayoutProjectItem>()
                       .FirstOrDefault(m => m.Name.Equals(Constants.MAPS_DEFAULT_LAYOUT_NAME,
                       StringComparison.CurrentCultureIgnoreCase));
                Layout layout = null;
                if (lytItem != null)
                {
                    layout = lytItem.GetLayout();
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(UpdateMapElementsAsync),
                        "Unable to find default layout!!");
                    MessageBox.Show("Unable to find default layout. Cannot update map elements!");
                    return;
                }

                if (!String.IsNullOrEmpty(mapDefinition.Title))
                {
                    if (mapDefinition.Title != null)
                    {
                        GraphicElement textBox = layout.FindElement(Constants.MAPS_TITLE) as GraphicElement;
                        if (textBox != null)
                        {
                            CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                            graphic.Text = mapDefinition.Title;
                            textBox.SetGraphic(graphic);

                        }
                    }
                    if (subTitleText != null)
                    {
                        GraphicElement textBox = layout.FindElement(Constants.MAPS_SUBTITLE) as GraphicElement;
                        if (textBox != null)
                        {
                            CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                            graphic.Text = subTitleText;
                            textBox.SetGraphic(graphic);
                        }
                    }
                    if (mapDefinition.UnitsText != null)
                    {
                        GraphicElement textBox = layout.FindElement(Constants.MAPS_TEXTBOX1) as GraphicElement;
                        if (textBox != null)
                        {
                            CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                            graphic.Text = mapDefinition.UnitsText;
                            textBox.SetGraphic(graphic);
                        }
                    }
                }
            });
        }

        public static async Task<BA_ReturnCode> DisplayNorthArrowAsync(Layout layout, string mapFrameName)
        {
            var arcgis_2d = Project.Current.GetItems<StyleProjectItem>().First(si => si.Name == "ArcGIS 2D");

            if (arcgis_2d != null)
            {
                await QueuedTask.Run(() =>
                {
                    var northArrowItems = arcgis_2d.SearchNorthArrows("ESRI North 1");
                    if (northArrowItems == null || northArrowItems.Count == 0) return;
                    NorthArrowStyleItem northArrowStyleItem = northArrowItems[0];

                    //Reference the map frame and define the location
                    MapFrame mapFrame = layout.FindElement(mapFrameName) as MapFrame;
                    Coordinate2D nArrow = new Coordinate2D(7.7906, 0.9906);

                    //Construct the north arrow
                    NorthArrow northArrow = LayoutElementFactory.Instance.CreateNorthArrow(layout, nArrow, mapFrame, northArrowStyleItem);
                    northArrow.SetHeight(0.7037);
                });
                return BA_ReturnCode.Success;
            }
            else
            {
                return BA_ReturnCode.UnknownError;
            }
        }

        public static async Task<BA_ReturnCode> DisplayScaleBarAsync(Layout layout, string mapFrameName)
        {
            var arcgis_2d = Project.Current.GetItems<StyleProjectItem>().First(si => si.Name == "ArcGIS 2D");

            if (arcgis_2d != null)
            {
                await QueuedTask.Run(() =>
                {
                    var scaleBars = arcgis_2d.SearchScaleBars("Scale Line ");
                    if (scaleBars == null || scaleBars.Count == 0) return;
                        ScaleBarStyleItem scaleBarStyleItem = scaleBars[0];

                    //Reference the map frame and define the location
                    MapFrame mapFrame = layout.FindElement(mapFrameName) as MapFrame;
                    double coordX = 3.7732;
                    Coordinate2D location = new Coordinate2D(coordX, 1.0975);

                    //Construct the scale bar
                    ScaleBar scaleBar = LayoutElementFactory.Instance.CreateScaleBar(layout, location, mapFrame, scaleBarStyleItem);
                    CIMScaleBar cimScaleBar = (CIMScaleBar)scaleBar.GetDefinition();
                    cimScaleBar.MarkPosition = ScaleBarVerticalPosition.Above;
                    cimScaleBar.UnitLabelPosition = ScaleBarLabelPosition.AfterLabels;
                    //cimScaleBar.AlignToZeroPoint = true;
                    scaleBar.SetDefinition(cimScaleBar);

                    // Second scale bar for kilometers
                    // https://support.esri.com/en/technical-article/000011784
                    // This article recommended setting AlignToZeroPoint to true but when I set this, the scale bars aren't added to
                    // the map. Bug? Current version: 2.63
                    scaleBars = arcgis_2d.SearchScaleBars("Scale Line 3");
                    if (scaleBars == null || scaleBars.Count == 0) return;
                    ScaleBarStyleItem scaleBarStyleItem2 = scaleBars[0];

                    //Define the location
                    Coordinate2D location2 = new Coordinate2D(coordX, 0.8035);

                    //Construct the scale bar
                    ScaleBar scaleBar2 = LayoutElementFactory.Instance.CreateScaleBar(layout, location2, mapFrame, scaleBarStyleItem2);
                    CIMScaleBar cimScaleBar2 = (CIMScaleBar)scaleBar2.GetDefinition();
                    cimScaleBar2.Units = LinearUnit.Kilometers;
                    cimScaleBar2.UnitLabel = cimScaleBar2.Units.Name + "s";
                    cimScaleBar2.UnitLabelPosition = ScaleBarLabelPosition.AfterLabels;
                    //cimScaleBar2.AlignToZeroPoint = true;
                    scaleBar2.SetDefinition(cimScaleBar2);

                });
                return BA_ReturnCode.Success;
            }
            else
            {
                return BA_ReturnCode.UnknownError;
            }
        }

        public static BA_Objects.MapDefinition LoadMapDefinition(BagisMapType mapType)
        {

            BA_Objects.MapDefinition mapDefinition = null;
            IList<string> lstLayers = new List<string>();
            IList<string> lstLegendLayers = new List<string>();

            // Get Analysis object for maps that need it
            BA_Objects.Analysis oAnalysis = new BA_Objects.Analysis();
            if (mapType == BagisMapType.PRISM || mapType == BagisMapType.WINTER_PRECIPITATION)
            {
                string settingsPath = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAPS + "\\" +
                    Constants.FILE_SETTINGS;
                if (System.IO.File.Exists(settingsPath))
                {
                    using (var file = new System.IO.StreamReader(settingsPath))
                    {
                        var reader = new System.Xml.Serialization.XmlSerializer(typeof(BA_Objects.Analysis));
                        oAnalysis = (BA_Objects.Analysis)reader.Deserialize(file);
                    }
                }
            }

            switch (mapType)
            {
                case BagisMapType.ELEVATION:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE};
                    lstLegendLayers = new List<string>();
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    lstLegendLayers.Add(Constants.MAPS_ELEV_ZONE);

                    string strDemDisplayUnits = (string)Module1.Current.BatchToolSettings.DemDisplayUnits;
                    mapDefinition = new BA_Objects.MapDefinition("ELEVATION DISTRIBUTION",
                        "Elevation Units = " + strDemDisplayUnits, Constants.FILE_EXPORT_MAP_ELEV_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SLOPE:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_SLOPE_ZONE};
                    lstLegendLayers = new List<string>();
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    lstLegendLayers.Add(Constants.MAPS_SLOPE_ZONE);
                    mapDefinition = new BA_Objects.MapDefinition("SLOPE DISTRIBUTION",
                        " ", Constants.FILE_EXPORT_MAP_SLOPE_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.ASPECT:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ASPECT_ZONE};
                    lstLegendLayers = new List<string>();
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    lstLegendLayers.Add(Constants.MAPS_ASPECT_ZONE);
                    mapDefinition = new BA_Objects.MapDefinition("ASPECT DISTRIBUTION",
                        " ", Constants.FILE_EXPORT_MAP_ASPECT_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SNODAS_SWE:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.LAYER_NAMES_SNODAS_SWE[5]};
                    lstLegendLayers = new List<string>();
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    lstLegendLayers.Add(Constants.LAYER_NAMES_SNODAS_SWE[5]);
                    mapDefinition = new BA_Objects.MapDefinition(Constants.MAP_TITLES_SNODAS_SWE[5],
                        "Depth Units = " + Module1.Current.BatchToolSettings.SweDisplayUnits, Constants.FILE_EXPORT_MAPS_SWE[5]);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.PRISM:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_PRISM_ZONE};
                    lstLegendLayers = new List<string>();
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    lstLegendLayers.Add(Constants.MAPS_PRISM_ZONE);
                    string strTitle = "PRECIPITATION DISTRIBUTION";
                    if (!String.IsNullOrEmpty(oAnalysis.PrecipZonesBegin))
                    {
                        string strPrefix = LookupTables.PrismText[oAnalysis.PrecipZonesBegin].ToUpper();
                        strTitle = strPrefix + " " + strTitle;
                    }
                    mapDefinition = new BA_Objects.MapDefinition(strTitle,
                        "Precipitation Units = Inches", Constants.FILE_EXPORT_MAP_PRECIPITATION_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SNOTEL:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_SNOTEL_REPRESENTED};
                    lstLegendLayers = new List<string>();
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    lstLegendLayers.Add(Constants.MAPS_SNOTEL_REPRESENTED);
                    mapDefinition = new BA_Objects.MapDefinition("SNOTEL SITES REPRESENTATION",
                        " ", Constants.FILE_EXPORT_MAP_SNOTEL_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SCOS:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_SNOW_COURSE_REPRESENTED};
                    lstLegendLayers = new List<string>();
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE_REPRESENTED);
                    mapDefinition = new BA_Objects.MapDefinition("SNOW COURSE SITES REPRESENTATION",
                        " ", Constants.FILE_EXPORT_MAP_SCOS_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SITES_ALL:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_ALL_SITES_REPRESENTED};
                    lstLegendLayers = new List<string>();
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    lstLegendLayers.Add(Constants.MAPS_ALL_SITES_REPRESENTED);
                    mapDefinition = new BA_Objects.MapDefinition("SNOTEL AND SNOW COURSE SITES REPRESENTATION",
                        " ", Constants.FILE_EXPORT_MAP_SNOTEL_AND_SCOS_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.ROADS:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Module1.Current.RoadsLayerLegend};
                    lstLegendLayers = new List<string> { Module1.Current.RoadsLayerLegend };
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    mapDefinition = new BA_Objects.MapDefinition("PROXIMITY TO ACCESS ROAD",
                        " ", Constants.FILE_EXPORT_MAP_ROADS_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.PUBLIC_LAND_ZONES:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_FEDERAL_PUBLIC_LAND_ZONES};
                    lstLegendLayers = new List<string> { Constants.MAPS_FEDERAL_PUBLIC_LAND_ZONES };
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    mapDefinition = new BA_Objects.MapDefinition("FEDERAL NON-WILDERNESS LAND",
                        " ", Constants.FILE_EXPORT_MAP_PUBLIC_LAND_ZONES_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.BELOW_TREELINE:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_BELOW_TREELINE};
                    lstLegendLayers = new List<string> { Constants.MAPS_BELOW_TREELINE };
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    mapDefinition = new BA_Objects.MapDefinition("AREA BELOW TREELINE",
                        " ", Constants.FILE_EXPORT_MAP_BELOW_TREELINE_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SITES_LOCATION:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_SITES_LOCATION};
                    lstLegendLayers = new List<string>();
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    lstLegendLayers.Add(Constants.MAPS_SITES_LOCATION);
                    mapDefinition = new BA_Objects.MapDefinition("POTENTIAL SITE LOCATIONS",
                        " ", Constants.FILE_EXPORT_MAP_SITES_LOCATION_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.PRECIPITATION_CONTRIBUTION:
                    lstLayers = new List<string> { Constants.MAPS_SUBBASIN_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_PRECIPITATION_CONTRIBUTION,
                                                    Constants.MAPS_AOI_BOUNDARY};
                    lstLegendLayers = new List<string>();
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    lstLegendLayers.Add(Constants.MAPS_PRECIPITATION_CONTRIBUTION);
                    mapDefinition = new BA_Objects.MapDefinition("SUBBASIN ANNUAL PRECIPITATION CONTRIBUTION",
                        "Units = Acre Feet", Constants.FILE_EXPORT_MAP_PRECIPITATION_CONTRIBUTION_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.CRITICAL_PRECIP:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_CRITICAL_PRECIPITATION_ZONES};
                    lstLegendLayers = new List<string> { Constants.MAPS_CRITICAL_PRECIPITATION_ZONES, Constants.MAPS_ELEV_ZONE };
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    mapDefinition = new BA_Objects.MapDefinition("CRITICAL PRECIPITATION ZONES",
                        " ", Constants.FILE_EXPORT_MAP_CRITICAL_PRECIPITATION_ZONES_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;

                case BagisMapType.PUBLIC_LAND_OWNERSHIP:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_PUBLIC_LAND_OWNERSHIP };
                    lstLegendLayers = new List<string> { Constants.MAPS_PUBLIC_LAND_OWNERSHIP };
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    mapDefinition = new BA_Objects.MapDefinition("PUBLIC LAND OWNERSHIP",
                        " ", Constants.FILE_EXPORT_MAP_PUBLIC_LAND_OWNERSHIP_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.WINTER_PRECIPITATION:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_WINTER_PRECIPITATION};
                    lstLegendLayers = new List<string>();
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    lstLegendLayers.Add(Constants.MAPS_WINTER_PRECIPITATION);
                    strTitle = "WINTER PRECIPITATION";
                    if (!String.IsNullOrEmpty(oAnalysis.WinterStartMonth))
                    {
                        string strSuffix = " (" + oAnalysis.WinterStartMonth.ToUpper() + " - " + 
                            oAnalysis.WinterEndMonth.ToUpper() + ")";
                        strTitle = strTitle + strSuffix;
                    }
                    mapDefinition = new BA_Objects.MapDefinition(strTitle,
                        "Precipitation Units = Inches", Constants.FILE_EXPORT_MAP_WINTER_PRECIPITATION_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
            }
            return mapDefinition;
        }

        public static void DeactivateMapButtons()
        {
            foreach (string strButtonState in Constants.STATES_WATERSHED_MAP_BUTTONS)
            {
                Module1.DeactivateState(strButtonState);
            }
            //foreach (string strButtonState in Constants.STATES_SITE_ANALYSIS_MAP_BUTTONS)
            //{
            //    Module1.DeactivateState(strButtonState);
            //}
            // if you can't use the maps, you can't export to pdf
            Module1.DeactivateState("BtnMapLoad_State");
        }

        public static async Task<BA_ReturnCode> LoadSweMapAsync(string strRaster, string strNewLayerName,
                                                                string strTitle, string strFileMapExport, bool bIsDelta)
        {
            RasterDataset rDataset = null;
            Layer oLayer = null;
            Map map = MapView.Active.Map;
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            Uri uriSweGdb = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers));
            if (bIsDelta)
            {
                uriSweGdb = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis));
            }
            Layout layout = null;
            await QueuedTask.Run(() =>
            {
                Project proj = Project.Current;

                //Get the default map layout
                LayoutProjectItem lytItem =
                   proj.GetItems<LayoutProjectItem>()
                       .FirstOrDefault(m => m.Name.Equals(Constants.MAPS_DEFAULT_LAYOUT_NAME,
                       StringComparison.CurrentCultureIgnoreCase));
                if (lytItem != null)
                {
                    layout = lytItem.GetLayout();
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(LoadSweMapAsync),
                        "Unable to find default layout!!");
                    MessageBox.Show("Unable to find default layout. Cannot display maps!");
                    return;
                }

                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(uriSweGdb)))
                {
                    // Use the geodatabase.
                    try
                    {
                        rDataset = geodatabase.OpenDataset<RasterDataset>(strRaster);
                    }
                    catch (GeodatabaseTableException e)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(LoadSweMapAsync),
                           "Unable to open raster " + strRaster);
                        Module1.Current.ModuleLogManager.LogError(nameof(LoadSweMapAsync),
                            "Exception: " + e.Message);
                        return;
                    }
                }
                if (!bIsDelta)
                {
                    oLayer = map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(Module1.Current.DisplayedSweMap, StringComparison.CurrentCultureIgnoreCase));
                }
                else
                {
                    oLayer = map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(Module1.Current.DisplayedSweDeltaMap, StringComparison.CurrentCultureIgnoreCase));
                }
            });

            await QueuedTask.Run(() =>
            {
                if (oLayer.CanReplaceDataSource(rDataset))
                {
                    oLayer.ReplaceDataSource(rDataset);
                    oLayer.SetName(strNewLayerName);
                }
                GraphicElement textBox = layout.FindElement(Constants.MAPS_TITLE) as GraphicElement;
                if (textBox != null)
                {
                    CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                    graphic.Text = strTitle;
                    textBox.SetGraphic(graphic);
                }
                textBox = layout.FindElement(Constants.MAPS_SUBTITLE) as GraphicElement;
                if (textBox != null)
                {
                    CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                    graphic.Text = Module1.Current.Aoi.NwccName.ToUpper();
                    textBox.SetGraphic(graphic);
                    success = BA_ReturnCode.Success;
                }

                textBox = layout.FindElement(Constants.MAPS_TEXTBOX1) as GraphicElement;
                if (textBox != null)
                {
                    CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                    if (graphic != null)
                    {
                        if (bIsDelta)
                        {
                            graphic.Text = "Depth Units = " + Module1.Current.BatchToolSettings.SweDisplayUnits +
                                           "\r\nSource = SNODAS";
                        }
                        else
                        {
                            graphic.Text = "Depth Units = " + Module1.Current.BatchToolSettings.SweDisplayUnits;
                        }
                    }
                    textBox.SetGraphic(graphic);
                 }
            });

            // toggle layers according to map definition
            var allLayers = MapView.Active.Map.Layers.ToList();
            IList<string> lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                         Constants.MAPS_HILLSHADE, strNewLayerName};
            if (bIsDelta)
            {
                lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, strNewLayerName};
            }
            IList<string> lstLegend = new List<string>();

            if (Module1.Current.Aoi.HasSnotel)
            {
                lstLayers.Add(Constants.MAPS_SNOTEL);
                lstLegend.Add(Constants.MAPS_SNOTEL);
            }
            if (Module1.Current.Aoi.HasSnowCourse)
            {
                lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                lstLegend.Add(Constants.MAPS_SNOW_COURSE);
            }
            lstLegend.Add(strNewLayerName);
            await QueuedTask.Run(() =>
            {
                foreach (var layer in allLayers)
                {
                    if (lstLayers.Contains(layer.Name))
                    {
                        layer.SetVisibility(true);
                    }
                    else
                    {
                        layer.SetVisibility(false);
                    }
                }
            });

            success = await MapTools.UpdateLegendAsync(layout, lstLegend);

            if (success == BA_ReturnCode.Success)
            {
                Module1.Current.DisplayedMap = strFileMapExport;
                if (!bIsDelta)
                {
                    Module1.Current.DisplayedSweMap = strNewLayerName;
                }
                else
                {
                    Module1.Current.DisplayedSweDeltaMap = strNewLayerName;
                }
                
            }
            return success;
        }

        public static async Task<BA_ReturnCode> UpdateMapAsync(string strGeodatabasePath, string strRaster, string strOldLayerName,
                                                               string strNewLayerName, string strTitle, string strUnitsText,
                                                               bool bDisplaySites, string strFileMapExport)
        {
            RasterDataset rDataset = null;
            Layer oLayer = null;
            Map map = MapView.Active.Map;
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            Uri uriGdb = new Uri(strGeodatabasePath);
            Layout layout = null;
            await QueuedTask.Run(() =>
            {
                Project proj = Project.Current;

                //Get the default map layout
                LayoutProjectItem lytItem =
                   proj.GetItems<LayoutProjectItem>()
                       .FirstOrDefault(m => m.Name.Equals(Constants.MAPS_DEFAULT_LAYOUT_NAME,
                       StringComparison.CurrentCultureIgnoreCase));
                if (lytItem != null)
                {
                    layout = lytItem.GetLayout();
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(LoadSweMapAsync),
                        "Unable to find default layout!!");
                    MessageBox.Show("Unable to find default layout. Cannot display maps!");
                    return;
                }

                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(uriGdb)))
                {
                    // Use the geodatabase.
                    try
                    {
                        rDataset = geodatabase.OpenDataset<RasterDataset>(strRaster);
                    }
                    catch (GeodatabaseTableException e)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(LoadSweMapAsync),
                           "Unable to open raster " + strRaster);
                        Module1.Current.ModuleLogManager.LogError(nameof(LoadSweMapAsync),
                            "Exception: " + e.Message);
                        return;
                    }
                }
                oLayer = map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(strOldLayerName, StringComparison.CurrentCultureIgnoreCase));
            });

            await QueuedTask.Run(() =>
            {
                if (oLayer.CanReplaceDataSource(rDataset))
                {
                    oLayer.ReplaceDataSource(rDataset);
                    oLayer.SetName(strNewLayerName);
                }
                GraphicElement textBox = layout.FindElement(Constants.MAPS_TITLE) as GraphicElement;
                if (textBox != null)
                {
                    CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                    graphic.Text = strTitle;
                    textBox.SetGraphic(graphic);
                }
                textBox = layout.FindElement(Constants.MAPS_SUBTITLE) as GraphicElement;
                if (textBox != null)
                {
                    CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                    graphic.Text = Module1.Current.Aoi.NwccName.ToUpper();
                    textBox.SetGraphic(graphic);
                    success = BA_ReturnCode.Success;
                }

                textBox = layout.FindElement(Constants.MAPS_TEXTBOX1) as GraphicElement;
                if (textBox != null)
                {
                    CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                    if (graphic != null)
                    {
                        graphic.Text = strUnitsText;
                    }
                    textBox.SetGraphic(graphic);
                }
            });

            // toggle layers according to map definition
            var allLayers = MapView.Active.Map.Layers.ToList();
            IList<string> lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                         Constants.MAPS_HILLSHADE, strNewLayerName};
            IList<string> lstLegend = new List<string>();

            if (Module1.Current.Aoi.HasSnotel == true && bDisplaySites)
            {
                lstLayers.Add(Constants.MAPS_SNOTEL);
                lstLegend.Add(Constants.MAPS_SNOTEL);
            }
            if (Module1.Current.Aoi.HasSnowCourse == true && bDisplaySites)
            {
                lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                lstLegend.Add(Constants.MAPS_SNOW_COURSE);
            }
            lstLegend.Add(strNewLayerName);
            await QueuedTask.Run(() =>
            {
                foreach (var layer in allLayers)
                {
                    if (lstLayers.Contains(layer.Name))
                    {
                        layer.SetVisibility(true);
                    }
                    else
                    {
                        layer.SetVisibility(false);
                    }
                }
            });

            success = await MapTools.UpdateLegendAsync(layout, lstLegend);

            if (success == BA_ReturnCode.Success)
            {
                Module1.Current.DisplayedMap = strFileMapExport;
                Module1.Current.DisplayedSeasonalPrecipContribMap = strNewLayerName;
            }
            return success;
        }


        public static async Task<BA_ReturnCode> DisplaySWEMapAsync(int idxDefaultMonth)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            string strPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true) +
                Constants.FILES_SNODAS_SWE[idxDefaultMonth];
            Uri uri = new Uri(strPath);

            double[] arrReturnValues = await MapTools.SWEUnitsConversionAsync(Constants.DATA_TYPE_SWE, idxDefaultMonth);
            double dblStretchMin = arrReturnValues[IDX_STRETCH_MIN];
            if (dblStretchMin != ERROR_MIN)
            {
                double dblStretchMax = arrReturnValues[IDX_STRETCH_MAX];
                double dblLabelMin = arrReturnValues[IDX_LABEL_MIN];
                double dblLabelMax = arrReturnValues[IDX_LABEL_MAX];

                success = await MapTools.DisplayStretchRasterWithSymbolAsync(uri, Constants.LAYER_NAMES_SNODAS_SWE[idxDefaultMonth], "ColorBrewer Schemes (RGB)",
                    "Green-Blue (Continuous)", 30, false, true, dblStretchMin, dblStretchMax, dblLabelMin,
                    dblLabelMax);
                IList<string> lstLayersFiles = new List<string>();
                if (success == BA_ReturnCode.Success)
                {
                    await QueuedTask.Run(() =>
                    {
                        // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                        Uri layersUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers));
                        using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(layersUri)))
                        {
                            IReadOnlyList<RasterDatasetDefinition> definitions = geodatabase.GetDefinitions<RasterDatasetDefinition>();
                            foreach (RasterDatasetDefinition def in definitions)
                            {
                                lstLayersFiles.Add(def.GetName());
                            }
                        }
                    });
                    int idx = 0;
                    foreach (string strSweName in Constants.FILES_SNODAS_SWE)
                    {
                        if (lstLayersFiles.Contains(strSweName))
                        {
                            switch (idx)
                            {
                                case 0:     //November
                                    Module1.ActivateState("MapButtonPalette_BtnSweNov_State");
                                    break;
                                case 1:     //December
                                    Module1.ActivateState("MapButtonPalette_BtnSweDec_State");
                                    break;
                                case 2:     //January
                                    Module1.ActivateState("MapButtonPalette_BtnSweJan_State");
                                    break;
                                case 3:     //February
                                    Module1.ActivateState("MapButtonPalette_BtnSweFeb_State");
                                    break;
                                case 4:     //March
                                    Module1.ActivateState("MapButtonPalette_BtnSweMar_State");
                                    break;
                                case 5:     //April
                                    Module1.ActivateState("MapButtonPalette_BtnSweApr_State");
                                    break;
                                case 6:     //May
                                    Module1.ActivateState("MapButtonPalette_BtnSweMay_State");
                                    break;
                                case 7:     //June
                                    Module1.ActivateState("MapButtonPalette_BtnSweJun_State");
                                    break;
                                case 8:     //July
                                    Module1.ActivateState("MapButtonPalette_BtnSweJul_State");
                                    break;
                            }
                        }
                        idx++;
                    }
                }
                Module1.Current.DisplayedSweMap = Constants.LAYER_NAMES_SNODAS_SWE[idxDefaultMonth];
            }
            return success;
        }

        private static async Task<double[]> SWEUnitsConversionAsync(string strDataType, int idxDefaultMonth)
        {
            double[] arrReturnValues = new double[4];
            IDictionary<string, BA_Objects.DataSource> dictLocalDataSources = GeneralTools.QueryLocalDataSources();
            BA_Objects.DataSource oValuesDataSource = null;
            BA_Objects.DataSource oUnitsDataSource = null;
            if (dictLocalDataSources.ContainsKey(strDataType))
            {
               oValuesDataSource = dictLocalDataSources[strDataType];
            }
            if (dictLocalDataSources.ContainsKey(Constants.DATA_TYPE_SWE))
            {
                oUnitsDataSource = dictLocalDataSources[Constants.DATA_TYPE_SWE];
            }
            if (oValuesDataSource != null && oUnitsDataSource != null)
            {
                double dblStretchMin = oValuesDataSource.minValue;
                double dblStretchMax = oValuesDataSource.maxValue;
                string strPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true) +
                    Constants.FILES_SNODAS_SWE[idxDefaultMonth];
                string strBagisTag = await GeneralTools.GetBagisTagAsync(strPath, Constants.META_TAG_XPATH);
                string layerUnits = "";
                if (!string.IsNullOrEmpty(strBagisTag))
                {
                    layerUnits = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_ZUNIT_VALUE, ';');
                }
                if (string.IsNullOrEmpty(layerUnits))
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(SWEUnitsConversionAsync),
                        "Unable to read units from layer. Reading from local config file!!");
                    layerUnits = oUnitsDataSource.units;
                }
                string strSweDisplayUnits = Module1.Current.BatchToolSettings.SweDisplayUnits;
                if (layerUnits != null && !strSweDisplayUnits.Equals(layerUnits))
                {
                    double dblLabelMin = dblStretchMin;
                    double dblLabelMax = dblStretchMax;
                    switch (strSweDisplayUnits)
                    {
                        case Constants.UNITS_INCHES:
                            dblLabelMin = LinearUnit.Millimeters.ConvertTo(dblStretchMin, LinearUnit.Inches);
                            dblLabelMax = LinearUnit.Millimeters.ConvertTo(dblStretchMax, LinearUnit.Inches);
                            break;
                        case Constants.UNITS_MILLIMETERS:
                            dblLabelMin = LinearUnit.Inches.ConvertTo(dblStretchMin, LinearUnit.Millimeters);
                            dblLabelMax = LinearUnit.Inches.ConvertTo(dblStretchMax, LinearUnit.Millimeters);
                            break;
                        default:

                            Module1.Current.ModuleLogManager.LogError(nameof(SWEUnitsConversionAsync),
                                "The display units are invalid!!");
                            break;
                    }
                    arrReturnValues[IDX_STRETCH_MIN] = dblStretchMin;
                    arrReturnValues[IDX_STRETCH_MAX] = dblStretchMax;
                    arrReturnValues[IDX_LABEL_MIN] = dblLabelMin;
                    arrReturnValues[IDX_LABEL_MAX] = dblLabelMax;
                }
            }
            else
            {
                arrReturnValues[IDX_STRETCH_MIN] = ERROR_MIN;
            }
            return arrReturnValues;
        }

        public static async Task<BA_ReturnCode> DisplaySWEDeltaMapAsync(int idxDefaultDeltaMonth, int idxDefaultUnitsMonth)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            string strPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) +
                Constants.FILES_SWE_DELTA[idxDefaultDeltaMonth];
            Uri uri = new Uri(strPath);

            double[] arrReturnValues = await MapTools.SWEUnitsConversionAsync(Constants.DATA_TYPE_SWE_DELTA, idxDefaultUnitsMonth);
            double dblStretchMin = arrReturnValues[IDX_STRETCH_MIN];
            if (dblStretchMin != ERROR_MIN)
            {
                double dblStretchMax = arrReturnValues[IDX_STRETCH_MAX];
                double dblLabelMin = arrReturnValues[IDX_LABEL_MIN];
                double dblLabelMax = arrReturnValues[IDX_LABEL_MAX];

                // Need to set stretch min and max to same value so that white correctly displays 0 on all maps
                // Check min and max values for whichever has the greatest absolute value
                // Set min to the negative of that number and max to the positive of that number.
                // https://community.esri.com/t5/arcgis-mapping-and-charting/set-meaningful-break-value-in-divergent-color-ramp/td-p/393182
                if (Math.Abs(dblStretchMin) > Math.Abs(dblStretchMax))
                {
                    dblStretchMax = Math.Abs(dblStretchMin);
                }
                else if (Math.Abs(dblStretchMin) < Math.Abs(dblStretchMax))
                {
                    dblStretchMin = -1 * dblStretchMax;
                }

                success = await MapTools.DisplayStretchRasterWithSymbolAsync(uri, Constants.LAYER_NAMES_SWE_DELTA[idxDefaultDeltaMonth], "ColorBrewer Schemes (RGB)",
                    "Red-Blue (Continuous)", 0, false, true, dblStretchMin, dblStretchMax, dblLabelMin,
                    dblLabelMax);
                IList<string> lstLayersFiles = new List<string>();
                if (success == BA_ReturnCode.Success)
                {
                    await QueuedTask.Run(() =>
                    {
                        // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                        Uri analysisUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis));
                        using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(analysisUri)))
                        {
                            IReadOnlyList<RasterDatasetDefinition> definitions = geodatabase.GetDefinitions<RasterDatasetDefinition>();
                            foreach (RasterDatasetDefinition def in definitions)
                            {
                                lstLayersFiles.Add(def.GetName());
                            }
                        }
                    });
                    int idx = 0;
                    foreach (string strSweName in Constants.FILES_SWE_DELTA)
                    {
                        if (lstLayersFiles.Contains(strSweName))
                        {
                            switch (idx)
                            {
                                case 0:     //Dec - Nov SWE
                                    Module1.ActivateState("MapButtonPalette_BtnSweNovDelta_State");
                                    break;
                                case 1:     //Jan - Dec SWE
                                    Module1.ActivateState("MapButtonPalette_BtnSweDecDelta_State");
                                    break;
                                case 2:     //Feb - Jan SWE
                                    Module1.ActivateState("MapButtonPalette_BtnSweJanDelta_State");
                                    break;
                                case 3:     //Mar - Feb SWE
                                    Module1.ActivateState("MapButtonPalette_BtnSweFebDelta_State");
                                    break;
                                case 4:     //Apr - Mar SWE
                                    Module1.ActivateState("MapButtonPalette_BtnSweMarDelta_State");
                                    break;
                                case 5:     //May - Apr SWE
                                    Module1.ActivateState("MapButtonPalette_BtnSweAprDelta_State");
                                    break;
                                case 6:     //Jun - May SWE
                                    Module1.ActivateState("MapButtonPalette_BtnSweMayDelta_State");
                                    break;
                                case 7:     //Jul - Jun SWE
                                    Module1.ActivateState("MapButtonPalette_BtnSweJunDelta_State");
                                    break;
                            }
                        }
                        idx++;
                    }
                }
                Module1.Current.DisplayedSweDeltaMap = Constants.LAYER_NAMES_SWE_DELTA[idxDefaultDeltaMonth];
            }
            return success;
        }

        public static async Task<BA_ReturnCode> DisplaySeasonalPrecipContribMapAsync(int idxDefaultMonth, double dblMaxPercent)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            string strPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) +
                Constants.FILES_SEASON_PRECIP_CONTRIB[idxDefaultMonth];
            Uri uri = new Uri(strPath);
            

            success = await MapTools.DisplayStretchRasterWithSymbolAsync(uri, Constants.LAYER_NAMES_SEASON_PRECIP_CONTRIB[idxDefaultMonth], "ColorBrewer Schemes (RGB)",
                    "Yellow-Orange-Red (continuous)", 30, false, true, 0, dblMaxPercent, 0, Math.Ceiling(dblMaxPercent));
            IList<string> lstLayersFiles = new List<string>();
                if (success == BA_ReturnCode.Success)
                {
                    await QueuedTask.Run(() =>
                    {
                        // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                        Uri analysisUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis));
                        using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(analysisUri)))
                        {
                            IReadOnlyList<RasterDatasetDefinition> definitions = geodatabase.GetDefinitions<RasterDatasetDefinition>();
                            foreach (RasterDatasetDefinition def in definitions)
                            {
                                lstLayersFiles.Add(def.GetName());
                            }
                        }
                    });
                    int idx = 0;
                    foreach (string strSqName in Constants.FILES_SEASON_PRECIP_CONTRIB)
                    {
                        if (lstLayersFiles.Contains(strSqName))
                        {
                            switch (idx)
                            {
                                case 0:     //SQ1
                                    Module1.ActivateState("MapButtonPalette_BtnSeasonalPrecipContribSQ1_State");
                                    break;
                                case 1:     //SQ2
                                    Module1.ActivateState("MapButtonPalette_BtnSeasonalPrecipContribSQ2_State");
                                    break;
                                case 2:     //SQ3
                                    Module1.ActivateState("MapButtonPalette_BtnSeasonalPrecipContribSQ3_State");
                                    break;
                                case 3:     //SQ4
                                    Module1.ActivateState("MapButtonPalette_BtnSeasonalPrecipContribSQ4_State");
                                    break;
                            }
                        }
                        idx++;
                    }
                }
                Module1.Current.DisplayedSeasonalPrecipContribMap = Constants.LAYER_NAMES_SEASON_PRECIP_CONTRIB[idxDefaultMonth];
            return success;
        }

        public static async Task<BA_ReturnCode> PublishMapsAsync(ReportType rType)
        {
            string[] arrStates = Constants.STATES_WATERSHED_MAP_BUTTONS;
            //if (rType.Equals(ReportType.SiteAnalysis))
            //{
            //    arrStates = Constants.STATES_SITE_ANALYSIS_MAP_BUTTONS;
            //}
            foreach (string strButtonState in arrStates)
            {
                if (FrameworkApplication.State.Contains(strButtonState))
                {
                    int foundS1 = strButtonState.IndexOf("_State");
                    string strMapButton = strButtonState.Remove(foundS1);
                    ICommand cmd = FrameworkApplication.GetPlugInWrapper(strMapButton) as ICommand;
                    Module1.Current.ModuleLogManager.LogDebug(nameof(PublishMapsAsync),
                        "About to toggle map button " + strMapButton);

                    if ((cmd != null))
                    {
                        do
                        {
                            await Task.Delay(TimeSpan.FromSeconds(0.4));  // build in delay until the command can execute
                        }
                        while (!cmd.CanExecute(null));
                        cmd.Execute(null);
                    }

                    do
                    {
                        await Task.Delay(TimeSpan.FromSeconds(0.4));  // build in delay so maps can load
                    }
                    while (Module1.Current.MapFinishedLoading == false);

                    BA_ReturnCode success2 = await GeneralTools.ExportMapToPdfAsync();    // export each map to pdf
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(PublishMapsAsync),
                        strButtonState + " not enabled for this AOI ");
                }
            }
            return BA_ReturnCode.Success;
        }
    }
}
