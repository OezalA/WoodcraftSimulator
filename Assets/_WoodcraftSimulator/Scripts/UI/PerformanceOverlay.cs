using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using WoodcraftSimulator.World;

namespace WoodcraftSimulator.UI
{
    /// <summary>
    /// Performance overlay (Phase 13f, D-064): F3 shows frames per second, frame times (average and worst of the last
    /// second), draw calls, triangles and memory in the lower left; while it is shown, a summary goes to the log every
    /// <see cref="logSeconds"/> ("[Perf] ..."), so a play session can be read afterwards from the Editor log. Render
    /// counters exist in the Editor and development builds only (elsewhere they read 0). A measuring tool, not a
    /// setting: off by default, not saved.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PerformanceOverlay : MonoBehaviour
    {
        [Tooltip("Seconds between two log summaries while the overlay is shown.")]
        [SerializeField, Min(1f)] private float logSeconds = 10f;

        private Label label;
        private bool shown;
        private ProfilerRecorder batches;
        private ProfilerRecorder setPassCalls;
        private ProfilerRecorder triangles;
        private ProfilerRecorder systemMemory;

        // The last second (display) and the current log period.
        private float secondStart;
        private int secondFrames;
        private float secondWorst;
        private float logStart;
        private int logFrames;
        private float logWorst;
        private int logSlowFrames;
        private readonly StringBuilder text = new StringBuilder(256);

        private void OnEnable()
        {
            label = new Label { name = "performance-overlay", pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.left = 8f;
            label.style.bottom = 8f;
            label.style.paddingLeft = 8f;
            label.style.paddingRight = 8f;
            label.style.paddingTop = 4f;
            label.style.paddingBottom = 4f;
            label.style.fontSize = 13f;
            label.style.color = Color.white;
            label.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            label.style.display = DisplayStyle.None;
            GetComponent<UIDocument>().rootVisualElement.Add(label);
        }

        private void OnDisable()
        {
            label?.RemoveFromHierarchy();
            StopRecorders();
            shown = false;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            {
                Toggle();
            }

            if (!shown)
            {
                return;
            }

            float frame = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            secondFrames++;
            secondWorst = Mathf.Max(secondWorst, frame);
            logFrames++;
            logWorst = Mathf.Max(logWorst, frame);
            if (frame > 1f / 30f)
            {
                logSlowFrames++;
            }

            if (now - secondStart >= 1f)
            {
                ShowSecond(now - secondStart);
                secondStart = now;
                secondFrames = 0;
                secondWorst = 0f;
            }

            if (now - logStart >= logSeconds)
            {
                LogPeriod(now - logStart);
                logStart = now;
                logFrames = 0;
                logWorst = 0f;
                logSlowFrames = 0;
            }
        }

        private void Toggle()
        {
            shown = !shown;
            label.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (shown)
            {
                batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
                setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
                systemMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
                secondStart = logStart = Time.unscaledTime;
                secondFrames = logFrames = logSlowFrames = 0;
                secondWorst = logWorst = 0f;
                label.text = "…";
                Debug.Log($"[Perf] Overlay on at {Place()} ({Screen.width}×{Screen.height}, {SystemInfo.graphicsDeviceName}).");
            }
            else
            {
                StopRecorders();
            }
        }

        private void ShowSecond(float seconds)
        {
            float fps = secondFrames / Mathf.Max(0.001f, seconds);
            text.Clear();
            text.Append($"{fps:0} FPS   {1000f / Mathf.Max(1f, fps):0.0} ms   worst {secondWorst * 1000f:0.0} ms\n");
            text.Append($"draw calls {Value(batches)}   set-pass {Value(setPassCalls)}   triangles {Thousands(Value(triangles))}\n");
            text.Append($"memory {Value(systemMemory) / (1024 * 1024)} MB   {Place()}   [F3]");
            label.text = text.ToString();
        }

        private void LogPeriod(float seconds)
        {
            float fps = logFrames / Mathf.Max(0.001f, seconds);
            Debug.Log($"[Perf] {Place()}: {fps:0.0} FPS avg, worst frame {logWorst * 1000f:0.0} ms, " +
                      $"{logSlowFrames} of {logFrames} frames over 33 ms; draw calls {Value(batches)}, " +
                      $"set-pass {Value(setPassCalls)}, triangles {Value(triangles)}, " +
                      $"memory {Value(systemMemory) / (1024 * 1024)} MB.");
        }

        private static string Place()
        {
            SceneFlow flow = Core.Services.Get<SceneFlow>();
            return flow != null && flow.Current != null ? flow.Current.Id : "?";
        }

        private static long Value(ProfilerRecorder recorder) => recorder.Valid ? recorder.LastValue : 0L;

        private static string Thousands(long value) => value >= 1000 ? $"{value / 1000}k" : value.ToString();

        // Only recorders that were started (a default recorder has nothing to dispose).
        private void StopRecorders()
        {
            if (!batches.Valid && !systemMemory.Valid)
            {
                return;
            }

            batches.Dispose();
            setPassCalls.Dispose();
            triangles.Dispose();
            systemMemory.Dispose();
        }
    }
}
