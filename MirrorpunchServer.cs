using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

namespace Mirror.Punch
{
    /// <summary>
    /// Represents a server in a P2P scenario
    /// </summary>
    public class MirrorpunchServer : MirrorpunchCommon
    {
        #region Instance vars
        ///--------------------------------------------------------------------
        /// MirrorpunchServer instance vars
        ///--------------------------------------------------------------------

        private int _connectionId = 0;
        private uint _maxConnections;
        private ServerStatus _status = ServerStatus.OFFLINE;
        private PlayerRegistry _playerRegistry;

        #endregion Instance vars

        #region Actions
        ///--------------------------------------------------------------------
        /// MirrorpunchServer actions
        ///--------------------------------------------------------------------

        public Action<int> OnConnected;
        public Action<int> OnDisconnected;
        public Action<int, ArraySegment<byte>> OnDataReceived;
        public Action<int, Exception> OnError;

        #endregion Actions

        #region Properties
        ///--------------------------------------------------------------------
        /// MirrorpunchServer properties
        ///--------------------------------------------------------------------

        public ServerStatus Status => _status;
        public PlayerRegistry PlayerRegistry => _playerRegistry;

        #endregion Properties

        #region Methods
        ///--------------------------------------------------------------------
        /// MirrorpunchServer methods
        ///--------------------------------------------------------------------

        public MirrorpunchServer (MirrorpunchSteam mp, uint max)
        {
            _name = "MirrorpunchServer";
            _maxConnections = max;
            _mpSteam = mp;
            _playerRegistry = new PlayerRegistry(_maxConnections);
        }

        protected override bool IsKnown (SteamId id)
        {
            return PlayerRegistry.Contains(id);
        }

        protected override void HandleSessionRequest (SteamId id)
        {
            try
            {
                // Ignore a session request from ourselves
                if (id == SteamClient.SteamId)
                    throw new Exception("Received a session request from ourselves, somehow. There are better ways to handle loneliness, you just gotta reach out.");

                // Ignore a session request if we already have this client in the registry
                if (PlayerRegistry.Contains(id))
                    throw new Exception("Received a new session request from a peer that's already in the registry");

                int conn = GetConnectionId();

                PlayerRegistry.Add(id, conn);
                AcceptSessionWithUser(id);

                // Raise OnConnected event
                OnConnected?.Invoke(conn);
            }
            catch (Exception e)
            {
                Output.LogError($"{Name}: Exception in Server.HandleSessionRequest ({e.Message})");
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

                // Ignore a disconnect request if we already have this client in the registry
                if (!PlayerRegistry.Contains(id))
                    throw new Exception("Received a disconnect request from a peer that's not in the registry");

                int conn = PlayerRegistry.GetConnId(id);

                // If no connection id could be found for that SteamId
                if (conn == -1)
                    throw new Exception($"Invalid connection id for SteamId {id}, can't disconnect");

                PlayerRegistry.Remove(id);
                CloseSessionWithUser(id);

                // Raise OnDisconnected event
                OnDisconnected?.Invoke(conn);
            }
            catch (Exception e)
            {
                Output.LogError($"{Name}: Exception in Server.HandleDisconnectRequest ({e.Message})");
                return;
            }
        }

        protected override void HandleConnectionFailed (SteamId id, P2PSessionError error)
        {
            try
            {
                if (!id.IsValid)
                    throw new Exception("An invalid SteamId is reporting connection failure");

                if (!PlayerRegistry.Contains(id))
                    throw new Exception("Received connection failure message from a peer that isn't in the player registry");

                int conn = PlayerRegistry.GetConnId(id);

                // If no connection id could be found for that SteamId
                if (conn == -1)
                    throw new Exception($"Invalid connection id for SteamId {id}, can't disconnect");

                string info = P2PErrorToString(error);

                Exception ex = new Exception($"Connection failed with host {id} ({info})");
                Output.LogError(ex.Message);

                PlayerRegistry.Remove(id);
                CloseSessionWithUser(id);

                // Raise OnError event
                OnError?.Invoke(conn, ex);
            }
            catch (Exception e)
            {
                Output.LogWarning($"{Name}: Exception in Server.HandleConnectionFailed ({e.Message})");
                return;
            }
        }

        protected override void HandleReceivedData (SteamId id, ArraySegment<byte> data)
        {
            try
            {
                if (!id.IsValid)
                    throw new Exception("Received data from an invalid SteamId");

                if (data == null || data.Array.Length == 0)
                    throw new Exception("Data is null or zero-length");

                int conn = PlayerRegistry.GetConnId(id);

                if (conn == -1)
                    throw new Exception("Received data from a SteamId that isn't in the player registry");

                // Raise OnDataReceived event
                OnDataReceived?.Invoke(conn, data);
            }
            catch (Exception e)
            {
                Output.LogError($"{Name}: Exception in Server.HandleReceivedData ({e.Message})");
                return;
            }
        }

        protected override void ResetStatus ()
        {
            _active = false;
            _status = ServerStatus.OFFLINE;
        }

        /// <summary>
        /// Gets the current connection id and iterates the value
        /// </summary>
        public int GetConnectionId()
        {
            int id = _connectionId;
            _connectionId++;
            return id;
        }

        public bool ServerDisconnect(int conn)
        {
            try
            {
                SteamId id = PlayerRegistry.GetSteamId(conn);

                if (!id.IsValid)
                    throw new Exception($"No valid SteamId found in player registry for connection id {conn}");

                PlayerRegistry.Remove(id, conn);
                CloseSessionWithUser(id);

                return true;
            }
            catch (Exception e)
            {
                Output.LogError($"{Name}: Exception in ServerDisconnect ({e.Message})");
                return false;
            }
        }

        public string ServerGetClientAddress(int conn)
        {
            try
            {
                SteamId id = PlayerRegistry.GetSteamId(conn);

                if (!id.IsValid)
                    throw new Exception($"No valid SteamId found in player registry for connection id {conn}");

                return id.ToString();
            }
            catch (Exception e)
            {
                Output.LogError($"{Name}: Exception in ServerGetClientAddress ({e.Message})");
                return null;
            }
        }

        /// <summary>
        /// Attempts to send a packet to a connected client, if any; returns success
        /// </summary>
        public bool ServerSend (int conn, int channel, byte[] data)
        {
            try
            {
                if (!Active)
                    throw new Exception("Server is not active, can't send packet");

                if (channel < 0 || channel >= (int)P2PChannel.NUM_CHANNELS)
                    throw new Exception("Invalid send channel specified");

                SteamId id = PlayerRegistry.GetSteamId(conn);

                if (!id.IsValid)
                    throw new Exception($"No valid SteamId found in player registry for connection id {conn}");

                return SendPacket(id, data, data.Length, (P2PChannel)channel);
            }
            catch (Exception e)
            {
                Output.LogError($"{Name}: Exception in ServerSend ({e.Message})");
                return false;
            }
        }

        /// <summary>
        /// Starts the server and the receive loop
        /// </summary>
        public async void ServerStart ()
        {
            // Set status to active and listening
            _active = true;
            _status = ServerStatus.LISTENING;

            // Enter the receive loop, until an exception is caught
            await ReceiveLoop(this);

            ServerStop();
        }

        /// <summary>
        /// Stops the server
        /// </summary>
        public void ServerStop ()
        {
            ResetStatus();

            // Close any user sessions
            foreach(SteamId id in PlayerRegistry.SteamDict.Keys)
                CloseSessionWithUser(id);

            // Clear the registry
            PlayerRegistry.Clear();
        }

        #endregion Methods
    }
}
