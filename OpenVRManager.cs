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
    internal class OVRException : Exception {
        public OVRException(string message) : base(message) { }
    }

    internal class TrackedObjectListEntry
    {
        public uint Index { get; set; }
        public string Name { get; set; }

    }

    internal class OpenVRManager
    {
        private CVRSystem? cVR;
        private BlockingCollection<Key> InputKeyQueue = new BlockingCollection<Key>();
        private CancellationTokenSource CancelTokenSource = new CancellationTokenSource();
        private Thread? currentThread = null;
        private bool calibrationThreadRunning = false;
        private Dictionary<string, uint> Controllers = new Dictionary<string, uint>();
        private Dictionary<string, uint> Trackers = new Dictionary<string, uint>();

        public OpenVRManager()
        {

        }

        public bool IsCalibrationThreadRunning()
        {
            return calibrationThreadRunning;
        }

        public void InjectKeyPress(Key key)
        {
            InputKeyQueue.Add(key);
        }

        public void UpdateControllers()
        {
            if (cVR == null) return;

            Controllers.Clear();
            Trackers.Clear();

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
                    Controllers.Add(GetStringTrackedDeviceProperty(id, ETrackedDeviceProperty.Prop_SerialNumber_String), id);
                }
                for (uint i = 0; i < noOfTrackers; i++)
                {
                    uint id = trackerIds[i];
                    Trackers.Add(GetStringTrackedDeviceProperty(id, ETrackedDeviceProperty.Prop_SerialNumber_String), id);
                }
            }
            catch (OVRException e)
            {
                MessageBox.Show("Updating controllers and trackers encountered an unexpected OpenVR error: " + e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public List<string> GetControllers()
        {
            return Controllers.Keys.ToList();
        }

        public List<string> GetTrackers()
        {
            return Trackers.Keys.ToList();
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

        public void InitBackground()
        {
            if (cVR != null)
            {
                Shutdown();
            }

            EVRInitError error = EVRInitError.None;
            cVR = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);
        }

        public void InitOverlay()
        {
            if (cVR != null)
            {
                Shutdown();
            }

            EVRInitError error = EVRInitError.None;
            cVR = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Overlay);
        }

        public void Shutdown()
        {
            if (cVR != null)
            {
                OpenVR.Shutdown();
                cVR = null;
            }
        }

        public void StartTrackingThread()
        {
            if (currentThread != null) return;
        }

        public void TrackingThreadMain()
        {

        }

        public void StartCalibrationThread(string controllerSn)
        {
            if (currentThread != null) return;

            if (!Controllers.ContainsKey(controllerSn))
            {
                MessageBox.Show("The controller " + controllerSn + " does not exist", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            uint controllerHandle = Controllers[controllerSn];

            this.currentThread = new Thread(() => CalibrationThreadMain(controllerHandle));
            this.currentThread.Name = "CalibrationThread";
            this.currentThread.Start();
            this.calibrationThreadRunning = true;
        }

        public void CalibrationThreadMain(uint controllerHandle)
        {
            ulong OverlayXHandle = OpenVR.k_ulOverlayHandleInvalid;
            ulong OverlayYHandle = OpenVR.k_ulOverlayHandleInvalid;
            ulong OverlayZHandle = OpenVR.k_ulOverlayHandleInvalid;
            Key nextKey;

            HmdMatrix34_t OverlayXMat = new HmdMatrix34_t();

            try
            {
                ThrowOVRError(OpenVR.Overlay.CreateOverlay("com.jangxx.vrc_osc_calibrate_x", "OSC Debug X Plane", ref OverlayXHandle));
                ThrowOVRError(OpenVR.Overlay.SetOverlayWidthInMeters(OverlayXHandle, 0.1f));
                ThrowOVRError(OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(OverlayXHandle, controllerHandle, ref OverlayXMat));
                ThrowOVRError(OpenVR.Overlay.ShowOverlay(OverlayXHandle));
                ThrowOVRError(OpenVR.Overlay.SetOverlayFromFile(OverlayXHandle, Path.GetFullPath("img\\debug overlay X.png")));

                while (InputKeyQueue.TryTake(out nextKey, -1, CancelTokenSource.Token))
                {

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
            //finally
            //{
            //    Shutdown();
            //}
        }

        private void ThrowOVRError(EVROverlayError err)
        {
            if (err != EVROverlayError.None)
            {
                throw new OVRException(err.ToString());
            }
        }

        public void StopThread()
        {
            if (currentThread == null) return;

            CancelTokenSource.Cancel();
            currentThread.Join();
            currentThread = null;
            this.calibrationThreadRunning = false;
        }
    }
}
