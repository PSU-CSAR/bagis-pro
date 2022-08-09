using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace bagis_pro.Buttons
{
    internal class BtnExportForSnodas : Button
    {
        protected override void OnClick()
        {
            // read JSON directly from a file
            JObject o2 = null;
            using (StreamReader file = File.OpenText(@"c:\Docs\Downloads\marlette_lake_inflow_10232010\pourpoint.geojson"))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                o2 = (JObject)JToken.ReadFrom(reader);
            }
            if (o2 != null)
            {
                dynamic esriDefinition = (JObject)o2;
                JArray arrFeatures = (JArray) esriDefinition.features;
                dynamic firstFeature = arrFeatures[0];
                var properties = firstFeature.properties;
                var name = properties.stationName;
            }
        }
    }
}
