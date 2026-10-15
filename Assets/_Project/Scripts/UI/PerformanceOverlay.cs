using System.Text;
using Cinderbird.Core;
using Cinderbird.Save;
using Cinderbird.Terrain;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.UI
{
    /// <summary>
    /// Debug read-out for the performance budget, toggled with a key (F3), also written to the log every 5 s as a
    /// "[Perf]" line while shown: frame times since the last log line
    /// (average, 95th percentile, worst), main-thread and GPU time, garbage per frame, and the dig, save and
    /// start-up timings. Values over the <see cref="PerformanceBudget"/> are red. Rebuilt a few times per second
    /// without allocating, so it doesn't distort the garbage figure it shows.
    /// </summary>
    public sealed class PerformanceOverlay : MonoBehaviour
    {
        private const float RefreshInterval = 0.25f;
        private const float LogInterval = 5f;
        // Room for the frames between two log lines (5 s) even at very high frame rates.
        private const int FrameWindow = 4096;
        private const string Red = "<color=#FF5A4A>";
        private const string EndColor = "</color>";
        // A frame slower than this is written down with what the game was busy with (perf.log).
        private const float HitchMilliseconds = 40f;
        // At most this many hitch lines per log interval, so a bad stretch doesn't flood the log.
        private const int MaxHitchLinesPerInterval = 20;

        [SerializeField] private TMP_Text _text;
        [SerializeField] private VoxelTerrain _terrain;
        [SerializeField] private InputActionReference _toggleAction;
        [SerializeField] private bool _visibleOnStart = true;
        [Tooltip("Optional: values over budget are shown in red.")]
        [SerializeField] private PerformanceBudget _budget;
        [Tooltip("Optional: shows the main-thread time of the last save.")]
        [SerializeField] private SaveController _save;
        [Tooltip("Optional: shows how long start-up took.")]
        [SerializeField] private GameBootstrap _bootstrap;
        [Tooltip("Optional: shows the main-thread time of settling ash.")]
        [SerializeField] private AshSettling _settling;

        private readonly FrameStats _frames = new FrameStats(FrameWindow);
        private readonly FrameStats _mainThread = new FrameStats(FrameWindow);
        private readonly FrameStats _gpu = new FrameStats(FrameWindow);
        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private readonly StringBuilder _builder = new StringBuilder(512);
        private ProfilerRecorder _gcRecorder;
        private long _worstGcBytes;
        private string[] _qualityNames;
        private bool _visible;
        private float _nextRefreshTime;

        // For the log line: what happened since the previous one.
        private readonly StringBuilder _logBuilder = new StringBuilder(512);
        private float _nextLogTime;
        private int _skipGcFrames;
        private long _worstGcSinceLog;
        private int _gcFramesSinceLog;
        private int _digsSinceLog;
        private float _worstDigSinceLog;
        private float _worstMeshSinceLog;
        private int _mostChunksSinceLog;
        private float _worstListenersSinceLog;
        private bool _listenerTimePending;
        private int _savesSinceLog;
        private float _worstSaveSinceLog;
        private float _lastSeenSave;
        private int _lastSeenSettleUpdate;
        private int _settleUpdatesSinceLog;
        private float _worstSettleSinceLog;
        private float _worstSettleWorkSinceLog;
        private float _worstSettleCommitSinceLog;
        private float _worstSettleMeshSinceLog;
        private float _lastSettleMilliseconds;
        private int _lastSeenUpload;
        private int _lastSeenColliderRound;
        private float _worstColliderSinceLog;
        private float _worstUploadSinceLog;
        private bool _pausedOrLoading;

        // Slow frames, written to perf.log with what they were busy with.
        private readonly StringBuilder _hitchBuilder = new StringBuilder(256);
        private FrameCostRecorders _costs;
        private PerfLogFile _logFile;
        private int _hitchLinesSinceLog;

        private void Awake()
        {
            if (_text == null || _terrain == null || _toggleAction == null)
            {
                Debug.LogError($"{nameof(PerformanceOverlay)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            if (!DevTools.Enabled)
            {
                // A testing aid: players get neither the overlay nor its log lines.
                _text.enabled = false;
                enabled = false;
                return;
            }

            // QualitySettings.names builds a new array on every call; read it once.
            _qualityNames = QualitySettings.names;
            SetVisible(_visibleOnStart);
            _logFile = new PerfLogFile(Application.persistentDataPath);
        }

        private void OnEnable()
        {
            if (_toggleAction != null)
            {
                _toggleAction.action.Enable();
            }

            // Counted by Unity in the Editor and in builds; reports 0 where the counter isn't available.
            _gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _costs = new FrameCostRecorders();
            if (_terrain != null)
            {
                _terrain.Dug += OnDug;
            }
        }

        private void OnDisable()
        {
            _gcRecorder.Dispose();
            _costs?.Dispose();
            _costs = null;
            if (_terrain != null)
            {
                _terrain.Dug -= OnDug;
            }
        }

        private void Update()
        {
            if (_toggleAction.action.WasPressedThisFrame())
            {
                SetVisible(!_visible);
            }

            Sample();
            if (!_visible)
            {
                return;
            }

            if (Time.unscaledTime >= _nextLogTime)
            {
                _nextLogTime = Time.unscaledTime + LogInterval;
                WriteLogLine();
            }

            if (Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = Time.unscaledTime + RefreshInterval;
            Rebuild();
        }

        private void OnDug(DigResult result, Vector3 center, float radius)
        {
            _digsSinceLog++;
            _worstDigSinceLog = Mathf.Max(_worstDigSinceLog, _terrain.LastDigMilliseconds);
            _worstMeshSinceLog = Mathf.Max(_worstMeshSinceLog, _terrain.LastMeshMilliseconds);
            _mostChunksSinceLog = Mathf.Max(_mostChunksSinceLog, _terrain.LastMeshedChunkCount);
            // The listener time is only known after every listener (this one included) has run: read it next frame.
            _listenerTimePending = true;
        }

        // One line in the log (Player.log in a build) every few seconds, so a test run can be read afterwards
        // instead of copied off the screen. Building the line allocates, so the garbage figure skips the next
        // frames rather than report the overlay's own logging.
        private void WriteLogLine()
        {
            StringBuilder b = _logBuilder;
            b.Clear();
            int level = QualitySettings.GetQualityLevel();
            b.Append("[Perf] t=").AppendInt(Mathf.RoundToInt(Time.unscaledTime)).Append("s ")
                .Append(level >= 0 && level < _qualityNames.Length ? _qualityNames[level] : "?")
                .Append(QualitySettings.vSyncCount > 0 ? " vsync=on " : " vsync=off ")
                .AppendInt(Screen.width).Append('x').AppendInt(Screen.height)
                .Append(_pausedOrLoading ? " (paused/loading)" : string.Empty);

            float average = _frames.Average;
            b.Append(" | fps=").AppendFixed(average > 0f ? 1000f / average : 0f, 0)
                .Append(" frame=").AppendFixed(average, 1).Append(" p95=").AppendFixed(_frames.Percentile(0.95f), 1)
                .Append(" worst=").AppendFixed(_frames.Max, 1);
            b.Append(" | cpu=").AppendFixed(_mainThread.Average, 1).Append(" p95=").AppendFixed(_mainThread.Percentile(0.95f), 1);
            b.Append(" | gpu=").AppendFixed(_gpu.Average, 1).Append(" p95=").AppendFixed(_gpu.Percentile(0.95f), 1);
            b.Append(" | gc_worst=").AppendInt(_worstGcSinceLog).Append("B gc_frames=").AppendInt(_gcFramesSinceLog);
            b.Append(" | digs=").AppendInt(_digsSinceLog).Append(" dig_max=").AppendFixed(_worstDigSinceLog, 1).Append("ms")
                .Append(" mesh_max=").AppendFixed(_worstMeshSinceLog, 1).Append("ms chunks_max=").AppendInt(_mostChunksSinceLog)
                .Append(" after_dig_max=").AppendFixed(_worstListenersSinceLog, 1).Append("ms");
            b.Append(" | saves=").AppendInt(_savesSinceLog).Append(" save_max=").AppendFixed(_worstSaveSinceLog, 1).Append("ms");
            b.Append(" | settles=").AppendInt(_settleUpdatesSinceLog).Append(" settle_max=").AppendFixed(_worstSettleSinceLog, 1)
                .Append("ms (write ").AppendFixed(_worstSettleCommitSinceLog, 1).Append(" mesh ").AppendFixed(_worstSettleMeshSinceLog, 1)
                .Append(") upload_max=").AppendFixed(_worstUploadSinceLog, 1)
                .Append("ms collider_max=").AppendFixed(_worstColliderSinceLog, 1)
                .Append("ms work_max=").AppendFixed(_worstSettleWorkSinceLog, 1).Append("ms");
            if (_bootstrap != null)
            {
                b.Append(" | start=").AppendFixed(_bootstrap.StartupSeconds, 2).Append('s');
            }

            string line = b.ToString();
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, this, "{0}", line);
            _logFile?.WriteLine(line);
            _hitchLinesSinceLog = 0;
            _digsSinceLog = 0;
            _worstDigSinceLog = 0f;
            _worstMeshSinceLog = 0f;
            _mostChunksSinceLog = 0;
            _worstListenersSinceLog = 0f;
            _savesSinceLog = 0;
            _worstSaveSinceLog = 0f;
            _settleUpdatesSinceLog = 0;
            _worstSettleSinceLog = 0f;
            _worstSettleWorkSinceLog = 0f;
            _worstSettleCommitSinceLog = 0f;
            _worstSettleMeshSinceLog = 0f;
            _worstUploadSinceLog = 0f;
            _worstColliderSinceLog = 0f;
            _worstGcSinceLog = 0;
            _gcFramesSinceLog = 0;
            _skipGcFrames = 2;
            _pausedOrLoading = false;
            // Each line covers its own interval, so lines can be compared step by step.
            _frames.Clear();
            _mainThread.Clear();
            _gpu.Clear();
        }

        private void Sample()
        {
            float frameMilliseconds = Time.unscaledDeltaTime * 1000f;
            _frames.Add(frameMilliseconds);
            if (frameMilliseconds > HitchMilliseconds && Time.timeScale > 0f && Time.frameCount > 10)
            {
                WriteHitch(frameMilliseconds);
            }

            _pausedOrLoading |= Time.timeScale == 0f;
            if (_skipGcFrames > 0)
            {
                _skipGcFrames--;
            }
            else if (_gcRecorder.Valid)
            {
                long bytes = _gcRecorder.LastValue;
                _worstGcBytes = System.Math.Max(_worstGcBytes, bytes);
                _worstGcSinceLog = System.Math.Max(_worstGcSinceLog, bytes);
                if (bytes > 0)
                {
                    _gcFramesSinceLog++;
                }
            }

            if (_listenerTimePending)
            {
                _listenerTimePending = false;
                _worstListenersSinceLog = Mathf.Max(_worstListenersSinceLog, _terrain.LastDugListenersMilliseconds);
            }

            if (_settling != null && _settling.UpdateCount != _lastSeenSettleUpdate)
            {
                _lastSeenSettleUpdate = _settling.UpdateCount;
                _settleUpdatesSinceLog++;
                _lastSettleMilliseconds = _settling.LastUpdateMilliseconds;
                _worstSettleSinceLog = Mathf.Max(_worstSettleSinceLog, _lastSettleMilliseconds);
                _worstSettleWorkSinceLog = Mathf.Max(_worstSettleWorkSinceLog, _settling.LastWorkMilliseconds);
                _worstSettleCommitSinceLog = Mathf.Max(_worstSettleCommitSinceLog, _settling.LastCommitMilliseconds);
                _worstSettleMeshSinceLog = Mathf.Max(_worstSettleMeshSinceLog, _settling.LastMeshMilliseconds);
            }

            if (_terrain.ColliderRoundCount != _lastSeenColliderRound)
            {
                _lastSeenColliderRound = _terrain.ColliderRoundCount;
                _worstColliderSinceLog = Mathf.Max(_worstColliderSinceLog, _terrain.LastColliderMilliseconds);
            }

            // Sliding ash is meshed on workers and uploaded a frame later; that upload is main-thread time too.
            if (_terrain.UploadCount != _lastSeenUpload)
            {
                _lastSeenUpload = _terrain.UploadCount;
                _worstUploadSinceLog = Mathf.Max(_worstUploadSinceLog, _terrain.LastUploadMilliseconds);
            }

            // A save happened when its timing changes; the value itself is that save's main-thread time.
            if (_save != null && _save.LastCaptureMilliseconds != _lastSeenSave)
            {
                _lastSeenSave = _save.LastCaptureMilliseconds;
                _savesSinceLog++;
                _worstSaveSinceLog = Mathf.Max(_worstSaveSinceLog, _lastSeenSave);
            }

            // Needs Project Settings → Player → Frame Timing Stats; without it nothing is captured.
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) == 0)
            {
                return;
            }

            FrameTiming timing = _timings[0];
            // The main thread's own work: its frame time minus the time it waited to present (VSync, GPU). With VSync on the
            // wait can be reported for another frame than the frame time (it came out negative); the frame time alone is
            // then the closer figure. Measure with VSync off for exact numbers.
            float work = (float)(timing.cpuMainThreadFrameTime - timing.cpuMainThreadPresentWaitTime);
            _mainThread.Add(work >= 0f ? work : (float)timing.cpuMainThreadFrameTime);
            if (timing.gpuFrameTime > 0d)
            {
                _gpu.Add((float)timing.gpuFrameTime);
            }
        }

        private void Rebuild()
        {
            StringBuilder b = _builder;
            b.Clear();

            float average = _frames.Average;
            b.AppendFixed(average > 0f ? 1000f / average : 0f, 0).Append(" FPS   frame ").AppendFixed(average, 1)
                .Append(" ms   p95 ").AppendFixed(_frames.Percentile(0.95f), 1)
                .Append("   worst ").AppendFixed(_frames.Max, 1).AppendLine();

            b.Append("CPU ");
            AppendMilliseconds(_mainThread, _budget != null ? _budget.MainThreadMilliseconds : 0f);
            b.Append("   GPU ");
            AppendMilliseconds(_gpu, _budget != null ? _budget.GpuMilliseconds : 0f);
            b.Append("   GC ");
            bool gcOver = _budget != null && _worstGcBytes > _budget.GcBytesPerFrame;
            AppendColored(gcOver).AppendInt(_worstGcBytes).Append(" B/frame");
            EndColored(gcOver).AppendLine();

            b.Append("Dig ");
            AppendTimed(_terrain.LastDigMilliseconds, _budget != null ? _budget.DigMilliseconds : 0f, 1).Append(" ms   Mesh ")
                .AppendFixed(_terrain.LastMeshMilliseconds, 1).Append(" ms / ").AppendInt(_terrain.LastMeshedChunkCount).Append(" chunks");
            if (_save != null)
            {
                b.Append("   Save ");
                AppendTimed(_save.LastCaptureMilliseconds, _budget != null ? _budget.SaveCaptureMilliseconds : 0f, 1).Append(" ms");
            }

            if (_settling != null)
            {
                b.Append("   Settle ");
                AppendTimed(_lastSettleMilliseconds, _budget != null ? _budget.SettleMilliseconds : 0f, 1).Append(" ms");
            }

            if (_bootstrap != null && _bootstrap.StartupSeconds > 0f)
            {
                b.Append("   Start ");
                AppendTimed(_bootstrap.StartupSeconds, _budget != null ? _budget.StartupSeconds : 0f, 2).Append(" s");
            }

            b.AppendLine();
            int level = QualitySettings.GetQualityLevel();
            b.Append(level >= 0 && level < _qualityNames.Length ? _qualityNames[level] : "?")
                .Append(QualitySettings.vSyncCount > 0 ? "   VSync on   " : "   VSync off   ")
                .AppendInt(Screen.width).Append(" x ").AppendInt(Screen.height);

            _text.SetText(b);
            // TextMeshPro may turn the new text into a string internally; keep that out of the garbage figure.
            _skipGcFrames = System.Math.Max(_skipGcFrames, 2);
            // Worst garbage per refresh window, so a single allocating frame isn't missed between refreshes.
            _worstGcBytes = 0;
        }

        private void AppendMilliseconds(FrameStats stats, float budget)
        {
            if (stats.Count == 0)
            {
                _builder.Append("n/a");
                return;
            }

            float p95 = stats.Percentile(0.95f);
            AppendTimed(stats.Average, budget, 1).Append(" ms (p95 ");
            AppendTimed(p95, budget, 1).Append(')');
        }

        private StringBuilder AppendTimed(float value, float budget, int decimals)
        {
            bool over = budget > 0f && value > budget;
            AppendColored(over).AppendFixed(value, decimals);
            return EndColored(over);
        }

        private StringBuilder AppendColored(bool over)
        {
            return over ? _builder.Append(Red) : _builder;
        }

        private StringBuilder EndColored(bool over)
        {
            return over ? _builder.Append(EndColor) : _builder;
        }

        // The frame that just ended was slow: which of the game's own systems took the time (and Unity's garbage
        // collector). Anything not listed is Unity itself (physics, rendering) or something not yet measured.
        private void WriteHitch(float frameMilliseconds)
        {
            if (_hitchLinesSinceLog >= MaxHitchLinesPerInterval)
            {
                return;
            }

            _hitchLinesSinceLog++;
            StringBuilder b = _hitchBuilder;
            b.Clear();
            b.Append("[Hitch] t=").AppendFixed(Time.unscaledTime, 1).Append("s frame=").AppendFixed(frameMilliseconds, 1).Append("ms |");
            _costs?.AppendLastFrame(b);
            b.Append(" | settling=").Append(_settling != null && _settling.IsSettling ? "yes" : "no");
            string line = b.ToString();
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, this, "{0}", line);
            _logFile?.WriteLine(line);
            // Writing the line allocated; keep that out of the garbage figure.
            _skipGcFrames = System.Math.Max(_skipGcFrames, 2);
        }

        private void OnDestroy()
        {
            _logFile?.Dispose();
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            _text.enabled = visible;
            _frames.Clear();
            _mainThread.Clear();
            _gpu.Clear();
        }
    }
}
