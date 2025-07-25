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

namespace bagis_pro.Basin
{
    /// <summary>
    /// Interaction logic for WinClipDem.xaml
    /// </summary>
    public partial class WinClipDem : ArcGIS.Desktop.Framework.Controls.ProWindow
    {
        public WinClipDem()
        {
            InitializeComponent();
            this.DataContext = new WinClipDemModel(this);
        }
    }
}
