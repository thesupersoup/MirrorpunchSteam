using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using Mirror;
using static Mirror.Punch.MirrorpunchCommon;

namespace Mirror.Punch
{
    /// <summary>
    /// Mirror Transport for Steam using Facepunch.Steamworks
    /// </summary>
    public class MirrorpunchSteam : Transport
    {
        #region Instance vars
        ///--------------------------------------------------------------------
        /// MirrorpunchSteam instance vars
        ///--------------------------------------------------------------------

        [SerializeField]
        [Tooltip("MirrorpunchSteam will attempt to initialize, maintain, and shutdown the Steamworks API")]
        private bool _maintainClient = false;
        [SerializeField]
        [Tooltip("Your game's unique AppId")]
        private uint _appId = 0;
        [SerializeField]
        [Tooltip("Max number of connections if acting as a server")]
        private uint _maxConnections = 5;
        [SerializeField]
        [Tooltip("Rate to poll for packets, in milliseconds")]
        private double _tickRate = TICKRATE_32;
        [SerializeField]
        [Tooltip("Allow or disallow P2P connects to fall back on Steam server relay if direct connection or NAT traversal can't be established")]
        private bool _allowRelay = true;
        [SerializeField]
        [Tooltip("Will cause Available() to return false, regardless of other flags")]
        private bool _enableTransport = true;
        [SerializeField]
        [Tooltip("Will disable transport functionality and cause Available() to return false if SteamClient.IsLoggedOn is false")]
        private bool _forceOnline = false;

        private MirrorpunchServer _server = null;
        private MirrorpunchClient _client = null;

        private bool _eventsRegistered = false;

        private Action<int>                     _svConnected = null;
        private Action<int>                     _svDisconnected = null;
        private Action<int, byte[]>             _svDataReceived = null;
        private Action<int, Exception>          _svError = null;

        private Action                          _clConnected = null;
        private Action                          _clDisconnected = null;
        private Action<byte[]>                  _clDataReceived = null;
        private Action<Exception>               _clError = null;

        #endregion Instance vars

        #region Properties
        ///--------------------------------------------------------------------
        /// MirrorpunchSteam properties
        ///--------------------------------------------------------------------

        /// <summary>
        /// MirrorpunchSteam will attempt to initialize, maintain, and shutdown the Steamworks API
        /// </summary>
        public bool MaintainClient => _maintainClient;

        /// <summary>
        /// Your game's unique AppId
        /// </summary>
        public uint AppId => _appId;

        /// <summary>
        /// Max number of connections if acting as a server
        /// </summary>
        public uint MaxConnections => _maxConnections;

        /// <summary>
        /// Rate to poll for packets, in milliseconds
        /// </summary>
        public double TickRate => _tickRate;

        /// <summary>
        /// Allow or disallow P2P connects to fall back on Steam server relay if direct connection or NAT traversal can't be established
        /// </summary>
        public bool AllowRelay => _allowRelay;

        /// <summary>
        /// Will disable transport functionality and cause Available() to return false, regardless of other flags
        /// </summary>
        public bool EnableTransport => _enableTransport;

        /// <summary>
        /// Will cause Available() to return false if SteamClient.IsLoggedOn is false
        /// </summary>
        public bool ForceOnline => _forceOnline;

        public MirrorpunchServer Server => _server;
        public MirrorpunchClient Client => _client;

        #endregion Properties

        #region Methods
        ///--------------------------------------------------------------------
        /// MirrorpunchSteam methods
        ///--------------------------------------------------------------------

        private void OnEnable()
        {
            if (MaintainClient)
            {
                try
                {
                    if (AppId <= 0)
                        throw new Exception("Mirrorpunch: AppId is invalid");

                    SteamClient.Init(AppId);
                }
                catch (Exception e)
                {
                    string error = $"Mirrorpunch: Exception when initializing SteamClient ({e.Message}), disabling transport";

                    _enableTransport = false;
                    Output.LogError(error);
                }
            }

            if (EnableTransport)
            {
                InitCommon();
                InitServerClient();

                // Register events after server/client objects are initialized
                RegisterMirrorEvents();
            }
        }

        private void Start()
        {
            if (!SteamClient.IsValid)
            {
                _enableTransport = false;
                Output.LogError("Mirrorpunch: SteamClient is invalid in Start, disabling transport");
                return;
            }
        }

        /// <summary>
        /// Initialize common values
        /// </summary>
        private void InitCommon()
        {
            SteamNetworking.AllowP2PPacketRelay(_allowRelay);
            MirrorpunchCommon.SetTickRate(_tickRate);
        }

        /// <summary>
        /// Initialize the server and client objects
        /// </summary>
        private void InitServerClient()
        {
            _server = new MirrorpunchServer(this, _maxConnections);
            _client = new MirrorpunchClient(this);

            _server.Init();
            _client.Init();
        }

        private void RegisterMirrorEvents()
        {
            // Server
            _svConnected = (id) => OnServerConnected?.Invoke(id);
            _svDisconnected = (id) => OnServerDisconnected?.Invoke(id);
            _svDataReceived = (id, data) => OnServerDataReceived?.Invoke(id, new ArraySegment<byte>(data));
            _svError = (id, exception) => OnServerError?.Invoke(id, exception);

            Server.OnConnected += _svConnected;
            Server.OnDisconnected += _svDisconnected;
            Server.OnDataReceived += _svDataReceived;
            Server.OnError += _svError;

            // Client
            _clConnected = () => OnClientConnected?.Invoke();
            _clDisconnected = () => OnClientDisconnected?.Invoke();
            _clDataReceived = (data) => OnClientDataReceived?.Invoke(new ArraySegment<byte>(data));
            _clError = (exception) => OnClientError?.Invoke(exception);

            Client.OnConnected += _clConnected;
            Client.OnDisconnected += _clDisconnected;
            Client.OnDataReceived += _clDataReceived;
            Client.OnError += _clError;

            _eventsRegistered = true;
        }

        private void UnregisterMirrorEvents()
        {
            // Server
            Server.OnConnected -= _svConnected;
            Server.OnDisconnected -= _svDisconnected;
            Server.OnDataReceived -= _svDataReceived;
            Server.OnError -= _svError;

            _svConnected = null;
            _svDisconnected = null;
            _svDataReceived = null;
            _svError = null;

            // Client
            Client.OnConnected -= _clConnected;
            Client.OnDisconnected -= _clDisconnected;
            Client.OnDataReceived -= _clDataReceived;
            Client.OnError -= _clError;

            _clConnected = null;
            _clDisconnected = null;
            _clDataReceived = null;
            _clError = null;

            _eventsRegistered = false;
        }

        /// <summary>
        /// Set transport enabled/disabled
        /// </summary>
        public void SetEnableTransport(bool enabled)
        {
            _enableTransport = enabled;
        }

        /// <summary>
        /// Returns false if the enable transport instance var is false, if SteamClient conditions are not met, and if the platform is WebGL
        /// </summary>
        public override bool Available()
        {
            // Check if the transport is disabled
            if (!_enableTransport)
                return false;

            // Must be logged in to Steam Client
            if (!SteamClient.IsValid)
                return false;

            // If force online is true, then SteamClient.IsLoggedOn must also be true
            if (_forceOnline && !SteamClient.IsLoggedOn)
                return false;

            return Application.platform != RuntimePlatform.WebGLPlayer;
        }

        public override void ClientConnect(string id)
        {
            if(!ulong.TryParse(id, out ulong hostId))
            {
                Output.LogError($"Mirrorpunch: Could not parse string ({id}) into ulong, can't connect");
                return;
            }

            Client.ConnectToId(hostId);
        }

        public override bool ClientConnected()
        {
            return Client.ClientConnected();
        }

        public override void ClientDisconnect()
        {
            Client.ClientDisconnect();
        }

        public override bool ClientSend(int channelId, byte[] data)
        {
            return Client.ClientSend(channelId, data);
        }

        public override int GetMaxPacketSize(int channelId = 0)
        {
            return MirrorpunchCommon.MP_PACKET_MAX;
        }

        /// <summary>
        /// Checks if server is active
        /// </summary>
        public override bool ServerActive()
        {
            return Server.Active;
        }

        public override bool ServerDisconnect(int connectionId)
        {
            return Server.ServerDisconnect(connectionId);
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            return Server.ServerGetClientAddress(connectionId);
        }

        public override bool ServerSend(int connectionId, int channelId, byte[] data)
        {
            return Server.ServerSend(connectionId, channelId, data);
        }

        public override void ServerStart()
        {
            Server.ServerStart();
        }

        public override void ServerStop()
        {
            Server.ServerStop();
        }

        public void LateUpdate ()
        {
            while(EnableTransport)
            {
                ProcessMessages(Client);
                ProcessMessages(Server);
            }
        }

        public override void Shutdown()
        {
            if (_maintainClient)
                SteamClient.Shutdown();

            if (_eventsRegistered)
                UnregisterMirrorEvents();

            _client?.CleanUp();
            _server?.CleanUp();
        }

        #endregion Methods
    }
}
