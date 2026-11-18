using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using UnityEngine.SceneManagement;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Persistence;
using WoodcraftSimulator.Player;

namespace WoodcraftSimulator.World
{
    /// <summary>
    /// Which place is loaded (Phase 12.5, D-056). Lives in the core scene with the player and the services; the
    /// workshop and each customer location are content scenes loaded additively and made the active scene (their
    /// light, sky and fog apply, new objects such as cut pieces go there). On start it reads the save, loads the place
    /// the player was at and applies the save; <see cref="Travel"/> leaves one place for another (its state is captured
    /// first, so it is found again as it was left).
    /// </summary>
    public sealed class SceneFlow : MonoBehaviour
    {
        [Tooltip("Every place; the first is where a new game starts (the workshop).")]
        [SerializeField] private LocationDefinition[] locations = Array.Empty<LocationDefinition>();

        [Tooltip("The core scene's own path, to start over (reload everything).")]
        [SerializeField] private string coreScenePath = "Assets/_WoodcraftSimulator/Scenes/Core/Core.unity";

        [SerializeField] private SaveGameService saveService;
        [SerializeField] private PlayerController player;

        private Scene content;

        public IReadOnlyList<LocationDefinition> Locations => locations;

        /// <summary>The place loaded now (null while the first one is still loading).</summary>
        public LocationDefinition Current { get; private set; }

        public string CurrentLocationId => Current != null ? Current.Id : LocationDefinition.WorkshopId;

        /// <summary>The content scene is loaded and the save applied.</summary>
        public bool IsReady { get; private set; }

        /// <summary>Loading a place (the player can't act meanwhile).</summary>
        public bool IsTravelling { get; private set; }

        /// <summary>Where the player is going (or the first place loading), for the travel screen (D-062).</summary>
        public LocationDefinition Destination { get; private set; }

        /// <summary>How far the trip is, 0..1: leaving the place, loading the next one, setting it up (D-062).</summary>
        public float Progress { get; private set; }

        /// <summary>The first place after starting (or reloading) the game; false for a trip with the van.</summary>
        public bool IsFirstLoad => !IsReady;

        /// <summary>Raised after a place was loaded and set up.</summary>
        public event Action LocationChanged;

        public LocationDefinition Find(string id) =>
            Array.Find(locations, each => each != null && each.Id == id) ?? (locations.Length > 0 ? locations[0] : null);

        private IEnumerator Start()
        {
            if (saveService == null || player == null || locations.Length == 0 || locations[0] == null)
            {
                Debug.LogError($"{nameof(SceneFlow)}: Save Service, Player and at least the workshop location must be assigned.", this);
                yield break;
            }

            var timer = Stopwatch.StartNew();
            SaveGameData save = saveService.ReadSave();
            LocationDefinition start = Find(save != null ? SaveLocations.Of(save.location) : null);
            long read = timer.ElapsedMilliseconds;

            Destination = start;
            Progress = 0f;
            IsTravelling = true;
            player.enabled = false;
            yield return LoadContent(start, 0f, 0.9f);
            long loaded = timer.ElapsedMilliseconds;
            saveService.ApplyAll(save, content, start.Id);
            Progress = 1f;
            Core.Services.Get<Economy.WorkshopEconomy>()?.DeliverAllPending(); // bought while away (D-056)
            Traveller.ShowFor(start.Id);
            player.enabled = true;
            IsTravelling = false;
            IsReady = true;
            LocationChanged?.Invoke();
            Debug.Log($"[Perf] Start at '{start.Id}': read save {read} ms, load scene {loaded - read} ms, " +
                      $"apply save {timer.ElapsedMilliseconds - loaded} ms, total {timer.ElapsedMilliseconds} ms.");
        }

        /// <summary>Leaves the current place for another (its state is kept in the save data).</summary>
        public void Travel(LocationDefinition target, Action<Scene> beforeUnload = null, Action<Scene> afterLoad = null)
        {
            if (!IsReady || IsTravelling || target == null || target == Current)
            {
                return;
            }

            StartCoroutine(TravelRoutine(target, beforeUnload, afterLoad));
        }

        private IEnumerator TravelRoutine(LocationDefinition target, Action<Scene> beforeUnload, Action<Scene> afterLoad)
        {
            Destination = target;
            Progress = 0f;
            IsTravelling = true;
            player.enabled = false;
            var timer = Stopwatch.StartNew();
            string from = CurrentLocationId;

            Scene leaving = content;
            // First what leaves with the van (it must not stay in this place's state), then the place is captured.
            beforeUnload?.Invoke(leaving);
            saveService.CaptureLocation(CurrentLocationId, leaving);
            long captured = timer.ElapsedMilliseconds;
            Progress = 0.1f;
            if (leaving.IsValid() && leaving.isLoaded)
            {
                AsyncOperation unloading = SceneManager.UnloadSceneAsync(leaving);
                while (unloading != null && !unloading.isDone)
                {
                    Progress = Mathf.Lerp(0.1f, 0.3f, unloading.progress);
                    yield return null;
                }
            }

            long unloaded = timer.ElapsedMilliseconds;
            yield return LoadContent(target, 0.3f, 0.9f);
            long loaded = timer.ElapsedMilliseconds;
            saveService.ApplyLocation(target.Id, content);
            Progress = 1f;
            Core.Services.Get<Economy.WorkshopEconomy>()?.DeliverAllPending(); // material waiting for this place
            Traveller.ShowFor(target.Id);
            afterLoad?.Invoke(content);

            PlaceInfo info = InfoOf(content);
            if (info != null)
            {
                Transform arrival = info.ArrivalPoint;
                player.Teleport(arrival.position, arrival.eulerAngles.y, 0f);
            }

            player.enabled = true;
            IsTravelling = false;
            LocationChanged?.Invoke();
            Debug.Log($"[Perf] Travel '{from}' → '{target.Id}': capture {captured} ms, unload {unloaded - captured} ms, " +
                      $"load scene {loaded - unloaded} ms, set up {timer.ElapsedMilliseconds - loaded} ms, " +
                      $"total {timer.ElapsedMilliseconds} ms.");
        }

        /// <summary>Reloads everything from the core scene (start over, or apply a changed language).</summary>
        public void ReloadAll()
        {
            Time.timeScale = 1f;
            LoadScene(coreScenePath, LoadSceneMode.Single);
        }

        private IEnumerator LoadContent(LocationDefinition location, float progressFrom, float progressTo)
        {
            Current = location;

            // A place already open (e.g. the workshop next to the core scene in the Editor) is used as it is.
            Scene existing = SceneManager.GetSceneByPath(location.ScenePath);
            if (!existing.IsValid() || !existing.isLoaded)
            {
                AsyncOperation loading = LoadScene(location.ScenePath, LoadSceneMode.Additive);
                while (loading != null && !loading.isDone)
                {
                    Progress = Mathf.Lerp(progressFrom, progressTo, loading.progress);
                    yield return null;
                }

                existing = SceneManager.GetSceneByPath(location.ScenePath);
            }

            content = existing;
            if (content.IsValid())
            {
                SceneManager.SetActiveScene(content);
            }
            else
            {
                Debug.LogError($"[{nameof(SceneFlow)}] Could not load '{location.ScenePath}' ({location.DisplayName}).", this);
            }
        }

        /// <summary>
        /// Loads a scene by path. In the Editor this works for scenes that are not in Build Settings (they aren't: the
        /// owner adds them for a player build, D-056); a player build needs them there.
        /// </summary>
        private static AsyncOperation LoadScene(string path, LoadSceneMode mode)
        {
#if UNITY_EDITOR
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(mode));
#else
            return SceneManager.LoadSceneAsync(path, mode);
#endif
        }

        private static PlaceInfo InfoOf(Scene scene)
        {
            if (!scene.IsValid())
            {
                return null;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                PlaceInfo info = root.GetComponentInChildren<PlaceInfo>(true);
                if (info != null)
                {
                    return info;
                }
            }

            return null;
        }

        /// <summary>A short line for the travel screen.</summary>
        public string TravelText => IsFirstLoad || Destination == null
            ? Loc.T("Loading ...", "Yükleniyor ...")
            : Loc.T($"On the way to {Destination.DisplayName} ...", $"{Destination.DisplayName} yolunda ...");
    }
}
