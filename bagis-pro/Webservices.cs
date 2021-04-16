using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
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
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Portal;
using Newtonsoft.Json.Linq;
using System.Collections.Specialized;
using System.Net.Http;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;

namespace bagis_pro
{
    internal class Webservices : Module
    {
        private static Webservices _this = null;

        /// <summary>
        /// Retrieve the singleton instance to this module here
        /// </summary>
        public static Webservices Current
        {
            get
            {
                return _this ?? (_this = (Webservices)FrameworkApplication.FindModule("bagis_pro_Webservices"));
            }
        }

        #region Overrides
        /// <summary>
        /// Called by Framework when ArcGIS Pro is closing
        /// </summary>
        /// <returns>False to prevent Pro from closing, otherwise True</returns>
        protected override bool CanUnload()
        {
            //TODO - add your business logic
            //return false to ~cancel~ Application close
            return true;
        }

        #endregion Overrides

        public async void AddPortalItem()
        {

            var myPortal = ArcGISPortalManager.Current.GetActivePortal();
            if (!myPortal.IsSignedOn())
            {
                MessageBox.Show("Log onto portal before clicking this button!!");
            }
            string strToken = myPortal.GetToken();

            var owner = myPortal.GetSignOnUsername();
            //var owner = "owner";
            //var token = await Task.Run(() => GetToken(owner));
            //string strToken = Convert.ToString(token);

            //var url = $"{myPortal.PortalUri.AbsoluteUri}arcgis/sharing/rest/content/users/{owner}";
            var url = $"https://www.arcgis.com/sharing/rest/portals/self?f=json&token=" + strToken;
            var response = new EsriHttpClient().Get(url);
            var json = await response.Content.ReadAsStringAsync();
            dynamic portalSelf = JObject.Parse(json);

            var uploadUrl = "https://" + Convert.ToString(portalSelf.urlKey) +
                            ".maps.arcgis.com/sharing/rest/content/users/" + owner + "/addItem";

            //byte[] fileBytes = File.ReadAllBytes("C:\\Docs\\animas_AOI_prms\\maps_publish\\title_page.pdf");
            //convert filestream to byte array
            byte[] fileBytes;
            using (var fileStream = File.OpenRead("C:\\Docs\\animas_AOI_prms\\maps_publish\\title_page.pdf"))
            {
                var binaryReader = new BinaryReader(fileStream);
                fileBytes = binaryReader.ReadBytes((int)fileStream.Length);
            }
            var fileBinaryContent = new ByteArrayContent(fileBytes);

            string strTitle = "Testing 1 2 3";

            using (FileStream stream =
                new FileStream("C:\\Docs\\animas_AOI_prms\\maps_publish\\title_page.pdf", FileMode.Open))
            using (var formData = new MultipartFormDataContent())
            {
                // Add the HttpContent objects to the form data

                // <input type="text" name="f" />
                formData.Add(new StringContent("json"), "f");
                formData.Add(new StringContent(strToken), "token");
                formData.Add(new StringContent("true"), "async");
                formData.Add(new StringContent("PDF"), "type");
                formData.Add(new StringContent(strTitle), "title");
                formData.Add(new StringContent("eBagis"), "tags");
                formData.Add(new StringContent("upload from BAGIS"), "description");
                var multipartContent = new MultipartFormDataContent
                {
                    { fileBinaryContent, "file" }
                };
                formData.Add(multipartContent);

                // Invoke the request to the server
                // equivalent to pressing the submit button on
                // a form with attributes (action="{url}" method="post")            
                response = await new EsriHttpClient().PostAsync(uploadUrl, formData);
                json = await response.Content.ReadAsStringAsync();


            }

        }

            private async Task<string> GetToken(string userName)
        {
            string password = "password";
            string url = "https://nrcs.maps.arcgis.com/sharing/rest/generateToken";
            string token = "";
            using (var formData = new MultipartFormDataContent())
            {
                // Add the HttpContent objects to the form data
                // <input type="text" name="f" />
                formData.Add(new StringContent(password), "password");
                formData.Add(new StringContent(userName), "userName");
                formData.Add(new StringContent("json"), "f");
                formData.Add(new StringContent("https://ebagis.geog.pdx.edu/"), "referer");
                formData.Add(new StringContent("f"), "json");


                // Invoke the request to the server
                // equivalent to pressing the submit button on
                // a form with attributes (action="{url}" method="post")
                var response = await new EsriHttpClient().PostAsync(url, formData);
                var json = await response.Content.ReadAsStringAsync();
                dynamic tokenResponse = JObject.Parse(json);
                token = Convert.ToString(tokenResponse.token);
            }

            return token;
        }

        /// <summary>
        /// Query a feature service for a single value
        /// </summary>
        /// <param name="oWebServiceUri">example: "https://services.arcgis.com/SXbDpmb7xQkk44JV/arcgis/rest/services/stations_USGS_ACTIVE/FeatureServer"</param>
        /// <param name="layerNumber">The ordinal of the feature layer. Example: 0</param>
        /// <param name="fieldName"></param>
        /// <param name="queryFilter"></param>
        /// <returns></returns>
        public async Task<string> QueryServiceForSingleValueAsync(Uri oWebServiceUri, string layerNumber, string fieldName, QueryFilter queryFilter)
        {
            string returnValue = "";
            await QueuedTask.Run(() =>
            {
                try
                {
                    ServiceConnectionProperties serviceConnectionProperties = new ServiceConnectionProperties(oWebServiceUri);
                    using (Geodatabase geodatabase = new Geodatabase(serviceConnectionProperties))
                    {
                        Table table = geodatabase.OpenDataset<Table>(layerNumber);
                        using (RowCursor cursor = table.Search(queryFilter, false))
                        {
                            cursor.MoveNext();
                            Feature onlyFeature = (Feature)cursor.Current;
                            if (onlyFeature != null)
                            {
                                int idx = onlyFeature.FindField(fieldName);
                                if (idx > -1)
                                {
                                    returnValue = Convert.ToString(onlyFeature[idx]);
                                }
                            }

                        }
                    }
                }
                catch (Exception e)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(QueryServiceForSingleValueAsync),
                        "Exception: " + e.Message);
                }
            });
            return returnValue;
        }

        public async Task<IDictionary<string, dynamic>> QueryDataSourcesAsync(string webserviceUrl)
        {
            IDictionary<string, dynamic> dictDataSources = new Dictionary<string, dynamic>();
            webserviceUrl = webserviceUrl + @"/api/rest/desktop/settings/bagis-pro/";
            EsriHttpResponseMessage response = new EsriHttpClient().Get(webserviceUrl);
            JObject jsonVal = JObject.Parse(await response.Content.ReadAsStringAsync()) as JObject;
            JArray arrDataSources = (JArray)jsonVal["dataSources"];

            foreach (dynamic dSource in arrDataSources)
            {
                string key = dSource.layerType;
                if (!dictDataSources.ContainsKey(key))
                {
                    dictDataSources.Add(key, dSource);
                }
            }
            return dictDataSources;
        }

        public async Task<BA_ReturnCode> DownloadBatchSettingsAsync(string webserviceUrl, string strSaveToPath)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            webserviceUrl = webserviceUrl + @"/api/rest/desktop/settings/bagis-pro/";
            EsriHttpResponseMessage response = new EsriHttpClient().Get(webserviceUrl);
            JObject jsonVal = JObject.Parse(await response.Content.ReadAsStringAsync()) as JObject;
            dynamic oSettings = (JObject) jsonVal["BatchSettings"];
            using (System.IO.StreamWriter file = File.CreateText(strSaveToPath))
            using (JsonTextWriter writer = new JsonTextWriter(file))
            {
                oSettings.WriteTo(writer);
            }
            success = BA_ReturnCode.Success;
            return success;
        }

        public async Task<BA_ReturnCode> GetPortalFile(string portalOrganization, string itemId, string downLoadPath)
        {
            try
            {
                var enumPortal = ArcGISPortalManager.Current.GetPortals();
                ArcGISPortal myPortal = null;
                foreach (var oPortal in enumPortal)
                {
                    var info = await oPortal.GetPortalInfoAsync();
                    if (info.OrganizationName.Equals(portalOrganization))
                    {
                        myPortal = oPortal;
                    }
                }
                if (myPortal == null)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(GetPortalFile),
                        "The NRCS Portal is missing from the ArcGIS Pro 'Portals' tab. The requested file cannot be downloaded! ArcGIS Pro will " +
                        "use a previous version of the file if it exists");
                    return BA_ReturnCode.UnknownError;
                }
                if (!myPortal.IsSignedOn())
                {
                    var result = await myPortal.SignInAsync();
                    if (result.success == false)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(GetPortalFile),
                            "Unable to signIn to the NRCS Portal. Can you connect to the portal in the ArcGIS Pro 'Portals' tab? The requested file cannot be downloaded! " +
                            "ArcGIS Pro will use a previous version of the file if it exists");
                        return BA_ReturnCode.UnknownError;
                    }
                }

                //assume we query for some content
                var query = PortalQueryParameters.CreateForItemsWithId(itemId);
                var results = await myPortal.SearchForContentAsync(query);

                var portalItem = results.Results.First();   //first item

                bool success = false;
                if (portalItem != null)
                {
                    //rename the original, if it exists so that we get the most current copy
                    if (File.Exists(downLoadPath))
                    {
                        string strDirectory = Path.GetDirectoryName(downLoadPath);
                        string strFile = Path.GetFileNameWithoutExtension(downLoadPath) + "_1" + Path.GetExtension(downLoadPath);
                        File.Copy(downLoadPath, strDirectory + "\\" + strFile, true);
                        File.Delete(downLoadPath);
                        Module1.Current.ModuleLogManager.LogDebug(nameof(GetPortalFile),
                            "Renamed " + downLoadPath + " so a new copy could be downloaded");
                    }
                    //download the item
                    success = await portalItem.GetItemDataAsync(downLoadPath);
                }
                if (success == true)
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(GetPortalFile),
                        "The requested file cannot was successfully downloaded from the Portal");
                    return BA_ReturnCode.Success;
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(GetPortalFile),
                        "The requested file cannot be downloaded from the Portal! ArcGIS Pro will " +
                        "use a previous version of the file if it exists");
                        return BA_ReturnCode.UnknownError;
                }
            }
            catch (Exception e)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(GetPortalFile),
                    "Exception: " + e.Message);
                return BA_ReturnCode.UnknownError;
            }
        }
    }
}
