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

namespace bagis_pro.Buttons
{
    internal class BtnGenerateLayers : Button
    {
        protected async override void OnClick()
        {
            if (String.IsNullOrEmpty(Module1.Current.Aoi.Name))
            {
                MessageBox.Show("No AOI selected for analysis !!", "BAGIS-PRO");
                return;
            }

            await AnalysisTools.GenerateSiteLayersAsync();

            MessageBox.Show("Analysis layers generated!!", "BAGIS-PRO");
        }
    }

    internal class BtnClipLayers : Button
    {
        protected async override void OnClick()
        {
            if (String.IsNullOrEmpty(Module1.Current.Aoi.Name))
            {
                MessageBox.Show("No AOI selected for analysis !!", "BAGIS-PRO");
                return;
            }

            Uri clipFileUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, false));
            Uri imageServiceUri = new Uri(@"C:\Users\lbross\Documents\ArcGIS\Projects\MyProject1\arcgis on bagis.geog.pdx.edu\DAILY_SWE_NORMALS\daily_swe_normal_apr_01.ImageServer");
            Webservices ws = new Webservices();
            BA_ReturnCode success = await ws.ClipImageToAoi(clipFileUri, Constants.FILE_AOI_VECTOR, imageServiceUri);

            MessageBox.Show("Analysis layers clipped!!", "BAGIS-PRO");
        }
    }
}
