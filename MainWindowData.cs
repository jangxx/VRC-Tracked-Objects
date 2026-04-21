using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRC_OSC_ExternallyTrackedObject
{
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

    public class MainWindowData : ObservableObject
    {
        private bool _openVRConnected = false;
        public bool OpenVRConnected
        {
            get { return _openVRConnected; }
            set { _openVRConnected = value; RaisePropertyChanged(nameof(OpenVRConnected)); }
        }

        private bool _inputsLocked = false;
        public bool InputsLocked
        {
            get { return _inputsLocked; }
            set { _inputsLocked = value; RaisePropertyChanged(nameof(InputsLocked)); }
        }

        private string _newAvatarIdInput = "";
        public string NewAvatarIdInput
        {
            get { return _newAvatarIdInput; }
            set { _newAvatarIdInput = value; RaisePropertyChanged(nameof(NewAvatarIdInput)); }
        }

        private string _newAvatarNameInput = "";
        public string NewAvatarNameInput
        {
            get { return _newAvatarNameInput; }
            set { _newAvatarNameInput = value; RaisePropertyChanged(nameof(NewAvatarNameInput)); }
        }

        private string _newConfigurationNameInput = "";
        public string NewConfigurationNameInput
        {
            get { return _newConfigurationNameInput; }
            set { _newConfigurationNameInput = value; RaisePropertyChanged(nameof(NewConfigurationNameInput)); }
        }

        private string _renameConfigurationNameInput = "";
        public string RenameConfigurationNameInput
        {
            get { return _renameConfigurationNameInput; }
            set { _renameConfigurationNameInput = value; RaisePropertyChanged(nameof(RenameConfigurationNameInput)); }
        }

        private AvatarConfig? _currentAvatarConfig;
        public AvatarConfig? CurrentAvatarConfig
        {
            get { return _currentAvatarConfig; }
            set { _currentAvatarConfig = value; RaisePropertyChanged(nameof(CurrentAvatarConfig)); }
        }

        public ObservableCollection<DeviceListItem> ControllerList { get; } = new ObservableCollection<DeviceListItem>();
        public ObservableCollection<DeviceListItem> TrackerList { get; } = new ObservableCollection<DeviceListItem>();

        private DeviceListItem? _selectedController;
        public DeviceListItem? SelectedController
        {
            get { return _selectedController; }
            set { _selectedController = value; RaisePropertyChanged(nameof(SelectedController)); }
        }

        private DeviceListItem? _selectedTracker;
        public DeviceListItem? SelectedTracker
        {
            get { return _selectedTracker; }
            set { _selectedTracker = value; RaisePropertyChanged(nameof(SelectedTracker)); }
        }

        public MainWindowData()
        {
            RegisterObservableCollection(nameof(ControllerList), ControllerList);
            RegisterObservableCollection(nameof(TrackerList), TrackerList);
        }
    }
}
