using System;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System.Threading.Tasks;

namespace Mirror.Punch
{
    /// <summary>
    /// Wrapper class for common Steamworks networking API calls and Mirrorpunch constant values
    /// </summary>
    public abstract class MirrorpunchCommon
    {
        #region Constants
        ///--------------------------------------------------------------------
        /// MirrorpunchCommon constants
        ///--------------------------------------------------------------------

        public const int MP_TIMEOUT = 15000;                // Amount of time (ms) to wait before triggering a timeout
        public const int MP_PROCESS_WARNING = 1000;         // Amount of time (ms) to trigger a warning in ProcessMessages
        public const int MP_QUEUE_SIZE_WARNING = 75000;     // Triggers a warning if the ReceiveQueue has more than this number of packets waiting
        public const int MP_PACKET_MAX = 65507;             // Maximum packet size in byte; theoretical UDP limit, just want to catch bad actors

        public const double MP_QUEUE_WARNING_TIMEOUT = 10.0;    // Time (seconds) to throw another queue size warning

        // Common tick rates, in milliseconds
        public const double TICKRATE_32 = 31.25,
                            TICKRATE_64 = 15.625,
                            TICKRATE_128 = 7.8125;

        #endregion Constants

        #region Variables
        ///--------------------------------------------------------------------
        /// MirrorpunchCommon variables
        ///--------------------------------------------------------------------

        // Defaults to 32 tick
        protected static double _tickRate = TICKRATE_32;

        protected string _name = "MirrorpunchCommon";
        protected bool _active = false;
        protected MirrorpunchSteam _mpSteam = null;
        protected P2PPacketQueue _receiveQueue = new P2PPacketQueue();

        protected bool _callbacksRegistered = false;

        #endregion Variables

        #region Properties
        ///--------------------------------------------------------------------
        /// MirrorpunchCommon properties
        ///--------------------------------------------------------------------

        public static double TickRate => _tickRate;

        public string Name => _name;
        public bool Active => _active;
        public MirrorpunchSteam MpSteam => _mpSteam;
        public P2PPacketQueue ReceiveQueue => _receiveQueue;

        #endregion Properties

        #region Methods
        ///--------------------------------------------------------------------
        /// MirrorpunchCommon methods
        ///--------------------------------------------------------------------

        /// <summary>
        /// Initialize unique functionality and variables
        /// </summary>
        public virtual void Init()
        {
            Output.Log($"Initializing {Name}");

            if(MpSteam == null)
            {
                Output.LogError($"{Name}: Reference to MirrorpunchSteam is null in Init, can't proceed");
                ResetStatus();
                return;
            }

            RegisterCallbacks();
        }

        /// <summary>
        /// Register pertinent callbacks
        /// </summary>
        public virtual void RegisterCallbacks()
        {
            // Steamworks callbacks
            SteamNetworking.OnP2PSessionRequest += OnSessionRequest;
            SteamNetworking.OnP2PConnectionFailed += OnConnectionFailed;

            _callbacksRegistered = true;
        }

        /// <summary>
        /// Unregister pertinent callbacks
        /// </summary>
        public virtual void UnregisterCallbacks()
        {
            // Steamworks callbacks
            SteamNetworking.OnP2PSessionRequest -= OnSessionRequest;
            SteamNetworking.OnP2PConnectionFailed -= OnConnectionFailed;

            _callbacksRegistered = false;
        }

        /// <summary>
        /// Sets the static tick rate variable, for use by both the server and client
        /// </summary>
        public static bool SetTickRate(double tick)
        {
            if (tick <= 0.0)
                return false;

            _tickRate = tick;
            return true;
        }

        /// <summary>
        /// Called from SteamNetworking callback
        /// </summary>
        public void OnSessionRequest(SteamId id)
        {
            HandleSessionRequest(id);
        }

        protected abstract void HandleSessionRequest (SteamId id);

        public void OnDisconnectRequest(SteamId id)
        {
            HandleDisconnectRequest(id);
        }

        protected abstract void HandleDisconnectRequest (SteamId id);

        /// <summary>
        /// Called from SteamNetworking callback
        /// </summary>
        public void OnConnectionFailed(SteamId id, P2PSessionError error)
        {
            HandleConnectionFailed(id, error);
        }

        protected abstract void HandleConnectionFailed (SteamId id, P2PSessionError error);

        public void OnReceivedData(SteamId id, ArraySegment<byte> data)
        {
            HandleReceivedData(id, data);
        }

        protected abstract void HandleReceivedData (SteamId id, ArraySegment<byte> data);

        protected abstract void ResetStatus ();

        protected abstract bool IsKnown (SteamId id);

        /// <summary>
        /// Should be called in response to the SteamNetworking callback OnP2PSessionRequest
        /// </summary>
        public bool AcceptSessionWithUser (SteamId id) => SteamNetworking.AcceptP2PSessionWithUser(id);

        /// <summary>
        /// Closes the session; subsequent packets received from the specified SteamId will trigger the SteamNetworking callback OnP2PSessionRequest
        /// </summary>
        public bool CloseSessionWithUser (SteamId id) => SteamNetworking.CloseP2PSessionWithUser(id);

        /// <summary>
        /// Returns whether or not the packed could be sent
        /// </summary>
        public bool SendPacket (SteamId id, byte[] data, int len = -1, P2PChannel channel = P2PChannel.RELIABLE)
        {
            if (len > MP_PACKET_MAX || data.Length > MP_PACKET_MAX)
            {
                Output.LogWarning($"{Name}: Attempted to send a packet which exceeded MP_MAX_SIZE!");
                return false;
            }

            if(channel < 0 || channel >= P2PChannel.NUM_CHANNELS)
            {
                Output.LogWarning($"{Name}: Invalid send channel specified, defaulting to Reliable");
                return SteamNetworking.SendP2PPacket(id, data, len, (int)P2PChannel.RELIABLE, P2PSend.Reliable);
            }

            // Cast channel to send type
            P2PSend send = (P2PSend)channel;

            return SteamNetworking.SendP2PPacket(id, data, len, (int)channel, send);
        }

        /// <summary>
        /// Returns whether or not a packet is available on the specified channel
        /// </summary>
        public bool IsPacketAvailable (P2PChannel channel = P2PChannel.RELIABLE)
        {
            return SteamNetworking.IsP2PPacketAvailable((int)channel);
        }

        /// <summary>
        /// Attempts to get the next packet on the specified channel
        /// </summary>
        public bool GetNextPacket (out P2Packet? packet, P2PChannel channel = P2PChannel.RELIABLE)
        {
            packet = null;

            if (IsPacketAvailable(channel))
            {
                packet = ReadPacket(channel);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Asynchronously attempts to get the next packet on the specified channel
        /// </summary>
        public async Task<P2Packet?> GetNextPacketAsync(P2PChannel channel = P2PChannel.RELIABLE)
        {
            DateTime timeoutStart = DateTime.Now;
            P2Packet? packet;

            while (!GetNextPacket(out packet, channel))
            {
                TimeSpan timeoutChk = DateTime.Now - timeoutStart;

                if (timeoutChk.TotalMilliseconds >= MP_TIMEOUT)
                    return null;

                await Task.Delay(TimeSpan.FromMilliseconds(TickRate));
            }

            return packet;
        }

        /// <summary>
        /// Returns null if there is no packet available to be read
        /// </summary>
        private P2Packet? ReadPacket (P2PChannel channel)
        {
            return SteamNetworking.ReadP2PPacket((int)channel);
        }

        /// <summary>
        /// Adds a packet to the receive queue
        /// </summary>
        private void EnqueuePacket (P2Packet packet)
        {
            _receiveQueue.Enqueue(packet);
        }

        /// <summary>
        /// Attempts to dequeue a packet from the receive queue, returns null if none
        /// </summary>
        private P2Packet? DequeuePacket()
        {
            return _receiveQueue.Dequeue();
        }

        /// <summary>
        /// Static loop method for receiving Steam P2P packets and enqueuing them in the receive queue
        /// </summary>
        public static async Task ReceiveLoop (MirrorpunchCommon a)
        {
            if (!a.Active)
                return;   // Don't bother with receive loop if inactive

            bool warned = false;
            DateTime timeWarned = DateTime.Now;

            try
            {
                while (a.Active)
                {
                    for (int i = 0; i < (int)P2PChannel.NUM_CHANNELS; i++)
                    {
                        if (!a.GetNextPacket(out P2Packet? packet, (P2PChannel)i))
                            continue;

                        if (packet == null)
                            continue;

                        int packetLen = packet.Value.Data.Length;

                        if (packetLen >= MP_PACKET_MAX)
                        {
                            Output.LogWarning($"{a.Name}: Received a packet that is too large ({packetLen})");
                            continue;
                        }

                        a.EnqueuePacket((P2Packet)packet);
                    }

                    if (!warned && a.ReceiveQueue.Length > MP_QUEUE_SIZE_WARNING)
                    {
                        Output.LogWarning($"{a.Name}: ReceiveQueue is backing up ({a.ReceiveQueue.Length} packets queued)");
                        timeWarned = DateTime.Now;
                    }

                    if (warned)
                    {
                        TimeSpan timeSinceWarning = DateTime.Now - timeWarned;
                        if (timeSinceWarning.TotalSeconds > MP_QUEUE_WARNING_TIMEOUT)
                            warned = false;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(TickRate));
                }

                // Return if behavior is no longer active
                return;
            }
            catch (Exception e)
            {
                Output.LogError($"{a.Name}: Exception raised in ReceiveLoop ({e.Message})");
                return;   // Return on any exception
            }
        }

        /// <summary>
        /// Static method to process any messages in the receive queue
        /// </summary>
        public static bool ProcessMessages (MirrorpunchCommon a)
        {
            if (!a.Active)
                return false;   // Don't process messages if inactive

            if (!a.ReceiveQueue.HasNodes)
                return true;    // Only return false if something goes wrong

            DateTime timer = DateTime.Now;

            try
            {
                // Want to avoid a potential infinite loop, so only looping through a known number of nodes
                for (int i = 0; i < a.ReceiveQueue.Length; i++)
                {
                    P2Packet? packet = a.ReceiveQueue.Dequeue();

                    if (packet == null)
                        continue;

                    SteamId senderId = packet.Value.SteamId;

                    if (!a.IsKnown(senderId))
                        throw new Exception($"Received a packet from an unexpected SteamId [{senderId}]");

                    if (packet.Value.Data == null || packet.Value.Data.Length == 0)
                        continue;

                    byte byteType = packet.Value.Data[0];

                    if (byteType < 0 || byteType >= (byte)PacketType.NUM_TYPES)
                        throw new Exception("Packet with invalid PacketType received; check sending code?");

                    PacketType packetType = (PacketType)byteType;
                    byte[] data = packet.Value.Data;

                    switch (packetType)
                    {
                        case PacketType.CONNECT:
                            Output.Log($"Connection requested by {senderId}");
                            a.OnSessionRequest(senderId);
                            break;
                        case PacketType.CONNECTION_ACCEPTED:
                            Output.Log($"Connection accepted by {senderId}");
                            break;
                        case PacketType.DATA:
                            if (data.Length == 1)
                                throw new Exception($"Data packet received from {senderId} with length 1; no data attached?");
                            a.OnReceivedData(senderId, new ArraySegment<byte>(data, 1, data.Length - 1));
                            break;
                        case PacketType.DISCONNECT:
                            Output.Log($"Disconnect requested by {senderId}");
                            a.OnDisconnectRequest(senderId);
                            break;
                        default:
                            throw new Exception($"Unexpected packet type received from {senderId} [{byteType}]");
                    }
                }

                TimeSpan checkTimer = DateTime.Now - timer;

                if (checkTimer.TotalMilliseconds >= MP_PROCESS_WARNING)
                    Output.LogWarning($"{a.Name}: ProcessMessages took longer than expected ({checkTimer.TotalMilliseconds} ms)");

                // We made it here without an exception, report success
                return true;
            }
            catch (Exception e)
            {
                Output.LogError($"{a.Name}: Exception in ProcessMessages ({e.Message})");
                return false;
            }
        }

        /// <summary>
        /// Convert a P2PSessionError to a string
        /// </summary>
        public static string P2PErrorToString(P2PSessionError e)
        {
            string error;

            switch(e)
            {
                case P2PSessionError.None:
                    error = "None";
                    break;
                case P2PSessionError.NotRunningApp:
                    error = "Not running app";
                    break;
                case P2PSessionError.NoRightsToApp:
                    error = "No rights to app";
                    break;
                case P2PSessionError.DestinationNotLoggedIn:
                    error = "Destination not logged in";
                    break;
                case P2PSessionError.Timeout:
                    error = "Timeout";
                    break;
                case P2PSessionError.Max:
                    error = "Max";
                    break;
                default:
                    error = "Unknown error";
                    break;
            }

            return error;
        }

        /// <summary>
        /// Call when shutting down the transport
        /// </summary>
        public void CleanUp()
        {
            if (_callbacksRegistered)
                UnregisterCallbacks();
        }
        #endregion Methods
    }
}
