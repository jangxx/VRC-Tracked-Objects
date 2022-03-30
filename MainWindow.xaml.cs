using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace VRC_OSC_ExternallyTrackedObject
{
    internal class AvatarParams
    {
        public string Activate { get; set; } = "OSCTrackingActivate";
        public string PositionX { get; set; } = "OSCTrackingPosX";
        public string PositionY { get; set; } = "OSCTrackingPosY";
        public string PositionZ { get; set; } = "OSCTrackingPosZ";
        public string RotationX { get; set; } = "OSCTrackingRotX";
        public string RotationY { get; set; } = "OSCTrackingRotY";
        public string RotationZ { get; set; } = "OSCTrackingRotZ";
    }

    internal class AvatarCalibration
    {
        public double Scale { get; set; } = 1;
        public double TranslationX { get; set; } = 0;
        public double TranslationY { get; set; } = 0;
        public double TranslationZ { get; set; } = 0;
        public double RotationX { get; set; } = 0;
        public double RotationY { get; set; } = 0;
        public double RotationZ { get; set; } = 0;
    }

    internal class AvatarConfig
    {
        public string Name { get; set; } = "";
        public AvatarParams Parameters { get; set; } = new AvatarParams();
        public AvatarCalibration Calibration { get; set; } = new AvatarCalibration();
    }

    internal class Configuration
    {
        public bool Autostart { get; set; } = false;
        public string OscInputAddress { get; set; } = "127.0.0.1:9001";
        public string OscOutputAddress { get; set; } = "127.0.0.1:9000";
        public string? ControllerSerial { get; set; }
        public string? TrackerSerial { get; set; }
        public Dictionary<string, AvatarConfig> Avatars { get; set; } = new Dictionary<string, AvatarConfig>();
    }

    internal class AvatarListItem
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string DisplayName
        {
            get { return Name + " (" + Id + ")"; }
        }

        public AvatarListItem(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    //public class MainWindowProperties : INotifyPropertyChanged
    //{
    //    public event PropertyChangedEventHandler PropertyChanged;
    //    protected virtual void OnPropertyChanged(string propertyName)
    //    {
    //        PropertyChangedEventHandler handler = PropertyChanged;
    //        if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
    //    }
    //    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    //    {
    //        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
    //        field = value;
    //        OnPropertyChanged(propertyName);
    //        return true;
    //    }

    //    private bool allInputsEnabled;
    //    public bool AllInputsEnabled
    //    {
    //        get { return allInputsEnabled; }
    //        set { SetField(ref allInputsEnabled, value); }
    //    }
    //}

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Configuration CurrentConfig = new Configuration();
        private ObservableCollection<AvatarListItem> AvatarList = new ObservableCollection<AvatarListItem>();
        //public MainWindowProperties DisplayProperties { get; set; }
        private OpenVRManager OpenVRManager = new OpenVRManager();

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            AvatarList.Add(new AvatarListItem("ididiid", "namenamenae"));

            AvatarDropdown.ItemsSource = AvatarList;
            AvatarListBox.ItemsSource = AvatarList;

            //this.DisplayProperties = new MainWindowProperties();
            //this.DisplayProperties.AllInputsEnabled = true;

            if (AvatarList.Count > 0)
            {
                AvatarDropdown.SelectedIndex = 0;
            }

            this.OpenVRManager.InitOverlay();
        }

        private void Btn_saveConfig(object sender, RoutedEventArgs e)
        {
        }

        private void Btn_openConfig(object sender, RoutedEventArgs e)
        {
        }

        private void Btn_addAvatar(object sender, RoutedEventArgs e)
        {
            if (NewAvatarId.InputText == "" || NewAvatarId.InputText == null)
            {
                MessageBox.Show("The id field can not be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (NewAvatarName.InputText == "" || NewAvatarName.InputText == null)
            {
                MessageBox.Show("The name field can not be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var avatarId = NewAvatarId.InputText;
            var avatarName = NewAvatarName.InputText;

            var config = new AvatarConfig();
            config.Name = avatarName;

            CurrentConfig.Avatars.Add(avatarId, config);

            var listItem = new AvatarListItem(avatarId, avatarName);
            AvatarList.Add(listItem);

            NewAvatarId.InputText = "";
            NewAvatarName.InputText = "";

            if (AvatarList.Count == 1)
            {
                AvatarDropdown.SelectedIndex = 0;
            }
        }

        private void Btn_deleteAvatar(object sender, RoutedEventArgs e)
        {
            if (AvatarListBox.SelectedItem == null) return;

            CurrentConfig.Avatars.Remove(((AvatarListItem)AvatarListBox.SelectedItem).Id);
            AvatarList.Remove((AvatarListItem)AvatarListBox.SelectedItem);
        }

        private void AvatarDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AvatarDropdown.SelectedItem == null)
            {
                ClearAndDisableAllInputs();
                return;
            }

            var currentAvatarId = ((AvatarListItem)AvatarDropdown.SelectedItem).Id;
        }

        private void ClearAndDisableAllInputs()
        {
            ParamTrigger.InputText = "";
            ParamTrigger.IsEnabled = false;
            ParamPosX.InputText = "";
            ParamPosX.IsEnabled = false;
            ParamPosY.InputText = "";
            ParamPosY.IsEnabled = false;
            ParamPosZ.InputText = "";
            ParamPosZ.IsEnabled = false;
            ParamRotX.InputText = "";
            ParamRotX.IsEnabled = false;
            ParamRotY.InputText = "";
            ParamRotY.IsEnabled = false;
            ParamRotZ.InputText = "";
            ParamRotZ.IsEnabled = false;
            CalibrationScale.InputText = "";
            CalibrationScale.IsEnabled = false;
            CalibrationPosX.InputText = "";
            CalibrationPosX.IsEnabled = false;
            CalibrationPosY.InputText = "";
            CalibrationPosY.IsEnabled = false;
            CalibrationPosZ.InputText = "";
            CalibrationPosZ.IsEnabled = false;
            CalibrationRotX.InputText = "";
            CalibrationRotX.IsEnabled = false;
            CalibrationRotY.InputText = "";
            CalibrationRotY.IsEnabled = false;
            CalibrationRotZ.InputText = "";
            CalibrationRotZ.IsEnabled = false;

            StartTrackingButton.IsEnabled = false;
            StartCalibrationButton.IsEnabled = false;
        }

        private void FillAndEnableAllInputs(AvatarConfig config)
        {
            ParamTrigger.InputText = config.Parameters.Activate;
            ParamTrigger.IsEnabled= true;
            ParamPosX.InputText = config.Parameters.PositionX;
            ParamPosX.IsEnabled= true;
            ParamPosY.InputText = config.Parameters.PositionY;
            ParamPosY.IsEnabled= true;
            ParamPosZ.InputText = config.Parameters.PositionZ;
            ParamPosZ.IsEnabled= true;
            ParamRotX.InputText = config.Parameters.RotationX;
            ParamRotX.IsEnabled= true;
            ParamRotY.InputText = config.Parameters.RotationY;
            ParamRotY.IsEnabled= true;
            ParamRotZ.InputText = config.Parameters.RotationZ;
            ParamRotZ.IsEnabled= true;
            CalibrationScale.InputText = config.Calibration.Scale.ToString();
            CalibrationScale.IsEnabled= true;
            CalibrationPosX.InputText = config.Calibration.TranslationX.ToString();
            CalibrationPosX.IsEnabled= true;
            CalibrationPosY.InputText = config.Calibration.TranslationY.ToString();
            CalibrationPosY.IsEnabled= true;
            CalibrationPosZ.InputText = config.Calibration.TranslationZ.ToString();
            CalibrationPosZ.IsEnabled= true;
            CalibrationRotX.InputText = config.Calibration.RotationX.ToString();
            CalibrationRotX.IsEnabled= true;
            CalibrationRotY.InputText = config.Calibration.RotationY.ToString();
            CalibrationRotY.IsEnabled= true;
            CalibrationRotZ.InputText = config.Calibration.RotationZ.ToString();
            CalibrationRotZ.IsEnabled= true;

            StartTrackingButton.IsEnabled= true;
            StartCalibrationButton.IsEnabled= true;
        }

        private void StartCalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            //this.OpenVRManager.StartCalibrationThread();
        }

        private void StartTrackingButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void MainWindowName_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!this.OpenVRManager.IsCalibrationThreadRunning()) return;

            if (e.Key == System.Windows.Input.Key.Up ||
                e.Key == System.Windows.Input.Key.Down ||
                e.Key == System.Windows.Input.Key.Left ||
                e.Key == System.Windows.Input.Key.Right)
            {
                this.OpenVRManager.InjectKeyPress(e.Key);
            }

            e.Handled = true;
        }

        private void TrackerDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void ControllerDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}