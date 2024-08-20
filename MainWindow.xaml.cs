using MathNet.Numerics.LinearAlgebra;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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


    public class ConfigurationListItem
    {
        public AvatarConfig Config { get; set; }

        public string DisplayName
        {
            get { return Config.Name + " (" + Config.Avatars.Count + " avatars)"; }
        }

        public ConfigurationListItem(AvatarConfig config)
        {
            Config = config;
        }
    }

    public class ConfigurationAvatarListItem
    {
        public string Name { get; set; }
        public string Id { get; set; }

        public string DisplayName
        {
            get { return Name + " (" + Id + ")"; }
        }

        public ConfigurationAvatarListItem(string name, string id)
        {
            Name = name;
            Id = id;
        }
    }

    public class DeviceListItem
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
        private Configuration _currentConfig = new Configuration();
        public ObservableCollection<ConfigurationListItem> ConfigurationList { get; } = new ObservableCollection<ConfigurationListItem>();
        public ObservableCollection<ConfigurationAvatarListItem> ConfigurationAvatarList { get; } = new ObservableCollection<ConfigurationAvatarListItem>();
        public ObservableCollection<DeviceListItem> ControllerList { get; } = new ObservableCollection<DeviceListItem>();
        public ObservableCollection<DeviceListItem> TrackerList { get; } = new ObservableCollection<DeviceListItem>();
        //public MainWindowProperties DisplayProperties { get; set; }
        private OpenVRManager _openVRManager = new OpenVRManager();
        private OscManager _oscManager = new OscManager();
        private ProcessThread? _processThread;

        private bool _hasUnsavedChanges = false;
        private bool _suppressInputEvents = false;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            SetInputValues(() =>
            {
                OSCInputAddress.InputText = _currentConfig.OscInputAddress;
                OSCOutputAddress.InputText = _currentConfig.OscOutputAddress;
            });

            ClearAndDisableAllInputs();
            MainTabs.SelectedItem = ConfigurationsTab;

            _openVRManager.CalibrationUpdate += OnCalibrationUpdate;

            _oscManager.TrackingActiveChanged += OnTrackingActiveChanged;
            _oscManager.ThreadCrashed += OnOscThreadCrashed;
            
        }

        public bool Init()
        {
            bool initSuccess = _openVRManager.InitOverlay();

            if (!initSuccess) return false;

            UpdateControllersAndTrackers();
            return true;
        }

        public void ProcessStartupConfig()
        {
            if (_currentConfig.Configurations.Count > 0)
            {
                MainTabs.SelectedItem = TrackingTab;
            }

            if (_currentConfig.Autostart)
            {
                StartTracking();
            }
        }

        private AvatarConfig? FindAvatarConfig(string avatarId)
        {
            foreach (var config in _currentConfig.Configurations)
            {
                foreach (var configAvatar in config.Avatars) {
                    if (configAvatar.Id == avatarId)
                    {
                        return config;
                    }
                }
            }

            return null;
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
                        if (ConfigurationDropdown.SelectedIndex != -1)
                        {
                            _currentConfig.Configurations[ConfigurationDropdown.SelectedIndex].Calibration.CopyFrom(calibrationUpdateArgs.CalibrationValues!);

                        }

                        SetInputValues(() =>
                        {
                            CalibrationPosX.InputText = NumberToInput(calibrationUpdateArgs.CalibrationValues!.TranslationX);
                            CalibrationPosY.InputText = NumberToInput(calibrationUpdateArgs.CalibrationValues!.TranslationY);
                            CalibrationPosZ.InputText = NumberToInput(calibrationUpdateArgs.CalibrationValues!.TranslationZ);
                            CalibrationRotX.InputText = NumberToInput(calibrationUpdateArgs.CalibrationValues!.RotationX);
                            CalibrationRotY.InputText = NumberToInput(calibrationUpdateArgs.CalibrationValues!.RotationY);
                            CalibrationRotZ.InputText = NumberToInput(calibrationUpdateArgs.CalibrationValues!.RotationZ);
                            CalibrationScale.InputText = NumberToInput(calibrationUpdateArgs.CalibrationValues!.Scale);
                        });

                        _hasUnsavedChanges = true;
                        break;
                }
            }));
        }

        private string NumberToInput(double number)
        {
            var dec = Convert.ToDecimal(FixZero(number));
            return Math.Round(dec, 6).ToString(CultureInfo.InvariantCulture);
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

        // this is called from the process thread, so we need to move the data to the UI thread first
        private void OnAvatarConfigChanged(object? sender, EventArgs args)
        {
            var avatarConfigChangeArgs = (AvatarConfigChangedArgs)args;

            var dispatcher = Application.Current.Dispatcher;
            dispatcher.BeginInvoke(new Action(() =>
            {
                for (int i = 0; i < ConfigurationList.Count; i++)
                {
                    if (ConfigurationList[i].Config == avatarConfigChangeArgs.AvatarConfig)
                    {
                        ConfigurationDropdown.SelectedIndex = i;
                    }
                }
            }));
        }

        private void OnOscThreadCrashed(object? sender, EventArgs args)
        {
            var dispatcher = Application.Current.Dispatcher;
            dispatcher.BeginInvoke(new Action(() =>
            {
                ShowTrackingStopped();
                _openVRManager.StopThread();
                _oscManager.Stop();
            }));
        }

        private void CopySettingsToConfig()
        {
            _currentConfig.OscInputAddress = OSCInputAddress.InputText;
            _currentConfig.OscOutputAddress = OSCOutputAddress.InputText;
            _currentConfig.Autostart = AutostartCheckbox.IsChecked == true;
        }

        private void Btn_saveDefaultConfig(object sender, RoutedEventArgs e)
        {
            if (ConfigurationDropdown.SelectedIndex != -1)
            {
                CopyInputValuesToConfig(_currentConfig.Configurations[ConfigurationDropdown.SelectedIndex]);
            }

            CopySettingsToConfig();

            // try to create config folder
            try
            {
                var defaultConfigFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Const.DefaultConfigPath
                );

                Debug.WriteLine("Saving config in " + defaultConfigFilePath);
                Directory.CreateDirectory(defaultConfigFilePath);

                string jsonString = JsonSerializer.Serialize(_currentConfig, new JsonSerializerOptions() { WriteIndented = true });
                File.WriteAllText(Path.Combine(defaultConfigFilePath, Const.DefaultConfigName), jsonString);

                _hasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while saving config: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Btn_saveConfig(object sender, RoutedEventArgs e)
        {
            if (ConfigurationDropdown.SelectedIndex != -1)
            {
                CopyInputValuesToConfig(_currentConfig.Configurations[ConfigurationDropdown.SelectedIndex]);
            }

            CopySettingsToConfig();

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JSON file|*.json";
            if (saveFileDialog.ShowDialog() == true)
            {
                string jsonString = JsonSerializer.Serialize(_currentConfig, new JsonSerializerOptions() { WriteIndented = true });
                File.WriteAllText(saveFileDialog.FileName, jsonString);

                _hasUnsavedChanges = false;
            }
        }

        public void LoadConfig(string path)
        {
            Configuration? config;

            try
            {
                config = ConfigLoader.LoadConfig(path);
            }
            catch (JsonException ex)
            {
                MessageBox.Show("Could not parse config file: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (config == null) return;

            _currentConfig = config;

            if (_currentConfig.ControllerSerial != null && _currentConfig.ControllerSerial.Length > 0) {
                bool found = false;

                for (int i = 0; i < ControllerList.Count; i++)
                {
                    if (ControllerList[i].Serial == _currentConfig.ControllerSerial)
                    {
                        ControllerDropdown.SelectedIndex = i;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    ControllerList.Add(new DeviceListItem(_currentConfig.ControllerSerial, false));
                    ControllerDropdown.SelectedValue = _currentConfig.ControllerSerial;
                }
            }

            if (_currentConfig.TrackerSerial != null && _currentConfig.TrackerSerial.Length > 0)
            {
                bool found = false;

                for (int i = 0; i < TrackerList.Count; i++)
                {
                    if (TrackerList[i].Serial == _currentConfig.TrackerSerial)
                    {
                        TrackerDropdown.SelectedIndex = i;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    TrackerList.Add(new DeviceListItem(_currentConfig.TrackerSerial, false));
                    TrackerDropdown.SelectedValue = _currentConfig.TrackerSerial;
                }
            }

            SetInputValues(() =>
            {
                OSCInputAddress.InputText = _currentConfig.OscInputAddress;
                OSCOutputAddress.InputText = _currentConfig.OscOutputAddress;
            });
            AutostartCheckbox.IsChecked = _currentConfig.Autostart;

            UpdateConfigurationList();

            if (ConfigurationList.Count > 0)
            {
                ConfigurationDropdown.SelectedIndex = 0;
            }

            _hasUnsavedChanges = false;
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

        private void Btn_openDefaultConfig(object sender, RoutedEventArgs e)
        {
            var messageBoxResult = MessageBox.Show("Are you sure you want to reload the config from the default location? This will overwrite all changes you have not saved.", "Confirm reload", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (messageBoxResult != MessageBoxResult.Yes) return;

            var defaultConfigFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Const.DefaultConfigPath,
                Const.DefaultConfigName
            );

            if (File.Exists(defaultConfigFilePath))
            {
                LoadConfig(defaultConfigFilePath);
            }
        }

        private void Btn_addConfiguration(object sender, RoutedEventArgs e)
        {
            if (NewConfigurationName.InputText == "" || NewConfigurationName.InputText == null)
            {
                MessageBox.Show("The name field can not be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var configurationName = NewConfigurationName.InputText;

            var config = new AvatarConfig();
            config.Name = configurationName;
            _currentConfig.Configurations.Add(config);

            ConfigurationList.Add(new ConfigurationListItem(config));
            _hasUnsavedChanges = true;

            SetInputValues(() =>
            {
                NewConfigurationName.InputText = "";
            });

            if (ConfigurationList.Count == 1)
            {
                ConfigurationDropdown.SelectedIndex = 0;
            }
        }

        private void Btn_deleteConfiguration(object sender, RoutedEventArgs e)
        {
            if (ConfigurationDropdown.SelectedItem == null) return;

            var messageBoxResult = MessageBox.Show("Are you sure you want to delete this configuration?", "Confirm deletion", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (messageBoxResult != MessageBoxResult.Yes) return;

            _currentConfig.Configurations.RemoveAt(ConfigurationDropdown.SelectedIndex);
            ConfigurationList.RemoveAt(ConfigurationDropdown.SelectedIndex);

            _hasUnsavedChanges = true;
        }

        private void Btn_renameConfiguration(object sender, RoutedEventArgs e)
        {
            if (RenameConfigurationName.InputText == "" || RenameConfigurationName.InputText == null)
            {
                MessageBox.Show("The name field can not be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _currentConfig.Configurations[ConfigurationDropdown.SelectedIndex].Name = RenameConfigurationName.InputText;
            _hasUnsavedChanges = true;

            UpdateConfigurationList();
        }

        private void Btn_deleteAvatarFromConfiguration(object sender, RoutedEventArgs e)
        {
            var deleteConfiguration = (ConfigurationAvatarListItem)ConfigurationAvatarsListBox.SelectedItem;

            if (deleteConfiguration != null)
            {
                var currentConfig = _currentConfig.Configurations[ConfigurationDropdown.SelectedIndex];

                foreach (var avatarConfig in currentConfig.Avatars)
                {
                    if (avatarConfig.Id == deleteConfiguration.Id)
                    {
                        currentConfig.Avatars.Remove(avatarConfig);
                        _hasUnsavedChanges = true;
                        break;
                    }
                }

                UpdateConfigurationList();
                UpdateConfigurationAvatarList();
            }
        }

        private void Btn_addAvatarToConfiguration(object sender, RoutedEventArgs e)
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

            var existingAvatar = FindAvatarConfig(avatarId);

            if (existingAvatar != null)
            {
                MessageBox.Show(
                    "This avatar already exists in configuration '" + existingAvatar.Name + "'. You need to delete it there first, or use the move functionality to move it over to this configuration.",
                    "Duplicate avatar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            var avatarConfig = new AvatarConfigAvatar();
            avatarConfig.Name = avatarName;
            avatarConfig.Id = avatarId;

            var currentConfig = _currentConfig.Configurations[ConfigurationDropdown.SelectedIndex];

            currentConfig.Avatars.Add(avatarConfig);
            _hasUnsavedChanges = true;

            UpdateConfigurationList();
            UpdateConfigurationAvatarList();

            SetInputValues(() =>
            {
                NewAvatarId.InputText = "";
                NewAvatarName.InputText = "";
            });
        }

        private void Btn_copyAvatarId(object sender, RoutedEventArgs e)
        {
            var selectedConfiguration = (ConfigurationAvatarListItem)ConfigurationAvatarsListBox.SelectedItem;

            if (selectedConfiguration != null)
            {
                Clipboard.SetText(selectedConfiguration.Id);
            }
        }

        private void Btn_moveAvatarToConfig(object sender, RoutedEventArgs e)
        {
            var selectedConfiguration = (ConfigurationAvatarListItem)ConfigurationAvatarsListBox.SelectedItem;
            var targetConfig = (e.OriginalSource as MenuItem)?.CommandParameter as AvatarConfig;

            if (targetConfig != null && selectedConfiguration != null)
            {
                var currentConfig = _currentConfig.Configurations[ConfigurationDropdown.SelectedIndex];

                foreach (var avatarConfig in currentConfig.Avatars)
                {
                    if (avatarConfig.Id == selectedConfiguration.Id)
                    {
                        targetConfig.Avatars.Add(avatarConfig);
                        currentConfig.Avatars.Remove(avatarConfig);
                        _hasUnsavedChanges = true;
                        break;
                    }
                }

                UpdateConfigurationList();
                UpdateConfigurationAvatarList();
            }
        }

        private void UpdateConfigurationList()
        {
            int selectedBefore = ConfigurationDropdown.SelectedIndex;

            ConfigurationList.Clear();
            foreach (var configItem in _currentConfig.Configurations)
            {
                ConfigurationList.Add(new ConfigurationListItem(configItem));
            }

            if (selectedBefore >= 0 && selectedBefore < ConfigurationList.Count)
            {
                ConfigurationDropdown.SelectedIndex = selectedBefore;
            }
        }

        private void UpdateConfigurationAvatarList()
        {
            ConfigurationAvatarList.Clear();

            if (ConfigurationDropdown.SelectedIndex != -1)
            {
                foreach (var avatarConfig in _currentConfig.Configurations[ConfigurationDropdown.SelectedIndex].Avatars)
                {
                    ConfigurationAvatarList.Add(new ConfigurationAvatarListItem(avatarConfig.Name, avatarConfig.Id));
                }
            }
        }

        private void ConfigurationDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConfigurationDropdown.SelectedItem == null)
            {
                ClearAndDisableAllInputs();
                return;
            }

            UpdateConfigurationAvatarList();

            // first, store all the current input values in the config object
            if (e.RemovedItems.Count > 0)
            {
                var previousListItem = e.RemovedItems[0] as ConfigurationListItem;

                if (previousListItem != null)
                {
                    CopyInputValuesToConfig(previousListItem.Config);
                }
            }

            // afterwards switch everything over to the new avatar
            var avatarConfig = _currentConfig.Configurations[ConfigurationDropdown.SelectedIndex];

            FillAndEnableAllInputs(avatarConfig);
        }

        private void ClearAndDisableAllInputs()
        {
            SetInputValues(() =>
            {
                ParamTrigger.InputText = "";                
                ParamPosX.InputText = "";                
                ParamPosY.InputText = "";                
                ParamPosZ.InputText = "";                
                ParamRotX.InputText = "";                
                ParamRotY.InputText = "";               
                ParamRotZ.InputText = "";                
                CalibrationScale.InputText = "";                
                CalibrationPosX.InputText = "";                
                CalibrationPosY.InputText = "";                
                CalibrationPosZ.InputText = "";                
                CalibrationRotX.InputText = "";
                CalibrationRotY.InputText = "";               
                CalibrationRotZ.InputText = "";
                
            });

            ControlAllCalibrationInputs(false);
            ControlAllParameterInputs(false);

            StartTrackingButton.IsEnabled = false;
            StartCalibrationButton.IsEnabled = false;

            AvatarsGroupBox.IsEnabled = false;
            RenameConfigurationGroupBox.IsEnabled = false;
            DeleteConfigurationButton.IsEnabled = false;
        }

        private void FillAllCalibrationInputs(AvatarCalibration calibration)
        {
            SetInputValues(() =>
            {
                CalibrationScale.InputText = calibration.Scale.ToString(CultureInfo.InvariantCulture);
                CalibrationPosX.InputText = calibration.TranslationX.ToString(CultureInfo.InvariantCulture);
                CalibrationPosY.InputText = calibration.TranslationY.ToString(CultureInfo.InvariantCulture);
                CalibrationPosZ.InputText = calibration.TranslationZ.ToString(CultureInfo.InvariantCulture);
                CalibrationRotX.InputText = calibration.RotationX.ToString(CultureInfo.InvariantCulture);
                CalibrationRotY.InputText = calibration.RotationY.ToString(CultureInfo.InvariantCulture);
                CalibrationRotZ.InputText = calibration.RotationZ.ToString(CultureInfo.InvariantCulture);
            });
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
            SetInputValues(() =>
            {
                ParamTrigger.InputText = parameters.Activate;
                ParamPosX.InputText = parameters.PositionX;
                ParamPosY.InputText = parameters.PositionY;
                ParamPosZ.InputText = parameters.PositionZ;
                ParamRotX.InputText = parameters.RotationX;
                ParamRotY.InputText = parameters.RotationY;
                ParamRotZ.InputText = parameters.RotationZ;
            });
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

            SetInputValues(() =>
            {
                RenameConfigurationName.InputText = config.Name;
            });

            StartTrackingButton.IsEnabled = true;
            StartCalibrationButton.IsEnabled = true;

            AvatarsGroupBox.IsEnabled = true;
            RenameConfigurationGroupBox.IsEnabled = true;
            DeleteConfigurationButton.IsEnabled = true;
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
            if (_openVRManager.IsAnyThreadRunning())
            {
                StartCalibrationButton.Content = "Start Calibration";
                StartTrackingButton.IsEnabled = true;
                ConfigurationDropdown.IsEnabled = true;
                TrackingTab.IsEnabled = true;
                ConfigurationsTab.IsEnabled = true;

                _openVRManager.StopThread();

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
                
                if (ConfigurationDropdown.SelectedIndex == -1)
                {
                    MessageBox.Show("No avatar selected. You need to select an avatar to calibrate the values for.", "Avatar missing", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string currentController = ((DeviceListItem)ControllerDropdown.SelectedItem).Serial;
                var currentConfiguration = _currentConfig.Configurations[ConfigurationDropdown.SelectedIndex];

                // load values from inputs
                CopyCalibrationValuesToConfig(currentConfiguration);

                // copy the current calibration to the calibration thread
                // updates are then sent by the calibration thread via events
                AvatarCalibration calibration = currentConfiguration.Calibration;

                StartCalibrationButton.Content = "Stop Calibration";
                StartTrackingButton.IsEnabled = false;
                ConfigurationDropdown.IsEnabled = false;
                TrackingTab.IsEnabled = false;
                ConfigurationsTab.IsEnabled = false;

                _openVRManager.StartCalibrationThread(currentController, calibration);
            }
        }

        private void StartTrackingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_processThread != null)
            {
                _processThread.Stop();
                _processThread = null;

                ShowTrackingStopped();
                _openVRManager.StopThread();
                _oscManager.Stop();
            }
            else
            {
                var currentConfiguration = _currentConfig.Configurations[ConfigurationDropdown.SelectedIndex];

                // load values from inputs
                CopyInputValuesToConfig(currentConfiguration);

                StartTracking();
            }
        }

        private void ShowTrackingStopped()
        {
            StartTrackingButton.Content = "Start Tracking";
            CurrentStatusLabel.Content = "inactive";
            StartCalibrationButton.IsEnabled = true;
            ConfigurationDropdown.IsEnabled = true;
            DeleteConfigurationButton.IsEnabled = true;
            CalibrationTab.IsEnabled = true;
            AvatarsTab.IsEnabled = true;
            ConfigurationsTab.IsEnabled = true;
            FileMenu.IsEnabled = true;
            ControlAllParameterInputs(true);
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

            if (IPEndPoint.TryParse(OSCInputAddress.InputText, out IPEndPoint? endpoint))
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

            bool trackingStarted = _openVRManager.StartTrackingThread(currentController, currentTracker);

            if (!trackingStarted) return;

            StartTrackingButton.Content = "Stop Tracking";
            StartCalibrationButton.IsEnabled = false;
            ConfigurationDropdown.IsEnabled = false;
            DeleteConfigurationButton.IsEnabled = false;
            CalibrationTab.IsEnabled = false;
            AvatarsTab.IsEnabled = false;
            ConfigurationsTab.IsEnabled = false;
            FileMenu.IsEnabled = false;
            ControlAllParameterInputs(false);

            _processThread = new ProcessThread(_oscManager, _openVRManager, _currentConfig.Configurations);
            _processThread.AvatarConfigChanged += OnAvatarConfigChanged;
            _processThread.Start();

            _oscManager.Start(inputAddress, inputPort, outputAddress, outputPort, _currentConfig.Configurations);
        }

        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!_openVRManager.IsCalibrationThreadRunning()) return;

            if (e.Key == System.Windows.Input.Key.Up ||
                e.Key == System.Windows.Input.Key.Down ||
                e.Key == System.Windows.Input.Key.Left ||
                e.Key == System.Windows.Input.Key.Right)
            {
                _openVRManager.InjectKeyPress(e.Key);
            }

            e.Handled = true;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have some unsaved changes. Are you sure you want to exit?",
                    "Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }
        }

        private void TrackerDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender == null) return;

            ComboBox? senderComboBox = sender as ComboBox;

            if (senderComboBox == null) return;

            object selectedValue = senderComboBox.SelectedValue;

            if (selectedValue == null) return;

            _currentConfig.TrackerSerial = (string)selectedValue;
        }

        private void ControllerDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender == null) return;

            ComboBox? senderComboBox = sender as ComboBox;

            if (senderComboBox == null) return;

            object selectedValue = senderComboBox.SelectedValue;

            if (selectedValue == null) return;

            _currentConfig.ControllerSerial = (string)selectedValue;
        }

        private void UpdateControllersAndTrackers()
        {
            _openVRManager.UpdateControllers();

            ControllerList.Clear();

            List<string> controllers = (_currentConfig.AllowAllDevices) ? _openVRManager.GetAllDevices() : _openVRManager.GetControllers();

            foreach (string controllerId in controllers)
            {
                ControllerList.Add(new DeviceListItem(controllerId));
            }

            TrackerList.Clear();

            List<string> trackers = (_currentConfig.AllowAllDevices) ? _openVRManager.GetAllDevices() : _openVRManager.GetTrackers();
            foreach (string trackerId in trackers)
            {
                TrackerList.Add(new DeviceListItem(trackerId));
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

        private double FixZero(double input)
        {
            return (input < 0.00001 && input > -0.00001) ? 0 : input;
        }

        private void LabeledInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_suppressInputEvents)
            {
                _hasUnsavedChanges = true;
            }
        }

        private void SetInputValues(Action fn)
        {
            _suppressInputEvents = true;

            try
            {
                fn();
            }
            finally
            {
                _suppressInputEvents = false;
            }
        }
    }
}