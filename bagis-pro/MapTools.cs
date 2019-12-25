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

namespace bagis_pro
{
    public class MapTools
    {
        public static async Task<Layout> SetDefaultLayoutNameAsync(string layoutName)
        {
            return await QueuedTask.Run( () =>
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
            await QueuedTask.Run( () =>
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
                        Console.WriteLine("AddLineLayerAsync: Unable to open feature class " + strFileName);
                        Console.WriteLine("AddLineLayerAsync: " + e.Message);
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
                        Console.WriteLine("DisplayPointMarkersAsync: Unable to open feature class " + strFileName);
                        Console.WriteLine("DisplayPointMarkersAsync: " + e.Message);
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
                        SymbolTemplate = SymbolFactory.Instance.ConstructPointSymbol(markerColor, markerSize,  markerStyle)
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
            await QueuedTask.Run(() => {
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
            string[] arrLayerNames = new string[6];
            arrLayerNames[0] = Constants.MAPS_AOI_BOUNDARY;
            arrLayerNames[1] = Constants.MAPS_STREAMS;
            arrLayerNames[2] = Constants.MAPS_SNOTEL;
            arrLayerNames[3] = Constants.MAPS_SNOW_COURSE;
            arrLayerNames[4] = Constants.MAPS_HILLSHADE;
            arrLayerNames[5] = Constants.MAPS_ELEV_ZONE;
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
                        Console.WriteLine("DisplayRasterAsync: Unable to open raster " + strFileName);
                        Console.WriteLine("DisplayRasterAsync: " + e.Message);
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

        public static async Task DisplayRasterWithSymbolAsync(Uri rasterUri, string displayName, string styleCategory, string styleName, 
            string fieldName, int transparency)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (rasterUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(rasterUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(rasterUri.LocalPath);
            }
            // Open the requested raster so we know it exists; return if it doesn't
            await QueuedTask.Run(async () =>
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
                        Console.WriteLine("DisplayRasterWithSymbolAsync: Unable to open raster " + strFileName);
                        Console.WriteLine("DisplayRasterWithSymbolAsync: " + e.Message);
                        return;
                    }
                }
                RasterLayer rasterLayer = null;
                // Create the raster layer on the active map
                await QueuedTask.Run(() =>
                {
                    rasterLayer = (RasterLayer) LayerFactory.Instance.CreateLayer(rasterUri, MapView.Active.Map);
                });

                // Set raster layer transparency and name
                if (rasterLayer != null)
                {
                    rasterLayer.SetTransparency(transparency);
                    rasterLayer.SetName(displayName);
                    // Create and deploy the unique values renderer
                    await MapTools.SetToUniqueValueColorizer(displayName,styleCategory, styleName, fieldName);
                }
            });
        }

        public static async Task SetToUniqueValueColorizer(string layerName, string styleCategory, 
            string styleName, string fieldName)
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

        public static async Task<bool> DisplayTextBoxAsync(Layout layout, string elementName, double xPos, double yPos,
                                        CIMColor fontColor, double fontSize, string fontFamily, string fontStyle, string textBoxText)
        {
            await QueuedTask.Run(() =>
            {
                //Build 2D point geometry
                Coordinate2D coord2D = new Coordinate2D(5, 5);

                //Set symbolology, create and add element to layout
                CIMTextSymbol sym = SymbolFactory.Instance.ConstructTextSymbol(fontColor, fontSize, fontFamily, fontStyle);
                sym.HorizontalAlignment = HorizontalAlignment.Left;
                GraphicElement ptTxtElm = LayoutElementFactory.Instance.CreatePointTextGraphicElement(layout, coord2D, textBoxText, sym);
                ptTxtElm.SetName(elementName);
                ptTxtElm.SetAnchor(Anchor.CenterPoint);
                ptTxtElm.SetX(xPos);
                ptTxtElm.SetY(yPos);
            });

            return true;
        }

        public static async Task DisplayLegendAsync (Layout layout, string styleCategory, string styleName)
        {
            //Construct on the worker thread
            await QueuedTask.Run( () =>
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

                // Choose the items we want to show
                IList<string> lstLegend = new List<string>();
                if (Module1.Current.AoiHasSnotel == true)
                    lstLegend.Add(Constants.MAPS_SNOTEL);
                if (Module1.Current.AoiHasSnowCourse == true)
                    lstLegend.Add(Constants.MAPS_SNOW_COURSE);
                lstLegend.Add(Constants.MAPS_ELEV_ZONE);
                CIMLegend cimLeg = legendElm.GetDefinition() as CIMLegend;
                CIMLegendItem[] myLegendItems = new CIMLegendItem[lstLegend.Count];
                int i = 0;
                foreach (CIMLegendItem legItem in cimLeg.Items)
                {
                    if (lstLegend.Contains(legItem.Name))
                    {
                        legItem.ShowHeading = false;
                        myLegendItems[i] = legItem;
                        i++;
                    }
                }
                if (myLegendItems[0] != null)
                {
                    cimLeg.Items = myLegendItems;
                }

                // Add border to legend
                //StyleProjectItem style =
                //    Project.Current.GetItems<StyleProjectItem>().FirstOrDefault(s => s.Name == styleCategory);
                //if (style == null) return;
                //var lineList = await QueuedTask.Run(() => style.SearchSymbols(StyleItemType.LineSymbol, styleName));
                //if (lineList == null || lineList.Count == 0) return;
                //CIMSymbol lineSymbol = lineList[0].Symbol;
                //cimLeg.GraphicFrame.BorderSymbol = lineSymbol.getr
                cimLeg.GraphicFrame.BorderSymbol = new CIMSymbolReference
                {
                    Symbol = SymbolFactory.Instance.ConstructLineSymbol(ColorFactory.Instance.BlackRGB, 1.5, SimpleLineStyle.Solid)
                };
                cimLeg.GraphicFrame.BorderGapX = 3;
                cimLeg.GraphicFrame.BorderGapY = 3;
                legendElm.SetDefinition(cimLeg);

            });
        }

        public static async Task UpdateMapElementsAsync(string layoutName, string titleText, string subTitleText, string textBoxText)
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
                if (!String.IsNullOrEmpty(titleText))
                {
                    if (titleText != null)
                    {
                        GraphicElement textBox = layout.FindElement(Constants.MAPS_TITLE) as GraphicElement;
                        if (textBox != null)
                        {
                            await QueuedTask.Run(() =>
                            {
                                CIMTextGraphic graphic = (CIMTextGraphic ) textBox.Graphic;
                                graphic.Text = titleText;
                                textBox.SetGraphic(graphic);
                            });
                        }
                    }
                    if (subTitleText != null)
                    {
                        GraphicElement textBox = layout.FindElement(Constants.MAPS_SUBTITLE) as GraphicElement;
                        if (textBox != null)
                        {
                            await QueuedTask.Run(() =>
                            {
                                CIMTextGraphic graphic = (CIMTextGraphic)textBox.Graphic;
                                graphic.Text = subTitleText;
                                textBox.SetGraphic(graphic);
                            });
                        }
                    }
                    if (textBoxText != null)
                    {
                        GraphicElement textBox = layout.FindElement(Constants.MAPS_TEXTBOX1) as GraphicElement;
                        if (textBox != null)
                        {
                            await QueuedTask.Run(() =>
                            {
                                CIMTextGraphic graphic = (CIMTextGraphic)textBox.Graphic;
                                graphic.Text = textBoxText;
                                textBox.SetGraphic(graphic);
                            });
                        }
                    }
                }
            }
        }

    }



}
