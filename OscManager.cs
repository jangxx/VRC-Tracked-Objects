﻿using Rug.Osc;
using System;
using System.Collections.Generic;
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
    }

    internal class OscManager
    {
        private CancellationTokenSource CancelTokenSource = new CancellationTokenSource();
        private Thread? currentThread = null;
        private AvatarParams? currentConfig = null;
        private bool currentlyActive = false;
        private OscSender? oscSender;
        private OscReceiver? oscReceiver;

        public EventHandler? TrackingActiveChanged;

        public void Start(string inputAddress, int inputPort, string outputAddress, int outputPort, AvatarParams avParams)
        {
            oscSender = new OscSender(System.Net.IPAddress.Parse(outputAddress), outputPort);
            oscReceiver = new OscReceiver(System.Net.IPAddress.Parse(inputAddress), inputPort);
            currentConfig = avParams;

            currentThread = new Thread(new ThreadStart(ListenThread));
        }

        public void ListenThread()
        {
            if (oscReceiver == null || currentConfig == null) throw new Exception("ListenThread was set up incorrectly");

            try
            {
                while(oscReceiver.State != OscSocketState.Closed)
                {
                    if (oscReceiver.State == OscSocketState.Connected)
                    {
                        OscPacket packet = oscReceiver.Receive();
                        OscMessage msg = OscMessage.Parse(packet.ToString());
                        
                        if (msg.Address == currentConfig.Activate && msg.Count > 0)
                        {
                            bool activate = (bool)msg[0];

                            this.currentlyActive = activate;

                            var args = new TrackingActiveChangedArgs() { Active = activate };
                            var handler = TrackingActiveChanged;
                            handler?.Invoke(this, args);
                        }

                        // everything else is ignored
                    }
                }
            }
            catch(Exception e)
            {
                if (oscReceiver.State == OscSocketState.Connected)
                {
                    MessageBox.Show("OSC thread encountered an unexpected error: " + e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void Stop()
        {
            if (oscReceiver != null)
            {
                oscReceiver.Close();
            }
            if (oscSender != null)
            {
                oscSender.Close();
            }

            oscReceiver = null;
            oscSender = null;

            currentConfig = null;

            if (currentThread != null)
            {
                currentThread.Join();
            }
            currentThread = null;
        }

        public void SendValues(float posX, float posY, float posZ, float rotX, float rotY, float rotZ)
        {
            if (this.oscSender == null || this.currentConfig == null)
            {
                throw new Exception("SendValues was called without the OSC manager being set up");
            }

            if (!this.currentlyActive)
            {
                return; // discard the message to not spam the game with useless messages
            }

            oscSender.Send(new OscMessage(this.currentConfig.PositionX, posX));
            oscSender.Send(new OscMessage(this.currentConfig.PositionY, posY));
            oscSender.Send(new OscMessage(this.currentConfig.PositionZ, posZ));
            oscSender.Send(new OscMessage(this.currentConfig.RotationX, rotX));
            oscSender.Send(new OscMessage(this.currentConfig.RotationY, rotY));
            oscSender.Send(new OscMessage(this.currentConfig.RotationZ, rotZ));
        }
    }
}