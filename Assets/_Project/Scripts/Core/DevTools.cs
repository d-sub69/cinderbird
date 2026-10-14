using UnityEngine;

namespace Cinderbird.Core
{
    /// <summary>
    /// Whether testing aids work in this build: debug keys (F3 overlay, F4 layers, F5/F9/F8 saves) and Test Mode.
    /// On in the Editor, in development builds and in the performance-test build (Cinderbird → Build); off in the
    /// release build players get, so nobody can wipe their save with one key.
    /// </summary>
    public static class DevTools
    {
        /// <summary>Scripting define the performance-test build adds to an otherwise normal release build.</summary>
        public const string PerfTestDefine = "CINDERBIRD_DEV_TOOLS";

        public static bool Enabled
        {
            get
            {
#if CINDERBIRD_DEV_TOOLS
                return true;
#else
                return Debug.isDebugBuild;
#endif
            }
        }
    }
}
