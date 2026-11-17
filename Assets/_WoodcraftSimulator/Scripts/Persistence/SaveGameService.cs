using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Cutting;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Inventory;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Localization;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;
using WoodcraftSimulator.Player;
using WoodcraftSimulator.Tools;
using WoodcraftSimulator.Workshop;

namespace WoodcraftSimulator.Persistence
{
    /// <summary>
    /// Saves and loads the workshop (D-036): one JSON file in <see cref="Application.persistentDataPath"/>.
    /// On start the last save is loaded (migrated, validated, then applied); on quit the game is saved.
    /// An invalid file is never applied: it is moved aside and the generated workshop is used.
    /// Systems with more state capture/restore it themselves (e.g. <see cref="AssemblyStation.CaptureState"/>); this
    /// service finds them by <see cref="PersistentId"/> and applies them in dependency order: boards, assemblies
    /// (need the boards; the job first sets their blueprint), items and belt, waste, switches, cut lists, player.
    /// </summary>
    public sealed class SaveGameService : MonoBehaviour
    {
        private const string FileName = "workshop.json";

        [SerializeField] private TimberBoardFactory boardFactory;
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerToolBelt toolBelt;

        [Tooltip("Optional: the workshop's sawdust total.")]
        [SerializeField] private WorkshopWaste waste;

        [Tooltip("Optional: the job board (active job, delivered stock, completed jobs).")]
        [SerializeField] private JobBoard jobBoard;

        [Tooltip("Optional: money and fastener stock (D-040).")]
        [SerializeField] private Economy.WorkshopEconomy economy;

        [Tooltip("Optional: equipment upgrades owned (D-044).")]
        [SerializeField] private Economy.WorkshopUpgrades upgrades;

        [Tooltip("Outdoor sites (terrace D-046, pergola D-049), each with a PersistentId.")]
        [SerializeField] private Workshop.SitePreparation[] sites = Array.Empty<Workshop.SitePreparation>();

        private bool restarting;

        public string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Short human-readable result of the last load/save (for the pause menu).</summary>
        public string LastStatus { get; private set; } = Loc.T("Not saved yet", "Henüz kaydedilmedi");

        /// <summary>Raised after <see cref="LastStatus"/> changed.</summary>
        public event Action StatusChanged;

        private void Start()
        {
            if (boardFactory == null || player == null || toolBelt == null)
            {
                Debug.LogError($"{nameof(SaveGameService)}: Board Factory, Player and Tool Belt must be assigned.", this);
                enabled = false;
                return;
            }

            Load();
        }

        // Exiting Play Mode in the Editor also calls this.
        private void OnApplicationQuit()
        {
            if (enabled && !restarting)
            {
                Save();
            }
        }

        public bool Save()
        {
            try
            {
                SaveGameData data = Capture();
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                WriteAtomically(SavePath, json);
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

        /// <summary>Deletes the save and reloads the scene: the generated workshop again.</summary>
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

            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            if (buildIndex < 0)
            {
                Debug.LogError($"[{nameof(SaveGameService)}] The scene is not in Build Settings, so it can't be reloaded. " +
                               "The save was deleted; restart Play Mode for a new workshop.", this);
                return;
            }

            // The pause menu stopped time; the reloaded scene must start running.
            Time.timeScale = 1f;
            SceneManager.LoadScene(buildIndex);
        }

        /// <summary>Saves, then reloads the scene from that save (e.g. after a language change).</summary>
        public void SaveAndReload()
        {
            if (!Save())
            {
                return;
            }

            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            if (buildIndex < 0)
            {
                Debug.LogError($"[{nameof(SaveGameService)}] The scene is not in Build Settings, so it can't be reloaded.", this);
                return;
            }

            restarting = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(buildIndex);
        }

        private void Load()
        {
            string path = SavePath;
            if (!File.Exists(path))
            {
                SetStatus(Loc.T("New workshop (no save yet)", "Yeni atölye (henüz kayıt yok)"));
                return;
            }

            SaveGameData data;
            try
            {
                data = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Reject(path, new[] { $"Unreadable file: {exception.Message}" });
                return;
            }

            if (!SaveFormat.TryMigrate(data, out string migrationError))
            {
                Reject(path, new[] { migrationError });
                return;
            }

            IReadOnlyList<string> errors = SaveFormat.Validate(data, boardFactory.IsKnownSpecies);
            if (errors.Count > 0)
            {
                Reject(path, errors);
                return;
            }

            Apply(data);
            SetStatus(Loc.T($"Loaded save from {FormatSavedAt(data.savedAtUtc)}", $"Kayıt yüklendi: {FormatSavedAt(data.savedAtUtc)}"));
        }

        private SaveGameData Capture()
        {
            var data = new SaveGameData
            {
                version = SaveFormat.CurrentVersion,
                savedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                player = new PlayerData { pose = ToPose(player.GroundPosition, player.transform.rotation), pitch = player.Pitch }
            };

            // A piece in the apprentice's hands goes into its bin first (D-052).
            foreach (Workshop.Apprentice apprentice in FindObjectsByType<Workshop.Apprentice>(FindObjectsSortMode.None))
            {
                apprentice.FinishNow();
            }

            var boards = new List<BoardData>();
            foreach (TimberBoard board in FindObjectsByType<TimberBoard>(FindObjectsSortMode.None))
            {
                if (board.Piece == null || !board.TryGetComponent(out PersistentId id))
                {
                    continue;
                }

                BoardData boardData = SaveFormat.ToData(id.Id, board.Piece, board.Marks);
                boardData.pose = ToPose(board.transform);
                boardData.surface = (int)board.Surface;
                boards.Add(boardData);
            }

            data.boards = boards.ToArray();
            CaptureItemsAndBelt(data);

            var assemblies = new List<AssemblyData>();
            foreach (KeyValuePair<string, AssemblyStation> station in ById<AssemblyStation>())
            {
                if (station.Value.Build != null)
                {
                    assemblies.Add(station.Value.CaptureState(station.Key));
                }
            }

            data.assemblies = assemblies.ToArray();

            if (waste != null && data.waste != null)
            {
                MaterialWasteLog log = waste.Log;
                data.waste = new WasteData
                {
                    cutCount = log.CutCount, volumeCubicMeters = log.TotalVolumeCubicMeters, massKg = log.TotalMassKg
                };
            }

            var switches = new List<SwitchData>();
            foreach (KeyValuePair<string, LightSwitch> lightSwitch in ById<LightSwitch>())
            {
                switches.Add(new SwitchData { id = lightSwitch.Key, on = lightSwitch.Value.IsOn });
            }

            data.switches = switches.ToArray();

            var cutLists = new List<CutListData>();
            foreach (KeyValuePair<string, CutList> cutList in ById<CutList>())
            {
                cutLists.Add(new CutListData { id = cutList.Key, selectedIndex = cutList.Value.SelectedIndex });
            }

            data.cutLists = cutLists.ToArray();
            if (jobBoard != null)
            {
                data.job = jobBoard.Capture();
                data.offers = jobBoard.CaptureOffers();
                data.career = jobBoard.CaptureCareer() ?? new CareerData();
            }

            if (economy != null)
            {
                data.economy = economy.Capture();
            }

            if (upgrades != null)
            {
                data.upgrades = upgrades.Capture();
            }

            var siteData = new List<SiteData>();
            foreach (Workshop.SitePreparation each in sites)
            {
                if (each != null && each.TryGetComponent(out PersistentId siteId))
                {
                    siteData.Add(new SiteData { id = siteId.Id, done = each.Capture() });
                }
            }

            data.sites = siteData.ToArray();

            // Large movable things that are not timber (the stepladder, D-049).
            var props = new List<PropData>();
            foreach (KeyValuePair<string, Carryable> prop in ById<Carryable>())
            {
                if (!prop.Value.TryGetComponent(out TimberBoard _))
                {
                    props.Add(new PropData { id = prop.Key, pose = ToPose(prop.Value.transform) });
                }
            }

            data.props = props.ToArray();

            // Offcut bins and their pieces (D-052).
            var bins = new List<BinData>();
            foreach (KeyValuePair<string, Workshop.OffcutBin> bin in ById<Workshop.OffcutBin>())
            {
                var pieces = new List<BoardData>();
                foreach (StoredPiece stored in bin.Value.Stock.Pieces)
                {
                    BoardData piece = SaveFormat.ToData(string.Empty, stored.Piece, new BoardMarks(stored.Piece.Dimensions.Length));
                    piece.surface = (int)stored.Surface;
                    pieces.Add(piece);
                }

                bins.Add(new BinData { id = bin.Key, pieces = pieces.ToArray() });
            }

            data.bins = bins.ToArray();
            return data;
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
                items.Add(new ItemData { id = item.Key, onBelt = stored, pose = ToPose(item.Value.transform) });
            }

            data.items = items.ToArray();
        }

        private void Apply(SaveGameData data)
        {
            foreach (Workshop.Apprentice apprentice in FindObjectsByType<Workshop.Apprentice>(FindObjectsSortMode.None))
            {
                apprentice.ResetToHome();
            }

            // The builder's boards are replaced by the saved ones (cut pieces included).
            foreach (TimberBoard existing in FindObjectsByType<TimberBoard>(FindObjectsSortMode.None))
            {
                Destroy(existing.gameObject);
            }

            var boards = new Dictionary<string, Carryable>();
            foreach (BoardData boardData in data.boards ?? Array.Empty<BoardData>())
            {
                boardFactory.TryGetSpecies(boardData.speciesId, out WoodSpeciesDefinition definition);
                TimberPiece piece = SaveFormat.ToPiece(boardData, definition.ToDomain(), out BoardMarks marks);
                TimberBoard board = boardFactory.Create(boardData.id, definition, piece, marks,
                    ToPosition(boardData.pose), ToRotation(boardData.pose));
                board.SetSurface((BoardSurface)boardData.surface);

                // A board saved while carried was in the air: let it fall (supported ones don't move, D-031).
                // Installed boards are exempt: the station below takes them over.
                var carryable = board.GetComponent<Carryable>();
                carryable.RequestSupportCheck();
                boards[boardData.id] = carryable;
            }

            // Money and fasteners first: the job board's prompts read them. A save from before the economy starts it.
            if (economy != null)
            {
                economy.Restore(data.economy);
            }

            if (upgrades != null)
            {
                upgrades.Restore(data.upgrades);
            }

            // Before the assemblies: the site gates where piers may stand.
            RestoreSites(data);
            RestoreProps(data);
            RestoreBins(data);

            // The job sets the station's blueprint and the cut list before the assembly is restored.
            if (jobBoard != null && data.job != null)
            {
                jobBoard.Restore(data.job, data.offers, data.career);
            }

            Dictionary<string, AssemblyStation> stations = ById<AssemblyStation>();
            foreach (AssemblyData assembly in data.assemblies ?? Array.Empty<AssemblyData>())
            {
                if (stations.TryGetValue(assembly.stationId, out AssemblyStation station) && station.Build != null)
                {
                    station.RestoreState(assembly, id => boards.TryGetValue(id, out Carryable board) ? board : null);
                }
                else
                {
                    Warn($"assembly station '{assembly.stationId}' is not in the scene; its saved assembly is skipped.");
                }
            }

            ApplyItemsAndBelt(data);

            if (waste != null && data.waste != null)
            {
                waste.Log.Restore(data.waste.cutCount, data.waste.volumeCubicMeters, data.waste.massKg);
            }

            Dictionary<string, LightSwitch> lightSwitches = ById<LightSwitch>();
            foreach (SwitchData switchData in data.switches ?? Array.Empty<SwitchData>())
            {
                if (lightSwitches.TryGetValue(switchData.id, out LightSwitch lightSwitch))
                {
                    lightSwitch.SetOn(switchData.on);
                }
            }

            Dictionary<string, CutList> cutLists = ById<CutList>();
            foreach (CutListData cutListData in data.cutLists ?? Array.Empty<CutListData>())
            {
                if (cutLists.TryGetValue(cutListData.id, out CutList cutList))
                {
                    cutList.Restore(cutListData.selectedIndex);
                }
            }

            Quaternion yaw = ToRotation(data.player.pose);
            player.Teleport(ToPosition(data.player.pose), yaw.eulerAngles.y, data.player.pitch);
        }

        /// <summary>
        /// Items lying in the world go back to their saved pose; belt items into their slots. Items the save doesn't
        /// know (added in a later version of the workshop) stay where the scene has them.
        /// </summary>
        private void ApplyItemsAndBelt(SaveGameData data)
        {
            Dictionary<string, PickupItem> items = ById<PickupItem>();
            foreach (ItemData itemData in data.items ?? Array.Empty<ItemData>())
            {
                if (!items.TryGetValue(itemData.id, out PickupItem item))
                {
                    Warn($"item '{itemData.id}' is not in the scene; skipped.");
                }
                else if (!itemData.onBelt)
                {
                    item.transform.SetPositionAndRotation(ToPosition(itemData.pose), ToRotation(itemData.pose));
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

        /// <summary>All objects of a type that have a <see cref="PersistentId"/>, inactive ones included (belt items).</summary>
        private static Dictionary<string, T> ById<T>() where T : Component
        {
            var result = new Dictionary<string, T>();
            foreach (T component in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
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

        /// <summary>
        /// Site preparation by site id; a save from before D-049 has only the terrace's state in <c>site</c>.
        /// </summary>
        private void RestoreSites(SaveGameData data)
        {
            var byId = new Dictionary<string, string[]>();
            foreach (SiteData siteData in data.sites ?? Array.Empty<SiteData>())
            {
                if (siteData != null && !string.IsNullOrEmpty(siteData.id))
                {
                    byId[siteData.id] = siteData.done ?? Array.Empty<string>();
                }
            }

            if (byId.Count == 0 && data.site != null && data.site.Length > 0)
            {
                byId["TerraceSite"] = data.site;
            }

            foreach (Workshop.SitePreparation each in sites)
            {
                if (each != null && each.TryGetComponent(out PersistentId siteId))
                {
                    each.Restore(byId.TryGetValue(siteId.Id, out string[] done) ? done : Array.Empty<string>());
                }
            }
        }

        /// <summary>Fills the offcut bins (D-052); bins missing from the save (older saves) start empty.</summary>
        private void RestoreBins(SaveGameData data)
        {
            var saved = new Dictionary<string, BinData>();
            foreach (BinData bin in data.bins ?? Array.Empty<BinData>())
            {
                saved[bin.id] = bin;
            }

            foreach (KeyValuePair<string, Workshop.OffcutBin> bin in ById<Workshop.OffcutBin>())
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

        /// <summary>Puts the stepladder (and other props) back; one saved while carried falls to the ground.</summary>
        private void RestoreProps(SaveGameData data)
        {
            Dictionary<string, Carryable> props = ById<Carryable>();
            foreach (PropData prop in data.props ?? Array.Empty<PropData>())
            {
                if (props.TryGetValue(prop.id, out Carryable carryable) && !carryable.TryGetComponent(out TimberBoard _))
                {
                    carryable.transform.SetPositionAndRotation(ToPosition(prop.pose), ToRotation(prop.pose));
                    carryable.RequestSupportCheck();
                }
            }
        }

        private static Vector3 ToPosition(PoseData pose) => new Vector3(pose.px, pose.py, pose.pz);

        private static Quaternion ToRotation(PoseData pose) =>
            new Quaternion(pose.rx, pose.ry, pose.rz, pose.rw).normalized;
    }
}
