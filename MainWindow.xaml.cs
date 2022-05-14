using MathNet.Numerics.LinearAlgebra;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace VRC_OSC_ExternallyTrackedObject
{
    internal class AvatarParams
    {
        public string Activate { get; set; } = "/avatar/parameters/OSCTrackingEnabled";
        public string PositionX { get; set; } = "/avatar/parameters/OscTrackedPosX";
        public string PositionY { get; set; } = "/avatar/parameters/OscTrackedPosY";
        public string PositionZ { get; set; } = "/avatar/parameters/OscTrackedPosZ";
        public string RotationX { get; set; } = "/avatar/parameters/OscTrackedRotX";
        public string RotationY { get; set; } = "/avatar/parameters/OscTrackedRotY";
        public string RotationZ { get; set; } = "/avatar/parameters/OscTrackedRotZ";
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

    internal class DeviceListItem
    {
        public string Serial { get; set; }
        public bool Exists { get; set; }
        public string DisplayName
        {
            get { return Serial + ((!Exists) ? " (Not found)" : ""); }
        }

        public DeviceListItem(string serial, bool exists = true)
        {
            Serial = serial;
            Exists = exists;
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
        private ObservableCollection<DeviceListItem> ControllerList = new ObservableCollection<DeviceListItem>();
        private ObservableCollection<DeviceListItem> TrackerList = new ObservableCollection<DeviceListItem>();
        //public MainWindowProperties DisplayProperties { get; set; }
        private OpenVRManager OpenVRManager = new OpenVRManager();
        private OscManager OscManager = new OscManager();
        private string? CurrentAvatarId;

        private Matrix<float> CurrentInverseCalibrationMatrix = null;
        private Matrix<float> CurrentInverseCalibrationMatrixNoScale = null;

        public static float MAX_RELATIVE_DISTANCE = 1; // this is the max distance the tracker can be away from the controller. this is important for scaling the value since vrchat wants a value between -1 and 1

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            //AvatarList.Add(new AvatarListItem("testid", "test name"));

            //var config = new AvatarConfig();
            //config.Name = "test name";

            //CurrentConfig.Avatars.Add("testid", config);

            AvatarDropdown.ItemsSource = AvatarList;
            AvatarListBox.ItemsSource = AvatarList;

            ControllerDropdown.ItemsSource = ControllerList;
            TrackerDropdown.ItemsSource = TrackerList;

            //this.DisplayProperties = new MainWindowProperties();
            //this.DisplayProperties.AllInputsEnabled = true;

            OSCInputAddress.InputText = CurrentConfig.OscInputAddress;
            OSCOutputAddress.InputText = CurrentConfig.OscOutputAddress;

            if (AvatarList.Count > 0)
            {
                AvatarDropdown.SelectedIndex = 0;
            }

            this.OpenVRManager.CalibrationUpdate += OnCalibrationUpdate;
            this.OpenVRManager.TrackingData += OnTrackingData;

            this.OscManager.AvatarChanged += OnAvatarChanged;
            this.OscManager.TrackingActiveChanged += OnTrackingActiveChanged;

            
        }

        public bool Init()
        {
            bool initSuccess = this.OpenVRManager.InitOverlay();

            if (!initSuccess) return false;

            UpdateControllersAndTrackers();
            return true;
        }

        public void ProcessStartupConfig()
        {
            if (CurrentConfig.Autostart)
            {
                StartTracking();
            }
        }

        // this will be called within the calibration thread so we need to inject the event back into the UI thread
        private void OnCalibrationUpdate(object? sender, EventArgs args)
        {
            Dictionary<CalibrationField, LabeledInput> fields = new Dictionary<CalibrationField, LabeledInput>()
            {
                { CalibrationField.POSX, CalibrationPosX },
                { CalibrationField.POSY, CalibrationPosY },
                { CalibrationField.POSZ, CalibrationPosZ },
                { CalibrationField.ROTX, CalibrationRotX },
                { CalibrationField.ROTY, CalibrationRotY },
                { CalibrationField.ROTZ, CalibrationRotZ },
                { CalibrationField.SCALE, CalibrationScale },
            };

            var calibrationUpdateArgs = (CalibrationUpdateArgs)args;

            var dispatcher = Application.Current.Dispatcher;
            dispatcher.BeginInvoke(new Action(() =>
            {
                switch(calibrationUpdateArgs.Type)
                {
                    case CalibrationUpdateArgs.CalibrationUpdateType.ACTIVE_FIELD:
                        CalibrationScale.Highlighted = false;
                        CalibrationPosX.Highlighted = false;
                        CalibrationPosY.Highlighted = false;
                        CalibrationPosZ.Highlighted = false;
                        CalibrationRotX.Highlighted = false;
                        CalibrationRotY.Highlighted = false;
                        CalibrationRotZ.Highlighted = false;

                        fields[calibrationUpdateArgs.Field].Highlighted = true;

                        break;
                    case CalibrationUpdateArgs.CalibrationUpdateType.CALIBRATION_VALUE:
                        fields[calibrationUpdateArgs.Field].InputText = calibrationUpdateArgs.FloatValue.ToString(CultureInfo.InvariantCulture);
                        updateCalibrationValue(calibrationUpdateArgs.Field, calibrationUpdateArgs.FloatValue);
                        break;
                }
            }));
        }

        // will be called within the OVR tracking thread,
        // but we can send the OSC messages directly from here, so no injecting it into the UI thread neccessary
        private void OnTrackingData(object? sender, EventArgs args)
        {
            var trackingEventArgs = (TrackingDataArgs)args;

            if (CurrentInverseCalibrationMatrix == null) return;

            var controllerInverse = trackingEventArgs.Controller.Inverse();
            var _controllerToTracker = controllerInverse * trackingEventArgs.Tracker;

            var controllerToTracker = CurrentInverseCalibrationMatrix * _controllerToTracker;
            var controllerToTrackerNS = CurrentInverseCalibrationMatrixNoScale * _controllerToTracker;

            var relativeTranslate = controllerToTracker.Column(3);

            var relativeRotation = MathUtils.extractRotationsFromMatrix(controllerToTrackerNS.Inverse().SubMatrix(0, 3, 0, 3));

            if (Math.Abs(relativeTranslate[0]) >= MAX_RELATIVE_DISTANCE 
                || Math.Abs(relativeTranslate[1]) >= MAX_RELATIVE_DISTANCE
                || Math.Abs(relativeTranslate[2]) >= MAX_RELATIVE_DISTANCE)
            {
                relativeTranslate[0] = 0;
                relativeTranslate[1] = 0;
                relativeTranslate[2] = 0;
            }

            float rotationX = -relativeRotation[0] / (float)Math.PI;
            float rotationY = relativeRotation[1] / (float)Math.PI;
            float rotationZ = relativeRotation[2] / (float)Math.PI;

            this.OscManager.SendValues(
                -relativeTranslate[0] / MAX_RELATIVE_DISTANCE,
                relativeTranslate[1] / MAX_RELATIVE_DISTANCE,
                relativeTranslate[2] / MAX_RELATIVE_DISTANCE,
                rotationX,
                rotationY,
                rotationZ
            );
        }

        // this is called from the OSC thread, so we need to move the data to the UI thread first
        private void OnTrackingActiveChanged(object? sender, EventArgs args)
        {
            var eventArgs = (TrackingActiveChangedArgs)args;

            var dispatcher = Application.Current.Dispatcher;
            dispatcher.BeginInvoke(new Action(() =>
            {
                if (eventArgs.Active)
                {
                    CurrentStatusLabel.Content = "active";
                }
                else if (eventArgs.AvatarKnown)
                {
                    CurrentStatusLabel.Content = "inactive (disabled)";
                }
                else
                {
                    CurrentStatusLabel.Content = "inactive (unknown avatar)";
                }
            }));
        }

        // this is called from the OSC thread, so we need to move the data to the UI thread first
        private void OnAvatarChanged(object? sender, EventArgs args)
        {
            var avatarChangeArgs = (AvatarChangedArgs)args;

            var dispatcher = Application.Current.Dispatcher;
            dispatcher.BeginInvoke(new Action(() =>
            {
                Debug.WriteLine("Avatar changed to " + avatarChangeArgs.Id);

                if (CurrentConfig.Avatars.ContainsKey(avatarChangeArgs.Id))
                {
                    Debug.WriteLine("we know this avatar, creating inverse calibration matrix");

                    var calibration = CurrentConfig.Avatars[avatarChangeArgs.Id].Calibration;

                    CurrentInverseCalibrationMatrix = MathUtils.createTransformMatrix44(
                        (float)calibration.RotationX, (float)calibration.RotationY, (float)calibration.RotationZ,
                        (float)calibration.TranslationX, (float)calibration.TranslationY, (float)calibration.TranslationZ,
                        (float)calibration.Scale, (float)calibration.Scale, (float)calibration.Scale
                    ).Inverse();

                    CurrentInverseCalibrationMatrixNoScale = MathUtils.createTransformMatrix44(
                        (float)calibration.RotationX, (float)calibration.RotationY, (float)calibration.RotationZ,
                        (float)calibration.TranslationX, (float)calibration.TranslationY, (float)calibration.TranslationZ,
                        1.0f, 1.0f, 1.0f
                    ).Inverse();
                }
            }));
        }

        private void updateCalibrationValue(CalibrationField field, float value)
        {
            if (CurrentAvatarId == null) return;

            switch(field)
            {
                case CalibrationField.POSX:
                    CurrentConfig.Avatars[CurrentAvatarId].Calibration.TranslationX = value;
                    return;
                case CalibrationField.POSY:
                    CurrentConfig.Avatars[CurrentAvatarId].Calibration.TranslationY = value;
                    return;
                case CalibrationField.POSZ:
                    CurrentConfig.Avatars[CurrentAvatarId].Calibration.TranslationZ = value;
                    return;
                case CalibrationField.ROTX:
                    CurrentConfig.Avatars[CurrentAvatarId].Calibration.RotationX = value;
                    return;
                case CalibrationField.ROTY:
                    CurrentConfig.Avatars[CurrentAvatarId].Calibration.RotationY = value;
                    return;
                case CalibrationField.ROTZ:
                    CurrentConfig.Avatars[CurrentAvatarId].Calibration.RotationZ = value;
                    return;
                case CalibrationField.SCALE:
                    CurrentConfig.Avatars[CurrentAvatarId].Calibration.Scale = value;
                    return;
            }
        }

        private void CopySettingsToConfig()
        {
            CurrentConfig.OscInputAddress = OSCInputAddress.InputText;
            CurrentConfig.OscOutputAddress = OSCOutputAddress.InputText;
            CurrentConfig.Autostart = AutostartCheckbox.IsChecked == true;
        }

        private void Btn_saveConfig(object sender, RoutedEventArgs e)
        {
            if (CurrentAvatarId != null)
            {
                CopyInputValuesToConfig(CurrentConfig.Avatars[CurrentAvatarId]);
            }

            CopySettingsToConfig();

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JSON file|*.json";
            if (saveFileDialog.ShowDialog() == true)
            {
                string jsonString = JsonSerializer.Serialize(CurrentConfig, new JsonSerializerOptions() { WriteIndented = true });
                File.WriteAllText(saveFileDialog.FileName, jsonString);
            }
        }

        public void LoadConfig(String path)
        {
            string jsonString = File.ReadAllText(path);

            if (jsonString == null) return;

            Configuration config = new Configuration();

            try
            {
                config = JsonSerializer.Deserialize<Configuration>(jsonString);
            }
            catch (JsonException ex)
            {
                MessageBox.Show("Could not parse config file: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (config == null) return;

            CurrentConfig = config;

            if (CurrentConfig.ControllerSerial != null && CurrentConfig.ControllerSerial.Length > 0) {
                bool found = false;

                for (int i = 0; i < ControllerList.Count; i++)
                {
                    if (ControllerList[i].Serial == CurrentConfig.ControllerSerial)
                    {
                        ControllerDropdown.SelectedIndex = i;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    ControllerList.Add(new DeviceListItem(CurrentConfig.ControllerSerial, false));
                    ControllerDropdown.SelectedValue = CurrentConfig.ControllerSerial;
                }
            }

            if (CurrentConfig.TrackerSerial != null && CurrentConfig.TrackerSerial.Length > 0)
            {
                bool found = false;

                for (int i = 0; i < TrackerList.Count; i++)
                {
                    if (TrackerList[i].Serial == CurrentConfig.TrackerSerial)
                    {
                        TrackerDropdown.SelectedIndex = i;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    TrackerList.Add(new DeviceListItem(CurrentConfig.TrackerSerial, false));
                    TrackerDropdown.SelectedValue = CurrentConfig.TrackerSerial;
                }
            }

            OSCInputAddress.InputText = CurrentConfig.OscInputAddress;
            OSCOutputAddress.InputText = CurrentConfig.OscOutputAddress;
            AutostartCheckbox.IsChecked = CurrentConfig.Autostart;

            AvatarList.Clear();
            foreach (var avi in CurrentConfig.Avatars)
            {
                AvatarList.Add(new AvatarListItem(avi.Key, avi.Value.Name));
            }

            if (AvatarList.Count > 0)
            {
                AvatarDropdown.SelectedIndex = 0;
            }
        }

        private void Btn_openConfig(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON file|*.json";
            if (openFileDialog.ShowDialog() == true)
            {
                LoadConfig(openFileDialog.FileName);
            }
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

            // first, store all the current input values in the config object
            if (CurrentAvatarId != null && CurrentConfig.Avatars.ContainsKey(CurrentAvatarId))
            {
                var currentAvatarConfig = CurrentConfig.Avatars[CurrentAvatarId];
                CopyInputValuesToConfig(currentAvatarConfig);
            }

            // afterwards switch everything over to the new avatar
            CurrentAvatarId = ((AvatarListItem)AvatarDropdown.SelectedItem).Id;
            var avatarConfig = CurrentConfig.Avatars[CurrentAvatarId];

            FillAndEnableAllInputs(avatarConfig);

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

        private void FillAllCalibrationInputs(AvatarCalibration calibration)
        {
            CalibrationScale.InputText = calibration.Scale.ToString(CultureInfo.InvariantCulture);
            CalibrationPosX.InputText = calibration.TranslationX.ToString(CultureInfo.InvariantCulture);
            CalibrationPosY.InputText = calibration.TranslationY.ToString(CultureInfo.InvariantCulture);
            CalibrationPosZ.InputText = calibration.TranslationZ.ToString(CultureInfo.InvariantCulture);
            CalibrationRotX.InputText = calibration.RotationX.ToString(CultureInfo.InvariantCulture);
            CalibrationRotY.InputText = calibration.RotationY.ToString(CultureInfo.InvariantCulture);
            CalibrationRotZ.InputText = calibration.RotationZ.ToString(CultureInfo.InvariantCulture);
        }

        private void ControlAllCalibrationInputs(bool enabled)
        {
            CalibrationScale.IsEnabled = enabled;
            CalibrationPosX.IsEnabled = enabled;
            CalibrationPosY.IsEnabled = enabled;
            CalibrationPosZ.IsEnabled = enabled;
            CalibrationRotX.IsEnabled = enabled;
            CalibrationRotY.IsEnabled = enabled;
            CalibrationRotZ.IsEnabled = enabled;
        }

        private void FillAllParameterInputs(AvatarParams parameters)
        {
            ParamTrigger.InputText = parameters.Activate;
            ParamPosX.InputText = parameters.PositionX;
            ParamPosY.InputText = parameters.PositionY;
            ParamPosZ.InputText = parameters.PositionZ;
            ParamRotX.InputText = parameters.RotationX;
            ParamRotY.InputText = parameters.RotationY;
            ParamRotZ.InputText = parameters.RotationZ;
        }

        private void ControlAllParameterInputs(bool enabled)
        {
            ParamTrigger.IsEnabled = enabled;
            ParamPosX.IsEnabled = enabled;
            ParamPosY.IsEnabled = enabled;
            ParamPosZ.IsEnabled = enabled;
            ParamRotX.IsEnabled = enabled;
            ParamRotY.IsEnabled = enabled;
            ParamRotZ.IsEnabled = enabled;
        }

        private void FillAndEnableAllInputs(AvatarConfig config)
        {
            FillAllParameterInputs(config.Parameters);
            ControlAllParameterInputs(true);
            FillAllCalibrationInputs(config.Calibration);
            ControlAllCalibrationInputs(true);

            StartTrackingButton.IsEnabled = true;
            StartCalibrationButton.IsEnabled = true;
        }

        private void CopyInputValuesToConfig(AvatarConfig config)
        {
            CopyCalibrationValuesToConfig(config);
            CopyParametersToConfig(config);
        }

        private void CopyCalibrationValuesToConfig(AvatarConfig config)
        {
            config.Calibration.Scale = float.Parse(CalibrationScale.InputText, CultureInfo.InvariantCulture);
            config.Calibration.TranslationX = float.Parse(CalibrationPosX.InputText, CultureInfo.InvariantCulture);
            config.Calibration.TranslationY = float.Parse(CalibrationPosY.InputText, CultureInfo.InvariantCulture);
            config.Calibration.TranslationZ = float.Parse(CalibrationPosZ.InputText, CultureInfo.InvariantCulture);
            config.Calibration.RotationX = float.Parse(CalibrationRotX.InputText, CultureInfo.InvariantCulture);
            config.Calibration.RotationY = float.Parse(CalibrationRotY.InputText, CultureInfo.InvariantCulture);
            config.Calibration.RotationZ = float.Parse(CalibrationRotZ.InputText, CultureInfo.InvariantCulture);
        }

        private void CopyParametersToConfig(AvatarConfig config)
        {
            config.Parameters.Activate = ParamTrigger.InputText;
            config.Parameters.PositionX = ParamPosX.InputText;
            config.Parameters.PositionY = ParamPosY.InputText;
            config.Parameters.PositionZ = ParamPosZ.InputText;
            config.Parameters.RotationX = ParamRotX.InputText;
            config.Parameters.RotationY = ParamRotY.InputText;
            config.Parameters.RotationZ = ParamRotZ.InputText;
        }

        private void StartCalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.OpenVRManager.IsAnyThreadRunning())
            {
                StartCalibrationButton.Content = "Start Calibration";
                StartTrackingButton.IsEnabled = true;
                AvatarDropdown.IsEnabled = true;
                TrackingTab.IsEnabled = true;
                AvatarsTab.IsEnabled = true;

                this.OpenVRManager.StopThread();

                // clear highlight as well
                CalibrationScale.Highlighted = false;
                CalibrationPosX.Highlighted = false;
                CalibrationPosY.Highlighted = false;
                CalibrationPosZ.Highlighted = false;
                CalibrationRotX.Highlighted = false;
                CalibrationRotY.Highlighted = false;
                CalibrationRotZ.Highlighted = false;
            } 
            else
            {
                if (ControllerDropdown.SelectedItem == null)
                {
                    MessageBox.Show("No controller selected", "Controller missing", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                if (AvatarDropdown.SelectedItem == null)
                {
                    MessageBox.Show("No avatar selected. You need to select an avatar to calibrate the values for.", "Avatar missing", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string currentController = ((DeviceListItem)ControllerDropdown.SelectedItem).Serial;
                string currentAvatar = ((AvatarListItem)AvatarDropdown.SelectedItem).Id;

                // load values from inputs
                CopyCalibrationValuesToConfig(CurrentConfig.Avatars[currentAvatar]);

                // copy the current calibration to the calibration thread
                // updates are then sent by the calibration thread via events
                AvatarCalibration calibration = CurrentConfig.Avatars[currentAvatar].Calibration;

                StartCalibrationButton.Content = "Stop Calibration";
                StartTrackingButton.IsEnabled = false;
                AvatarDropdown.IsEnabled = false;
                TrackingTab.IsEnabled = false;
                AvatarsTab.IsEnabled = false;

                this.OpenVRManager.StartCalibrationThread(currentController, calibration);
            }
        }

        private void StartTrackingButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.OpenVRManager.IsAnyThreadRunning())
            {
                StartTrackingButton.Content = "Start Tracking";
                StartCalibrationButton.IsEnabled = true;
                AvatarDropdown.IsEnabled = true;
                CalibrationTab.IsEnabled = true;
                AvatarsTab.IsEnabled = true;

                this.OpenVRManager.StopThread();
                this.OscManager.Stop();
            }
            else
            {
                string currentAvatar = ((AvatarListItem)AvatarDropdown.SelectedItem).Id;

                // load values from inputs
                CopyParametersToConfig(CurrentConfig.Avatars[currentAvatar]);

                StartTracking();
            }
        }

        private void StartTracking()
        {
            if (ControllerDropdown.SelectedItem == null)
            {
                MessageBox.Show("No controller selected", "Controller missing", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (TrackerDropdown.SelectedItem == null)
            {
                MessageBox.Show("No tracker selected", "Tracker missing", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (OSCInputAddress.InputText == null || OSCInputAddress.InputText.Length == 0)
            {
                MessageBox.Show("No input address provided", "Input address missing", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (OSCOutputAddress.InputText == null || OSCOutputAddress.InputText.Length == 0)
            {
                MessageBox.Show("No output address provided", "Output address missing", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string inputAddress;
            int inputPort;
            string outputAddress;
            int outputPort;

            if (IPEndPoint.TryParse(OSCInputAddress.InputText, out IPEndPoint endpoint))
            {
                inputAddress = endpoint.Address.ToString();
                inputPort = endpoint.Port;
            }
            else
            {
                MessageBox.Show("Could not parse input address. Make sure it has the format <address>:<port>", "Invalid input address", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (IPEndPoint.TryParse(OSCOutputAddress.InputText, out endpoint))
            {
                outputAddress = endpoint.Address.ToString();
                outputPort = endpoint.Port;
            }
            else
            {
                MessageBox.Show("Could not parse output address. Make sure it has the format <address>:<port>", "Invalid input address", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string currentController = ((DeviceListItem)ControllerDropdown.SelectedItem).Serial;
            string currentTracker = ((DeviceListItem)TrackerDropdown.SelectedItem).Serial;

            bool trackingStarted = this.OpenVRManager.StartTrackingThread(currentController, currentTracker);

            if (!trackingStarted) return;

            StartTrackingButton.Content = "Stop Tracking";
            StartCalibrationButton.IsEnabled = false;
            AvatarDropdown.IsEnabled = false;
            CalibrationTab.IsEnabled = false;
            AvatarsTab.IsEnabled = false;

            this.OscManager.Start(inputAddress, inputPort, outputAddress, outputPort, CurrentConfig.Avatars);
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
            if (sender == null) return;

            object selectedValue = (sender as ComboBox).SelectedValue;

            if (selectedValue == null) return;

            CurrentConfig.TrackerSerial = (string)selectedValue;
        }

        private void ControllerDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender == null) return;

            object selectedValue = (sender as ComboBox).SelectedValue;

            if (selectedValue == null) return;

            CurrentConfig.ControllerSerial = (string)selectedValue;
        }

        private void UpdateControllersAndTrackers()
        {
            this.OpenVRManager.UpdateControllers();

            this.ControllerList.Clear();
            foreach (string controllerId in this.OpenVRManager.GetControllers())
            {
                this.ControllerList.Add(new DeviceListItem(controllerId));
            }

            this.TrackerList.Clear();
            foreach (string trackerId in this.OpenVRManager.GetTrackers())
            {
                this.TrackerList.Add(new DeviceListItem(trackerId));
            }
        }

        private void DevicesRefresh_Click(object sender, RoutedEventArgs e)
        {
            var currentController = ControllerDropdown.SelectedItem as DeviceListItem;
            var currentControllerSerial = (currentController != null) ? currentController.Serial : null;
            var currentTracker = TrackerDropdown.SelectedItem as DeviceListItem;
            var currentTrackerSerial = (currentTracker != null) ? currentTracker.Serial : null;

            UpdateControllersAndTrackers();

            if (currentControllerSerial != null)
            {
                ControllerDropdown.SelectedValue = currentControllerSerial;
            }
            if (currentTrackerSerial != null)
            {
                TrackerDropdown.SelectedValue = currentTrackerSerial;
            }
        }
    }
}