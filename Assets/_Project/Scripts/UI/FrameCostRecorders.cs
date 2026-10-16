using System;
using System.Text;
using Unity.Profiling;

namespace Cinderbird.UI
{
    /// <summary>
    /// Reads, frame by frame, how long the game's own profiler markers took (dig, meshing, colliders, settling,
    /// glimmers, saving, the helper's searches) and Unity's garbage collection, so a slow frame can say what it was busy with. Values are for
    /// the last finished frame; markers not recorded in this build read 0. Dispose when done.
    /// </summary>
    public sealed class FrameCostRecorders : IDisposable
    {
        private static readonly string[] MarkerNames =
        {
            "Cinderbird.Dig", "Cinderbird.Mesh", "Cinderbird.Colliders", "Cinderbird.Settle", "Cinderbird.SettleWait",
            "Cinderbird.Glimmers", "Cinderbird.LooseEmbers", "Cinderbird.Save", "Cinderbird.Helper",
        };

        private static readonly string[] ShortNames = { "dig", "mesh", "colliders", "settle", "settle_wait", "glimmers", "loose", "save", "helper" };

        private readonly ProfilerRecorder[] _markers = new ProfilerRecorder[MarkerNames.Length];
        private ProfilerRecorder _gc;

        public FrameCostRecorders()
        {
            for (int i = 0; i < MarkerNames.Length; i++)
            {
                _markers[i] = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, MarkerNames[i]);
            }

            _gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Collect");
            if (!_gc.Valid)
            {
                _gc.Dispose();
                _gc = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "GC.Collect");
            }
        }

        /// <summary>Appends " dig=0.1 mesh=2.3 ..." for the last finished frame, in milliseconds.</summary>
        public void AppendLastFrame(StringBuilder builder)
        {
            for (int i = 0; i < _markers.Length; i++)
            {
                builder.Append(' ').Append(ShortNames[i]).Append('=').AppendFixed(Milliseconds(_markers[i]), 1);
            }

            builder.Append(" gc=").AppendFixed(Milliseconds(_gc), 1);
        }

        public void Dispose()
        {
            for (int i = 0; i < _markers.Length; i++)
            {
                _markers[i].Dispose();
            }

            _gc.Dispose();
        }

        private static float Milliseconds(ProfilerRecorder recorder)
        {
            return recorder.Valid ? recorder.LastValue / 1_000_000f : 0f;
        }
    }
}
