using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

            SaveGameData save = saveService.ReadSave();
            LocationDefinition start = Find(save != null ? SaveLocations.Of(save.location) : null);

            IsTravelling = true;
            player.enabled = false;
            yield return LoadContent(start);
            saveService.ApplyAll(save, content, start.Id);
            Traveller.ShowFor(start.Id);
            player.enabled = true;
            IsTravelling = false;
            IsReady = true;
            LocationChanged?.Invoke();
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
            IsTravelling = true;
            player.enabled = false;

            Scene leaving = content;
            // First what leaves with the van (it must not stay in this place's state), then the place is captured.
            beforeUnload?.Invoke(leaving);
            saveService.CaptureLocation(CurrentLocationId, leaving);
            if (leaving.IsValid() && leaving.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(leaving);
            }

            yield return LoadContent(target);
            saveService.ApplyLocation(target.Id, content);
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
        }

        /// <summary>Reloads everything from the core scene (start over, or apply a changed language).</summary>
        public void ReloadAll()
        {
            Time.timeScale = 1f;
            LoadScene(coreScenePath, LoadSceneMode.Single);
        }

        private IEnumerator LoadContent(LocationDefinition location)
        {
            Current = location;

            // A place already open (e.g. the workshop next to the core scene in the Editor) is used as it is.
            Scene existing = SceneManager.GetSceneByPath(location.ScenePath);
            if (!existing.IsValid() || !existing.isLoaded)
            {
                AsyncOperation loading = LoadScene(location.ScenePath, LoadSceneMode.Additive);
                if (loading != null)
                {
                    yield return loading;
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

        /// <summary>A short line for the HUD while travelling.</summary>
        public string TravelText => Current != null
            ? Loc.T($"On the way to {Current.DisplayName} ...", $"{Current.DisplayName} yolunda ...")
            : Loc.T("Loading ...", "Yükleniyor ...");
    }
}
