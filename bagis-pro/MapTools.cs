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
        public static async Task DisplayMaps(string strAoiPath)
        {
            BA_Objects.Aoi oAoi = Module1.Current.Aoi;
            if (String.IsNullOrEmpty(oAoi.Name))
            {
                if (System.IO.Directory.Exists(strAoiPath))
                {
                    // Initialize AOI object
                    oAoi = new BA_Objects.Aoi("animas_AOI_prms", strAoiPath);
                    // Store current AOI in Module1
                    Module1.Current.Aoi = oAoi;
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
                if (oMap.Layers.Count() > 0)
                {
                    string strMessage = "Adding the maps to the display will overwrite the current arrangement of data layers. " +
                           "This action cannot be undone." + System.Environment.NewLine + "Do you wish to continue ?";
                    MessageBoxResult oRes = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(strMessage, "BAGIS", MessageBoxButton.YesNo);
                    if (oRes != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                Layout layout = await MapTools.GetDefaultLayoutAsync(Constants.MAPS_DEFAULT_LAYOUT_NAME);
                if (layout != null)
                {
                    bool bFoundIt = false;
                    //A layout view may exist but it may not be active
                    //Iterate through each pane in the application and check to see if the layout is already open and if so, activate it
                    foreach (var pane in ProApp.Panes)
                    {
                        if (!(pane is ILayoutPane layoutPane))  //if not a layout view, continue to the next pane    
                            continue;
                        if (layoutPane.LayoutView.Layout == layout) //if there is a match, activate the view  
                        {
                            (layoutPane as Pane).Activate();
                            bFoundIt = true;
                        }
                    }
                    if (!bFoundIt)
                    {
                        ILayoutPane iNewLayoutPane = await ProApp.Panes.CreateLayoutPaneAsync(layout); //GUI thread
                    }
                    await MapTools.SetDefaultMapFrameDimensionAsync(Constants.MAPS_DEFAULT_MAP_FRAME_NAME, layout, oMap,
                        1.0, 2.0, 7.5, 9.0);

                    //remove existing layers from map frame
                    await MapTools.RemoveLayersfromMapFrame();

                    //add aoi boundary to map and zoom to layer
                    string strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi, true) +
                                     Constants.FILE_AOI_VECTOR;
                    Uri aoiUri = new Uri(strPath);
                    await MapTools.AddAoiBoundaryToMapAsync(aoiUri, Constants.MAPS_AOI_BOUNDARY);

                    //add Snotel Represented Area Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SNOTEL_REPRESENTED;
                    Uri uri = new Uri(strPath);
                    CIMColor fillColor = CIMColor.CreateRGBColor(255, 0, 0, 50);    //Red with 30% transparency
                    BA_ReturnCode success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Constants.MAPS_SNOTEL_REPRESENTED);
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

                    // add aoi streams layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                              Constants.FILE_STREAMS;
                    uri = new Uri(strPath);
                    await MapTools.AddLineLayerAsync(uri, Constants.MAPS_STREAMS, ColorFactory.Instance.BlueRGB);

                    // add Snotel Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                              Constants.FILE_SNOTEL;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPointMarkersAsync(uri, Constants.MAPS_SNOTEL, ColorFactory.Instance.BlueRGB,
                        SimpleMarkerStyle.X, 10);
                    if (success == BA_ReturnCode.Success)
                        Module1.Current.AoiHasSnotel = true;

                    // add Snow Course Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                              Constants.FILE_SNOW_COURSE;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPointMarkersAsync(uri, Constants.MAPS_SNOW_COURSE, CIMColor.CreateRGBColor(0, 255, 255),
                        SimpleMarkerStyle.Star, 12);
                    if (success == BA_ReturnCode.Success)
                        Module1.Current.AoiHasSnowCourse = true;

                    // add hillshade layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Surfaces, true) +
                        Constants.FILE_HILLSHADE;
                    uri = new Uri(strPath);
                    await MapTools.DisplayRasterAsync(uri, Constants.MAPS_HILLSHADE, 0);

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
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                        Constants.FILES_SNODAS_SWE[0];
                    uri = new Uri(strPath);
                    //success = await MapTools.DisplayStretchRasterWithSymbolAsync(uri, Constants.MAPS_SNODAS_SWE_JAN, "ColorBrewer Schemes (RGB)",
                    //            "Green-Blue (Continuous)", 30, false);
                    string strLayerFilePath = @"C:\Docs\animas_AOI_prms\maps_publish\SWE.lyrx";
                    success = await MapTools.DisplayRasterFromLayerFileAsync(uri, Constants.MAPS_SNODAS_SWE_JAN, strLayerFilePath, 30, false);
                    if (success == BA_ReturnCode.Success)
                        Module1.ActivateState("MapButtonPalette_BtnJanSwe_State");

                    // add Precipitation layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_PRECIP_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.DisplayRasterWithSymbolAsync(uri, Constants.MAPS_PRISM_ZONE, "ArcGIS Colors",
                               "Precipitation", "NAME", 30, false);
                    if (success == BA_ReturnCode.Success)
                        Module1.ActivateState("MapButtonPalette_BtnPrism_State");


                    // create map elements
                    await MapTools.AddMapElements(Constants.MAPS_DEFAULT_LAYOUT_NAME, "ArcGIS Colors", "1.5 Point");
                    await MapTools.DisplayNorthArrowAsync(layout, Constants.MAPS_DEFAULT_MAP_FRAME_NAME);
                    await MapTools.DisplayScaleBarAsync(layout, Constants.MAPS_DEFAULT_MAP_FRAME_NAME);

                    // update map elements for default map (elevation)
                    BA_Objects.MapDefinition defaultMap = MapTools.LoadMapDefinition(BagisMapType.ELEVATION);
                    await MapTools.UpdateMapElementsAsync(layout, Module1.Current.Aoi.Name.ToUpper(), defaultMap);
                    await MapTools.UpdateLegendAsync(layout, defaultMap);


                    //zoom to aoi boundary layer
                    double bufferFactor = 1.1;
                    bool bZoomed = await MapTools.ZoomToExtentAsync(aoiUri, bufferFactor);
                }
            }
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

        public static async Task SetDefaultMapFrameDimensionAsync(string mapFrameName, Layout oLayout, Map oMap, double xMin,
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
                    await ProApp.Panes.CreateMapPaneAsync(map);
                }
                return map;
            });
        }

        public static async Task AddAoiBoundaryToMapAsync(Uri aoiUri, string displayName = "", double lineSymbolWidth = 1.0)
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
                    IsVisible = true,
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
                    IsVisible = true,
                    RendererDefinition = new SimpleRendererDefinition()
                    {
                        //SymbolTemplate = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor).MakeSymbolReference()
                        SymbolTemplate = SymbolFactory.Instance.ConstructPolygonSymbol(
                        fillColor, SimpleFillStyle.Solid,
                        SymbolFactory.Instance.ConstructStroke(ColorFactory.Instance.BlackRGB, 0))
                        .MakeSymbolReference()
                    }
                };

                FeatureLayer fLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flyrCreatnParam, MapView.Active.Map);
                fLayer.SetVisibility(isVisible);
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
                        Debug.WriteLine("AddLineLayerAsync: Unable to open feature class " + strFileName);
                        Debug.WriteLine("AddLineLayerAsync: " + e.Message);
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
                                    SimpleMarkerStyle markerStyle, double markerSize)
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
                        Debug.WriteLine("DisplayPointMarkersAsync: Unable to open feature class " + strFileName);
                        Debug.WriteLine("DisplayPointMarkersAsync: " + e.Message);
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
            });
            success = BA_ReturnCode.Success;
            return success;
        }

        public static async Task<bool> ZoomToExtentAsync(Uri aoiUri, double bufferFactor = 1)
        {
            //Get the active map view.
            var mapView = MapView.Active;
            if (mapView == null)
                return false;
            string strFileName = null;
            string strFolderPath = null;
            if (aoiUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(aoiUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(aoiUri.LocalPath);
            }

            Envelope zoomEnv = null;
            await QueuedTask.Run(() =>
            {
                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (
                  Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                {
                    // Use the geodatabase.
                    FeatureClassDefinition fcDefinition = geodatabase.GetDefinition<FeatureClassDefinition>(strFileName);
                    zoomEnv = fcDefinition.GetExtent().Expand(bufferFactor, bufferFactor, true);
                }
            });

            //Zoom the view to a given extent.
            return await mapView.ZoomToAsync(zoomEnv, null);
        }

        public static async Task RemoveLayer(Map map, string layerName)
        {
            await QueuedTask.Run(() =>
            {
                Layer oLayer =
                    map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(layerName, StringComparison.CurrentCultureIgnoreCase));
                if (oLayer != null)
                    map.RemoveLayer(oLayer);
            });
        }

        public static async Task RemoveLayersfromMapFrame()
        {
            string[] arrLayerNames = new string[13];
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
            arrLayerNames[11] = Constants.MAPS_SNODAS_SWE_JAN;
            arrLayerNames[12] = Constants.MAPS_PRISM_ZONE;
            var map = MapView.Active.Map;
            await QueuedTask.Run(() =>
            {
                foreach (string strName in arrLayerNames)
                {
                    Layer oLayer =
                        map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(strName, StringComparison.CurrentCultureIgnoreCase));
                    if (oLayer != null)
                        map.RemoveLayer(oLayer);
                }
            });
        }

        public static async Task DisplayRasterAsync(Uri rasterUri, string displayName, int transparency)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (rasterUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(rasterUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(rasterUri.LocalPath);
            }
            await QueuedTask.Run(() =>
            {
                RasterDataset rDataset = null;
                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                {
                    // Use the geodatabase.
                    try
                    {
                        rDataset = geodatabase.OpenDataset<RasterDataset>(strFileName);
                    }
                    catch (GeodatabaseTableException e)
                    {
                        Debug.WriteLine("DisplayRasterAsync: Unable to open raster " + strFileName);
                        Debug.WriteLine("DisplayRasterAsync: " + e.Message);
                        return;
                    }
                }
                // Create a new colorizer definition using default constructor.
                StretchColorizerDefinition stretchColorizerDef = new StretchColorizerDefinition();
                RasterLayer rasterLayer = (RasterLayer)LayerFactory.Instance.CreateLayer(rasterUri, MapView.Active.Map);
                if (rasterLayer != null)
                {
                    rasterLayer.SetTransparency(transparency);
                    rasterLayer.SetName(displayName);
                }
            });
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
                Debug.Print("DisplayRasterWithSymbolAsync: Unable to add locate raster!!");
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
                    rasterLayer.SetTransparency(transparency);
                    rasterLayer.SetName(displayName);
                    rasterLayer.SetVisibility(isVisible);
                    // Create and deploy the unique values renderer
                    await MapTools.SetToUniqueValueColorizer(displayName, styleCategory, styleName, fieldName);
                }
            });
            return BA_ReturnCode.Success;
        }

        public static async Task<BA_ReturnCode> DisplayStretchRasterWithSymbolAsync(Uri rasterUri, string displayName, string styleCategory, string styleName,
            int transparency, bool isVisible)
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
                Debug.Print("DisplayStretchRasterWithSymbolAsync: Unable to add locate raster!!");
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
                    rasterLayer.SetTransparency(transparency);
                    rasterLayer.SetName(displayName);
                    rasterLayer.SetVisibility(isVisible);
                    // Create and deploy the unique values renderer
                    await MapTools.SetToStretchValueColorizer(displayName, styleCategory, styleName);
                }
            });
            return BA_ReturnCode.Success;
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
                Debug.Print("DisplayRasterFromLayerFileAsync: Unable to add locate raster!!");
                return BA_ReturnCode.ReadError;
            }
            // Open the requested raster so we know it exists; return if it doesn't
            await QueuedTask.Run(async () =>
            {
                RasterLayer rasterLayer = null;
                // Create the raster layer on the active map
                await QueuedTask.Run(() =>
                {
                    rasterLayer = (RasterLayer) LayerFactory.Instance.CreateLayer(rasterUri, MapView.Active.Map);
                });

                // Set raster layer transparency and name
                if (rasterLayer != null)
                {
                    //Get the Layer Document from the lyrx file
                    var lyrDocFromLyrxFile = new LayerDocument(layerFilePath);
                    var cimLyrDoc = lyrDocFromLyrxFile.GetCIMLayerDocument();

                    //Get the colorizer from the layer file
                    var layerDefs = cimLyrDoc.LayerDefinitions;
                    var colorizerFromLayerFile = ((CIMRasterLayer) cimLyrDoc.LayerDefinitions[0]).Colorizer as CIMRasterStretchColorizer;

                    //Apply the colorizer to the raster layer
                    rasterLayer?.SetColorizer(colorizerFromLayerFile);

                    //Set the name and transparency
                    rasterLayer?.SetName(displayName);
                    rasterLayer?.SetTransparency(transparency);
                    rasterLayer?.SetVisibility(bIsVisible);
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

        public static async Task SetToStretchValueColorizer(string layerName, string styleCategory, string styleName)
        {
            // Get the layer we want to symbolize from the map
            Layer oLayer =
                MapView.Active.Map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(layerName, StringComparison.CurrentCultureIgnoreCase));
            if (oLayer == null)
                return;
            RasterLayer rasterLayer = (RasterLayer) oLayer;

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

        public static async Task AddMapElements(string layoutName, string styleCategory, string styleName)
        {
            //Finding the first project item with name matches with layoutName
            Layout layout = null;
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
                await MapTools.DisplayTextBoxAsync(layout, Constants.MAPS_TITLE, 4.0, 10.5, ColorFactory.Instance.BlackRGB, 24, "Times New Roman",
                    "Bold", "Title");
                // Map SubTitle
                await MapTools.DisplayTextBoxAsync(layout, Constants.MAPS_SUBTITLE, 4.0, 10.1, ColorFactory.Instance.BlackRGB, 14, "Times New Roman",
                    "Regular", "SubTitle");
                // (optional) textbox
                await MapTools.DisplayTextBoxAsync(layout, Constants.MAPS_TEXTBOX1, 5.0, 1.0, ColorFactory.Instance.BlackRGB, 12, "Times New Roman",
                    "Regular", "Text Box 1");
                // Legend
                await MapTools.DisplayLegendAsync(layout, styleCategory, styleName);
            }
        }

        public static async Task<bool> DisplayTextBoxAsync(Layout layout, string elementName, double xPos, double yPos,
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

            return true;
        }

        public static async Task DisplayLegendAsync(Layout layout, string styleCategory, string styleName)
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
        }

        public async static Task UpdateLegendAsync(Layout layout, BA_Objects.MapDefinition mapDefinition)
        {
            await QueuedTask.Run(() =>
            {
                //Get LayoutCIM and iterate through its elements
                var layoutDef = layout.GetDefinition();

                foreach (var elem in layoutDef.Elements)
                {
                    if (elem is CIMLegend)
                    {
                        var legend = elem as CIMLegend;
                        foreach (var legendItem in legend.Items)
                        {
                            if (mapDefinition.LegendLayerList.Contains(legendItem.Name))
                            {
                                legendItem.IsVisible = true;
                            }
                            else
                            {
                                legendItem.IsVisible = false;
                            }
                        }
                    }
                }
                //Apply the changes back to the layout
                layout.SetDefinition(layoutDef);
            });
        }

        public static async Task UpdateMapElementsAsync(Layout layout, string titleText, BA_Objects.MapDefinition mapDefinition)
        {
            if (layout != null)
            {
                if (!String.IsNullOrEmpty(titleText))
                {
                    if (titleText != null)
                    {
                        GraphicElement textBox = layout.FindElement(Constants.MAPS_TITLE) as GraphicElement;
                        if (textBox != null)
                        {
                            await QueuedTask.Run(() =>
                            {
                                CIMTextGraphic graphic = (CIMTextGraphic)textBox.Graphic;
                                graphic.Text = titleText;
                                textBox.SetGraphic(graphic);
                            });
                        }
                    }
                    if (mapDefinition.SubTitle != null)
                    {
                        GraphicElement textBox = layout.FindElement(Constants.MAPS_SUBTITLE) as GraphicElement;
                        if (textBox != null)
                        {
                            await QueuedTask.Run(() =>
                            {
                                CIMTextGraphic graphic = (CIMTextGraphic)textBox.Graphic;
                                graphic.Text = mapDefinition.SubTitle;
                                textBox.SetGraphic(graphic);
                            });
                        }
                    }
                    if (mapDefinition.UnitsText != null)
                    {
                        GraphicElement textBox = layout.FindElement(Constants.MAPS_TEXTBOX1) as GraphicElement;
                        if (textBox != null)
                        {
                            await QueuedTask.Run(() =>
                            {
                                CIMTextGraphic graphic = (CIMTextGraphic)textBox.Graphic;
                                graphic.Text = mapDefinition.UnitsText;
                                textBox.SetGraphic(graphic);
                            });
                        }
                    }
                }
            }
        }

        public static async Task DisplayNorthArrowAsync(Layout layout, string mapFrameName)
        {
            var arcgis_2d = Project.Current.GetItems<StyleProjectItem>().First(si => si.Name == "ArcGIS 2D");

            await QueuedTask.Run(() =>
            {
                if (arcgis_2d != null)
                {
                    var northArrowItems = arcgis_2d.SearchNorthArrows("ESRI North 1");
                    if (northArrowItems == null || northArrowItems.Count == 0) return;
                    NorthArrowStyleItem northArrowStyleItem = northArrowItems[0];

                    //Reference the map frame and define the location
                    MapFrame mapFrame = layout.FindElement(mapFrameName) as MapFrame;
                    Coordinate2D nArrow = new Coordinate2D(7.7906, 0.8906);

                    //Construct the north arrow
                    NorthArrow northArrow = LayoutElementFactory.Instance.CreateNorthArrow(layout, nArrow, mapFrame, northArrowStyleItem);
                    northArrow.SetHeight(0.7037);
                }
            });
        }

        public static async Task DisplayScaleBarAsync(Layout layout, string mapFrameName)
        {
            var arcgis_2d = Project.Current.GetItems<StyleProjectItem>().First(si => si.Name == "ArcGIS 2D");

            await QueuedTask.Run(() =>
            {
                if (arcgis_2d != null)
                {
                    var scaleBars = arcgis_2d.SearchScaleBars("Alternating Scale Bar");
                    if (scaleBars == null || scaleBars.Count == 0) return;
                    ScaleBarStyleItem scaleBarStyleItem = scaleBars[0];

                    //Reference the map frame and define the location
                    MapFrame mapFrame = layout.FindElement(mapFrameName) as MapFrame;
                    Coordinate2D location = new Coordinate2D(3.8, 0.3);

                    //Construct the scale bar
                    ScaleBar scaleBar = LayoutElementFactory.Instance.CreateScaleBar(layout, location, mapFrame, scaleBarStyleItem);
                    CIMScaleBar cimScaleBar = (CIMScaleBar)scaleBar.GetDefinition();
                    cimScaleBar.Divisions = 2;
                    cimScaleBar.Subdivisions = 4;
                    cimScaleBar.DivisionsBeforeZero = 1;
                    cimScaleBar.MarkFrequency = ScaleBarFrequency.Divisions;
                    cimScaleBar.MarkPosition = ScaleBarVerticalPosition.Above;
                    cimScaleBar.UnitLabelPosition = ScaleBarLabelPosition.AfterLabels;
                    scaleBar.SetDefinition(cimScaleBar);

                }
            });
        }

        public static BA_Objects.MapDefinition LoadMapDefinition(BagisMapType mapType)
        {

            BA_Objects.MapDefinition mapDefinition = null;
            IList<string> lstLayers = new List<string>();
            IList<string> lstLegendLayers = new List<string>();

            switch (mapType)
            {
                case BagisMapType.ELEVATION:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE};
                    lstLegendLayers = new List<string> { Constants.MAPS_ELEV_ZONE };
                    if (Module1.Current.AoiHasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.AoiHasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    // @ToDo: manage elevation units better
                    mapDefinition = new BA_Objects.MapDefinition("ELEVATION DISTRIBUTION",
                        "Elevation Units = Feet", Constants.FILE_EXPORT_MAP_ELEV_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SLOPE:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_SLOPE_ZONE};
                    lstLegendLayers = new List<string> { Constants.MAPS_SLOPE_ZONE };
                    if (Module1.Current.AoiHasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.AoiHasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    mapDefinition = new BA_Objects.MapDefinition("SLOPE DISTRIBUTION",
                        " ", Constants.FILE_EXPORT_MAP_SLOPE_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.ASPECT:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ASPECT_ZONE};
                    lstLegendLayers = new List<string> { Constants.MAPS_ASPECT_ZONE };
                    if (Module1.Current.AoiHasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.AoiHasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    mapDefinition = new BA_Objects.MapDefinition("ASPECT DISTRIBUTION",
                        " ", Constants.FILE_EXPORT_MAP_ASPECT_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SNODAS_SWE:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_SNODAS_SWE_JAN};
                    lstLegendLayers = new List<string> { Constants.MAPS_SNODAS_SWE_JAN };
                    if (Module1.Current.AoiHasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.AoiHasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    mapDefinition = new BA_Objects.MapDefinition(Constants.MAP_TITLES_SNODAS_SWE[0],
                        "Depth Units = Inches", Constants.FILE_EXPORT_MAP_SWE_JANUARY_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.PRISM:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_PRISM_ZONE};
                    lstLegendLayers = new List<string> { Constants.MAPS_PRISM_ZONE };
                    if (Module1.Current.AoiHasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.AoiHasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    mapDefinition = new BA_Objects.MapDefinition("PRECIPITATION DISTRIBUTION",
                        "Precipitation Units = Inches", Constants.FILE_EXPORT_MAP_PRECIPITATION_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SNOTEL:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_SNOTEL_REPRESENTED};
                    lstLegendLayers = new List<string> { Constants.MAPS_SNOTEL_REPRESENTED };
                    if (Module1.Current.AoiHasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    mapDefinition = new BA_Objects.MapDefinition("SNOTEL SITES REPRESENTATION",
                        " ", Constants.FILE_EXPORT_MAP_SNOTEL_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SCOS:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_SNOW_COURSE_REPRESENTED};
                    lstLegendLayers = new List<string> { Constants.MAPS_SNOW_COURSE_REPRESENTED };
                    if (Module1.Current.AoiHasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    mapDefinition = new BA_Objects.MapDefinition("SNOW COURSE SITES REPRESENTATION",
                        " ", Constants.FILE_EXPORT_MAP_SCOS_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SITES_ALL:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_ALL_SITES_REPRESENTED};
                    lstLegendLayers = new List<string> { Constants.MAPS_ALL_SITES_REPRESENTED };
                    if (Module1.Current.AoiHasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    if (Module1.Current.AoiHasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    mapDefinition = new BA_Objects.MapDefinition("SNOTEL AND SNOW COURSE SITES REPRESENTATION",
                        " ", Constants.FILE_EXPORT_MAP_SNOTEL_AND_SCOS_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
            }
            return mapDefinition;
        }

        public static void DeactivateMapButtons()
        {
            foreach (string strButtonState in Constants.STATES_MAP_BUTTON)
            {
                Module1.DeactivateState(strButtonState);
            }
            // if you can't use the maps, you can't export to pdf
            Module1.DeactivateState("BtnMapLoad_State");
        }

        public static async Task<IList<string>> PublishSnodasSweMapsAsync(Uri uriSnodasGdb, int idxDisplayedMap, Map map, Layout layout)
        {
            List<string> lstPdfFilesToAppend = new List<string>();
            List<string> lstLayersToRemove = new List<string>();

            int idx = 1;
            for (idx = 1; idx < Constants.FILES_SNODAS_SWE.Length; idx++) //start with the second raster, the first is already displayed
            {
                Uri fullUri = new Uri(uriSnodasGdb.LocalPath + "\\" + Constants.FILES_SNODAS_SWE[idx]);
                string strPdfFile = await PublishSnodasSweMapAsync(fullUri, map, Constants.LAYER_NAMES_SNODAS_SWE[idx],
                    Constants.MAP_TITLES_SNODAS_SWE[idx], Constants.FILE_EXPORT_MAPS_SWE[idx]);
                if (!String.IsNullOrEmpty(strPdfFile))
                {
                    lstPdfFilesToAppend.Add(strPdfFile);
                    lstLayersToRemove.Add(Constants.LAYER_NAMES_SNODAS_SWE[idx]);
                }
            }
            // Switch the map back to January so it matches the menu item
            if (! Module1.Current.DisplayedMap.Equals(Constants.FILE_EXPORT_MAP_SWE_JANUARY_PDF))
            {
                ICommand cmd = FrameworkApplication.GetPlugInWrapper("MapButtonPalette_BtnJanSwe") as ICommand;

                if ((cmd != null))
                {
                    do
                    {
                        await Task.Delay(TimeSpan.FromSeconds(0.5));  // build in delay until the command can execute
                    }
                    while (!cmd.CanExecute(null));
                    cmd.Execute(null);
                }
            }
            // Delete extra swe layers
            foreach (string layerName in lstLayersToRemove)
            {
                await (MapTools.RemoveLayer(map, layerName));
            }
            return lstPdfFilesToAppend;
        }

        private static async Task<string> PublishSnodasSweMapAsync(Uri uriSnodasGdb, Map map, string strNewLayerName, string strTitle,
                                                    string strFileMapExport)
        {
            Module1.Current.MapFinishedLoading = false;
            // add SNOTEL SWE layer
            //BA_ReturnCode success = await MapTools.DisplayStretchRasterWithSymbolAsync(uriSnodasGdb, strNewLayerName, "ColorBrewer Schemes (RGB)",
            //            "Green-Blue (Continuous)", 30, false);
            string strLayerFilePath = @"C:\Docs\animas_AOI_prms\maps_publish\SWE.lyrx";
            BA_ReturnCode success = await MapTools.DisplayRasterFromLayerFileAsync(uriSnodasGdb, strNewLayerName, strLayerFilePath, 30, false);

            //Get map definition
            BA_Objects.MapDefinition thisMap = new BA_Objects.MapDefinition(strTitle,
                "Depth Units = Inches", strFileMapExport);
            IList<string> lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                         Constants.MAPS_HILLSHADE, strNewLayerName};
            IList<string> lstLegendLayers = new List<string> { strNewLayerName };
            if (Module1.Current.AoiHasSnotel == true)
            {
                lstLayers.Add(Constants.MAPS_SNOTEL);
                lstLegendLayers.Add(Constants.MAPS_SNOTEL);
            }
            if (Module1.Current.AoiHasSnowCourse == true)
            {
                lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
            }
            thisMap.LayerList = lstLayers;
            thisMap.LegendLayerList = lstLegendLayers;

            // toggle layers according to map definition
            var allLayers = MapView.Active.Map.Layers.ToList();
            await QueuedTask.Run(() =>
            {
                foreach (var layer in allLayers)
                {
                    if (thisMap.LayerList.Contains(layer.Name))
                    {
                        layer.SetVisibility(true);
                    }
                    else
                    {
                        layer.SetVisibility(false);
                    }
                }
            });

            Layout layout = await MapTools.GetDefaultLayoutAsync(Constants.MAPS_DEFAULT_LAYOUT_NAME);
            await MapTools.UpdateMapElementsAsync(layout, Module1.Current.Aoi.Name.ToUpper(), thisMap);
            await MapTools.UpdateLegendAsync(layout, thisMap);
            Module1.Current.DisplayedMap = thisMap.PdfFileName;

            Module1.Current.MapFinishedLoading = true;
            if (success == BA_ReturnCode.Success)
            {
                Module1.Current.DisplayedMap = strFileMapExport;
                do
                {
                    await Task.Delay(TimeSpan.FromSeconds(0.5));  // build in delay so maps can load
                }
                while (Module1.Current.MapFinishedLoading == false);
                success = await GeneralTools.ExportMapToPdfAsync();
            }
            if (success == BA_ReturnCode.Success)
            {
                return strFileMapExport;
            }
            else
            {
                return "";
            }
        }
    }

}
