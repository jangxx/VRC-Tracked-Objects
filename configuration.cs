using MathNet.Numerics.LinearAlgebra;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace VRC_OSC_ExternallyTrackedObject
{
    public class AvatarParams
    {
        public string Activate { get; set; } = "/avatar/parameters/OSCTrackingEnabled";
        public string PositionX { get; set; } = "/avatar/parameters/OscTrackedPosX";
        public string PositionY { get; set; } = "/avatar/parameters/OscTrackedPosY";
        public string PositionZ { get; set; } = "/avatar/parameters/OscTrackedPosZ";
        public string RotationX { get; set; } = "/avatar/parameters/OscTrackedRotX";
        public string RotationY { get; set; } = "/avatar/parameters/OscTrackedRotY";
        public string RotationZ { get; set; } = "/avatar/parameters/OscTrackedRotZ";
    }

    public class AvatarCalibration
    {
        public double Scale { get; set; } = 1;
        public double TranslationX { get; set; } = 0;
        public double TranslationY { get; set; } = 0;
        public double TranslationZ { get; set; } = 0;
        public double RotationX { get; set; } = 0;
        public double RotationY { get; set; } = 0;
        public double RotationZ { get; set; } = 0;

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

    internal class AvatarConfigV0
    {
        public string Name { get; set; } = "";
        public AvatarParams Parameters { get; set; } = new AvatarParams();
        public AvatarCalibration Calibration { get; set; } = new AvatarCalibration();
    }

    internal class ConfigurationV0
    {
        public bool Autostart { get; set; } = false;
        public bool AllowAllDevices { get; set; } = false;
        public string OscInputAddress { get; set; } = "127.0.0.1:9001";
        public string OscOutputAddress { get; set; } = "127.0.0.1:9000";
        public string? ControllerSerial { get; set; }
        public string? TrackerSerial { get; set; }
        public Dictionary<string, AvatarConfigV0> Avatars { get; set; } = new Dictionary<string, AvatarConfigV0>();
    }

    public class AvatarConfigAvatar
    {
        public string Name { get; set; } = "";
        public string Id { get; set; } = "";
    }

    public class AvatarConfig
    {
        public string Name { get; set; } = "";
        public AvatarParams Parameters { get; set; } = new AvatarParams();
        public AvatarCalibration Calibration { get; set; } = new AvatarCalibration();
        public List<AvatarConfigAvatar> Avatars { get; set; } = new List<AvatarConfigAvatar>();
    }

    internal class ConfigurationVersion
    {
        public int Version { get; set; } = 0;
    }

    internal class Configuration : ConfigurationVersion
    {
        public bool Autostart { get; set; } = false;
        public bool AllowAllDevices { get; set; } = false;
        public string OscInputAddress { get; set; } = "127.0.0.1:9001";
        public string OscOutputAddress { get; set; } = "127.0.0.1:9000";
        public string? ControllerSerial { get; set; }
        public string? TrackerSerial { get; set; }
        public List<AvatarConfig> Configurations { get; set; } = new List<AvatarConfig>();

        public Configuration()
        {
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