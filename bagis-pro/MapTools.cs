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
            return await QueuedTask.Run(async () =>
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

        public static async Task<MapFrame> SetDefaultMapFrameNameAsync(string mapFrameName, Layout oLayout, Map oMap)
        {
            return await QueuedTask.Run(async () =>
            {
                //Finding the mapFrame with mapFrameName
                MapFrame mfElm = oLayout.FindElement(mapFrameName) as MapFrame; ;
                if (mfElm == null)
                {
                    //Build 2D envelope geometry
                    Coordinate2D mf_ll = new Coordinate2D(1.0, 2.0);
                    Coordinate2D mf_ur = new Coordinate2D(7.5, 9.0);
                    Envelope mf_env = EnvelopeBuilder.CreateEnvelope(mf_ll, mf_ur);
                    mfElm = LayoutElementFactory.Instance.CreateMapFrame(oLayout, mf_env, oMap);
                    mfElm.SetName(mapFrameName);
                }
                return mfElm;
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
                }
                await ProApp.Panes.CreateMapPaneAsync(map);
                return map;
            });
        }
    }
}
