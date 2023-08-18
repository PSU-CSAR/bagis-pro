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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro
{
    internal class BtnWinExportPdf : Button
    {

        private WinExportPdf _winexportpdf = null;

        protected override void OnClick()
        {
            //already open?
            if (_winexportpdf != null)
                return;
            _winexportpdf = new WinExportPdf();
            _winexportpdf.Owner = FrameworkApplication.Current.MainWindow;  // Required for modeless dialog
            _winexportpdf.Closed += (o, e) => { _winexportpdf = null; };
            //_winexportpdf.Show();
            //uncomment for modal
            var result = _winexportpdf.ShowDialog();
        }

    }
}
