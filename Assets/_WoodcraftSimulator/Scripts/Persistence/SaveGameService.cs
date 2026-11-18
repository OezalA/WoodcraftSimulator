using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Cutting;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Inventory;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;
using WoodcraftSimulator.Player;
using WoodcraftSimulator.Tools;
using WoodcraftSimulator.Workshop;
using WoodcraftSimulator.World;

namespace WoodcraftSimulator.Persistence
{
    /// <summary>
    /// Saves and loads the game (D-036): one JSON file in <see cref="Application.persistentDataPath"/>. An invalid file
    /// is never applied: it is moved aside and a new game starts.
    /// Since Phase 12.5 (D-056) the game spans places (the workshop, customer locations), each a content scene next to
    /// the core scene. The save keeps every place: its entries (boards, assemblies, sites, bins, switches, cut lists)
    /// carry their place. <see cref="world"/> is the whole save in memory; the place loaded now is captured from the
    /// scene (<see cref="CaptureLocation"/>) when it is left or the game is saved, and applied when it loads
    /// (<see cref="ApplyLocation"/>). Global state (player, belt, tools and stepladder with the place they lie at,
    /// money, career, jobs, upgrades, sawdust) is captured and applied as a whole. <see cref="SceneFlow"/> drives it.
    /// </summary>
    public sealed class SaveGameService : MonoBehaviour
    {
        private const string FileName = "workshop.json";

        [SerializeField] private TimberBoardFactory boardFactory;
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerToolBelt toolBelt;

        [Tooltip("Optional: the sawdust total.")]
        [SerializeField] private WorkshopWaste waste;

        [Tooltip("Optional: the job service (active job, offers, career).")]
        [SerializeField] private JobBoard jobBoard;

        [Tooltip("Optional: money and fastener stock (D-040).")]
        [SerializeField] private WorkshopEconomy economy;

        [Tooltip("Optional: equipment upgrades owned (D-044).")]
        [SerializeField] private WorkshopUpgrades upgrades;

        // The whole save in memory: every place's last captured state plus the global state.
        private SaveGameData world = new SaveGameData();
        private bool ready;
        private bool restarting;

        public string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Short human-readable result of the last load/save (for the pause menu).</summary>
        public string LastStatus { get; private set; } = Loc.T("Not saved yet", "Henüz kaydedilmedi");

        /// <summary>Raised after <see cref="LastStatus"/> changed.</summary>
        public event Action StatusChanged;

        // Exiting Play Mode in the Editor also calls this.
        private void OnApplicationQuit()
        {
            if (enabled && ready && !restarting)
            {
                Save();
            }
        }

        public bool Save()
        {
            if (!ready)
            {
                return false;
            }

            try
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                SaveGameData data = Capture();
                long captured = timer.ElapsedMilliseconds;
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                long serialized = timer.ElapsedMilliseconds;
                WriteAtomically(SavePath, json);
                Debug.Log($"[Perf] Save: capture {captured} ms, JSON {serialized - captured} ms, " +
                          $"write {timer.ElapsedMilliseconds - serialized} ms, {json.Length / 1024} KB.");
                SetStatus(Loc.T($"Saved {DateTime.Now:HH:mm}", $"Kaydedildi {DateTime.Now:HH:mm}"));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{nameof(SaveGameService)}] Saving failed: {exception}", this);
                SetStatus(Loc.T("Saving failed (see Console)", "Kayıt başarısız (Console'a bak)"));
                return false;
            }
        }

        /// <summary>Deletes the save and reloads everything: a new game.</summary>
        public void StartNewWorkshop()
        {
            restarting = true;
            try
            {
                if (File.Exists(SavePath))
                {
                    File.Delete(SavePath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{nameof(SaveGameService)}] Could not delete the save: {exception}", this);
            }

            Core.Services.Get<SceneFlow>()?.ReloadAll();
        }

        /// <summary>Saves, then reloads everything from that save (e.g. after a language change).</summary>
        public void SaveAndReload()
        {
            if (!Save())
            {
                return;
            }

            restarting = true;
            Core.Services.Get<SceneFlow>()?.ReloadAll();
        }

        /// <summary>
        /// Reads, migrates and validates the save; null if there is none or it is invalid (then moved aside). Called by
        /// <see cref="SceneFlow"/> before any place is loaded (the save says which one).
        /// </summary>
        public SaveGameData ReadSave()
        {
            string path = SavePath;
            if (boardFactory == null || player == null || toolBelt == null)
            {
                Debug.LogError($"{nameof(SaveGameService)}: Board Factory, Player and Tool Belt must be assigned.", this);
                return null;
            }

            if (!File.Exists(path))
            {
                SetStatus(Loc.T("New workshop (no save yet)", "Yeni atölye (henüz kayıt yok)"));
                return null;
            }

            SaveGameData data;
            try
            {
                data = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Reject(path, new[] { $"Unreadable file: {exception.Message}" });
                return null;
            }

            if (!SaveFormat.TryMigrate(data, out string migrationError))
            {
                Reject(path, new[] { migrationError });
                return null;
            }

            IReadOnlyList<string> errors = SaveFormat.Validate(data, boardFactory.IsKnownSpecies);
            if (errors.Count > 0)
            {
                Reject(path, errors);
                return null;
            }

            SetStatus(Loc.T($"Loaded save from {FormatSavedAt(data.savedAtUtc)}", $"Kayıt yüklendi: {FormatSavedAt(data.savedAtUtc)}"));
            return data;
        }

        /// <summary>
        /// Applies a save after its place has loaded (null = a new game: the scenes stay as built). Order: money and
        /// upgrades, the jobs (they set the stations' blueprints), the place (boards, sites, bins, assemblies, ...),
        /// then the tools, stepladder and belt, the sawdust and the player.
        /// </summary>
        public void ApplyAll(SaveGameData data, Scene content, string locationId)
        {
            world = data ?? new SaveGameData();
            if (data == null)
            {
                ready = true;
                return;
            }

            if (economy != null)
            {
                economy.Restore(data.economy);
                economy.RestorePending(ToPending(data.pendingDeliveries));
            }

            if (upgrades != null)
            {
                upgrades.Restore(data.upgrades);
            }

            if (jobBoard != null && data.job != null)
            {
                jobBoard.Restore(data.job, data.offers, data.career);
            }

            ApplyLocation(locationId, content);
            ApplyItemsAndBelt(data);
            ApplyProps(data);

            if (waste != null && data.waste != null)
            {
                waste.Log.Restore(data.waste.cutCount, data.waste.volumeCubicMeters, data.waste.massKg);
            }

            if (SaveLocations.Of(data.location) == locationId)
            {
                Quaternion yaw = ToRotation(data.player.pose);
                player.Teleport(ToPosition(data.player.pose), yaw.eulerAngles.y, data.player.pitch);
            }

            ready = true;
        }

        /// <summary>
        /// Puts a place's saved state into its freshly loaded scene. A place that was never captured (first visit, a
        /// new game) stays as built.
        /// </summary>
        public void ApplyLocation(string locationId, Scene scene)
        {
            if (!WasCaptured(locationId))
            {
                return;
            }

            foreach (Apprentice apprentice in InScene<Apprentice>(scene))
            {
                apprentice.ResetToHome();
            }

            // The scene's own boards are replaced by the saved ones (cut pieces included).
            foreach (TimberBoard existing in InScene<TimberBoard>(scene))
            {
                Destroy(existing.gameObject);
            }

            var boards = new Dictionary<string, Carryable>();
            foreach (BoardData boardData in SaveLocations.At(world.boards, locationId, b => b.location))
            {
                boardFactory.TryGetSpecies(boardData.speciesId, out WoodSpeciesDefinition definition);
                TimberPiece piece = SaveFormat.ToPiece(boardData, definition.ToDomain(), out BoardMarks marks);
                TimberBoard board = boardFactory.Create(boardData.id, definition, piece, marks,
                    ToPosition(boardData.pose), ToRotation(boardData.pose));
                board.SetSurface((BoardSurface)boardData.surface);
                if (board.gameObject.scene != scene)
                {
                    SceneManager.MoveGameObjectToScene(board.gameObject, scene);
                }

                // A board saved while carried was in the air: let it fall (supported ones don't move, D-031).
                // Installed boards are exempt: the station below takes them over.
                var carryable = board.GetComponent<Carryable>();
                carryable.RequestSupportCheck();
                boards[boardData.id] = carryable;
            }

            // Before the assemblies: the site gates where piers may stand.
            RestoreSites(locationId, scene);
            RestoreBins(locationId, scene);

            Dictionary<string, AssemblyStation> stations = ById<AssemblyStation>(scene);
            foreach (AssemblyData assembly in SaveLocations.At(world.assemblies, locationId, a => a.location))
            {
                if (stations.TryGetValue(assembly.stationId, out AssemblyStation station) && station.Build != null)
                {
                    station.RestoreState(assembly, id => boards.TryGetValue(id, out Carryable board) ? board : null);
                }
                else
                {
                    Warn($"assembly station '{assembly.stationId}' is not at '{locationId}'; its saved assembly is skipped.");
                }
            }

            Dictionary<string, LightSwitch> lightSwitches = ById<LightSwitch>(scene);
            foreach (SwitchData switchData in SaveLocations.At(world.switches, locationId, s => s.location))
            {
                if (lightSwitches.TryGetValue(switchData.id, out LightSwitch lightSwitch))
                {
                    lightSwitch.SetOn(switchData.on);
                }
            }

            Dictionary<string, CutList> cutLists = ById<CutList>(scene);
            foreach (CutListData cutListData in SaveLocations.At(world.cutLists, locationId, c => c.location))
            {
                if (cutLists.TryGetValue(cutListData.id, out CutList cutList))
                {
                    cutList.Restore(cutListData.selectedIndex);
                }
            }
        }

        /// <summary>Takes a place's state from its scene into the save data (before it unloads, or to save).</summary>
        public void CaptureLocation(string locationId, Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            // A piece in the apprentice's hands goes into its bin first (D-052).
            foreach (Apprentice apprentice in InScene<Apprentice>(scene))
            {
                apprentice.FinishNow();
            }

            var boards = new List<BoardData>();
            foreach (TimberBoard board in InScene<TimberBoard>(scene))
            {
                if (board.Piece == null || !board.TryGetComponent(out PersistentId id))
                {
                    continue;
                }

                BoardData boardData = SaveFormat.ToData(id.Id, board.Piece, board.Marks);
                boardData.pose = ToPose(board.transform);
                boardData.surface = (int)board.Surface;
                boardData.location = locationId;
                boards.Add(boardData);
            }

            var assemblies = new List<AssemblyData>();
            foreach (KeyValuePair<string, AssemblyStation> station in ById<AssemblyStation>(scene))
            {
                if (station.Value.Build != null)
                {
                    AssemblyData assembly = station.Value.CaptureState(station.Key);
                    assembly.location = locationId;
                    assemblies.Add(assembly);
                }
            }

            var switches = new List<SwitchData>();
            foreach (KeyValuePair<string, LightSwitch> lightSwitch in ById<LightSwitch>(scene))
            {
                switches.Add(new SwitchData { id = lightSwitch.Key, on = lightSwitch.Value.IsOn, location = locationId });
            }

            var cutLists = new List<CutListData>();
            foreach (KeyValuePair<string, CutList> cutList in ById<CutList>(scene))
            {
                cutLists.Add(new CutListData { id = cutList.Key, selectedIndex = cutList.Value.SelectedIndex, location = locationId });
            }

            var sites = new List<SiteData>();
            foreach (KeyValuePair<string, SitePreparation> site in ById<SitePreparation>(scene))
            {
                sites.Add(new SiteData { id = site.Key, done = site.Value.Capture(), location = locationId });
            }

            // Offcut bins and their pieces (D-052).
            var bins = new List<BinData>();
            foreach (KeyValuePair<string, OffcutBin> bin in ById<OffcutBin>(scene))
            {
                var pieces = new List<BoardData>();
                foreach (StoredPiece stored in bin.Value.Stock.Pieces)
                {
                    BoardData piece = SaveFormat.ToData(string.Empty, stored.Piece, new BoardMarks(stored.Piece.Dimensions.Length));
                    piece.surface = (int)stored.Surface;
                    pieces.Add(piece);
                }

                bins.Add(new BinData { id = bin.Key, pieces = pieces.ToArray(), location = locationId });
            }

            world.boards = SaveLocations.Replace(world.boards, locationId, b => b.location, boards);
            world.assemblies = SaveLocations.Replace(world.assemblies, locationId, a => a.location, assemblies);
            world.switches = SaveLocations.Replace(world.switches, locationId, s => s.location, switches);
            world.cutLists = SaveLocations.Replace(world.cutLists, locationId, c => c.location, cutLists);
            world.sites = SaveLocations.Replace(world.sites, locationId, s => s.location, sites);
            world.bins = SaveLocations.Replace(world.bins, locationId, b => b.location, bins);
            world.site = Array.Empty<string>(); // the pre-D-049 terrace field, now in sites
            MarkCaptured(locationId);
        }

        /// <summary>The whole save: the place loaded now captured from its scene, everything else from memory.</summary>
        private SaveGameData Capture()
        {
            SceneFlow flow = Core.Services.Get<SceneFlow>();
            string here = flow != null ? flow.CurrentLocationId : SaveLocations.Workshop;
            CaptureLocation(here, SceneManager.GetActiveScene());

            world.version = SaveFormat.CurrentVersion;
            world.savedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            world.location = here;
            world.player = new PlayerData { pose = ToPose(player.GroundPosition, player.transform.rotation), pitch = player.Pitch };
            CaptureItemsAndBelt(world);
            CaptureProps(world);

            if (waste != null)
            {
                MaterialWasteLog log = waste.Log;
                world.waste = new WasteData
                {
                    cutCount = log.CutCount, volumeCubicMeters = log.TotalVolumeCubicMeters, massKg = log.TotalMassKg
                };
            }

            if (jobBoard != null)
            {
                world.job = jobBoard.Capture();
                world.offers = jobBoard.CaptureOffers();
                world.career = jobBoard.CaptureCareer() ?? new CareerData();
            }

            if (economy != null)
            {
                world.economy = economy.Capture();
                world.pendingDeliveries = ToData(economy.Pending);
            }

            if (upgrades != null)
            {
                world.upgrades = upgrades.Capture();
            }

            return world;
        }

        // Which places' states are in the save. A save from before D-056 has none listed: its entries are the
        // workshop's (when it has any, i.e. it is not empty).
        private bool WasCaptured(string locationId)
        {
            if (Array.IndexOf(world.capturedLocations ?? Array.Empty<string>(), locationId) >= 0)
            {
                return true;
            }

            bool olderSave = (world.capturedLocations == null || world.capturedLocations.Length == 0) &&
                             ((world.boards?.Length ?? 0) > 0 || (world.assemblies?.Length ?? 0) > 0);
            return olderSave && locationId == SaveLocations.Workshop;
        }

        private void MarkCaptured(string locationId)
        {
            var list = new List<string>(world.capturedLocations ?? Array.Empty<string>());
            if (!list.Contains(locationId))
            {
                list.Add(locationId);
            }

            world.capturedLocations = list.ToArray();
        }

        private void CaptureItemsAndBelt(SaveGameData data)
        {
            var onBelt = new HashSet<PickupItem>();
            var slots = new string[toolBelt.Belt.SlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                PickupItem stored = toolBelt.GetStored(i);
                slots[i] = stored != null && stored.TryGetComponent(out PersistentId id) ? id.Id : string.Empty;
                if (stored != null)
                {
                    onBelt.Add(stored);
                }
            }

            int selected = toolBelt.SlotToSave;
            data.belt = new BeltData
            {
                slots = slots,
                selected = selected >= 0 && !string.IsNullOrEmpty(slots[selected]) ? selected : -1
            };

            var items = new List<ItemData>();
            foreach (KeyValuePair<string, PickupItem> item in ById<PickupItem>())
            {
                bool stored = onBelt.Contains(item.Value);
                items.Add(new ItemData
                {
                    id = item.Key, onBelt = stored, pose = ToPose(item.Value.transform), location = LocationOf(item.Value)
                });
            }

            data.items = items.ToArray();
        }

        /// <summary>Large movable things that are not timber (the stepladder, D-049), with the place they stand at.</summary>
        private static void CaptureProps(SaveGameData data)
        {
            var props = new List<PropData>();
            foreach (KeyValuePair<string, Carryable> prop in ById<Carryable>())
            {
                if (!prop.Value.TryGetComponent(out TimberBoard _))
                {
                    props.Add(new PropData { id = prop.Key, pose = ToPose(prop.Value.transform), location = LocationOf(prop.Value) });
                }
            }

            data.props = props.ToArray();
        }

        /// <summary>
        /// Items lying somewhere go back to their saved pose and place; belt items into their slots. Items the save
        /// doesn't know (added in a later version of the game) stay where the scene has them.
        /// </summary>
        private void ApplyItemsAndBelt(SaveGameData data)
        {
            Dictionary<string, PickupItem> items = ById<PickupItem>();
            foreach (ItemData itemData in data.items ?? Array.Empty<ItemData>())
            {
                if (!items.TryGetValue(itemData.id, out PickupItem item))
                {
                    Warn($"item '{itemData.id}' is not in the game; skipped.");
                }
                else if (!itemData.onBelt)
                {
                    item.transform.SetPositionAndRotation(ToPosition(itemData.pose), ToRotation(itemData.pose));
                    if (item.TryGetComponent(out Traveller traveller))
                    {
                        traveller.Location = SaveLocations.Of(itemData.location);
                    }
                }
            }

            BeltData belt = data.belt ?? new BeltData();
            string[] slotIds = belt.slots ?? Array.Empty<string>();
            var slots = new PickupItem[slotIds.Length];
            for (int i = 0; i < slotIds.Length; i++)
            {
                if (!string.IsNullOrEmpty(slotIds[i]))
                {
                    items.TryGetValue(slotIds[i], out slots[i]);
                }
            }

            toolBelt.Restore(slots, belt.selected);
        }

        /// <summary>Puts the stepladder (and other props) back at their place; one saved while carried falls down.</summary>
        private static void ApplyProps(SaveGameData data)
        {
            Dictionary<string, Carryable> props = ById<Carryable>();
            foreach (PropData prop in data.props ?? Array.Empty<PropData>())
            {
                if (props.TryGetValue(prop.id, out Carryable carryable) && !carryable.TryGetComponent(out TimberBoard _))
                {
                    carryable.transform.SetPositionAndRotation(ToPosition(prop.pose), ToRotation(prop.pose));
                    if (carryable.TryGetComponent(out Traveller traveller))
                    {
                        traveller.Location = SaveLocations.Of(prop.location);
                    }

                    carryable.RequestSupportCheck();
                }
            }
        }

        /// <summary>
        /// Site preparation by site id; a save from before D-049 has only the terrace's state in <c>site</c>.
        /// </summary>
        private void RestoreSites(string locationId, Scene scene)
        {
            var byId = new Dictionary<string, string[]>();
            foreach (SiteData siteData in SaveLocations.At(world.sites, locationId, s => s.location))
            {
                if (!string.IsNullOrEmpty(siteData.id))
                {
                    byId[siteData.id] = siteData.done ?? Array.Empty<string>();
                }
            }

            if (byId.Count == 0 && locationId == SaveLocations.Workshop && world.site != null && world.site.Length > 0)
            {
                byId["TerraceSite"] = world.site;
            }

            foreach (KeyValuePair<string, SitePreparation> site in ById<SitePreparation>(scene))
            {
                site.Value.Restore(byId.TryGetValue(site.Key, out string[] done) ? done : Array.Empty<string>());
            }
        }

        /// <summary>Fills the offcut bins (D-052); bins missing from the save (older saves) start empty.</summary>
        private void RestoreBins(string locationId, Scene scene)
        {
            var saved = new Dictionary<string, BinData>();
            foreach (BinData bin in SaveLocations.At(world.bins, locationId, b => b.location))
            {
                saved[bin.id] = bin;
            }

            foreach (KeyValuePair<string, OffcutBin> bin in ById<OffcutBin>(scene))
            {
                var pieces = new List<StoredPiece>();
                if (saved.TryGetValue(bin.Key, out BinData binData))
                {
                    foreach (BoardData pieceData in binData.pieces ?? Array.Empty<BoardData>())
                    {
                        if (boardFactory.TryGetSpecies(pieceData.speciesId, out WoodSpeciesDefinition definition))
                        {
                            TimberPiece piece = SaveFormat.ToPiece(pieceData, definition.ToDomain(), out _);
                            pieces.Add(new StoredPiece(piece, (BoardSurface)pieceData.surface));
                        }
                    }
                }

                bin.Value.Stock.Restore(pieces);
            }
        }

        private static string LocationOf(Component component) =>
            component.TryGetComponent(out Traveller traveller) ? traveller.Location : SaveLocations.Workshop;

        private static PendingDeliveryData[] ToData(IReadOnlyList<WorkshopEconomy.PendingDelivery> pending)
        {
            var data = new PendingDeliveryData[pending.Count];
            for (int i = 0; i < data.Length; i++)
            {
                WorkshopEconomy.PendingDelivery order = pending[i];
                data[i] = new PendingDeliveryData
                {
                    deliveryId = order.deliveryId, speciesId = order.speciesId, count = order.count,
                    lengthMillimeters = order.lengthMillimeters, widthMillimeters = order.widthMillimeters,
                    thicknessMillimeters = order.thicknessMillimeters
                };
            }

            return data;
        }

        private static IEnumerable<WorkshopEconomy.PendingDelivery> ToPending(PendingDeliveryData[] data)
        {
            foreach (PendingDeliveryData order in data ?? Array.Empty<PendingDeliveryData>())
            {
                if (order != null && order.count > 0)
                {
                    yield return new WorkshopEconomy.PendingDelivery
                    {
                        deliveryId = order.deliveryId, speciesId = order.speciesId, count = order.count,
                        lengthMillimeters = order.lengthMillimeters, widthMillimeters = order.widthMillimeters,
                        thicknessMillimeters = order.thicknessMillimeters
                    };
                }
            }
        }

        /// <summary>Objects of a type in one scene (inactive ones included).</summary>
        private static List<T> InScene<T>(Scene scene) where T : Component
        {
            var result = new List<T>();
            foreach (T component in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (component.gameObject.scene == scene)
                {
                    result.Add(component);
                }
            }

            return result;
        }

        /// <summary>Objects of a type that have a <see cref="PersistentId"/> (inactive ones included), in one scene or all.</summary>
        private static Dictionary<string, T> ById<T>(Scene? scene = null) where T : Component
        {
            var result = new Dictionary<string, T>();
            foreach (T component in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (scene.HasValue && component.gameObject.scene != scene.Value)
                {
                    continue;
                }

                if (component.TryGetComponent(out PersistentId id) && !string.IsNullOrEmpty(id.Id))
                {
                    if (result.ContainsKey(id.Id))
                    {
                        Debug.LogWarning($"[{nameof(SaveGameService)}] Two {typeof(T).Name} objects share the id '{id.Id}'.", component);
                        continue;
                    }

                    result.Add(id.Id, component);
                }
            }

            return result;
        }

        private void Warn(string message) => Debug.LogWarning($"[{nameof(SaveGameService)}] {message}", this);

        private void Reject(string path, IReadOnlyList<string> errors)
        {
            string aside = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty,
                $"workshop.invalid-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            try
            {
                File.Move(path, aside);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{nameof(SaveGameService)}] Could not move the invalid save aside: {exception}", this);
            }

            Debug.LogWarning($"[{nameof(SaveGameService)}] Save not loaded, kept as '{aside}':\n- {string.Join("\n- ", errors)}", this);
            SetStatus(Loc.T("Save could not be loaded (kept aside, see Console); new workshop", "Kayıt yüklenemedi (kenara alındı, Console'a bak); yeni atölye"));
        }

        private void SetStatus(string status)
        {
            LastStatus = status;
            StatusChanged?.Invoke();
        }

        /// <summary>Write to a temporary file first and replace the save only when that succeeded (keeps a .bak).</summary>
        private static void WriteAtomically(string path, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, contents);
            if (File.Exists(path))
            {
                File.Replace(temporary, path, path + ".bak");
            }
            else
            {
                File.Move(temporary, path);
            }
        }

        private static string FormatSavedAt(string savedAtUtc) =>
            DateTime.TryParse(savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime time)
                ? time.ToLocalTime().ToString("dd.MM. HH:mm", CultureInfo.InvariantCulture)
                : Loc.T("an unknown time", "bilinmeyen zaman");

        private static PoseData ToPose(Transform target) => ToPose(target.position, target.rotation);

        private static PoseData ToPose(Vector3 p, Quaternion r) =>
            new PoseData { px = p.x, py = p.y, pz = p.z, rx = r.x, ry = r.y, rz = r.z, rw = r.w };

        private static Vector3 ToPosition(PoseData pose) => new Vector3(pose.px, pose.py, pose.pz);

        private static Quaternion ToRotation(PoseData pose) =>
            new Quaternion(pose.rx, pose.ry, pose.rz, pose.rw).normalized;
    }
}
