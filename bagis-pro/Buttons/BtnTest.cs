using System;
using System.Collections.Generic;
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
using ArcGIS.Desktop.Mapping;

// I added these library references
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net.Http;

namespace bagis_pro.Buttons
{
    internal class BtnTest : Button
    {
        protected override void OnClick()
        {
            //int test = await AddPortalItem();
            Webservices ws = new Webservices();
            ws.UpdateAoiItemsAsync("13202000:ID:USGS");
        }

        private async Task<int> AddPortalItem()
        {
            var myPortal = ArcGISPortalManager.Current.GetActivePortal();
            if (!myPortal.IsSignedOn())
            {
                MessageBox.Show("Log onto portal before clicking this button!!");
            }
            string strToken = myPortal.GetToken();

            var owner = myPortal.GetSignOnUsername();
            var url = $"https://www.arcgis.com/sharing/rest/portals/self?f=json&token=" + strToken;
            var response = new EsriHttpClient().Get(url);
            var json = await response.Content.ReadAsStringAsync();
            dynamic portalSelf = JObject.Parse(json);

            var uploadUrl = "https://" + Convert.ToString(portalSelf.urlKey) +
                ".maps.arcgis.com/sharing/rest/content/users/" + owner + "/addItem";

            byte[] fileBytes;
            string fileToUpload = "C:\\Docs\\animas_AOI_prms\\maps_publish\\title_page.pdf";
            string strTitle = "Testing 1 2 3";
            // Read file into ByteArrayContent object that we can add to the request
            using (var fileStream = File.OpenRead(fileToUpload))
            {
                var binaryReader = new BinaryReader(fileStream);
                fileBytes = binaryReader.ReadBytes((int)fileStream.Length);
            }
            var fileBinaryContent = new ByteArrayContent(fileBytes);

            using (var formData = new MultipartFormDataContent())
            {
                // Add the HttpContent objects to the form data
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
                string strMessage = Convert.ToString(json);
            }
            return 1;
        }
    }
}
