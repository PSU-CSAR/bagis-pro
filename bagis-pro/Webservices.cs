using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Core.Portal;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using Newtonsoft.Json;
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
        public async Task<string[]> QueryServiceForValuesAsync(Uri oWebServiceUri, string layerNumber, string[] fieldNames, ArcGIS.Core.Data.QueryFilter queryFilter)
        {
            string[] returnValues = new string[fieldNames.Length];
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
                                for (int i = 0; i < fieldNames.Length; i++)
                                {
                                    int idx = onlyFeature.FindField(fieldNames[i]);
                                    if (idx > -1)
                                    {
                                        returnValues[i] = Convert.ToString(onlyFeature[idx]);
                                    }
                                }
                            }

                        }
                    }
                }
                catch (Exception e)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(QueryServiceForValuesAsync),
                        "Exception: " + e.Message);
                }
            });
            return returnValues;
        }

        public async Task<IDictionary<string, dynamic>> QueryDataSourcesAsync()
        {
            IDictionary<string, dynamic> dictDataSources = new Dictionary<string, dynamic>();
            EsriHttpResponseMessage response = new EsriHttpClient().Get(Constants.URI_BATCH_TOOL_SETTINGS);
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
        public async Task<int> QueryNifcMinYearAsync(IDictionary<string, dynamic> dictDatasources, string strDataType)
        {
            string wsUri = "";
            if (dictDatasources != null)
            {
                BA_Objects.DataSource dsFire = new BA_Objects.DataSource(dictDatasources[strDataType]);
                if (dsFire != null)
                {
                    wsUri = dsFire.uri;
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(QueryNifcMinYearAsync),
                        $@"Unable to find element {strDataType} uri in server data sources");
                }
                if (!string.IsNullOrEmpty(wsUri))
                {
                    string[] arrReturnValues = this.ParseUriAndLayerNumber(wsUri);
                    if (arrReturnValues.Length == 2 && !string.IsNullOrEmpty(arrReturnValues[0]))
                    {
                        int intYear = DateTime.Now.Year;
                        QueryFilter queryFilter = new QueryFilter();
                        bool bRecordsFound = false;
                        while (! bRecordsFound)
                        {
                            string strTimeStamp = $@"{intYear}-01-01 00:00:00";
                            queryFilter.WhereClause = $@"{Constants.FIELD_FIRECURRENT_DATE} >= timestamp '{strTimeStamp}'";
                            string[] arrSearch = { Constants.FIELD_FIRECURRENT_INCIDENT };
                            string[] arrFound = await this.QueryServiceForValuesAsync(new Uri(arrReturnValues[0]), arrReturnValues[1], arrSearch, queryFilter);
                            if (arrFound != null && !string.IsNullOrEmpty(arrFound[0]))
                            {
                                bRecordsFound = true;
                                return intYear;
                            }
                            intYear--;
                        }
                    }
                }
            }
            Module1.Current.ModuleLogManager.LogError(nameof(QueryNifcMinYearAsync),
                $@"Unable to calculate min year from NIFC data!");
            return 9999;
        }

        public async Task<BA_ReturnCode> DownloadBagisSettingsAsync(string strSaveToPath)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            EsriHttpResponseMessage response = new EsriHttpClient().Get(Constants.URI_BATCH_TOOL_SETTINGS);
            JObject jsonVal = JObject.Parse(await response.Content.ReadAsStringAsync()) as JObject;
            dynamic oSettings = (JObject) jsonVal["BagisSettings"];
            using (System.IO.StreamWriter file = File.CreateText(strSaveToPath))
            using (JsonTextWriter writer = new JsonTextWriter(file))
            {
                writer.Formatting = Formatting.Indented;
                oSettings.WriteTo(writer);
            }
            success = BA_ReturnCode.Success;
            return success;
        }

        public async Task<BA_ReturnCode> GetPortalFile(string portalOrganization, string itemId, string downLoadPath)
        {
            try
            {
                PortalItem portalItem = null;
                bool bSuccess = false;
                BA_ReturnCode objSuccess = BA_ReturnCode.UnknownError;
                var enumPortal = ArcGISPortalManager.Current.GetPortals();
                await QueuedTask.Run(async () =>
                {
                    ArcGISPortal myPortal = null;
                    foreach (var oPortal in enumPortal)
                    {
                        var info = await oPortal.GetPortalInfoAsync();
                        if (info.OrganizationName.Equals(portalOrganization))
                        {
                            myPortal = oPortal;
                            objSuccess = BA_ReturnCode.Success;
                            break;
                        }
                    }
                    if (myPortal == null)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(GetPortalFile),
                            "The NRCS Portal is missing from the ArcGIS Pro 'Portals' tab. The requested file cannot be downloaded! ArcGIS Pro will " +
                            "use a previous version of the file if it exists");
                        return;
                    }
                    else if (!myPortal.IsSignedOn())
                    {
                        var result = await myPortal.SignInAsync();
                        if (result.success == false)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(GetPortalFile),
                                "Unable to signIn to the NRCS Portal. Can you connect to the portal in the ArcGIS Pro 'Portals' tab? The requested file cannot be downloaded! " +
                                "ArcGIS Pro will use a previous version of the file if it exists");
                            return;
                        }
                    }
                    //assume we query for some content
                    var query = PortalQueryParameters.CreateForItemsWithId(itemId);
                    var results = await myPortal.SearchForContentAsync(query);
                    portalItem = results.Results.First();   //first item
                    objSuccess = BA_ReturnCode.Success;
                });


                if (objSuccess == BA_ReturnCode.Success && portalItem != null)
                {
                    //rename the original, if it exists so that we get the most current copy
                    if (File.Exists(downLoadPath))
                    {
                        string strDirectory = System.IO.Path.GetDirectoryName(downLoadPath);
                        string strFile = System.IO.Path.GetFileNameWithoutExtension(downLoadPath) + "_1" + System.IO.Path.GetExtension(downLoadPath);
                        File.Copy(downLoadPath, strDirectory + "\\" + strFile, true);
                        File.Delete(downLoadPath);
                        Module1.Current.ModuleLogManager.LogDebug(nameof(GetPortalFile),
                            "Renamed " + downLoadPath + " so a new copy could be downloaded");
                    }
                    //download the item
                    bSuccess = await portalItem.GetItemDataAsync(downLoadPath);
                }
                if (bSuccess == true)
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(GetPortalFile),
                        "The requested file was successfully downloaded from the Portal");
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

        public async Task<string> GetWesternStateBoundariesUriAsync()
        {
            if (!string.IsNullOrEmpty(Module1.Current.WesternStateBoundariesUri))
            {
                return Module1.Current.WesternStateBoundariesUri;
            }
            else
            {
                var response = new EsriHttpClient().Get(Constants.URI_DESKTOP_SETTINGS);
                var json = await response.Content.ReadAsStringAsync();
                dynamic oSettings = JObject.Parse(json);
                if (oSettings == null || String.IsNullOrEmpty(Convert.ToString(oSettings.westernStateBoundaries)))
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(GetWesternStateBoundariesUriAsync),
                        "Unable to retrieve settings from " + Constants.URI_DESKTOP_SETTINGS);
                    return "";
                }
                else
                {
                    return Convert.ToString(oSettings.westernStateBoundaries);
                }
            }
        }

        public async Task<string> GetForecastStationsUriAsync()
        {
                var response = new EsriHttpClient().Get(Constants.URI_DESKTOP_SETTINGS);
                var json = await response.Content.ReadAsStringAsync();
                dynamic oSettings = JObject.Parse(json);
                if (oSettings == null || String.IsNullOrEmpty(Convert.ToString(oSettings.gaugeStation)))
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(GetForecastStationsUriAsync),
                        "Unable to retrieve settings from " + Constants.URI_DESKTOP_SETTINGS);
                    return "";
                }
                else
                {                    
                    return Convert.ToString(oSettings.gaugeStation);
                }
        }

        public async Task<string> GetDem30UriFromDatasourcesAsync()
        {
            IDictionary<string, BA_Objects.DataSource> dictLocalDataSources = GeneralTools.QueryLocalDataSources();
            if (!dictLocalDataSources.ContainsKey(BA_Objects.DataSource.GetDemKey))
            {
                IDictionary<string, dynamic> dictDatasources = await this.QueryDataSourcesAsync();
                if (dictDatasources != null)
                {
                    BA_Objects.DataSource dsDem = new BA_Objects.DataSource(dictDatasources[BA_Objects.DataSource.GetDemKey]);
                    if (dsDem != null)
                    {
                        return dsDem.uri;
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(GetDem30UriFromDatasourcesAsync),
                            $@"Unable to find element 30m DEM in server data sources");
                        return "";
                    }
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(GetDem30UriFromDatasourcesAsync),
                        $@"Unable to retrieve data sources from server!");
                    return "";
                }
            }
            else
            {
                BA_Objects.DataSource dsDem = new BA_Objects.DataSource(dictLocalDataSources[BA_Objects.DataSource.GetDemKey]);
                return dsDem.uri;
            }
        }
        public async Task<string> GetDemElevUnitAsync()
        {
            var response = new EsriHttpClient().Get(Constants.URI_DESKTOP_SETTINGS);
            var json = await response.Content.ReadAsStringAsync();
            dynamic oSettings = JObject.Parse(json);
            if (oSettings == null || String.IsNullOrEmpty(Convert.ToString(oSettings.demElevUnit)))
            {
                Module1.Current.ModuleLogManager.LogError(nameof(GetDemElevUnitAsync),
                    "Unable to retrieve settings from " + Constants.URI_DESKTOP_SETTINGS);
                return "";
            }
            else
            {
                return Convert.ToString(oSettings.demElevUnit);
            }
        }

        public async Task<BA_ReturnCode> UpdateAoiItemsAsync(string stationTriplet)
        {
            string nwccAoiName = "";
            string huc = "";
            string aoiSummaryTag="";
            BA_ReturnCode success = GeneralTools.LoadBagisSettings();
            if (success != BA_ReturnCode.Success)
            {
                MessageBox.Show("Batch tool settings could not be loaded. The portal files cannot be updated!!");
                return success;
            }
            string[] arrResults = await GeneralTools.QueryForecastListAoiProperties(stationTriplet);
            if (arrResults.Length == 5)
            {
                nwccAoiName = arrResults[0].Trim();
                nwccAoiName = nwccAoiName.Replace(" ", "_");
                huc = arrResults[3];
                string[] pieces = stationTriplet.Split(':');
                if (pieces.Length == 3)
                {
                    aoiSummaryTag = arrResults[0].Trim() + " " + pieces[0] + " " + pieces[1];
                }
                else
                {
                    MessageBox.Show("Unable to parse station triplet. The portal files cannot be updated!!");
                    return BA_ReturnCode.ReadError;
                }
            }
            else
            {
                MessageBox.Show("Unable to retrieve AOI properties from Master. The portal files cannot be updated!!");
                return BA_ReturnCode.ReadError;
            }
            // Ensure that the user is signed into the NRCS Portal 
            BA_Objects.AGSPortalProperties portalProps = new BA_Objects.AGSPortalProperties();
            var info = await ArcGISPortalManager.Current.GetActivePortal().GetPortalInfoAsync();
            if (info.OrganizationName.Equals(BA_Objects.AGSPortalProperties.PORTAL_ORGANIZATION))
            {
                portalProps.IsNrcsPortal = true;
            }
            await QueuedTask.Run(() =>
            {
                portalProps.IsSignedIn = ArcGISPortalManager.Current.GetActivePortal().IsSignedOn();
                portalProps.UserName = ArcGISPortalManager.Current.GetActivePortal().GetSignOnUsername();
                if (portalProps.UserName.Equals(BA_Objects.AGSPortalProperties.NWCC_NRCS_USER))
                {
                    portalProps.IsNrcsUser = true;
                }
            });
            if (!portalProps.IsNrcsPortal)
            {
                MessageBox.Show("Please sign into the USDA NRCS ArcGIS Online portal before trying to update items!!", "BAGIS-PRO");
                return BA_ReturnCode.NotSupportedOperation;
            }
            if (!portalProps.IsSignedIn)
            {
                var result = await ArcGISPortalManager.Current.GetActivePortal().SignInAsync();
                if (result.success == false)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(GetPortalFile),
                        "Unable to signIn to the NRCS Portal. Can you connect to the portal in the ArcGIS Pro 'Portals' tab? Items cannot be updated ! " +
                        "ArcGIS Pro will use a previous version of the file if it exists");
                    return BA_ReturnCode.NotSupportedOperation;
                }
            }

            UriBuilder searchURL = new UriBuilder(ArcGISPortalManager.Current.GetActivePortal().PortalUri);

            EsriHttpClient httpClient = new EsriHttpClient();
            searchURL.Path = "sharing/rest/search";
            string pdfDocs = "(type:\"PDF\")";
            string titleAoi = "(title:\"" + nwccAoiName + "\")";
            searchURL.Query = string.Format("q=owner:{0} {1} {2} &f=json", portalProps.UserName, titleAoi, pdfDocs);
            var searchResponse = httpClient.Get(searchURL.Uri.ToString());
            dynamic resultItems = JObject.Parse(await searchResponse.Content.ReadAsStringAsync());

            long numberOfTotalItems = resultItems.total.Value;
            if (numberOfTotalItems == 0)
                return BA_ReturnCode.ReadError;
            //string fileName = aoiName + "_overview.pdf";
            List<string> allFileNames = new List<string>
            {
                nwccAoiName + "_" + Constants.FILE_EXPORT_OVERVIEW_PDF,
                nwccAoiName + "_" + Constants.FILE_EXPORT_MAP_ELEV_PDF,
                nwccAoiName + "_" + Constants.FILE_EXPORT_LAND_COVER_PDF,
                nwccAoiName + "_" + Constants.FILE_EXPORT_ASPECT_DISTRIBUTION_PDF,
                nwccAoiName + "_" + Constants.FILE_EXPORT_SLOPE_DISTRIBUTION_PDF,
                nwccAoiName + "_" + Constants.FILE_EXPORT_SITE_REPRESENTATION_PDF,
                nwccAoiName + "_" + Constants.FILE_EXPORT_PRECIPITATION_DISTRIBUTION_PDF,
                nwccAoiName + "_" + Constants.FILE_EXPORT_SEASONAL_PRECIP_DISTRIBUTION_PDF,
                nwccAoiName + "_" + Constants.FILE_EXPORT_SNODAS_SWE_PDF,
                nwccAoiName + "_" + Constants.FILE_EXPORT_POTENTIAL_SITE_ANALYSIS_PDF,
                nwccAoiName + "_" + Constants.FILE_EXPORT_WATERSHED_REPORT_PDF
            };
            List<string> requiredTags = new List<string>()
            {
                "GIS",
                "BAGIS",
                "SNOTEL",
                "eBagis",
                huc, 
                aoiSummaryTag
            };
            List<dynamic> resultItemList = new List<dynamic>();
            resultItemList.AddRange(resultItems.results);
            foreach (var item in resultItemList)
            {
                string itemFile = (string)item.name;
                if (allFileNames.Contains(itemFile))
                {
                    string itemId = (string)item.id;
                    string strTitle = (string)item.title;
                    List<string> tags = item.tags.ToObject<List<string>>();
                    UpdateItem(portalProps.UserName, itemId, strTitle, requiredTags, tags);
                }
            }
            return BA_ReturnCode.Success;
        }

        public void UpdateItem(string userName, string itemId, string strTitle, List<string> requiredTags, List<string> tags)
        {
            string strCredits = "Basin Analysis GIS is developed under the collaboration between NRCS NWCC " +
                "and the Center for Spatial Analysis &Research at Portland State University.";
            string strDescription = "This report was generated in Basin Analysis GIS (BAGIS). See the " +
                "<a href=\"https://nrcs.maps.arcgis.com/sharing/rest/content/items/b121d25cc73c4b30a700b8d2d2ea23bc/data\" " +
                "target=\"_blank\">Basin Analysis Report Users Manual</a> for a complete description of the report. Please contact NWCC " +
                "(<a href=\"https://www.wcc.nrcs.usda.gov/\" target=\"_blank\">https://www.wcc.nrcs.usda.gov/</a>) for any questions.";
            string strLicense = "Public domain data. See <a href='https://www.wcc.nrcs.usda.gov/disclaimer.htm' target='_blank' " +
                "rel='nofollow ugc noopener noreferrer'>https://www.wcc.nrcs.usda.gov/disclaimer.htm</a> for disclaimer.";
            if (tags == null)
            {
                tags = new List<string>();  // Ensure tags is never null to avoid exception
            }
            List<string> mergedTags = requiredTags.Union(tags).ToList();
            string strMerged = string.Join(",", mergedTags);

            // Generate summary from title
            string strSummary = "";
            if (!string.IsNullOrEmpty(strTitle))
            {
                string[] pieces = strTitle.Split(new char[0]);
                if (pieces.Length > 0)
                {
                    strSummary = pieces[0];
                }
                if (pieces.Length > 1)
                {
                    for (int i = 1; i < pieces.Length; i++)
                    {
                        strSummary = strSummary + " " + pieces[i].ToUpper();
                    }
                }

            }

            // Updating fields on item
            UriBuilder searchURL = new UriBuilder(ArcGISPortalManager.Current.GetActivePortal().PortalUri);
            searchURL.Path = "sharing/rest/content/users/" + userName + "/items/" + itemId + "/update";
            EsriHttpClient myClient = new EsriHttpClient();
            var postData = new List<KeyValuePair<string, string>>();
            postData.Add(new KeyValuePair<string, string>("f", "json"));
            postData.Add(new KeyValuePair<string, string>("description", strDescription));
            postData.Add(new KeyValuePair<string, string>("snippet", strSummary));
            postData.Add(new KeyValuePair<string, string>("licenseInfo", strLicense));
            postData.Add(new KeyValuePair<string, string>("accessInformation", strCredits));
            postData.Add(new KeyValuePair<string, string>("tags", strMerged));

            using (HttpContent content = new FormUrlEncodedContent(postData))
            {
                EsriHttpResponseMessage respMsg = myClient.Post(searchURL.Uri.ToString(), content);
                if (respMsg == null)
                    return;
                string outStr = respMsg.Content.ReadAsStringAsync().Result;
            }

            // Updating sharing for item
            searchURL.Path = "sharing/rest/content/users/" + userName + "/items/" + itemId + "/share";
            postData.Clear();
            postData.Add(new KeyValuePair<string, string>("f", "json"));
            postData.Add(new KeyValuePair<string, string>("everyone", "true"));
            postData.Add(new KeyValuePair<string, string>("groups", "a4474cec000e46869a9980930c7c9bd0"));
            using (HttpContent content = new FormUrlEncodedContent(postData))
            {
                EsriHttpResponseMessage respMsg = myClient.Post(searchURL.Uri.ToString(), content);
                if (respMsg == null)
                    return;
                string outStr = respMsg.Content.ReadAsStringAsync().Result;
            }
        }

        public async Task<double> QueryBagisSettingsVersionAsync()
        {
            try
            {
                IDictionary<string, dynamic> dictDataSources = new Dictionary<string, dynamic>();
                EsriHttpResponseMessage response = new EsriHttpClient().Get(Constants.URI_BATCH_TOOL_SETTINGS);
                JObject jsonVal = JObject.Parse(await response.Content.ReadAsStringAsync()) as JObject;
                dynamic oSettings = (JObject)jsonVal["BagisSettings"];
                return (double)oSettings.Version;
            }
            catch (Exception)
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(QueryBagisSettingsVersionAsync),
                    "An error occurred while trying to retrieve the batch settings version number from the ebagis server!");
                return -1;
            }

        }

        public string GenerateSnodasGeoJson(string pointOutputPath, string polygonOutputPath, string outputFolder)
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
            string stationTriplet = null;
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
                    stationTriplet = properties.stationTriplet;
                    objProperties[Constants.FIELD_JSON_NAME] = properties.stationName;
                    objProperties[Constants.FIELD_JSON_SOURCE] = "ref";
                    objOutput[Constants.FIELD_JSON_PROPERTIES] = objProperties;
                    arrGeometries.Add(firstFeature.geometry);
                }
            }

            if (stationTriplet == null)
            {
                return @$"ERROR: StationTriplet is null for this basin. No geojson generated!";
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
                if (arrFeatures.Count > 0)
                {
                    int idx = 0;
                    dynamic firstFeature = arrFeatures[idx];
                    dynamic allFeatures = (JObject) firstFeature.DeepClone();   // Clone the first feature so we have a scaffold object
                    allFeatures.geometry.type = "MultiPolygon"; // Set the geometry type
                    allFeatures.geometry.coordinates.Clear();   // Clear the existing coordinates
                    for (int i = idx; i < arrFeatures.Count; i++)
                    {
                        dynamic nextFeature = arrFeatures[i];
                        JArray arrCoordinates = (JArray) nextFeature.geometry.coordinates;
                        allFeatures.geometry.coordinates.Add(arrCoordinates);   // Add the coordinates for each feature
                    }
                    arrGeometries.Add(allFeatures.geometry);    // Add the multipolygon feature to the geometries    
                }
            }
            if (arrGeometries.Count == 2)
            {
                objOutput[Constants.FIELD_JSON_GEOMETRIES] = arrGeometries;
            }

            // write JSON directly to a file
            string strFileName = $@"{outputFolder}\{stationTriplet.Replace(':', '_')}.geojson";
            using (StreamWriter file = File.CreateText(strFileName))
            using (JsonTextWriter writer = new JsonTextWriter(file))
            {
                writer.Formatting = Formatting.Indented;
                objOutput.WriteTo(writer);
            }
            return null;
        }        
        public string[] ParseUriAndLayerNumber(string strWsUri)
        {
            string[] arrReturnValues = new string[2];
            arrReturnValues[1] = strWsUri.Split('/').Last();
            int intTrim = arrReturnValues[1].Length + 1;
            arrReturnValues[0] = strWsUri.Substring(0, strWsUri.Length - intTrim);
            return arrReturnValues;
        }

        public async Task<IList<string>> QueryMtbsImageServiceNamesAsync(IDictionary<string, dynamic> dictDatasources, string strDataType)
        {
            IList<string> returnList = new List<string>();
            string wsUri = "";
            try
            {
                BA_Objects.DataSource dsFire = new BA_Objects.DataSource(dictDatasources[strDataType]);
                if (dsFire != null)
                {
                    //Derive the rest uri from the imageserver uri
                    //http://bagis.geog.pdx.edu/arcgis/rest/services/usgs_mtbs_conus
                    //http://bagis.geog.pdx.edu/arcgis/services/usgs_mtbs_conus/
                    string[] arrPieces = dsFire.uri.Split("/services");
                    if (arrPieces.Length == 2)
                    {
                        wsUri = $@"{arrPieces[0]}/rest/services{arrPieces[1]}";
                    }
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(QueryMtbsImageServiceNamesAsync),
                        $@"Unable to find element {strDataType} uri in server data sources");
                }

                if (!string.IsNullOrEmpty(wsUri))
                {
                    string uriMtbs = $@"{wsUri}?f=pjson";
                    EsriHttpResponseMessage response = new EsriHttpClient().Get(uriMtbs);
                    JObject jsonVal = JObject.Parse(await response.Content.ReadAsStringAsync()) as JObject;
                    JArray arrServices = (JArray)jsonVal["services"];
                    foreach (dynamic dService in arrServices)
                    {
                        string sType = Convert.ToString(dService.type);
                        if (sType.Equals("ImageServer"))
                        {
                            returnList.Add(Convert.ToString(dService.name));
                        }
                    }
                }
                return returnList;
            }
            catch (Exception)
            {
                Module1.Current.ModuleLogManager.LogDebug(nameof(QueryMtbsImageServiceNamesAsync),
                    "An error occurred while trying to retrieve the batch settings version number from the ebagis server!");
                return null;
            }
        }
    }
}
