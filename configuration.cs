using MathNet.Numerics.LinearAlgebra;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VRC_OSC_ExternallyTrackedObject
{
    public class AvatarParams : ObservableObject
    {
        private string _activate = "/avatar/parameters/OSCTrackingEnabled";
        public string Activate {
            get { return _activate; }
            set { _activate = value; RaisePropertyChanged(nameof(Activate)); }
        }

        private string _positionX = "/avatar/parameters/OscTrackedPosX";
        public string PositionX
        {
            get { return _positionX; }
            set { _positionX = value; RaisePropertyChanged(nameof(PositionX)); }
        }

        private string _positionY = "/avatar/parameters/OscTrackedPosY";
        public string PositionY
        {
            get { return _positionY; }
            set { _positionY = value; RaisePropertyChanged(nameof(PositionY)); }
        }

        private string _positionZ = "/avatar/parameters/OscTrackedPosZ";
        public string PositionZ
        {
            get { return _positionZ; }
            set { _positionZ = value; RaisePropertyChanged(nameof(PositionZ)); }
        }

        private string _rotationX = "/avatar/parameters/OscTrackedRotX";
        public string RotationX
        {
            get { return _rotationX; }
            set { _rotationX = value; RaisePropertyChanged(nameof(RotationX)); }
        }

        private string _rotationY = "/avatar/parameters/OscTrackedRotY";
        public string RotationY
        {
            get { return _rotationY; }
            set { _rotationY = value; RaisePropertyChanged(nameof(RotationY)); }
        }

        private string _rotationZ = "/avatar/parameters/OscTrackedRotZ";
        public string RotationZ
        {
            get { return _rotationZ; }
            set { _rotationZ = value; RaisePropertyChanged(nameof(RotationZ)); }
        }
    }

    public class AvatarCalibration : ObservableObject
    {
        private double _scale = 1;
        public double Scale
        {
            get => _scale;
            set { _scale = value; RaisePropertyChanged(nameof(Scale)); }
        }

        private double _translationX = 0;
        public double TranslationX
        {
            get => _translationX;
            set { _translationX = value; RaisePropertyChanged(nameof(TranslationX)); }
        }

        private double _translationY = 0;
        public double TranslationY
        {
            get => _translationY;
            set { _translationY = value; RaisePropertyChanged(nameof(TranslationY)); }
        }

        private double _translationZ = 0;
        public double TranslationZ
        {
            get => _translationZ;
            set { _translationZ = value; RaisePropertyChanged(nameof(TranslationZ)); }
        }

        private double _rotationX = 0;
        public double RotationX
        {
            get => _rotationX;
            set { _rotationX = value; RaisePropertyChanged(nameof(RotationX)); }
        }

        private double _rotationY = 0;
        public double RotationY
        {
            get => _rotationY;
            set { _rotationY = value; RaisePropertyChanged(nameof(RotationY)); }
        }

        private double _rotationZ = 0;
        public double RotationZ
        {
            get => _rotationZ;
            set { _rotationZ = value; RaisePropertyChanged(nameof(RotationZ)); }
        }

        public void CopyFrom(AvatarCalibration other)
        {
            Scale = other.Scale;
            TranslationX = other.TranslationX;
            TranslationY = other.TranslationY;
            TranslationZ = other.TranslationZ;
            RotationX = other.RotationX;
            RotationY = other.RotationY;
            RotationZ = other.RotationZ;
        }

        public static AvatarCalibration FromMatrix(Matrix<double> mat44)
        {
            var rotation = MathUtils.extractRotationsFromMatrix44(mat44);
            var translation = MathUtils.extractTranslationFromMatrix44(mat44);
            var scaleVec = MathUtils.extractScaleFromMatrix44(mat44);

            var calibration = new AvatarCalibration();
            calibration.Scale = scaleVec.AbsoluteMinimum(); // the scale should be almost exactly the same for all dimensions so let's just pick the smallest one
            calibration.TranslationX = translation[0];
            calibration.TranslationY = translation[1];
            calibration.TranslationZ = translation[2];
            calibration.RotationX = rotation[0];
            calibration.RotationY = rotation[1];
            calibration.RotationZ = rotation[2];

            //Debug.WriteLine("From matrix:\n" + mat44.ToString());
            //Debug.WriteLine("To calibration: " + calibration.ToString());

            return calibration;
        }

        override public string ToString()
        {
            return $"AvatarCalibration(\n\tscale={Scale},\n\ttranslation=({TranslationX}, {TranslationY}, {TranslationZ})\n\trotation=({RotationX}, {RotationY}, {RotationZ})\n)";
        }
    }

    public class AvatarConfigV0
    {
        public string Name { get; set; } = "";
        public AvatarParams Parameters { get; set; } = new AvatarParams();
        public AvatarCalibration Calibration { get; set; } = new AvatarCalibration();
    }

    public class ConfigurationV0
    {
        public bool Autostart { get; set; } = false;
        public bool AllowAllDevices { get; set; } = false;
        public string OscInputAddress { get; set; } = "127.0.0.1:9001";
        public string OscOutputAddress { get; set; } = "127.0.0.1:9000";
        public string? ControllerSerial { get; set; }
        public string? TrackerSerial { get; set; }
        public Dictionary<string, AvatarConfigV0> Avatars { get; set; } = new Dictionary<string, AvatarConfigV0>();
    }

    public class AvatarConfigAvatar : ObservableObject
    {
        private string _name = "";
        public string Name {
            get { return _name; }
            set { _name = value; RaisePropertyChanged(nameof(Name)); RaisePropertyChanged(nameof(DisplayName)); }
        }

        private string _id = "";
        public string Id {
            get { return _id; }
            set { _id = value; RaisePropertyChanged(nameof(Id)); RaisePropertyChanged(nameof(DisplayName)); }
        }

        [JsonIgnore]
        public string DisplayName
        {
            get { return Name + " (" + Id + ")"; }
        }
    }

    public class AvatarConfig : ObservableObject
    {
        private string _name = "";
        public string Name {
            get { return _name; }
            set { _name = value; RaisePropertyChanged(nameof(Name)); RaisePropertyChanged(nameof(DisplayName)); }
        }

        public AvatarParams Parameters { get; set; } = new AvatarParams();
        public AvatarCalibration Calibration { get; set; } = new AvatarCalibration();
        public ObservableCollection<AvatarConfigAvatar> Avatars { get; set; } = new ObservableCollection<AvatarConfigAvatar>();

        public AvatarConfig()
        {
            RegisterObservableCollection("Avatars", this.Avatars);
        }

        [JsonIgnore]
        public string DisplayName
        {
            get { return Name + " (" + Avatars.Count + " avatars)"; }
        }
    }

    public class ConfigurationVersion : ObservableObject
    {
        public int Version { get; set; } = 0;
    }

    public class Configuration : ConfigurationVersion
    {
        private bool _autostart = false;
        public bool Autostart
        {
            get { return _autostart; }
            set { _autostart = value; RaisePropertyChanged(nameof(Autostart)); }
        }

        private bool _allowAllDevices = false;
        public bool AllowAllDevices {
            get { return _allowAllDevices; }
            set { _allowAllDevices = value; RaisePropertyChanged(nameof(AllowAllDevices)); }
        }

        private string _oscInputAddress = "127.0.0.1:9001";
        public string OscInputAddress {
            get { return _oscInputAddress; }
            set { _oscInputAddress = value; RaisePropertyChanged(nameof(OscInputAddress)); }
        }

        private string _oscOutputAddress = "127.0.0.1:9000";
        public string OscOutputAddress {
            get { return _oscOutputAddress;  }
            set { _oscOutputAddress = value; RaisePropertyChanged(nameof(OscOutputAddress)); }
        }

        private bool _useOscQuery = false;
        public bool UseOscQuery
        {
            get { return _useOscQuery; }
            set { _useOscQuery = value; RaisePropertyChanged(nameof(UseOscQuery)); }
        }

        private string? _controllerSerial;
        public string? ControllerSerial {
            get { return _controllerSerial; }
            set { _controllerSerial = value; RaisePropertyChanged(nameof(ControllerSerial)); }
        }

        private string? _trackerSerial;
        public string? TrackerSerial {
            get {  return _trackerSerial; }
            set { _trackerSerial = value; RaisePropertyChanged(nameof(TrackerSerial)); }
        }

        public ObservableCollection<AvatarConfig> Configurations { get; set; } = new ObservableCollection<AvatarConfig>();

        public Configuration()
        {
            RegisterObservableCollection("Configurations", this.Configurations);

            Version = 1;
        }

        public static Configuration? FromPreviousVersion(ConfigurationV0? previous)
        {
            if (previous == null) return null;

            var config = new Configuration();
            config.Autostart = previous.Autostart;
            config.AllowAllDevices = previous.AllowAllDevices;
            config.OscInputAddress = previous.OscInputAddress;
            config.OscOutputAddress = previous.OscOutputAddress;
            config.ControllerSerial = previous.ControllerSerial;
            config.TrackerSerial = previous.TrackerSerial;

            foreach (var avatar in previous.Avatars)
            {
                var newAvatarConfig = new AvatarConfig();
                newAvatarConfig.Name = avatar.Value.Name;
                newAvatarConfig.Parameters = avatar.Value.Parameters;
                newAvatarConfig.Calibration = avatar.Value.Calibration;

                newAvatarConfig.Avatars.Add(new AvatarConfigAvatar
                {
                    Id = avatar.Key,
                    Name = avatar.Value.Name,
                });

                config.Configurations.Add(newAvatarConfig);
            }

            return config;
        }

        public void CopyFrom(Configuration other)
        {
            Autostart = other.Autostart;
            AllowAllDevices = other.AllowAllDevices;
            OscInputAddress = other.OscInputAddress;
            OscOutputAddress = other.OscOutputAddress;
            UseOscQuery = other.UseOscQuery;
            ControllerSerial = other.ControllerSerial;
            TrackerSerial = other.TrackerSerial;

            Configurations.Clear();

            foreach (var avatarConfig in other.Configurations)
            {
                Configurations.Add(avatarConfig);
            }
        }
    }

    internal class ConfigLoader
    {
        public static Configuration? LoadConfig(string path)
        {
            string jsonString = File.ReadAllText(path);

            if (jsonString == null) return null;

            var configVersion = JsonSerializer.Deserialize<ConfigurationVersion>(jsonString);

            switch(configVersion?.Version)
            {
                case 0:
                    {
                        var config = JsonSerializer.Deserialize<ConfigurationV0>(jsonString);
                        return Configuration.FromPreviousVersion(config);
                    }
                case 1:
                    return JsonSerializer.Deserialize<Configuration>(jsonString);
                default:
                    return null;
            }
        }
    }
}