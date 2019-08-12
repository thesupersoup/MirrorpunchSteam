using Steamworks;

namespace Mirror.Punch
{
    /// <summary>
    /// Represents a Steam client, with a SteamId and connection id
    /// </summary>
    public class SteamPeer
    {
        private SteamId _id;
        private uint _conn;
        private double _timeSince;
        private double _timeout;

        public SteamId SteamId => _id;
        public uint ConnectionId => _conn;
        public double TimeSinceLastPacket => _timeSince;
        public bool HasTimedOut => _timeSince >= _timeout;

        public SteamPeer(SteamId id, uint conn, double timeout)
        {
            _id = id;
            _conn = conn;
            _timeSince = 0.0;
            _timeout = timeout;
        }

        /// <summary>
        /// Add to the time since last packet
        /// </summary>
        public void AddTime(double time)
        {
            _timeSince += time;
        }

        /// <summary>
        /// Reset the time since last packet
        /// </summary>
        public void ResetTime()
        {
            _timeSince = 0.0;
        }
    }
}
