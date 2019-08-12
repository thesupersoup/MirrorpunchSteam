namespace Mirror.Punch
{
    /// <summary>
    /// Enumeration of different packet types
    /// </summary>
    public enum PacketType : byte
    {
        CONNECT = 0,
        CONNECTION_ACCEPTED,
        DATA,
        DISCONNECT,
        NUM_TYPES
    }
}
