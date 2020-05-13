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

}
