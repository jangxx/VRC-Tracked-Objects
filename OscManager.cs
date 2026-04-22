using MeaMod.DNS.Multicast;
using Rug.Osc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using VRC.OSCQuery;

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
        private static string OSCQUERY_SERVICE_NAME = "VRC Tracked Objects";

        private Thread? _currentThread = null;
        private Dictionary<string, AvatarParams>? _currentConfig = null;
        private string? _currentAvatarId = null;
        private bool _currentlyActive = false;
        private OscSender? _oscSender;
        private OscReceiver? _oscReceiver;
        private OSCQueryService? _oscQueryService;
        private string? _currentOscqueryService = null;

        public EventHandler? TrackingActiveChanged;
        public EventHandler? AvatarChanged;
        public EventHandler? ThreadCrashed;

        public void StartWithAddresses(string inputAddress, int inputPort, string outputAddress, int outputPort, List<AvatarConfig> config)
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

            Debug.WriteLine("OSC thread started with fixed addresses");
            Debug.WriteLine($"Sending on {outputAddress}:{outputPort}");
            Debug.WriteLine($"Receiving on {inputAddress}:{inputPort}");
        }

        public void StartWithOscQuery(List<AvatarConfig> config)
        {
            // build dictionary from the config for faster lookups
            this._currentConfig = new Dictionary<string, AvatarParams>();
            foreach (var avatarConfig in config)
            {
                foreach (var avatarDefinition in avatarConfig.Avatars)
                {
                    this._currentConfig.Add(avatarDefinition.Id, avatarConfig.Parameters);
                }
            }

            int receivePort = Extensions.GetAvailableUdpPort();

            if (this._oscReceiver == null)
            {
                this._oscReceiver = new OscReceiver(System.Net.IPAddress.Loopback, receivePort);
            }

            this._oscQueryService = new OSCQueryServiceBuilder()
                .WithTcpPort(Extensions.GetAvailableTcpPort())
                .WithUdpPort(receivePort)
                .WithServiceName(OSCQUERY_SERVICE_NAME)
                .WithDefaults()
                .Build();

            // just exposing any endpoint will cause the game to send us all OSC messages
            this._oscQueryService.AddEndpoint("/avatar/change", "s", Attributes.AccessValues.WriteOnly);

            this._oscQueryService.OnOscQueryServiceAdded += async (profile) =>
            {
                if (profile.name != OSCQUERY_SERVICE_NAME && profile.name != this._currentOscqueryService)
                {
                    Debug.WriteLine($"OSCQuery Service found: {profile.name}");
                    if (profile.name.StartsWith("VRChat-Client"))
                    {
                        Debug.WriteLine($"Found VRChat client on {profile.address}:{profile.port}");

                        var tree = await Extensions.GetOSCTree(profile.address, profile.port);
                        var hostInfo = await Extensions.GetHostInfo(profile.address, profile.port);

                        this._currentOscqueryService = profile.name;

                        // wait until after we have successfully contacted the service before we switch over
                        if (this._oscSender != null)
                        {
                            this._oscSender.Close();
                        }
                        this._oscSender = new OscSender(System.Net.IPAddress.Parse(hostInfo.oscIP), 0, hostInfo.oscPort);

                        var avatarNode = tree.GetNodeWithPath("/avatar/change");
                        
                        if (avatarNode.Value != null && avatarNode.Value.Length == 1 && avatarNode.Value[0].GetType() == typeof(string))
                        {
                            this._currentAvatarId = avatarNode.Value[0] as string;
                            HandleAvatarIdChanged();
                        }

                        this._oscSender.Connect();
                    }
                }
            };

            this._currentAvatarId = null;
            this._currentlyActive = false;
            this._currentOscqueryService = null;

            this._oscReceiver.Connect();

            this._currentThread = new Thread(() => ListenThread());
            this._currentThread.Name = "OscThread";
            this._currentThread.IsBackground = true;
            this._currentThread.Start();

            Debug.WriteLine("OSC thread started with OSCQuery");
            Debug.WriteLine($"Receiving on {System.Net.IPAddress.Loopback}:{receivePort}");
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
                            this._currentAvatarId = (string)msg[0];

                            HandleAvatarIdChanged();
                        }
                        else if (this._currentAvatarId != null
                            && this._currentConfig.ContainsKey(this._currentAvatarId)
                            && msg.Address == _currentConfig[this._currentAvatarId].Activate
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

        private void HandleAvatarIdChanged()
        {
            if (this._currentAvatarId == null || this._currentConfig == null)
            {
                throw new Exception("HandleAvatarIdChanged was called with an invalid state");
            }

            this.AvatarChanged?.Invoke(this, new AvatarChangedArgs(this._currentAvatarId));

            this._currentlyActive = false;

            if (this._currentConfig.ContainsKey(this._currentAvatarId))
            {
                // if the activate parameter is set to nothing we are always activated, otherwise we wait for the trigger
                _currentlyActive = (_currentConfig[_currentAvatarId].Activate == "");
            }

            TrackingActiveChanged?.Invoke(this, new TrackingActiveChangedArgs(_currentlyActive, _currentConfig.ContainsKey(_currentAvatarId)));
        }

        public void Stop()
        {
            if (_oscReceiver != null)
            {
                _oscReceiver.Close();
            }
            if (_oscSender != null)
            {
                _oscSender.Close();
            }
            if (_oscQueryService != null)
            {
                _oscQueryService.Dispose();
            }

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
                Debug.WriteLine("SendValues was called with no sender set up. This is not an error in OSCQuery mode");
                return;
            }

            if ((!force && !_currentlyActive) || _currentAvatarId == null || !_currentConfig.ContainsKey(_currentAvatarId))
            {
                return; // discard the message to not spam the game with useless messages
            }

            Debug.WriteLine("[OSC] Sending pos=(" + posX + ", " + posY + ", " + posZ + ") rot=(" + rotX + ", " + rotY + ", " + rotZ + ")");

            _oscSender.Send(new OscMessage(_currentConfig[_currentAvatarId].PositionX, (float)posX));
            _oscSender.Send(new OscMessage(_currentConfig[_currentAvatarId].PositionY, (float)posY));
            _oscSender.Send(new OscMessage(_currentConfig[_currentAvatarId].PositionZ, (float)posZ));
            _oscSender.Send(new OscMessage(_currentConfig[_currentAvatarId].RotationX, (float)rotX));
            _oscSender.Send(new OscMessage(_currentConfig[_currentAvatarId].RotationY, (float)rotY));
            _oscSender.Send(new OscMessage(_currentConfig[_currentAvatarId].RotationZ, (float)rotZ));
        }
    }
}
