using System;
using UnityEngine;

namespace Mirror.Punch
{
    /// <summary>
    /// Sets console logging methods in a decoupled manner
    /// </summary>
    public static class Output
    {
        public static Action<string> Log = Debug.Log;
        public static Action<string> LogWarning = Debug.LogWarning;
        public static Action<string> LogError = Debug.LogError;
    }
}
