using System;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace Mirror.Punch
{
    /// <summary>
    /// Represents a client connecting to a server in a P2P scenario
    /// </summary>
    public class MirrorpunchClient : MirrorpunchCommon
    {
        #region Instance vars
        ///--------------------------------------------------------------------
        /// MirrorpunchClient instance vars
        ///--------------------------------------------------------------------

        private uint _connectionId;
        private PeerStatus _status = PeerStatus.OFFLINE;
        private SteamId _host = new SteamId();

        #endregion Instance vars

        #region Actions
        ///--------------------------------------------------------------------
        /// MirrorpunchClient actions
        ///--------------------------------------------------------------------

        public Action OnConnected;
        public Action OnDisconnected;
        public Action<ArraySegment<byte>> OnDataReceived;
        public Action<Exception> OnError;

        #endregion Actions

        #region Properties
        ///--------------------------------------------------------------------
        /// MirrorpunchClient properties
        ///--------------------------------------------------------------------

        public PeerStatus Status => _status;
        public SteamId HostId => _host;

        #endregion Properties

        #region Methods
        ///--------------------------------------------------------------------
        /// MirrorpunchClient methods
        ///--------------------------------------------------------------------

        public MirrorpunchClient (MirrorpunchSteam mp)
        {
            _name = "MirrorpunchClient";
            _mpSteam = mp;
        }

        public async void ConnectToId(SteamId hostId)
        {
            // Refuse to attempt another connection if one is active or in progress
            if (Active || Status == PeerStatus.CONNECTING)
            {
                Output.LogWarning($"{Name}: Attempted to connect while a previous connection is active or in progress");
                return;
            }

            // Set timeout start time, set client active, and set client status to Connecting
            DateTime timeoutStart = DateTime.Now;

            // Set status appropriately
            _active = true;
            _status = PeerStatus.CONNECTING;

            try
            {
                // Send a request to connect
                byte[] sendData = new byte[] { (byte)PacketType.CONNECT };
                SendPacket(hostId, sendData, sendData.Length);

                while (Status == PeerStatus.CONNECTING)
                {
                    // Wait for a packet to arrive
                    while (!IsPacketAvailable())
                    {
                        TimeSpan timeoutChk = DateTime.Now - timeoutStart;

                        if (timeoutChk.TotalMilliseconds >= MP_TIMEOUT)
                            throw new Exception("Timed out attempting to connect");

                        await Task.Delay(TimeSpan.FromMilliseconds(TickRate));
                    }

                    GetNextPacket(out P2Packet? packet);

                    if (packet == null)
                        throw new Exception("Null packet received");

                    // If the packet is not null, get data and sender SteamId
                    byte[] data = packet.Value.Data;
                    SteamId sender = packet.Value.SteamId;

                    if (data.Length < 1 || data == null)
                        throw new Exception("Null or zero-length data array received");

                    // Ignore packets sent by someone who isn't expected
                    if (sender != hostId)
                        continue;

                    if (data[0] != (byte)PacketType.CONNECTION_ACCEPTED)
                        throw new Exception($"Unexpected response ({data[0]})");

                    // Now connected to a new server, set status and refresh receive queue
                    _status = PeerStatus.CONNECTED;
                    _receiveQueue.Clear();

                    // Raise OnConnected event
                    OnConnected?.Invoke();
                }

                _host = hostId;
            }
            catch (Exception e)
            {
                Output.LogError($"{Name}: Error connecting to host ({e.Message})");

                // If we catch an exception, reset active and status
                ResetStatus();
            }
        }

        /// <summary>
        /// Checks if client is connected and active
        /// </summary>
        public bool ClientConnected ()
        {
            return Status == PeerStatus.CONNECTED && Active;
        }

        public void ClientDisconnect ()
        {
            try
            {
                if (Status != PeerStatus.CONNECTED || !HostId.IsValid)
                    throw new Exception("Attempted to disconnect while not connected or HostId is invalid");

                if (!SteamNetworking.CloseP2PSessionWithUser(HostId))
                    throw new Exception($"Unable to disconnect from {HostId}");
            }
            catch(Exception e)
            {
                Output.LogError($"{Name}: Exception in ClientDisconnect ({e.Message})");
            }
            finally
            {
                // Reset status and host id
                _status = PeerStatus.OFFLINE;
                _host = new SteamId();

                // Raise OnDisconnected event
                OnDisconnected?.Invoke();
            }
        }

        /// <summary>
        /// Attempts to send a packet to a connected host, if any; returns success
        /// </summary>
        public bool ClientSend (int channel, byte[] data)
        {
            try
            {
                if (Status != PeerStatus.CONNECTED || !HostId.IsValid)
                    throw new Exception("Not connected to a host, or HostId is invalid; can't send");

                if (channel < 0 || channel >= (int)P2PChannel.NUM_CHANNELS)
                    throw new Exception("Invalid send channel specified");

                return SendPacket(HostId, data, data.Length, (P2PChannel)channel);
            }
            catch (Exception e)
            {
                Output.LogError($"{Name}: Exception in ClientSend ({e.Message})");
                return false;
            }
        }

        protected override bool IsKnown (SteamId id)
        {
            return id == HostId;
        }

        protected override void HandleSessionRequest (SteamId id)
        {
            try
            {
                // Ignore a session request from ourselves
                if (id == SteamClient.SteamId)
                    throw new Exception("Received a session request from ourselves, somehow. There are better ways to handle loneliness, you just gotta reach out.");

                // Ignore a session request if we already have a host
                if (HostId.IsValid)
                    throw new Exception("Received a new session request when we already have a host");

                _host = id;
                AcceptSessionWithUser(id);
            }
            catch (Exception e)
            {
                Output.LogError($"{Name}: Exception in Client.HandleSessionRequest ({e.Message})");
                return;
            }
        }

        protected override void HandleDisconnectRequest (SteamId id)
        {
            try
            {
                // Ignore a disconnect request from an invalid SteamId
                if (!id.IsValid)
                    throw new Exception("Received a disconnect request from an invalid SteamId");

                // Ignore a disconnect request from ourselves
                if (id == SteamClient.SteamId)
                    throw new Exception("Received a disconnect request from ourselves, somehow. That's rough, buddy.");

                // Ignore a disconnect request if we don't have a host
                if (!HostId.IsValid)
                    throw new Exception("Received a disconnect request but we don't have a host");

                // Ignore a disconnect request from someone who isn't our host
                if (id != HostId)
                    throw new Exception("Received a disconnect request from a peer that isn't our current host");

                _host = new SteamId();
                CloseSessionWithUser(id);

                // Raise OnDisconnected event
                OnDisconnected?.Invoke();
            }
            catch (Exception e)
            {
                Output.LogError($"{Name}: Exception in Client.HandleDisconnectRequest ({e.Message})");
                return;
            }
        }

        protected override void HandleConnectionFailed (SteamId id, P2PSessionError error)
        {
            try
            {
                if (!id.IsValid)
                    throw new Exception("An invalid SteamId is reporting connection failure");

                if (!HostId.IsValid)
                    throw new Exception($"Connection failed with {id}, but we didn't have a host anyway...");

                if (id != HostId)
                    throw new Exception($"Connection failed with {id}, but they weren't our host");

                string info = P2PErrorToString(error);

                Exception ex = new Exception($"Connection failed with host {id} ({info})");
                Output.LogError(ex.Message);

                _host = new SteamId();
                CloseSessionWithUser(id);
                ResetStatus();

                // Raise OnError event
                OnError?.Invoke(ex);
            }
            catch (Exception e)
            {
                Output.LogWarning($"{Name}: Exception in Client.HandleConnectionFailed ({e.Message})");
                return;
            }
        }

        protected override void HandleReceivedData (SteamId id, ArraySegment<byte> data)
        {
            try
            {
                if (!id.IsValid)
                    throw new Exception("Received data from an invalid SteamId");

                if (!HostId.IsValid)
                    throw new Exception("Received data from a SteamId, but we don't have a host yet");

                if (id != HostId)
                    throw new Exception("Received data from a SteamId that isn't our current host");

                if (data == null || data.Array.Length == 0)
                    throw new Exception("Data is null or zero-length");

                // Raise OnDataReceived event
                OnDataReceived?.Invoke(data);
            }
            catch (Exception e)
            {
                Output.LogError($"{Name}: Exception in Client.HandleReceivedData ({e.Message})");
                return;
            }
        }

        protected override void ResetStatus ()
        {
            _active = false;
            _status = PeerStatus.OFFLINE;
        }

        #endregion Methods
    }
}
