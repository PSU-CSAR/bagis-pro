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
        protected override async void OnClick()
        {
            // read JSON from template file to get structure
            //JObject o1 = null;
            //using (StreamReader file = File.OpenText(@"C:\Users\lbross\Downloads\06049450_MT_USGS.geojson"))
            //using (JsonTextReader reader = new JsonTextReader(file))
            //{
            //    o1 = (JObject)JToken.ReadFrom(reader);
            //}
            //if (o1 != null)
            //{
            //    dynamic snodasDefinition = (JObject)o1;
            //    // single value fields
            //    var aType = snodasDefinition.type;
            //    var id = snodasDefinition.id;
            //    // array of geometries
            //    JArray arrGeometries2 = (JArray)snodasDefinition.geometries;
            //    foreach (dynamic item in arrGeometries2)
            //    {
            //        if (item.type == "Point")
            //        {
            //            // This is the point
            //        }
            //        else if (item.type == "MultiPolygon")
            //        {
            //            // This is the polygon
            //        }
            //    }
            //    var properties = snodasDefinition.properties;   // Some other properties
            //    var name = properties.name;
            //    var source = properties.source;
            //}

            string pointInputPath = @"C:\Docs\Downloads\marlette_lake_inflow_10232010\aoi.gdb\pourpoint";
            string polygonInputPath = @"C:\Docs\Downloads\marlette_lake_inflow_10232010\aoi.gdb\aoi_v";

            string pointOutputPath = Path.GetTempPath() + "pourpoint.geojson";
            string polygonOutputPath = Path.GetTempPath() + "polygon.geojson";

            BA_ReturnCode success = await GeoprocessingTools.FeaturesToSnodasGeoJsonAsync(pointInputPath, pointOutputPath, true);
            if (success.Equals(BA_ReturnCode.Success))
            {
                success = await GeoprocessingTools.FeaturesToSnodasGeoJsonAsync(polygonInputPath, polygonOutputPath, true);
            }

            if (success.Equals(BA_ReturnCode.Success))
            {
                // Build new JObject
                JObject objOutput = new JObject();
                objOutput[Constants.FIELD_JSON_TYPE] = "GeometryCollection";
                JArray arrGeometries = new JArray();

                // read pourpoint JSON directly from a file
                JObject o2 = null;
                using (StreamReader file = File.OpenText(pointOutputPath))
                using (JsonTextReader reader = new JsonTextReader(file))
                {
                    o2 = (JObject)JToken.ReadFrom(reader);
                }
                if (o2 != null)
                {
                    dynamic esriDefinition = (JObject)o2;
                    JArray arrFeatures = (JArray)esriDefinition.features;
                    if (arrFeatures.Count > 0)
                    {
                        // Always take the first one
                        dynamic firstFeature = arrFeatures[0];
                        var properties = firstFeature.properties;
                        var objProperties = new JObject();
                        objOutput[Constants.FIELD_JSON_ID] = properties.stationTriplet;
                        objProperties[Constants.FIELD_JSON_NAME] = properties.stationName;
                        objProperties[Constants.FIELD_JSON_SOURCE] = "ref";
                        objOutput[Constants.FIELD_JSON_PROPERTIES] = objProperties;
                        arrGeometries.Add(firstFeature.geometry);
                    }
                }

                // read polygon JSON directly from a file
                JObject o3 = null;
                using (StreamReader file = File.OpenText(polygonOutputPath))
                using (JsonTextReader reader = new JsonTextReader(file))
                {
                    o3 = (JObject)JToken.ReadFrom(reader);
                }
                if (o3 != null)
                {
                    dynamic esriDefinition = (JObject)o3;
                    JArray arrFeatures = (JArray)esriDefinition.features;
                    if (arrFeatures.Count == 1)
                    {
                        // Always take the first one
                        dynamic firstFeature = arrFeatures[0];
                        arrGeometries.Add(firstFeature.geometry);
                    }
                    else
                    {
                        MessageBox.Show("This file has more than one polygon. Only a single polygon is allowed!!");
                        return;
                    }
                }
                if (arrGeometries.Count == 2)
                {
                    objOutput[Constants.FIELD_JSON_GEOMETRIES] = arrGeometries;
                }

                // write JSON directly to a file
                using (StreamWriter file = File.CreateText(@"C:\Users\lbross\Downloads\snodas_output.geojson"))
                using (JsonTextWriter writer = new JsonTextWriter(file))
                {
                    writer.Formatting = Formatting.Indented;
                    objOutput.WriteTo(writer);
                }
            }
            else
            {
                MessageBox.Show("An error occurred!!", "BAGIS-PRO");
            }

        }
    }
}
