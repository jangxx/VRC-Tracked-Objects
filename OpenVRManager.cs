using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;

namespace VRC_OSC_ExternallyTrackedObject
{
    internal class OpenVRManager
    {
        private CVRSystem cVR;

        OpenVRManager()
        {

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
            }
        }
    }
}
