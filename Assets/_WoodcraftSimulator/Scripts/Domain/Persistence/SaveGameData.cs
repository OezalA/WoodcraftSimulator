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

        /// <summary>Terrace site preparation done ("marked" and prepared pier slot ids, D-046).</summary>
        public string[] site = Array.Empty<string>();

        /// <summary>
        /// Every outdoor site by id (D-049, terrace and pergola). When empty (older saves), <see cref="site"/> is the
        /// terrace's state.
        /// </summary>
        public SiteData[] sites = Array.Empty<SiteData>();

        /// <summary>Movable non-timber objects (the stepladder, D-049). Older saves have none: they stay put.</summary>
        public PropData[] props = Array.Empty<PropData>();

        /// <summary>Offcut bins by id with the pieces in them (D-052). Older saves have none: the bins start empty.</summary>
        public BinData[] bins = Array.Empty<BinData>();

        /// <summary>Career (D-054). Not started (older saves): rebuilt from the completed jobs.</summary>
        public CareerData career = new CareerData();

        /// <summary>The offers on the job board, one per sheet (D-054). Empty (older saves): new offers are drawn.</summary>
        public OfferData[] offers = Array.Empty<OfferData>();

        /// <summary>The place the player is at (location id, D-056); empty in older saves = the workshop.</summary>
        public string location;

        /// <summary>Places whose state is in this save (D-056); a place not listed is loaded as built (first visit).</summary>
        public string[] capturedLocations = Array.Empty<string>();

        /// <summary>Boards bought for a place that wasn't loaded, still to be delivered there (D-056).</summary>
        public PendingDeliveryData[] pendingDeliveries = Array.Empty<PendingDeliveryData>();
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
        /// <summary>The place (location id) it belongs to (D-056); empty in older saves = the workshop.</summary>
        public string location;

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
        /// <summary>The place (location id) it belongs to (D-056); empty in older saves = the workshop.</summary>
        public string location;

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

        /// <summary>Deposit the customer paid when the job was accepted (cents, D-046).</summary>
        public long depositCents;

        /// <summary>Completed and delivered job ids, oldest first (a job may appear more than once).</summary>
        public string[] completedJobIds = Array.Empty<string>();

        /// <summary>Board slot (sheet) of the active job's offer (D-054); older saves: 0 with their template.</summary>
        public int activeSlot;
    }

    [Serializable]
    public sealed class AssemblyData
    {
        /// <summary>The place (location id) it belongs to (D-056); empty in older saves = the workshop.</summary>
        public string location;

        /// <summary>Scene id of the assembly station.</summary>
        public string stationId;

        public string blueprintId;
        public InstalledPartData[] parts = Array.Empty<InstalledPartData>();
        public FastenedConnectionData[] fastened = Array.Empty<FastenedConnectionData>();

        /// <summary>Lean and brace of the installed posts (D-049). Older saves have none.</summary>
        public PostLeanData[] posts = Array.Empty<PostLeanData>();
    }

    [Serializable]
    public sealed class PostLeanData
    {
        public string slotId;
        public int leanXTenths;
        public int leanZTenths;
        public bool braced;
    }

    /// <summary>Preparation state of one outdoor site (D-046, D-049): "marked" and the prepared pier slot ids.</summary>
    [Serializable]
    public sealed class SiteData
    {
        /// <summary>The place (location id) it belongs to (D-056); empty in older saves = the workshop.</summary>
        public string location;

        /// <summary>Scene id of the site.</summary>
        public string id;

        public string[] done = Array.Empty<string>();
    }

    /// <summary>A large movable object that is not timber, e.g. the stepladder (D-049).</summary>
    [Serializable]
    public sealed class PropData
    {
        /// <summary>The place (location id) it belongs to (D-056); empty in older saves = the workshop.</summary>
        public string location;

        public string id;
        public PoseData pose = new PoseData();
    }

    /// <summary>Boards waiting to be delivered to a place that wasn't loaded (D-056).</summary>
    [Serializable]
    public sealed class PendingDeliveryData
    {
        public string deliveryId;
        public string speciesId;
        public float lengthMillimeters;
        public float widthMillimeters;
        public float thicknessMillimeters;
        public int count;
    }

    /// <summary>Experience, reputation, week, skills (D-054).</summary>
    [Serializable]
    public sealed class CareerData
    {
        public bool started;
        public int experience;
        public double reputation;
        public int week;
        public int jobsDelivered;

        /// <summary>Points per skill in the order of <c>Skill</c> (cutting, joining, finishing, outdoor).</summary>
        public int[] skillPoints = Array.Empty<int>();

        public bool apprenticeHired;
    }

    /// <summary>One job offer on the board (D-054).</summary>
    [Serializable]
    public sealed class OfferData
    {
        public string templateId;
        public string customer;

        /// <summary>Wood asked for; empty = the template's own.</summary>
        public string speciesId;

        public bool requiresFinish;
        public long payCents;

        /// <summary>Shown as a goal, not yet takeable (the reason is worked out again when loaded).</summary>
        public bool locked;
    }

    /// <summary>An offcut bin (D-052): its pieces as board data (no id, pose or marks needed).</summary>
    [Serializable]
    public sealed class BinData
    {
        /// <summary>The place (location id) it belongs to (D-056); empty in older saves = the workshop.</summary>
        public string location;

        public string id;
        public BoardData[] pieces = Array.Empty<BoardData>();
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
        /// <summary>The place (location id) it belongs to (D-056); empty in older saves = the workshop.</summary>
        public string location;

        public string id;
        public bool on;
    }

    [Serializable]
    public sealed class CutListData
    {
        /// <summary>The place (location id) it belongs to (D-056); empty in older saves = the workshop.</summary>
        public string location;

        public string id;
        public int selectedIndex;
    }
}
