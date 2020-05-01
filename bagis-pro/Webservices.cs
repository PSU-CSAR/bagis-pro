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
            //string strToken = myPortal.GetToken();

            //var owner = myPortal.GetSignOnUsername();
            var owner = "owner";
            var token = await Task.Run(() => GetToken(owner));
            string strToken = Convert.ToString(token);

            //var url = $"{myPortal.PortalUri.AbsoluteUri}arcgis/sharing/rest/content/users/{owner}";
            var url = $"https://www.arcgis.com/sharing/rest/portals/self?f=json&token=" + strToken;
            var response = new EsriHttpClient().Get(url);
            var json = await response.Content.ReadAsStringAsync();
            dynamic portalSelf = JObject.Parse(json);

            var uploadUrl = "https://" + Convert.ToString(portalSelf.urlKey) +
                            ".maps.arcgis.com/sharing/rest/content/users/" + owner + "/addItem";

            byte[] fileBytes = System.IO.File.ReadAllBytes("C:\\Docs\\animas_AOI_prms\\test\\title_page.pdf");

            string strTitle = "Testing 1 2 3";
            using (var formData = new MultipartFormDataContent())
            {
                // Add the HttpContent objects to the form data

                // <input type="text" name="f" />
                formData.Add(new StringContent("f"), "json");
                formData.Add(new StringContent("token"), strToken);
                formData.Add(new StringContent("async"), "True");
                formData.Add(new StringContent("type"), "PDF");
                formData.Add(new StringContent("title"), strTitle);
                formData.Add(new StringContent("tags"), "eBagis");
                formData.Add(new StringContent("description"), "upload from BAGIS");
                // <input type="file" name="PDF Test" />
                //formData.Add(bytesContent, "PDF Test", "PDF Test");
                formData.Add(new ByteArrayContent(fileBytes, 0, fileBytes.Count()), "PDF Test", "title_page.pdf");

                // Invoke the request to the server

                // equivalent to pressing the submit button on
                // a form with attributes (action="{url}" method="post")
                response = await new EsriHttpClient().PostAsync(url, formData);
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
    }
}
