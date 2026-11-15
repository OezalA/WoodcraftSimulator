using System;

namespace WoodcraftSimulator.Persistence
{
    // Save file data (D-036). Plain [Serializable] classes with public fields only, so the runtime can write them as
    // JSON with Unity's JsonUtility (no properties, dictionaries or nullables) while the domain stays free of
    // UnityEngine. Lengths are stored exactly as tenths of a millimeter (Length.TenthsOfMillimeter).
    // Only mutable game state is saved; static definitions (species, blueprints, items) are referenced by id.

    [Serializable]
    public sealed class SaveGameData
    {
        /// <summary>Format version of this file (see <see cref="SaveFormat.CurrentVersion"/>).</summary>
        public int version;

        /// <summary>UTC time of saving, ISO 8601 (informative only).</summary>
        public string savedAtUtc;

        public PlayerData player = new PlayerData();
        public BoardData[] boards = Array.Empty<BoardData>();
        public ItemData[] items = Array.Empty<ItemData>();
        public BeltData belt = new BeltData();
        public AssemblyData[] assemblies = Array.Empty<AssemblyData>();
        public WasteData waste = new WasteData();
        public SwitchData[] switches = Array.Empty<SwitchData>();
        public CutListData[] cutLists = Array.Empty<CutListData>();
        public JobData job = new JobData();

        /// <summary>Money and fastener stock (format 2). Not started = the starting funds are given on load.</summary>
        public EconomyData economy = new EconomyData();

        /// <summary>Equipment upgrades owned (ids, D-044). Older saves have none.</summary>
        public string[] upgrades = Array.Empty<string>();
    }

    [Serializable]
    public sealed class TransactionData
    {
        /// <summary><c>TransactionKind</c> as int (0 = starting funds, 1 = purchase, 2 = job income).</summary>
        public int kind;

        /// <summary>Signed amount in cents (negative = money out).</summary>
        public long amountCents;

        public string description = string.Empty;
    }

    [Serializable]
    public sealed class FastenerCountData
    {
        public string fastenerId;
        public int count;
    }

    [Serializable]
    public sealed class EconomyData
    {
        /// <summary>False in saves made before the economy (format 1): the game then starts the ledger.</summary>
        public bool started;

        public TransactionData[] transactions = Array.Empty<TransactionData>();
        public FastenerCountData[] fasteners = Array.Empty<FastenerCountData>();
    }

    [Serializable]
    public sealed class PoseData
    {
        public float px, py, pz;
        public float rx, ry, rz, rw = 1f;
    }

    [Serializable]
    public sealed class PlayerData
    {
        public PoseData pose = new PoseData();

        /// <summary>Camera pitch in degrees (yaw is the pose rotation).</summary>
        public float pitch;
    }

    [Serializable]
    public sealed class MarkData
    {
        public long positionFromStartTenths;

        /// <summary><c>BoardEnd</c> as int (0 = Start, 1 = End).</summary>
        public int measuredFrom;

        /// <summary><c>BoardFace</c> as int (0 = Top, 1 = Bottom).</summary>
        public int face;
    }

    [Serializable]
    public sealed class BoardData
    {
        /// <summary>Unique id of this physical piece in the save (referenced by assemblies).</summary>
        public string id;

        public string speciesId;
        public long lengthTenths;
        public long widthTenths;
        public long thicknessTenths;
        public PoseData pose = new PoseData();
        public MarkData[] marks = Array.Empty<MarkData>();

        /// <summary><c>BoardSurface</c> as int (0 = raw, 1 = sanded, 2 = oiled), D-045.</summary>
        public int surface;
    }

    /// <summary>A small item (tool) lying in the world. Items on the belt are listed in <see cref="BeltData"/>.</summary>
    [Serializable]
    public sealed class ItemData
    {
        /// <summary>Scene id of the item object (stable, set by the scene builder).</summary>
        public string id;

        public bool onBelt;
        public PoseData pose = new PoseData();
    }

    [Serializable]
    public sealed class BeltData
    {
        /// <summary>Item ids per slot; empty string = empty slot.</summary>
        public string[] slots = Array.Empty<string>();

        /// <summary>Slot in hand, -1 = none.</summary>
        public int selected = -1;
    }

    [Serializable]
    public sealed class InstalledPartData
    {
        public string slotId;
        public string boardId;
    }

    [Serializable]
    public sealed class FastenedConnectionData
    {
        public string connectionId;

        /// <summary>Fastened point indices.</summary>
        public int[] points = Array.Empty<int>();

        /// <summary>Head height (tenths of a mm above the surface) per entry of <see cref="points"/>; empty = flush.</summary>
        public long[] headHeightsTenths = Array.Empty<long>();

        /// <summary>Point indices with a pilot hole.</summary>
        public int[] pilotHoles = Array.Empty<int>();

        /// <summary>Seconds the joint's glue still had to dry (D-045); 0 = dry or not glued.</summary>
        public double dryingSeconds;
    }

    /// <summary>The job in progress (none if <see cref="activeJobId"/> is empty) and the jobs already delivered.</summary>
    [Serializable]
    public sealed class JobData
    {
        public string activeJobId = string.Empty;
        public double elapsedSeconds;
        public int boardsDelivered;
        public int boardsOrderedExtra;

        /// <summary>The workshop's sawdust total when the job was accepted (the job's own sawdust is the difference).</summary>
        public double sawdustAtStartCubicMeters;

        /// <summary>Completed and delivered job ids, oldest first (a job may appear more than once).</summary>
        public string[] completedJobIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class AssemblyData
    {
        /// <summary>Scene id of the assembly station.</summary>
        public string stationId;

        public string blueprintId;
        public InstalledPartData[] parts = Array.Empty<InstalledPartData>();
        public FastenedConnectionData[] fastened = Array.Empty<FastenedConnectionData>();
    }

    [Serializable]
    public sealed class WasteData
    {
        public int cutCount;
        public double volumeCubicMeters;
        public double massKg;
    }

    [Serializable]
    public sealed class SwitchData
    {
        public string id;
        public bool on;
    }

    [Serializable]
    public sealed class CutListData
    {
        public string id;
        public int selectedIndex;
    }
}
