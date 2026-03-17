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
using System.Windows.Shapes;

namespace bagis_pro
{
    /// <summary>
    /// Interaction logic for InputWindow.xaml
    /// This is a re-usable class to replicate the VB InputBox function that doesn't exist in C#
    /// </summary>
    public partial class InputWindow : Window
    {
        public string UserInput { get; private set; }
        public InputWindow(string title, string promptText, string defaultInput)
        {
            InitializeComponent();
            Title = title;
            PromptLabel.Content = promptText;
            if (! string.IsNullOrEmpty(defaultInput))
            {
                InputTextBox.Text = defaultInput;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            UserInput = InputTextBox.Text;
            DialogResult = true; // Sets the result and closes the window
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false; // Sets the result and closes the window
        }
    }
}
