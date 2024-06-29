using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Valve.VR;

namespace VRC_OSC_ExternallyTrackedObject
{
    public enum CalibrationField
    {
        POSX, POSY, POSZ,
        ROTX, ROTY, ROTZ,
        SCALE,
        CALIBRATION_LAST
    }

    internal class OVRException : Exception {
        public OVRException(string message) : base(message) { }
    }

    internal class DeviceListEntry
    {
        public ETrackedDeviceClass Type { get; set; }
        public uint Handle { get; set; }
    }

    internal class TrackedObjectListEntry
    {
        public uint Index { get; set; }
        public string Name { get; set; } = "";

    }

    public class CalibrationUpdateArgs : EventArgs
    {
        public enum CalibrationUpdateType
        {
            CALIBRATION_VALUE,
            ACTIVE_FIELD,
        };

        public CalibrationUpdateType Type { get; set; }
        public AvatarCalibration? CalibrationValues { get; set; }
        public CalibrationField Field { get; set; }
    }

    public class TrackingDataArgs : EventArgs
    {
        public Matrix<float>? Controller { get; set; }
        public Matrix<float>? Tracker { get; set; }
    }

    internal class OpenVRManager
    {
        private CVRSystem? _cVR;
        private BlockingCollection<Key> _inputKeyQueue = new BlockingCollection<Key>();
        private CancellationTokenSource _cancelTokenSource = new CancellationTokenSource();
        private Thread? _currentThread = null;
        private AvatarCalibration? _currentCalibration;
        private bool _calibrationThreadRunning = false;
        private Dictionary<string, DeviceListEntry> _devices = new Dictionary<string, DeviceListEntry>();

        public event EventHandler? CalibrationUpdate;
        public event EventHandler? TrackingData;

        public OpenVRManager()
        {

        }

        public bool IsCalibrationThreadRunning()
        {
            return _calibrationThreadRunning;
        }

        public void InjectKeyPress(Key key)
        {
            _inputKeyQueue.Add(key);
        }

        public void UpdateControllers()
        {
            if (_cVR == null) return;

            _devices.Clear();

            // this is not optimal, ideally we could put everything in the same array but I don't think c# slices actually support that?
            uint[] controllerIds = new uint[OpenVR.k_unMaxTrackedDeviceCount];
            uint[] trackerIds = new uint[OpenVR.k_unMaxTrackedDeviceCount];
            uint noOfControllers = OpenVR.System.GetSortedTrackedDeviceIndicesOfClass(ETrackedDeviceClass.Controller, controllerIds, OpenVR.k_unTrackedDeviceIndex_Hmd);
            uint noOfTrackers = OpenVR.System.GetSortedTrackedDeviceIndicesOfClass(ETrackedDeviceClass.GenericTracker, trackerIds, OpenVR.k_unTrackedDeviceIndex_Hmd);

            try
            {
                for (uint i = 0; i < noOfControllers; i++)
                {
                    uint id = controllerIds[i];
                    _devices.Add(
                        GetStringTrackedDeviceProperty(id, ETrackedDeviceProperty.Prop_SerialNumber_String),
                        new DeviceListEntry { Handle = id, Type = ETrackedDeviceClass.Controller } 
                    );
                }
                for (uint i = 0; i < noOfTrackers; i++)
                {
                    uint id = trackerIds[i];
                    _devices.Add(
                        GetStringTrackedDeviceProperty(id, ETrackedDeviceProperty.Prop_SerialNumber_String),
                        new DeviceListEntry { Handle = id, Type = ETrackedDeviceClass.GenericTracker }
                    );
                }
            }
            catch (OVRException e)
            {
                MessageBox.Show("Updating controllers and trackers encountered an unexpected OpenVR error: " + e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public List<string> GetControllers()
        {
            return _devices.Keys.Where(id => _devices[id].Type == ETrackedDeviceClass.Controller).ToList();
        }

        public List<string> GetTrackers()
        {
            return _devices.Keys.Where(id => _devices[id].Type == ETrackedDeviceClass.GenericTracker).ToList();
        }

        public List<string> GetAllDevices()
        {
            return _devices.Keys.ToList();
        }

        private string GetStringTrackedDeviceProperty(uint deviceIndex, ETrackedDeviceProperty prop)
        {
            ETrackedPropertyError err = ETrackedPropertyError.TrackedProp_Success;

            StringBuilder sb = new StringBuilder(128);
            uint propLen = OpenVR.System.GetStringTrackedDeviceProperty(deviceIndex, prop, sb, (uint)sb.Capacity, ref err);
            if (err == ETrackedPropertyError.TrackedProp_Success)
            {
                return sb.ToString();
            }
            else if (err == ETrackedPropertyError.TrackedProp_BufferTooSmall)
            {
                // try again with larger buffer
                sb.Capacity = (int)propLen;
                propLen = OpenVR.System.GetStringTrackedDeviceProperty(deviceIndex, prop, sb, (uint)sb.Capacity, ref err);
            }

            if (err != ETrackedPropertyError.TrackedProp_Success)
            {
                throw new OVRException(err.ToString());
            }

            return sb.ToString();
        }

        public bool InitBackground()
        {
            if (_cVR != null)
            {
                Shutdown();
            }

            EVRInitError error = EVRInitError.None;
            _cVR = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);

            if (error != EVRInitError.None)
            {
                MessageBox.Show("Error while connecting to SteamVR: " + error.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            else
            {
                return true;
            }
        }

        public bool InitOverlay()
        {
            if (_cVR != null)
            {
                Shutdown();
            }

            EVRInitError error = EVRInitError.None;
            _cVR = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Overlay);

            if (error != EVRInitError.None)
            {
                MessageBox.Show("Error while connecting to SteamVR: " + error.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            } 
            else
            {
                return true;
            }
        }

        public void Shutdown()
        {
            if (_cVR != null)
            {
                OpenVR.Shutdown();
                _cVR = null;
            }
        }

        public bool StartTrackingThread(string controllerSn, string trackerSn)
        {
            if (_currentThread != null) return false;

            if (!_devices.ContainsKey(controllerSn))
            {
                MessageBox.Show("The controller " + controllerSn + " does not exist", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!_devices.ContainsKey(trackerSn))
            {
                MessageBox.Show("The tracker " + trackerSn + " does not exist", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            uint controllerHandle = _devices[controllerSn].Handle;
            uint trackerHandle = _devices[trackerSn].Handle;

            this._cancelTokenSource = new CancellationTokenSource();

            this._currentThread = new Thread(() => TrackingThreadMain(controllerHandle, trackerHandle));
            this._currentThread.Name = "TrackingThread";
            this._currentThread.IsBackground = true;
            this._currentThread.Start();

            return true;
        }

        public void TrackingThreadMain(uint controllerHandle, uint trackerHandle)
        {
            TrackedDevicePose_t[] poses = new TrackedDevicePose_t[Math.Max(controllerHandle, trackerHandle) + 1];
            TrackedDevicePose_t[] empty = new TrackedDevicePose_t[0];

            var eventArgs = new TrackingDataArgs();

            while (true)
            {
                if (_cancelTokenSource.Token.WaitHandle.WaitOne(10))
                {
                    return; // cancellation was requested
                }

                //OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, poses);
                ThrowOVRError(OpenVR.Compositor.GetLastPoses(poses, empty));

                var controllerPose = poses[controllerHandle];
                var trackerPose = poses[trackerHandle];

                eventArgs.Controller = MathUtils.OVR34ToMat44(ref controllerPose.mDeviceToAbsoluteTracking);
                eventArgs.Tracker = MathUtils.OVR34ToMat44(ref trackerPose.mDeviceToAbsoluteTracking);

                var handler = TrackingData;
                handler?.Invoke(this, eventArgs);
            }
        }

        public void StartCalibrationThread(string controllerSn, AvatarCalibration avatarCalibration)
        {
            if (_currentThread != null) return;

            if (!_devices.ContainsKey(controllerSn))
            {
                MessageBox.Show("The controller " + controllerSn + " does not exist", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            uint controllerHandle = _devices[controllerSn].Handle;

            this._currentCalibration = avatarCalibration;

            this._cancelTokenSource = new CancellationTokenSource();

            this._currentThread = new Thread(() => CalibrationThreadMain(controllerHandle));
            this._currentThread.Name = "CalibrationThread";
            this._currentThread.IsBackground = true;
            this._currentThread.Start();
            this._calibrationThreadRunning = true;
        }

        public void CalibrationThreadMain(uint controllerHandle)
        {
            if (this._currentCalibration == null) return;

            ulong OverlayXHandle = OpenVR.k_ulOverlayHandleInvalid;
            ulong OverlayYHandle = OpenVR.k_ulOverlayHandleInvalid;
            ulong OverlayZHandle = OpenVR.k_ulOverlayHandleInvalid;
            Key nextKey;
            var currentCalibrationField = CalibrationField.POSX;

            Matrix<float> OverlayXMat = MathUtils.createTransformMatrix44((float)Math.PI, (float)(-Math.PI / 2), (float)(Math.PI / 2), 0, 0.05f, 0, 1, 1, 1);
            HmdMatrix34_t OverlayXMatOVR = new HmdMatrix34_t();
            MathUtils.CopyMat34ToOVR(ref OverlayXMat, ref OverlayXMatOVR);

            Matrix<float> OverlayYMat = MathUtils.createTransformMatrix44(0, 0, 0, 0, 0, 0.05f, 1, 1, 1);
            HmdMatrix34_t OverlayYMatOVR = new HmdMatrix34_t();
            MathUtils.CopyMat34ToOVR(ref OverlayYMat, ref OverlayYMatOVR);

            Matrix<float> OverlayZMat = MathUtils.createTransformMatrix44((float)(-Math.PI / 2), (float)Math.PI, (float)(Math.PI / 2), -0.05f, 0, 0, 1, 1, 1);
            HmdMatrix34_t OverlayZMatOVR = new HmdMatrix34_t();
            MathUtils.CopyMat34ToOVR(ref OverlayZMat, ref OverlayZMatOVR);

            Matrix<float> currentTransformMatrix = Matrix<float>.Build.Dense(4, 4);
            Matrix<float> m = Matrix<float>.Build.Dense(4, 4); // <-- matrix for temporary data storage in calculations

            try
            {
                ThrowOVRError(OpenVR.Overlay.CreateOverlay("com.jangxx.vrc_osc_calibrate_x", "OSC Debug X Plane", ref OverlayXHandle));
                ThrowOVRError(OpenVR.Overlay.SetOverlayWidthInMeters(OverlayXHandle, 0.1f));
                ThrowOVRError(OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(OverlayXHandle, controllerHandle, ref OverlayXMatOVR));
                ThrowOVRError(OpenVR.Overlay.ShowOverlay(OverlayXHandle));
                ThrowOVRError(OpenVR.Overlay.SetOverlayFromFile(OverlayXHandle, Path.GetFullPath("img\\debug overlay X.png")));

                ThrowOVRError(OpenVR.Overlay.CreateOverlay("com.jangxx.vrc_osc_calibrate_y", "OSC Debug Y Plane", ref OverlayYHandle));
                ThrowOVRError(OpenVR.Overlay.SetOverlayWidthInMeters(OverlayYHandle, 0.1f));
                ThrowOVRError(OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(OverlayYHandle, controllerHandle, ref OverlayYMatOVR));
                ThrowOVRError(OpenVR.Overlay.ShowOverlay(OverlayYHandle));
                ThrowOVRError(OpenVR.Overlay.SetOverlayFromFile(OverlayYHandle, Path.GetFullPath("img\\debug overlay Y.png")));

                ThrowOVRError(OpenVR.Overlay.CreateOverlay("com.jangxx.vrc_osc_calibrate_z", "OSC Debug Z Plane", ref OverlayZHandle));
                ThrowOVRError(OpenVR.Overlay.SetOverlayWidthInMeters(OverlayZHandle, 0.1f));
                ThrowOVRError(OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(OverlayZHandle, controllerHandle, ref OverlayZMatOVR));
                ThrowOVRError(OpenVR.Overlay.ShowOverlay(OverlayZHandle));
                ThrowOVRError(OpenVR.Overlay.SetOverlayFromFile(OverlayZHandle, Path.GetFullPath("img\\debug overlay Z.png")));

                { // emit an initial event to highlight the first field
                    var args = new CalibrationUpdateArgs() { Type = CalibrationUpdateArgs.CalibrationUpdateType.ACTIVE_FIELD, Field = currentCalibrationField };
                    var handler = CalibrationUpdate;
                    handler?.Invoke(this, args);
                }

                // do one initial update to show the current calibration
                MathUtils.fillTransformMatrix44(ref currentTransformMatrix,
                    (float)_currentCalibration.RotationX,
                    (float)_currentCalibration.RotationY,
                    (float)_currentCalibration.RotationZ,
                    (float)_currentCalibration.TranslationX,
                    (float)_currentCalibration.TranslationY,
                    (float)_currentCalibration.TranslationZ,
                    (float)_currentCalibration.Scale,
                    (float)_currentCalibration.Scale,
                    (float)_currentCalibration.Scale
                );

                currentTransformMatrix.Multiply(OverlayXMat, m);
                MathUtils.CopyMat34ToOVR(ref m, ref OverlayXMatOVR);
                currentTransformMatrix.Multiply(OverlayYMat, m);
                MathUtils.CopyMat34ToOVR(ref m, ref OverlayYMatOVR);
                currentTransformMatrix.Multiply(OverlayZMat, m);
                MathUtils.CopyMat34ToOVR(ref m, ref OverlayZMatOVR);

                OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(OverlayXHandle, controllerHandle, ref OverlayXMatOVR);
                OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(OverlayYHandle, controllerHandle, ref OverlayYMatOVR);
                OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(OverlayZHandle, controllerHandle, ref OverlayZMatOVR);

                while (_inputKeyQueue.TryTake(out nextKey, -1, _cancelTokenSource.Token))
                {
                    switch(nextKey)
                    {
                        // switch to previous param
                        case Key.Left:
                            {
                                int field_len = (int)CalibrationField.CALIBRATION_LAST;
                                currentCalibrationField = (CalibrationField)((((int)currentCalibrationField - 1) + field_len) % field_len);
                                var args = new CalibrationUpdateArgs() { Type = CalibrationUpdateArgs.CalibrationUpdateType.ACTIVE_FIELD, Field = currentCalibrationField };
                                var handler = CalibrationUpdate;
                                handler?.Invoke(this, args);
                                continue;
                            }

                        // switch to next param
                        case Key.Right:
                            {
                                int field_len = (int)CalibrationField.CALIBRATION_LAST;
                                currentCalibrationField = (CalibrationField)(((int)currentCalibrationField + 1) % field_len);
                                var args = new CalibrationUpdateArgs() { Type = CalibrationUpdateArgs.CalibrationUpdateType.ACTIVE_FIELD, Field = currentCalibrationField };
                                var handler = CalibrationUpdate;
                                handler?.Invoke(this, args);
                                continue;
                            }

                        // increase current parameter value
                        case Key.Up:
                            {
                                transformMatrixInDirection(ref currentTransformMatrix, currentCalibrationField, 1);
                                _currentCalibration.CopyFrom(AvatarCalibration.FromMatrix(currentTransformMatrix));
                                var args = new CalibrationUpdateArgs() { Type = CalibrationUpdateArgs.CalibrationUpdateType.CALIBRATION_VALUE, Field = currentCalibrationField, CalibrationValues = _currentCalibration };
                                var handler = CalibrationUpdate;
                                handler?.Invoke(this, args);
                                break;
                            }

                        // decrease current parameter value
                        case Key.Down:
                            {
                                transformMatrixInDirection(ref currentTransformMatrix, currentCalibrationField, -1);
                                _currentCalibration.CopyFrom(AvatarCalibration.FromMatrix(currentTransformMatrix));
                                var args = new CalibrationUpdateArgs() { Type = CalibrationUpdateArgs.CalibrationUpdateType.CALIBRATION_VALUE, Field = currentCalibrationField, CalibrationValues = _currentCalibration };
                                var handler = CalibrationUpdate;
                                handler?.Invoke(this, args);
                                break;
                            }
                    }

                    currentTransformMatrix.Multiply(OverlayXMat, m);
                    MathUtils.CopyMat34ToOVR(ref m, ref OverlayXMatOVR);
                    currentTransformMatrix.Multiply(OverlayYMat, m);
                    MathUtils.CopyMat34ToOVR(ref m, ref OverlayYMatOVR);
                    currentTransformMatrix.Multiply(OverlayZMat, m);
                    MathUtils.CopyMat34ToOVR(ref m, ref OverlayZMatOVR);

                    OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(OverlayXHandle, controllerHandle, ref OverlayXMatOVR);
                    OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(OverlayYHandle, controllerHandle, ref OverlayYMatOVR);
                    OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(OverlayZHandle, controllerHandle, ref OverlayZMatOVR);
                }
            }
            catch(OperationCanceledException)
            {
                Debug.WriteLine("Calibration thread exited");
            }
            catch (OVRException e)
            {
                MessageBox.Show("Calibration encountered an unexpected OpenVR error: " + e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch(Exception e)
            {
                MessageBox.Show("Calibration thread encountered an unexpected error: " + e.ToString() + " (" + e.Message + ")", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                //Shutdown();
                OpenVR.Overlay.DestroyOverlay(OverlayXHandle);
                OpenVR.Overlay.DestroyOverlay(OverlayYHandle);
                OpenVR.Overlay.DestroyOverlay(OverlayZHandle);
            }
        }

        private void transformMatrixInDirection(ref Matrix<float> calibrationMatrix, CalibrationField field, float direction)
        {
            switch (field)
            {
                case CalibrationField.POSX:
                    {
                        var transform = MathUtils.createTransformMatrix44(0, 0, 0, 0.005f * direction, 0, 0, 1, 1, 1);
                        transform.Multiply(calibrationMatrix, calibrationMatrix);
                        return;
                    }
                case CalibrationField.POSY:
                    {
                        var transform = MathUtils.createTransformMatrix44(0, 0, 0, 0, 0.005f * direction, 0, 1, 1, 1);
                        transform.Multiply(calibrationMatrix, calibrationMatrix);
                        return;
                    }
                case CalibrationField.POSZ:
                    {
                        var transform = MathUtils.createTransformMatrix44(0, 0, 0, 0, 0, 0.005f * direction, 1, 1, 1);
                        transform.Multiply(calibrationMatrix, calibrationMatrix);
                        return;
                    }
                case CalibrationField.ROTX:
                    {
                        var translate = MathUtils.extractTranslationFromMatrix44(calibrationMatrix).Clone();
                        MathUtils.createTransformMatrix44(0, 0, 0, -translate[0], -translate[1], -translate[2], 1, 1, 1).Multiply(calibrationMatrix, calibrationMatrix);
                        MathUtils.createTransformMatrix44(0.01f * direction, 0, 0, 0, 0, 0, 1, 1, 1).Multiply(calibrationMatrix, calibrationMatrix);
                        MathUtils.createTransformMatrix44(0, 0, 0, translate[0], translate[1], translate[2], 1, 1, 1).Multiply(calibrationMatrix, calibrationMatrix);
                        return;
                    }
                case CalibrationField.ROTY:
                    {
                        var translate = MathUtils.extractTranslationFromMatrix44(calibrationMatrix).Clone();
                        MathUtils.createTransformMatrix44(0, 0, 0, -translate[0], -translate[1], -translate[2], 1, 1, 1).Multiply(calibrationMatrix, calibrationMatrix);
                        MathUtils.createTransformMatrix44(0, 0.01f * direction, 0, 0, 0, 0, 1, 1, 1).Multiply(calibrationMatrix, calibrationMatrix);
                        MathUtils.createTransformMatrix44(0, 0, 0, translate[0], translate[1], translate[2], 1, 1, 1).Multiply(calibrationMatrix, calibrationMatrix);
                        return;
                    }
                case CalibrationField.ROTZ:
                    {
                        var translate = MathUtils.extractTranslationFromMatrix44(calibrationMatrix).Clone();
                        MathUtils.createTransformMatrix44(0, 0, 0, -translate[0], -translate[1], -translate[2], 1, 1, 1).Multiply(calibrationMatrix, calibrationMatrix);
                        MathUtils.createTransformMatrix44(0, 0, 0.01f * direction, 0, 0, 0, 1, 1, 1).Multiply(calibrationMatrix, calibrationMatrix);
                        MathUtils.createTransformMatrix44(0, 0, 0, translate[0], translate[1], translate[2], 1, 1, 1).Multiply(calibrationMatrix, calibrationMatrix);
                        return;
                    }
                case CalibrationField.SCALE:
                    {
                        var translate = MathUtils.extractTranslationFromMatrix44(calibrationMatrix).Clone();
                        MathUtils.createTransformMatrix44(0, 0, 0, -translate[0], -translate[1], -translate[2], 1, 1, 1).Multiply(calibrationMatrix, calibrationMatrix);
                        MathUtils.createTransformMatrix44(
                            0, 0, 0,
                            0, 0, 0,
                            1 + 0.005f * direction,
                            1 + 0.005f * direction,
                            1 + 0.005f * direction
                        ).Multiply(calibrationMatrix, calibrationMatrix);
                        MathUtils.createTransformMatrix44(0, 0, 0, translate[0], translate[1], translate[2], 1, 1, 1).Multiply(calibrationMatrix, calibrationMatrix);
                        return;
                    }
            }
        }

        private void ThrowOVRError(EVROverlayError err)
        {
            if (err != EVROverlayError.None)
            {
                throw new OVRException(err.ToString());
            }
        }

        private void ThrowOVRError(EVRCompositorError err)
        {
            if (err != EVRCompositorError.None)
            {
                throw new OVRException(err.ToString());
            }
        }

        public bool IsAnyThreadRunning()
        {
            return this._currentThread != null;
        }

        public void StopThread()
        {
            if (_currentThread == null) return;

            _cancelTokenSource.Cancel();
            _currentThread.Join();
            _currentThread = null;
            this._calibrationThreadRunning = false;
        }
    }
}