using System.Collections.Generic;
using UnityEngine;
using Steamworks;

namespace Mirror.Punch
{
    /// <summary>
	/// Used to keep track of players by SteamId and connection id
	/// </summary>
	public class PlayerRegistry
	{
        private uint _maxConnections = 0;
		private Dictionary<SteamId, int> _steamDict = new Dictionary<SteamId, int>();
        private Dictionary<int, SteamId> _connDict = new Dictionary<int, SteamId>();

        public uint Max => _maxConnections;
        public Dictionary<SteamId, int> SteamDict => _steamDict;
        public Dictionary<int, SteamId> ConnDict => _connDict;
        public int SteamCount => _steamDict.Count;
        public int ConnCount => _connDict.Count;


        public PlayerRegistry(uint max)
        {
            _maxConnections = max;
        }

        /// <summary>
        /// Attempts to add a player to the registry;
        /// returns false if the key exists or if the dictionary has reached max capacity
        /// </summary>
        public bool Add(SteamId steam, int conn)
        {
            if (_steamDict.ContainsKey(steam) || _connDict.ContainsKey(conn))
                return false;

            if (SteamCount >= Max || ConnCount >= Max)
                return false;

            _steamDict.Add(steam, conn);
            _connDict.Add(conn, steam);

            return true;
        }

        /// <summary>
        /// Attempts to remove a player entry from the registry by SteamId
        /// </summary>
        public bool Remove(SteamId steam)
        {
            if (!_steamDict.TryGetValue(steam, out int conn))
            {
                Output.LogError($"Error fetching connection id from SteamId dictionary");
                return false;
            }

            return Remove(steam, conn);
        }

        /// <summary>
        /// Attempts to remove a player entry from the registry by connection id
        /// </summary>
        public bool Remove(int conn)
        {
            if (!_connDict.TryGetValue(conn, out SteamId steam))
            {
                Output.LogError($"Error fetching connection id from SteamId dictionary");
                return false;
            }

            return Remove(steam, conn);
        }

        /// <summary>
        /// Fully remove a player from the registry
        /// </summary>
        public bool Remove(SteamId steam, int conn)
        {
            return _steamDict.Remove(steam) && _connDict.Remove(conn);
        }

        /// <summary>
        /// Check if the registry contains the specified SteamId
        /// </summary>
        public bool Contains(SteamId steam)
        {
            return _steamDict.ContainsKey(steam);
        }

        /// <summary>
        /// Check if the registry contains the specified connection id
        /// </summary>
        public bool Contains(int conn)
        {
            return _connDict.ContainsKey(conn);
        }

        /// <summary>
        /// Check if the player is fully registered
        /// </summary>
        public bool Contains(SteamId steam, int conn)
        {
            return _steamDict.ContainsKey(steam) && _connDict.ContainsKey(conn);
        }

        /// <summary>
        /// Tries to get SteamId from connection id; returns an invalid SteamId if none found
        /// </summary>
        public SteamId GetSteamId(int conn)
        {
            if (_connDict.TryGetValue(conn, out SteamId id))
                return id;

            return new SteamId();
        }

        /// <summary>
        /// Tries to get connection id from SteamId; returns -1 if none found
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public int GetConnId(SteamId id)
        {
            if (_steamDict.TryGetValue(id, out int conn))
                return conn;

            return -1;
        }

        /// <summary>
        /// Clears the player registry
        /// </summary>
        public void Clear()
        {
            _steamDict.Clear();
            _connDict.Clear();
        }

        public void CleanUp()
        {
            Clear();
        }
	}
}
