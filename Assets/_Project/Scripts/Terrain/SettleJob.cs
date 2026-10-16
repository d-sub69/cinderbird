using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// One area of ash settling, as <see cref="AshSettling"/> runs it: its simulation, where it is, whether it still
    /// has to be loaded, and for a scoop the drain that takes its ash out over the pour time.
    /// </summary>
    public sealed class SettleJob
    {
        public SettleJob(SettleParameters parameters)
        {
            Settler = new AshSettler(parameters);
        }

        public AshSettler Settler { get; }

        /// <summary>The columns it loads and changes; jobs whose areas touch never run at the same time.</summary>
        public SettleArea Area { get; private set; }

        public Vector3 Center { get; private set; }

        public float Radius { get; private set; }

        /// <summary>Not loaded yet: the worker loads it on its next run (or the main thread, if it must finish now).</summary>
        public bool NeedsLoading { get; set; }

        public bool IsScoop { get; private set; }

        /// <summary>Whether the last batch of steps still moved ash.</summary>
        public bool Moving { get; set; }

        /// <summary>A scoop whose Dug hasn't been reported yet (its ash isn't all out of the pile).</summary>
        public bool ScoopPending { get; set; }

        public DigResult ScoopResult { get; private set; }

        public float ScoopStartTime { get; private set; }

        public float ScoopSeconds { get; private set; }

        /// <summary>What the scoop grazed, laid out when its hole is open.</summary>
        public DigExposure ScoopExposure { get; private set; }

        /// <summary>Everything this slide changed so far, for one glimmer refresh when it ends.</summary>
        public Bounds SlideBounds { get; private set; }

        public bool HasSlideBounds { get; private set; }

        public void StartSlide(SettleArea area, Vector3 center, float radius)
        {
            Area = area;
            Center = center;
            Radius = radius;
            NeedsLoading = true;
            IsScoop = false;
            Moving = true;
            ScoopPending = false;
            ScoopExposure = null;
            HasSlideBounds = false;
        }

        public void StartScoop(SettleArea area, Vector3 center, float radius, DigResult result, float startTime, float seconds,
            DigExposure exposure)
        {
            StartSlide(area, center, radius);
            ScoopExposure = exposure;
            IsScoop = true;
            ScoopPending = true;
            ScoopResult = result;
            ScoopStartTime = startTime;
            ScoopSeconds = Mathf.Max(0.01f, seconds);
        }

        /// <summary>Loads the area into the simulation; on the worker, or on the main thread when it must finish now.</summary>
        public void Load(AshTerrain terrain)
        {
            if (!NeedsLoading)
            {
                return;
            }

            NeedsLoading = false;
            if (IsScoop)
            {
                Settler.BeginScoop(terrain, Center, Radius);
            }
            else
            {
                Settler.Begin(terrain, Center, Radius);
            }
        }

        public void AddChanged(Bounds changed)
        {
            if (HasSlideBounds)
            {
                Bounds bounds = SlideBounds;
                bounds.Encapsulate(changed);
                SlideBounds = bounds;
            }
            else
            {
                SlideBounds = changed;
                HasSlideBounds = true;
            }
        }
    }
}
