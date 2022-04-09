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

namespace VRC_OSC_ExternallyTrackedObject
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class LabeledInput : UserControl
    {
        public string LabelText
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("LabelText", typeof(string), typeof(LabeledInput), new PropertyMetadata(null));

        public string InputText
        {
            get { return (string)GetValue(InputTextProperty); }
            set { SetValue(InputTextProperty, value); }
        }
        public static readonly DependencyProperty InputTextProperty =
            DependencyProperty.Register("InputText", typeof(string), typeof(LabeledInput), new PropertyMetadata(null));

        public bool Highlighted
        {
            get { return (bool)GetValue(HighlightedProperty); }
            set { SetValue(HighlightedProperty, value); }
        }
        public static readonly DependencyProperty HighlightedProperty =
            DependencyProperty.Register("Highlighted", typeof(bool), typeof(LabeledInput), new PropertyMetadata(null));

        public LabeledInput()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == HighlightedProperty)
            {
                bool val = (bool)e.NewValue;

                if (val)
                {
                    OuterShell.Background = new SolidColorBrush(Colors.Red);
                }
                else
                {
                    OuterShell.Background = null;
                }
            }
            else
            {
                base.OnPropertyChanged(e);
            }
        }
    }
}
