using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;
using WoodcraftSimulator.Player;

namespace WoodcraftSimulator.Persistence
{
    /// <summary>
    /// Saves and loads the workshop (D-036): one JSON file in <see cref="Application.persistentDataPath"/>.
    /// On start the last save is loaded (migrated, validated, then applied); on quit the game is saved.
    /// An invalid file is never applied: it is moved aside and the generated workshop is used.
    /// Capture/apply of each system lives here for now; it moves into the systems once there are more (S3+).
    /// </summary>
    public sealed class SaveGameService : MonoBehaviour
    {
        private const string FileName = "workshop.json";

        [SerializeField] private TimberBoardFactory boardFactory;
        [SerializeField] private PlayerController player;

        private bool restarting;

        public string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Short human-readable result of the last load/save (for the pause menu).</summary>
        public string LastStatus { get; private set; } = "Not saved yet";

        /// <summary>Raised after <see cref="LastStatus"/> changed.</summary>
        public event Action StatusChanged;

        private void Start()
        {
            if (boardFactory == null || player == null)
            {
                Debug.LogError($"{nameof(SaveGameService)}: Board Factory and Player must be assigned.", this);
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
                SetStatus($"Saved {DateTime.Now:HH:mm}");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{nameof(SaveGameService)}] Saving failed: {exception}", this);
                SetStatus("Saving failed (see Console)");
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

        private void Load()
        {
            string path = SavePath;
            if (!File.Exists(path))
            {
                SetStatus("New workshop (no save yet)");
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
            SetStatus($"Loaded save from {FormatSavedAt(data.savedAtUtc)}");
        }

        private SaveGameData Capture()
        {
            var data = new SaveGameData
            {
                version = SaveFormat.CurrentVersion,
                savedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                player = new PlayerData { pose = ToPose(player.transform), pitch = player.Pitch }
            };

            var boards = new List<BoardData>();
            foreach (TimberBoard board in FindObjectsByType<TimberBoard>(FindObjectsSortMode.None))
            {
                if (board.Piece == null || !board.TryGetComponent(out PersistentId id))
                {
                    continue;
                }

                BoardData boardData = SaveFormat.ToData(id.Id, board.Piece, board.Marks);
                boardData.pose = ToPose(board.transform);
                boards.Add(boardData);
            }

            data.boards = boards.ToArray();
            return data;
        }

        private void Apply(SaveGameData data)
        {
            // The builder's boards are replaced by the saved ones (cut pieces included).
            foreach (TimberBoard existing in FindObjectsByType<TimberBoard>(FindObjectsSortMode.None))
            {
                Destroy(existing.gameObject);
            }

            foreach (BoardData boardData in data.boards)
            {
                boardFactory.TryGetSpecies(boardData.speciesId, out WoodSpeciesDefinition definition);
                TimberPiece piece = SaveFormat.ToPiece(boardData, definition.ToDomain(), out BoardMarks marks);
                TimberBoard board = boardFactory.Create(boardData.id, definition, piece, marks,
                    ToPosition(boardData.pose), ToRotation(boardData.pose));

                // A board saved while carried was in the air: let it fall (supported ones don't move, D-031).
                board.GetComponent<Carryable>().RequestSupportCheck();
            }

            Quaternion yaw = ToRotation(data.player.pose);
            player.Teleport(ToPosition(data.player.pose), yaw.eulerAngles.y, data.player.pitch);
        }

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
            SetStatus("Save could not be loaded (kept aside, see Console); new workshop");
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
                : "an unknown time";

        private static PoseData ToPose(Transform target)
        {
            Vector3 p = target.position;
            Quaternion r = target.rotation;
            return new PoseData { px = p.x, py = p.y, pz = p.z, rx = r.x, ry = r.y, rz = r.z, rw = r.w };
        }

        private static Vector3 ToPosition(PoseData pose) => new Vector3(pose.px, pose.py, pose.pz);

        private static Quaternion ToRotation(PoseData pose) =>
            new Quaternion(pose.rx, pose.ry, pose.rz, pose.rw).normalized;
    }
}
