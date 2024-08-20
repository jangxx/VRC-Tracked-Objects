using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace VRC_OSC_ExternallyTrackedObject
{
    internal class ProcessQueueData
    {
        public ProcessQueueData(Matrix<double> controller, Matrix<double> tracker)
        {
            Controller = controller;
            Tracker = tracker;
        }

        public Matrix<double> Controller { get; }
        public Matrix<double> Tracker { get; }

    }

    public class AvatarConfigChangedArgs : EventArgs
    {
        public AvatarConfigChangedArgs(AvatarConfig avatarConfig)
        {
            AvatarConfig = avatarConfig;
        }

        public AvatarConfig AvatarConfig { get; }
    }

    internal class ProcessThread
    {
        private CancellationTokenSource _cancelTokenSource = new CancellationTokenSource();
        private Thread _thread;
        private OscManager _oscManager;
        private OpenVRManager _openVRManager;
        private object _lock = new();
        private BlockingCollection<ProcessQueueData> _stack = new BlockingCollection<ProcessQueueData>(new ConcurrentStack<ProcessQueueData>());

        private Matrix<double>? _currentInverseCalibrationMatrix = null;
        private Matrix<double>? _currentInverseCalibrationMatrixNoScale = null;
        private List<AvatarConfig> _configurations;

        public EventHandler? AvatarConfigChanged;

        public ProcessThread(OscManager oscManager, OpenVRManager openVRManager, List<AvatarConfig> configurations)
        {
            _oscManager = oscManager;
            _openVRManager = openVRManager;
            _configurations = configurations;

            _thread = new Thread(() => Main());
            _thread.Name = "ProcessThread";
            _thread.IsBackground = true;
        }

        public void Start()
        {
            _openVRManager.TrackingData += OnTrackingData;
            _oscManager.AvatarChanged += OnAvatarChanged;

            _thread.Start();
        }

        public void Stop()
        {
            _openVRManager.TrackingData -= OnTrackingData;
            _oscManager.AvatarChanged -= OnAvatarChanged;

            _cancelTokenSource.Cancel();
            _thread.Join();
        }

        private void Main()
        {
            ProcessQueueData? data;

            try
            {
                while (_stack.TryTake(out data, -1, _cancelTokenSource.Token))
                {
                    if (data == null)
                    {
                        continue;
                    }

                    Matrix<double> controllerToTracker, controllerToTrackerNS;

                    lock (_lock)
                    {
                        var controllerInverse = data.Controller.Inverse();
                        var controllerToTrackerPre = controllerInverse * data.Tracker;

                        controllerToTracker = _currentInverseCalibrationMatrix * controllerToTrackerPre;
                        controllerToTrackerNS = _currentInverseCalibrationMatrixNoScale * controllerToTrackerPre;
                    }

                    var relativeTranslate = MathUtils.extractTranslationFromMatrix44(controllerToTracker);

                    var relativeRotation = MathUtils.extractRotationsFromMatrixZYX(controllerToTrackerNS.Inverse().SubMatrix(0, 3, 0, 3));

                    if (Math.Abs(relativeTranslate[0]) >= Const.MaxRelativeDistance
                        || Math.Abs(relativeTranslate[1]) >= Const.MaxRelativeDistance
                        || Math.Abs(relativeTranslate[2]) >= Const.MaxRelativeDistance)
                    {
                        relativeTranslate[0] = 1;
                        relativeTranslate[1] = 1;
                        relativeTranslate[2] = 1;
                    }

                    double rotationX = -relativeRotation[0] / Math.PI;
                    double rotationY = relativeRotation[1] / Math.PI;
                    double rotationZ = relativeRotation[2] / Math.PI;

                    _oscManager.SendValues(
                        -relativeTranslate[0] / Const.MaxRelativeDistance,
                        relativeTranslate[1] / Const.MaxRelativeDistance,
                        relativeTranslate[2] / Const.MaxRelativeDistance,
                        rotationX,
                        rotationY,
                        rotationZ
                    );
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Process Thread cancelled");
            }
        }

        // will be called within the OVR tracking thread
        private void OnTrackingData(object? sender, EventArgs args)
        {
            var trackingEventArgs = (TrackingDataArgs)args;

            if (_currentInverseCalibrationMatrix == null || trackingEventArgs.Controller == null || trackingEventArgs.Tracker == null) return;

            _stack.Add(new ProcessQueueData(trackingEventArgs.Controller, trackingEventArgs.Tracker));
        }

        // this is called from the OSC thread
        private void OnAvatarChanged(object? sender, EventArgs args)
        {
            var avatarChangeArgs = (AvatarChangedArgs)args;

            Debug.WriteLine("Avatar changed to " + avatarChangeArgs.Id);

            var config = FindAvatarConfig(avatarChangeArgs.Id);

            if (config != null)
            {
                Debug.WriteLine("we know this avatar, creating inverse calibration matrix");

                lock (_lock)
                {
                    var calibration = config.Calibration;

                    _currentInverseCalibrationMatrix = MathUtils.createTransformMatrix44(
                        calibration.RotationX, calibration.RotationY, calibration.RotationZ,
                        calibration.TranslationX, calibration.TranslationY, calibration.TranslationZ,
                        calibration.Scale, calibration.Scale, calibration.Scale
                    ).Inverse();

                    _currentInverseCalibrationMatrixNoScale = MathUtils.createTransformMatrix44(
                        calibration.RotationX, calibration.RotationY, calibration.RotationZ,
                        calibration.TranslationX, calibration.TranslationY, calibration.TranslationZ,
                        1.0f, 1.0f, 1.0f
                    ).Inverse();
                }

                AvatarConfigChanged?.Invoke(this, new AvatarConfigChangedArgs(config));
            }
            else
            {
                _currentInverseCalibrationMatrix = null;
                _currentInverseCalibrationMatrixNoScale = null;
            }
        }

        private AvatarConfig? FindAvatarConfig(string avatarId)
        {
            foreach (var config in _configurations)
            {
                foreach (var configAvatar in config.Avatars)
                {
                    if (configAvatar.Id == avatarId)
                    {
                        return config;
                    }
                }
            }

            return null;
        }
    }
}
