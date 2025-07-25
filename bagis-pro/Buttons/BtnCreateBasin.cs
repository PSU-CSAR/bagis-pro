
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using bagis_pro.Basin;
using System.Windows;

namespace bagis_pro.Buttons
{
    internal class BtnCreateBasin : Button
    {
        private Basin.WinClipDem _winClipDem = null;
        protected override void OnClick()
        {
            //already open?
            if (_winClipDem != null)
                return;
            _winClipDem = new WinClipDem();
            _winClipDem.Owner = FrameworkApplication.Current.MainWindow;
            _winClipDem.Closed += (o, e) => { _winClipDem = null; };
            //_winClipDem.Show();
            //uncomment for modal
            _winClipDem.ShowDialog();
        }
    }
}
