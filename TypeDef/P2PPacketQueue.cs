using System;
using Steamworks;
using Steamworks.Data;

namespace Mirror.Punch
{
    /// <summary>
    /// Nodes for a P2PPacket queue
    /// </summary>
    public sealed class P2PPacketNode : IDisposable
    {
        private P2Packet _packet;
        private SteamId _id;
        private P2PPacketNode _next = null;

        public P2Packet Packet
        {
            get { return _packet; }
            set { _packet = value; }
        }

        public SteamId Id
        {
            get { return _id; }
            set { _id = value; }
        }

        public P2PPacketNode Next
        {
            get { return _next; }
            set { _next = value; }
        }

        public P2PPacketNode(P2Packet packet)
        {
            _packet = packet;
            _id = packet.SteamId;
        }

        public void Dispose()
        {
            _next = null;
        }
    }

    /// <summary>
    /// Queue implementation for P2PPackets
    /// </summary>
    public class P2PPacketQueue
    {
        private P2PPacketNode _head = null;
        private P2PPacketNode _tail = null;
        private int _len = 0;

        private object _lock = new object();    // Threadsafe lock obj

        public int Length => _len;
        public bool HasNodes => _head != null;

        /// <summary>
        /// Add a P2PPacket to the queue
        /// </summary>
        public void Enqueue (P2Packet packet)
        {
            lock (_lock)    // Lock for thread safety
            {
                P2PPacketNode newNode = new P2PPacketNode(packet);

                _len++;

                if (_head == null)
                {
                    _head = newNode;
                    _tail = newNode;
                    return;
                }

                _tail.Next = newNode;
                _tail = newNode;
            }
        }

        /// <summary>
        /// Remove a P2PPacket from the queue
        /// </summary>
        public P2Packet? Dequeue ()
        {
            lock (_lock)    // Lock for thread safety
            {
                // Only human; probably unnecessary, but safer or something
                if (_len > 0)
                    _len--;

                if (_head != null)
                {
                    P2PPacketNode node = _head;
                    P2Packet packet = _head.Packet;
                    _head = _head.Next;
                    node.Dispose();
                    return packet;
                }

                return null;
            }
        }

        /// <summary>
        /// Clear all nodes from the queue
        /// </summary>
        public void Clear ()
        {
            object clearLock = new object();

            lock (clearLock)
            {
                P2Packet? packet = Dequeue();

                while (packet != null)
                    packet = Dequeue ();

                 // Guarantee clearing of head, tail, and length
                _head = null;
                _tail = null;
                _len = 0;
            }
        }
    }
}
