namespace Mirror.Punch
{
    /// <summary>
    /// Enumerator for different channels to send/receive packets on; matched to Steamworks.P2PSend enum
    /// </summary>
    public enum P2PChannel
    {
        UNRELIABLE = 0,
        UNRELIABLE_NO_DELAY = 1,
        RELIABLE = 2,
        RELIABLE_WITH_BUFFERING = 3,
        NUM_CHANNELS    // Keep at the end for handy enum length hack
    }
}
