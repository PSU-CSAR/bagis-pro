using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace bagis_pro
{
    /// <summary>
    /// Interaction logic for WinExportPdf.xaml
    /// </summary>
    public partial class WinExportPdf : ArcGIS.Desktop.Framework.Controls.ProWindow
    {
        public WinExportPdf()
        {
            InitializeComponent();
            this.DataContext = new WinExportPdfViewModel(this);
        }

        private void ClickOK(WinExportPdf oWinExportPdf)
        {
            oWinExportPdf.DialogResult = true;
            oWinExportPdf.Close();
        }

    }
}
