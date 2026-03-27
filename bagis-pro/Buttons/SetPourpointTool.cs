using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Internal.Mapping;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using PdfSharp.Charting;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace bagis_pro.Buttons
{
    internal class SetPourpointTool : MapTool
    {
        public SetPourpointTool()
        {
            IsSketchTool = true;
            SketchType = SketchGeometryType.Point;
            SketchOutputMode = SketchOutputMode.Map;
        }
        protected override Task OnToolActivateAsync(bool active)
        {
            return base.OnToolActivateAsync(active);
        }
        protected async override Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            Map oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
            string glName = Constants.MAPS_POURPOINT_LAYER;
            var gl_param = new GraphicsLayerCreationParams
            {
                Name = glName,
                IsVisible = true
            };
            GraphicElement oElement = null;
            await QueuedTask.Run(() =>
            {
                // Create a new graphics layer if one doesn't exist
                GraphicsLayer graphicsLayer = oMap.GetLayersAsFlattenedList().OfType<GraphicsLayer>().Where(f =>
                    f.Name == glName).FirstOrDefault();
                if (graphicsLayer == null)
                {
                    graphicsLayer = LayerFactory.Instance.CreateLayer<GraphicsLayer>(gl_param, oMap);
                }
                else
                {
                    graphicsLayer.RemoveElements();
                }

                //Set symbolology, create and add element to layout
                CIMPointSymbol oPointSymbol = SymbolFactory.Instance.ConstructPointSymbol(ColorFactory.Instance.GreenRGB, 
                    14.0, SimpleMarkerStyle.Circle);

                // Create the graphic using the geometry and symbol
                var cimGraphicElement = new CIMPointGraphic()
                {
                    Location = (MapPoint) geometry,
                    Symbol = oPointSymbol.MakeSymbolReference()
                };
                oElement = graphicsLayer.AddElement(cimGraphicElement);
            });

            if (oElement != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

    }
}
