using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using bagis_pro.AoiTools;
using System.Windows;


namespace bagis_pro.Buttons
{
    internal class BtnDefineAoi : Button
    {
        private WinDefineAoi _winDefineAoi = null;
        protected override void OnClick()
        {
            //already open?
            if (_winDefineAoi != null)
                return;
            _winDefineAoi = new WinDefineAoi();
            _winDefineAoi.Owner = FrameworkApplication.Current.MainWindow;
            _winDefineAoi.Closed += (o, e) => { _winDefineAoi = null; };
            _winDefineAoi.Show();
            //uncomment for modal
            //_windefineaoi.ShowDialog();
        }
    }
}
