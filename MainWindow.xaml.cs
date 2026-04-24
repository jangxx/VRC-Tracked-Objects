using MathNet.Numerics.LinearAlgebra;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace VRC_OSC_ExternallyTrackedObject
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Configuration _currentConfig = new Configuration();
        public Configuration CurrentConfig {
            get { return _currentConfig; }
        }

        private MainWindowData _windowData = new MainWindowData();
        public MainWindowData WindowData { get { return _windowData; } }

        private OpenVRManager _openVRManager = new OpenVRManager();
        private OscManager _oscManager = new OscManager();
        private ProcessThread? _processThread;

        private bool _hasUnsavedChanges = false;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            MainTabs.SelectedItem = ConfigurationsTab;

            _openVRManager.CalibrationUpdate += OnCalibrationUpdate;

            _oscManager.TrackingActiveChanged += OnTrackingActiveChanged;
            _oscManager.ThreadCrashed += OnOscThreadCrashed;

            this.WindowData.PropertyChanged += HandleWindowDataChange;
        }

        private void HandleWindowDataChange(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowData.SelectedController) && this.WindowData.SelectedController != null)
            {
                this.CurrentConfig.ControllerSerial = this.WindowData.SelectedController.Serial;
                this._hasUnsavedChanges = true;
            }
            if (e.PropertyName == nameof(MainWindowData.SelectedTracker) && this.WindowData.SelectedTracker != null)
            {
                this.CurrentConfig.TrackerSerial = this.WindowData.SelectedTracker.Serial;
                this._hasUnsavedChanges = true;
            }
        }

        public void Init()
        {
            this.WindowData.OpenVRConnected = _openVRManager.InitOverlay();

            if (this.WindowData.OpenVRConnected)
            {
                UpdateControllersAndTrackers();
            }
        }

        public void ProcessStartupConfig()
        {
            if (_currentConfig.Configurations.Count > 0)
            {
                MainTabs.SelectedItem = TrackingTab;
            }

            if (_currentConfig.Autostart && this.WindowData.OpenVRConnected)
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

        // this will be called within the calibration thread
        private void OnCalibrationUpdate(object? sender, EventArgs args)
        {
            var calibrationUpdateArgs = (CalibrationUpdateArgs)args;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (calibrationUpdateArgs.Type)
                {
                    case CalibrationUpdateArgs.CalibrationUpdateType.ACTIVE_FIELD:
                        this.WindowData.HighlightedField = calibrationUpdateArgs.Field;
                        break;
                    case CalibrationUpdateArgs.CalibrationUpdateType.CALIBRATION_VALUE:
                        if (this.WindowData.CurrentAvatarConfig != null && calibrationUpdateArgs.CalibrationValues != null)
                        {
                            this.WindowData.CurrentAvatarConfig.Calibration.CopyFrom(calibrationUpdateArgs.CalibrationValues);
                            _hasUnsavedChanges = true;
                        }
                        break;
                }
            });
        }

        // this is called from the OSC thread
        private void OnTrackingActiveChanged(object? sender, EventArgs args)
        {
            var eventArgs = (TrackingActiveChangedArgs)args;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (eventArgs.Active)
                {
                    this.WindowData.CurrentStatusText = "active";
                }
                else if (eventArgs.AvatarKnown)
                {
                    this.WindowData.CurrentStatusText = "inactive (disabled)";
                }
                else
                {
                    this.WindowData.CurrentStatusText = "inactive (unknown avatar)";
                }
            });
        }

        // this is called from the process thread
        private void OnAvatarConfigChanged(object? sender, EventArgs args)
        {
            var avatarConfigChangeArgs = (AvatarConfigChangedArgs)args;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                this.WindowData.CurrentAvatarConfig = avatarConfigChangeArgs.AvatarConfig;
            });
        }

        private void OnOscThreadCrashed(object? sender, EventArgs args)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                ShowTrackingStopped();
                _openVRManager.StopThread();
                _oscManager.Stop();
            });
        }

        private void Btn_saveDefaultConfig(object sender, RoutedEventArgs e)
        {
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

            _currentConfig.CopyFrom(config);

            if (_currentConfig.ControllerSerial != null && _currentConfig.ControllerSerial.Length > 0) {
                bool found = false;

                for (int i = 0; i < WindowData.ControllerList.Count; i++)
                {
                    if (WindowData.ControllerList[i].Serial == _currentConfig.ControllerSerial)
                    {
                        this.WindowData.SelectedController = WindowData.ControllerList[i];
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    var notFoundItem = new DeviceListItem(_currentConfig.ControllerSerial, false);
                    WindowData.ControllerList.Add(notFoundItem);
                    this.WindowData.SelectedController = notFoundItem;
                }
            }

            if (_currentConfig.TrackerSerial != null && _currentConfig.TrackerSerial.Length > 0)
            {
                bool found = false;

                for (int i = 0; i < WindowData.TrackerList.Count; i++)
                {
                    if (WindowData.TrackerList[i].Serial == _currentConfig.TrackerSerial)
                    {
                        this.WindowData.SelectedTracker = WindowData.TrackerList[i];
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    var notFoundItem = new DeviceListItem(_currentConfig.TrackerSerial, false);
                    WindowData.TrackerList.Add(notFoundItem);
                    this.WindowData.SelectedTracker = notFoundItem;
                }
            }

            if (this.CurrentConfig.Configurations.Count > 0)
            {
                this.WindowData.CurrentAvatarConfig = this.CurrentConfig.Configurations[0];
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
            if (this.WindowData.NewConfigurationNameInput.Length == 0)
            {
                MessageBox.Show("The name field can not be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var config = new AvatarConfig();
            config.Name = this.WindowData.NewConfigurationNameInput;

            this.CurrentConfig.Configurations.Add(config);

            if (this.CurrentConfig.Configurations.Count == 1)
            {
                this.WindowData.CurrentAvatarConfig = this.CurrentConfig.Configurations[0];
            }

            this.WindowData.NewConfigurationNameInput = "";

            _hasUnsavedChanges = true;
        }

        private void Btn_deleteConfiguration(object sender, RoutedEventArgs e)
        {
            if (this.WindowData.CurrentAvatarConfig == null) return;

            var messageBoxResult = MessageBox.Show("Are you sure you want to delete this configuration?", "Confirm deletion", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (messageBoxResult != MessageBoxResult.Yes) return;

            this.CurrentConfig.Configurations.Remove(this.WindowData.CurrentAvatarConfig);

            if (this.CurrentConfig.Configurations.Count > 0)
            {
                this.WindowData.CurrentAvatarConfig = this.CurrentConfig.Configurations[0];
            } else
            {
                this.WindowData.CurrentAvatarConfig = null;
            }

            _hasUnsavedChanges = true;
        }

        private void Btn_renameConfiguration(object sender, RoutedEventArgs e)
        {
            if (this.WindowData.RenameConfigurationNameInput.Length == 0)
            {
                MessageBox.Show("The name field can not be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (this.WindowData.CurrentAvatarConfig == null) return;

            this.WindowData.CurrentAvatarConfig.Name = this.WindowData.RenameConfigurationNameInput;
            this.WindowData.RenameConfigurationNameInput = "";

            _hasUnsavedChanges = true;
        }

        private void Btn_addAvatarToConfiguration(object sender, RoutedEventArgs e)
        {
            if (this.WindowData.NewAvatarIdInput == "")
            {
                MessageBox.Show("The id field can not be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (this.WindowData.NewAvatarNameInput == "")
            {
                MessageBox.Show("The name field can not be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var avatarId = this.WindowData.NewAvatarIdInput;
            var avatarName = this.WindowData.NewAvatarNameInput;

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

            this.WindowData.CurrentAvatarConfig?.Avatars.Add(avatarConfig);
            _hasUnsavedChanges = true;

            this.WindowData.NewAvatarIdInput = "";
            this.WindowData.NewAvatarNameInput = "";
        }

        private void Btn_copyAvatarId(object sender, RoutedEventArgs e)
        {
            if (ConfigurationAvatarsListBox.SelectedItem is AvatarConfigAvatar selected)
            {
                Clipboard.SetText(selected.Id);
            }
        }

        private void Btn_moveAvatarToConfig(object sender, RoutedEventArgs e)
        {
            var targetConfig = (e.OriginalSource as MenuItem)?.CommandParameter as AvatarConfig;

            if (ConfigurationAvatarsListBox.SelectedItem is AvatarConfigAvatar selected && targetConfig != null)
            {
                if (this.WindowData.CurrentAvatarConfig != null && this.WindowData.CurrentAvatarConfig.Avatars.Contains(selected))
                {
                    targetConfig.Avatars.Add(selected);
                    this.WindowData.CurrentAvatarConfig.Avatars.Remove(selected);
                    _hasUnsavedChanges = true;
                }
            }
        }

        private void Btn_deleteAvatarFromConfiguration(object sender, RoutedEventArgs e)
        {
            if (ConfigurationAvatarsListBox.SelectedItem is AvatarConfigAvatar selected)
            {
                if (this.WindowData.CurrentAvatarConfig != null && this.WindowData.CurrentAvatarConfig.Avatars.Contains(selected))
                {
                    this.WindowData.CurrentAvatarConfig.Avatars.Remove(selected);
                    _hasUnsavedChanges = true;
                }
            }
        }
       
        private void StartCalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            if (_openVRManager.IsAnyThreadRunning())
            {
                this.WindowData.InputsLocked = false;
                this.WindowData.StartCalibrationButtonText = "Start Calibration";
                this.WindowData.HighlightedField = null;

                _openVRManager.StopThread();
            } 
            else
            {
                if (this.WindowData.SelectedController == null)
                {
                    MessageBox.Show("No controller selected", "Controller missing", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                if (this.WindowData.CurrentAvatarConfig == null)
                {
                    MessageBox.Show("No avatar selected. You need to select an avatar to calibrate the values for.", "Avatar missing", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string currentController = this.WindowData.SelectedController.Serial;

                this.WindowData.StartCalibrationButtonText = "Stop Calibration";
                this.WindowData.InputsLocked = false;

                _openVRManager.StartCalibrationThread(currentController, this.WindowData.CurrentAvatarConfig.Calibration);
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
                StartTracking();
            }
        }

        private void ShowTrackingStopped()
        {
            this.WindowData.StartTrackingButtonText = "Start Tracking";
            this.WindowData.CurrentStatusText = "inactive";
            this.WindowData.InputsLocked = false;
        }

        private void StartTracking()
        {
            if (this.WindowData.SelectedController == null)
            {
                MessageBox.Show("No controller selected", "Controller missing", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (this.WindowData.SelectedTracker == null)
            {
                MessageBox.Show("No tracker selected", "Tracker missing", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (this.CurrentConfig.OscInputAddress.Length == 0)
            {
                MessageBox.Show("No input address provided", "Input address missing", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (this.CurrentConfig.OscOutputAddress.Length == 0)
            {
                MessageBox.Show("No output address provided", "Output address missing", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool useOscQuery = this.CurrentConfig.UseOscQuery;
            string? inputAddress = null;
            int? inputPort = null;
            string? outputAddress = null;
            int? outputPort = null;

            if (!useOscQuery)
            {
                if (IPEndPoint.TryParse(this.CurrentConfig.OscInputAddress, out IPEndPoint? endpoint))
                {
                    inputAddress = endpoint.Address.ToString();
                    inputPort = endpoint.Port;
                }
                else
                {
                    MessageBox.Show("Could not parse input address. Make sure it has the format <address>:<port>", "Invalid input address", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (IPEndPoint.TryParse(this.CurrentConfig.OscOutputAddress, out endpoint))
                {
                    outputAddress = endpoint.Address.ToString();
                    outputPort = endpoint.Port;
                }
                else
                {
                    MessageBox.Show("Could not parse output address. Make sure it has the format <address>:<port>", "Invalid input address", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            bool trackingStarted = _openVRManager.StartTrackingThread(
                this.WindowData.SelectedController.Serial,
                this.WindowData.SelectedTracker.Serial
            );

            if (!trackingStarted) return;

            this.WindowData.StartTrackingButtonText = "Stop Tracking";
            this.WindowData.InputsLocked = true;

            _processThread = new ProcessThread(_oscManager, _openVRManager, _currentConfig.Configurations.ToList());
            _processThread.AvatarConfigChanged += OnAvatarConfigChanged;
            _processThread.Start();

            if (!useOscQuery)
            {
                _oscManager.StartWithAddresses(
                    inputAddress!,
                    inputPort!.Value,
                    outputAddress!,
                    outputPort!.Value,
                    _currentConfig.Configurations.ToList()
                );
            }
            else
            {
                _oscManager.StartWithOscQuery(_currentConfig.Configurations.ToList());
            }
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

            if (!e.Cancel)
            {
                this._oscManager.Stop();
            }
        }

        private void UpdateControllersAndTrackers()
        {
            _openVRManager.UpdateControllers();

            this.WindowData.ControllerList.Clear();

            List<string> controllers = (_currentConfig.AllowAllDevices) ? _openVRManager.GetAllDevices() : _openVRManager.GetControllers();

            foreach (string controllerId in controllers)
            {
                this.WindowData.ControllerList.Add(new DeviceListItem(controllerId));
            }

            this.WindowData.TrackerList.Clear();

            List<string> trackers = (_currentConfig.AllowAllDevices) ? _openVRManager.GetAllDevices() : _openVRManager.GetTrackers();
            foreach (string trackerId in trackers)
            {
                this.WindowData.TrackerList.Add(new DeviceListItem(trackerId));
            }
        }

        private void DevicesRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (!this.WindowData.OpenVRConnected)
            {
                return;
            }

            var currentControllerSerial = this.WindowData.SelectedController?.Serial;
            var currentTrackerSerial = this.WindowData.SelectedTracker?.Serial;

            UpdateControllersAndTrackers();

            if (this.WindowData.ControllerList.Count > 0)
            {
                this.WindowData.SelectedController = this.WindowData.ControllerList[0];

                if (currentControllerSerial != null)
                {
                    foreach (var controller in this.WindowData.ControllerList)
                    {
                        if (controller.Serial == currentControllerSerial)
                        {
                            this.WindowData.SelectedController = controller;
                            break;
                        }
                    }
                }
            }
            if (this.WindowData.TrackerList.Count > 0)
            {
                this.WindowData.SelectedTracker = this.WindowData.TrackerList[0];

                if (currentTrackerSerial != null)
                {
                    foreach (var tracker in this.WindowData.TrackerList)
                    {
                        if (tracker.Serial == currentTrackerSerial)
                        {
                            this.WindowData.SelectedTracker = tracker;
                            break;
                        }
                    }
                }
            }
        }
    }
}