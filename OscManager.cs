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
        public TrackingActiveChangedArgs(bool active, bool avatarKnown)
        {
            Active = active;
            AvatarKnown = avatarKnown;
        }

        public bool Active { get; }
        public bool AvatarKnown { get; }
    }

    public class AvatarChangedArgs : EventArgs
    {
        public AvatarChangedArgs(string id)
        {
            Id = id;
        }

        public string Id { get; }
    }

    internal class OscManager
    {
        private static string AVATAR_CHANGE_ADDRESS = "/avatar/change";

        private Thread? _currentThread = null;
        private Dictionary<string, AvatarParams>? _currentConfig = null;
        private string? _currentAvatarId = null;
        private bool _currentlyActive = false;
        private OscSender? _oscSender;
        private OscReceiver? _oscReceiver;

        public EventHandler? TrackingActiveChanged;
        public EventHandler? AvatarChanged;
        public EventHandler? ThreadCrashed;

        public void Start(string inputAddress, int inputPort, string outputAddress, int outputPort, List<AvatarConfig> config)
        {
            if (_oscSender == null)
            {
                _oscSender = new OscSender(System.Net.IPAddress.Parse(outputAddress), 0, outputPort);
            }
            if (_oscReceiver == null)
            {
                _oscReceiver = new OscReceiver(System.Net.IPAddress.Parse(inputAddress), inputPort);
            }
            
            // build dictionary from the config for faster lookups
            _currentConfig = new Dictionary<string, AvatarParams>();
            foreach (var avatarConfig in config)
            {
                foreach (var avatarDefinition in avatarConfig.Avatars)
                {
                    _currentConfig.Add(avatarDefinition.Id, avatarConfig.Parameters);
                }
            }

            _currentAvatarId = null;
            _currentlyActive = false;

            _oscReceiver.Connect();
            _oscSender.Connect();

            _currentThread = new Thread(() => ListenThread());
            _currentThread.Name = "OscThread";
            _currentThread.IsBackground = true;
            _currentThread.Start();

            Debug.WriteLine("OSC thread started");
        }

        public void ListenThread()
        {
            if (_oscReceiver == null || _currentConfig == null) throw new Exception("ListenThread was set up incorrectly");

            try
            {
                TrackingActiveChanged?.Invoke(this, new TrackingActiveChangedArgs(false, false));

                while (_oscReceiver.State != OscSocketState.Closed)
                {
                    if (_oscReceiver.State == OscSocketState.Connected)
                    {
                        OscPacket packet = _oscReceiver.Receive();

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
                            _currentAvatarId = (string)msg[0];

                            AvatarChanged?.Invoke(this, new AvatarChangedArgs(_currentAvatarId));

                            _currentlyActive = false;

                            if (_currentConfig.ContainsKey(_currentAvatarId))
                            {
                                // if the activate parameter is set to nothing we are always activated, otherwise we wait for the trigger
                                _currentlyActive = (_currentConfig[_currentAvatarId].Activate == "");
                            }

                            TrackingActiveChanged?.Invoke(this, new TrackingActiveChangedArgs(_currentlyActive, _currentConfig.ContainsKey(_currentAvatarId)));
                        }
                        else if (_currentAvatarId != null
                            && _currentConfig.ContainsKey(_currentAvatarId)
                            && msg.Address == _currentConfig[_currentAvatarId].Activate
                            && msg.Count > 0
                        ) {
                            bool activate = (bool)msg[0];

                            _currentlyActive = activate;                            

                            TrackingActiveChanged?.Invoke(this, new TrackingActiveChangedArgs(activate, true));

                            if (!_currentlyActive)
                            {
                                SendValues(0, 0, 0, 0, 0, 0, true); // reset all values to 0 so that we can reuse those parameters for other avatars
                            }
                        }

                        // everything else is ignored
                    }
                }
            }
            catch(Exception e)
            {
                Debug.WriteLine("ListenThread ended with exception: " + e.Message);

                if (_oscReceiver != null && _oscReceiver.State == OscSocketState.Connected)
                {
                    MessageBox.Show("OSC thread encountered an unexpected error: " + e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    
                    var handler = ThreadCrashed;
                    handler?.Invoke(this, new EventArgs());
                }
            }
        }

        public void Stop()
        {
            if (_oscReceiver != null)
            {
                _oscReceiver.Close();
                //oscReceiver.Dispose();
            }
            if (_oscSender != null)
            {
                _oscSender.Close();
                //oscSender.Dispose();
            }

            //oscReceiver = null;
            //oscSender = null;

            _currentConfig = null;

            if (_currentThread != null)
            {
                _currentThread.Join();
            }
            _currentThread = null;
        }

        public void SendValues(double posX, double posY, double posZ, double rotX, double rotY, double rotZ, bool force = false)
        {
            if (_oscSender == null || _oscSender.State != OscSocketState.Connected || _currentConfig == null)
            {
                throw new Exception("SendValues was called without the OSC manager being set up");
            }

            if ((!force && !_currentlyActive) || _currentAvatarId == null || !_currentConfig.ContainsKey(_currentAvatarId))
            {
                return; // discard the message to not spam the game with useless messages
            }

            _oscSender.Send(new OscMessage(_currentConfig[_currentAvatarId].PositionX, (float)posX));
            _oscSender.Send(new OscMessage(_currentConfig[_currentAvatarId].PositionY, (float)posY));
            _oscSender.Send(new OscMessage(_currentConfig[_currentAvatarId].PositionZ, (float)posZ));
            _oscSender.Send(new OscMessage(_currentConfig[_currentAvatarId].RotationX, (float)rotX));
            _oscSender.Send(new OscMessage(_currentConfig[_currentAvatarId].RotationY, (float)rotY));
            _oscSender.Send(new OscMessage(_currentConfig[_currentAvatarId].RotationZ, (float)rotZ));

            //Debug.WriteLine("[OSC] Sending pos=(" + posX + ", " + posY + ", " + posZ + ") rot=(" + rotX + ", " + rotY + ", " + rotZ + ")");
        }
    }
}
