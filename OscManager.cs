using Rug.Osc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace VRC_OSC_ExternallyTrackedObject
{
    public class TrackingActiveChangedArgs : EventArgs
    {
        public bool Active { get; set; }
        public bool AvatarKnown { get; set; }
    }

    public class AvatarChangedArgs : EventArgs
    {
        public string Id { get; set; } = "";
    }

    internal class OscManager
    {
        private static string AVATAR_CHANGE_ADDRESS = "/avatar/change";

        private CancellationTokenSource CancelTokenSource = new CancellationTokenSource();
        private Thread? currentThread = null;
        private Dictionary<string, AvatarParams>? currentConfig = null;
        private string? currentAvatarId = null;
        private bool currentlyActive = false;
        private OscSender? oscSender;
        private OscReceiver? oscReceiver;

        public EventHandler? TrackingActiveChanged;
        public EventHandler? AvatarChanged;
        public EventHandler? ThreadCrashed;

        public void Start(string inputAddress, int inputPort, string outputAddress, int outputPort, List<AvatarConfig> config)
        {
            if (oscSender == null)
            {
                oscSender = new OscSender(System.Net.IPAddress.Parse(outputAddress), 0, outputPort);
            }
            if (oscReceiver == null)
            {
                oscReceiver = new OscReceiver(System.Net.IPAddress.Parse(inputAddress), inputPort);
            }
            
            // build dictionary from the config for faster lookups
            currentConfig = new Dictionary<string, AvatarParams>();
            foreach (var avatarConfig in config)
            {
                foreach (var avatarDefinition in avatarConfig.Avatars)
                {
                    currentConfig.Add(avatarDefinition.Id, avatarConfig.Parameters);
                }
            }

            //this.currentlyActive = true; // just for testing

            oscReceiver.Connect();
            oscSender.Connect();

            currentThread = new Thread(() => ListenThread());
            currentThread.Name = "OscThread";
            currentThread.IsBackground = true;
            currentThread.Start();

            Debug.WriteLine("OSC thread started");
        }

        public void ListenThread()
        {
            if (oscReceiver == null || currentConfig == null) throw new Exception("ListenThread was set up incorrectly");

            try
            {
                {
                    var args = new TrackingActiveChangedArgs()
                    {
                        Active = false,
                        AvatarKnown = false,
                    };
                    var handler = TrackingActiveChanged;
                    handler?.Invoke(this, args);
                }

                while (oscReceiver.State != OscSocketState.Closed)
                {
                    if (oscReceiver.State == OscSocketState.Connected)
                    {
                        OscPacket packet = oscReceiver.Receive();

                        string? msgStr = packet.ToString();

                        if (msgStr == null)
                        {
                            continue; // skip this packet so we don't crash the OSC thread
                        }

                        OscMessage msg;

                        try
                        {
                            msg = OscMessage.Parse(msgStr);
                        }
                        catch
                        {
                            continue;
                        }

                        //Debug.WriteLine(packet.ToString());

                        if (msg.Address == AVATAR_CHANGE_ADDRESS && msg.Count > 0)
                        {
                            currentAvatarId = (string)msg[0];

                            {
                                var args = new AvatarChangedArgs() { Id = currentAvatarId };
                                var handler = AvatarChanged;
                                handler?.Invoke(this, args);
                            }

                            this.currentlyActive = false;

                            if (currentConfig.ContainsKey(currentAvatarId))
                            {
                                // if the activate parameter is set to nothing we are always activated, otherwise we wait for the trigger
                                this.currentlyActive = (currentConfig[currentAvatarId].Activate == "");
                            }

                            {
                                var args = new TrackingActiveChangedArgs()
                                {
                                    Active = this.currentlyActive,
                                    AvatarKnown = currentConfig.ContainsKey(currentAvatarId),
                                };
                                var handler = TrackingActiveChanged;
                                handler?.Invoke(this, args);
                            }
                        }
                        else if (currentAvatarId != null
                            && currentConfig.ContainsKey(currentAvatarId)
                            && msg.Address == currentConfig[currentAvatarId].Activate
                            && msg.Count > 0
                        ) {
                            bool activate = (bool)msg[0];

                            this.currentlyActive = activate;

                            var args = new TrackingActiveChangedArgs() { Active = activate, AvatarKnown = true };
                            var handler = TrackingActiveChanged;
                            handler?.Invoke(this, args);

                            if (!this.currentlyActive)
                            {
                                SendValues(0, 0, 0, 0, 0, 0, true); // reset all values to 0 so that we can reuse those parameters
                            }
                        }

                        // everything else is ignored
                    }
                }
            }
            catch(Exception e)
            {
                if (oscReceiver != null && oscReceiver.State == OscSocketState.Connected)
                {
                    MessageBox.Show("OSC thread encountered an unexpected error: " + e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    
                    var handler = ThreadCrashed;
                    handler?.Invoke(this, new EventArgs());
                }
            }
        }

        public void Stop()
        {
            if (oscReceiver != null)
            {
                oscReceiver.Close();
                //oscReceiver.Dispose();
            }
            if (oscSender != null)
            {
                oscSender.Close();
                //oscSender.Dispose();
            }

            //oscReceiver = null;
            //oscSender = null;

            currentConfig = null;

            if (currentThread != null)
            {
                currentThread.Join();
            }
            currentThread = null;
        }

        public void SendValues(float posX, float posY, float posZ, float rotX, float rotY, float rotZ, bool force = false)
        {
            if (this.oscSender == null || this.oscSender.State != OscSocketState.Connected || this.currentConfig == null)
            {
                throw new Exception("SendValues was called without the OSC manager being set up");
            }

            if ((!force && !this.currentlyActive) || currentAvatarId == null || !this.currentConfig.ContainsKey(currentAvatarId))
            {
                return; // discard the message to not spam the game with useless messages
            }

            oscSender.Send(new OscMessage(this.currentConfig[currentAvatarId].PositionX, posX));
            oscSender.Send(new OscMessage(this.currentConfig[currentAvatarId].PositionY, posY));
            oscSender.Send(new OscMessage(this.currentConfig[currentAvatarId].PositionZ, posZ));
            oscSender.Send(new OscMessage(this.currentConfig[currentAvatarId].RotationX, rotX));
            oscSender.Send(new OscMessage(this.currentConfig[currentAvatarId].RotationY, rotY));
            oscSender.Send(new OscMessage(this.currentConfig[currentAvatarId].RotationZ, rotZ));

            //Debug.WriteLine("[OSC] Sending pos=(" + posX + ", " + posY + ", " + posZ + ") rot=(" + rotX + ", " + rotY + ", " + rotZ + ")");
        }
    }
}
