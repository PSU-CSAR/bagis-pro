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
    /// Interaction logic for DockAdminToolsView.xaml
    /// </summary>
    public partial class DockAdminToolsView : UserControl
    {
        public DockAdminToolsView()
        {
            InitializeComponent();
        }
        public void SelectedTime_Checked(object s, RoutedEventArgs e)
        {
            var radio = s as RadioButton;
            string strTag = Convert.ToString(radio.Tag);
            if (strTag.Equals("SelectedTimeChecked"))
            {
                bool bChecked = radio.IsChecked ?? false;
                ckAnnual.IsEnabled = bChecked;
                tbSelectMinYear.IsEnabled = bChecked;
                tbSelectMaxYear.IsEnabled = bChecked;
                ckPeriods.IsEnabled = bChecked;
                tbIncrementYears .IsEnabled = bChecked; 
            }

        }
    }
}
