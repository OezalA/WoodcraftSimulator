using System;
using System.Collections.Generic;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Persistence
{
    /// <summary>
    /// Versioning, migration and validation of save data (master prompt §6.16). A loaded file is first migrated to
    /// the current version, then validated; only a valid file is applied to the game.
    /// </summary>
    public static class SaveFormat
    {
        /// <summary>Current save format. Increase it and add a migration step when the format changes.</summary>
        public const int CurrentVersion = 2;

        /// <summary>Brings older data up to <see cref="CurrentVersion"/>. Newer or unknown versions are refused.</summary>
        public static bool TryMigrate(SaveGameData data, out string error)
        {
            if (data == null)
            {
                error = "No save data.";
                return false;
            }

            if (data.version < 1)
            {
                error = $"Unknown save version {data.version}.";
                return false;
            }

            if (data.version > CurrentVersion)
            {
                error = $"The save was made by a newer version of the game (format {data.version}, supported {CurrentVersion}).";
                return false;
            }

            // One step per version.
            if (data.version == 1)
            {
                // Format 2 adds the economy (D-040). A format-1 save has none: it stays "not started", so the game
                // gives the starting funds and screws when it is loaded.
                data.economy = new EconomyData();
                data.version = 2;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Checks the data for consistency. Returns all problems found (empty = valid). Unknown definition ids are
        /// checked against <paramref name="isKnownSpecies"/> when given.
        /// </summary>
        public static IReadOnlyList<string> Validate(SaveGameData data, Func<string, bool> isKnownSpecies = null)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("No save data.");
                return errors;
            }

            if (data.version != CurrentVersion)
            {
                errors.Add($"Version {data.version} is not the current format {CurrentVersion} (migrate first).");
            }

            Require(data.player != null && data.player.pose != null, "Player data is missing.", errors);
            if (data.player?.pose != null)
            {
                RequireFinite(data.player.pose, "player", errors);
            }

            var boardIds = new HashSet<string>();
            foreach (BoardData board in data.boards ?? Array.Empty<BoardData>())
            {
                if (board == null)
                {
                    errors.Add("A board entry is empty.");
                    continue;
                }

                string label = $"board '{board.id}'";
                if (string.IsNullOrWhiteSpace(board.id) || !boardIds.Add(board.id))
                {
                    errors.Add($"Board id '{board.id}' is empty or duplicated.");
                }

                if (string.IsNullOrWhiteSpace(board.speciesId))
                {
                    errors.Add($"{label}: species is missing.");
                }
                else if (isKnownSpecies != null && !isKnownSpecies(board.speciesId))
                {
                    errors.Add($"{label}: unknown species '{board.speciesId}'.");
                }

                if (board.lengthTenths <= 0 || board.widthTenths <= 0 || board.thicknessTenths <= 0)
                {
                    errors.Add($"{label}: dimensions must be positive.");
                }

                if (board.pose == null)
                {
                    errors.Add($"{label}: pose is missing.");
                }
                else
                {
                    RequireFinite(board.pose, label, errors);
                }

                foreach (MarkData mark in board.marks ?? Array.Empty<MarkData>())
                {
                    if (mark == null || mark.positionFromStartTenths < 0 || mark.positionFromStartTenths > board.lengthTenths ||
                        !Enum.IsDefined(typeof(BoardEnd), mark.measuredFrom) || !Enum.IsDefined(typeof(BoardFace), mark.face))
                    {
                        errors.Add($"{label}: a pencil mark is invalid.");
                    }
                }
            }

            var itemIds = new HashSet<string>();
            foreach (ItemData item in data.items ?? Array.Empty<ItemData>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id) || !itemIds.Add(item.id))
                {
                    errors.Add($"Item id '{item?.id}' is empty or duplicated.");
                    continue;
                }

                if (!item.onBelt)
                {
                    if (item.pose == null)
                    {
                        errors.Add($"item '{item.id}': pose is missing.");
                    }
                    else
                    {
                        RequireFinite(item.pose, $"item '{item.id}'", errors);
                    }
                }
            }

            BeltData belt = data.belt ?? new BeltData();
            string[] slots = belt.slots ?? Array.Empty<string>();
            foreach (string slotItem in slots)
            {
                if (!string.IsNullOrEmpty(slotItem) && !itemIds.Contains(slotItem))
                {
                    errors.Add($"Belt holds unknown item '{slotItem}'.");
                }
            }

            if (belt.selected < -1 || belt.selected >= slots.Length ||
                belt.selected >= 0 && string.IsNullOrEmpty(slots[belt.selected]))
            {
                errors.Add($"Belt selection {belt.selected} is invalid.");
            }

            var installedBoards = new HashSet<string>();
            var stationIds = new HashSet<string>();
            foreach (AssemblyData assembly in data.assemblies ?? Array.Empty<AssemblyData>())
            {
                if (assembly == null || string.IsNullOrWhiteSpace(assembly.stationId))
                {
                    errors.Add("An assembly entry has no station id.");
                    continue;
                }

                string label = $"assembly '{assembly.stationId}'";
                if (!stationIds.Add(assembly.stationId))
                {
                    errors.Add($"{label} is listed twice.");
                }

                if (string.IsNullOrWhiteSpace(assembly.blueprintId))
                {
                    errors.Add($"{label}: blueprint is missing.");
                }

                var slotIds = new HashSet<string>();
                foreach (InstalledPartData part in assembly.parts ?? Array.Empty<InstalledPartData>())
                {
                    if (part == null || string.IsNullOrWhiteSpace(part.slotId) || !boardIds.Contains(part.boardId ?? string.Empty))
                    {
                        errors.Add($"{label}: an installed part refers to an unknown board.");
                    }
                    else if (!installedBoards.Add(part.boardId))
                    {
                        errors.Add($"Board '{part.boardId}' is installed twice.");
                    }
                    else if (!slotIds.Add(part.slotId))
                    {
                        errors.Add($"{label}: slot '{part.slotId}' holds two boards.");
                    }
                }

                var connectionIds = new HashSet<string>();
                foreach (FastenedConnectionData connection in assembly.fastened ?? Array.Empty<FastenedConnectionData>())
                {
                    if (connection == null || string.IsNullOrWhiteSpace(connection.connectionId) ||
                        !connectionIds.Add(connection.connectionId))
                    {
                        errors.Add($"{label}: a fastened connection is empty or duplicated.");
                        continue;
                    }

                    int[] points = connection.points ?? Array.Empty<int>();
                    int[] pilots = connection.pilotHoles ?? Array.Empty<int>();
                    long[] heights = connection.headHeightsTenths ?? Array.Empty<long>();
                    if (Array.Exists(points, point => point < 0) || new HashSet<int>(points).Count != points.Length ||
                        Array.Exists(pilots, point => point < 0) || new HashSet<int>(pilots).Count != pilots.Length ||
                        heights.Length != 0 && heights.Length != points.Length)
                    {
                        errors.Add($"{label}: connection '{connection.connectionId}' has invalid fastening points.");
                    }
                }
            }

            var switchIds = new HashSet<string>();
            foreach (SwitchData lightSwitch in data.switches ?? Array.Empty<SwitchData>())
            {
                if (lightSwitch == null || string.IsNullOrWhiteSpace(lightSwitch.id) || !switchIds.Add(lightSwitch.id))
                {
                    errors.Add($"Switch id '{lightSwitch?.id}' is empty or duplicated.");
                }
            }

            var cutListIds = new HashSet<string>();
            foreach (CutListData cutList in data.cutLists ?? Array.Empty<CutListData>())
            {
                if (cutList == null || string.IsNullOrWhiteSpace(cutList.id) || !cutListIds.Add(cutList.id))
                {
                    errors.Add($"Cut list id '{cutList?.id}' is empty or duplicated.");
                }
                else if (cutList.selectedIndex < 0)
                {
                    errors.Add($"cut list '{cutList.id}': selection {cutList.selectedIndex} is invalid.");
                }
            }

            WasteData waste = data.waste ?? new WasteData();
            JobData job = data.job ?? new JobData();
            if (!(job.elapsedSeconds >= 0.0) || double.IsInfinity(job.elapsedSeconds) || job.boardsDelivered < 0 ||
                job.boardsOrderedExtra < 0 || job.boardsOrderedExtra > job.boardsDelivered ||
                !(job.sawdustAtStartCubicMeters >= 0.0) || double.IsInfinity(job.sawdustAtStartCubicMeters) ||
                Array.Exists(job.completedJobIds ?? Array.Empty<string>(), string.IsNullOrWhiteSpace))
            {
                errors.Add("Job data is invalid.");
            }

            ValidateEconomy(data.economy ?? new EconomyData(), errors);

            // "!(x >= 0)" also catches NaN.
            if (waste.cutCount < 0 || !(waste.volumeCubicMeters >= 0.0) || !(waste.massKg >= 0.0) ||
                double.IsInfinity(waste.volumeCubicMeters) || double.IsInfinity(waste.massKg))
            {
                errors.Add("Waste totals are invalid.");
            }

            return errors;
        }

        /// <summary>Save data of a piece and its marks (pose is filled in by the scene).</summary>
        public static BoardData ToData(string id, TimberPiece piece, BoardMarks marks)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            if (marks == null) throw new ArgumentNullException(nameof(marks));

            var markData = new MarkData[marks.Count];
            for (int i = 0; i < marks.Count; i++)
            {
                PencilMark mark = marks.All[i];
                markData[i] = new MarkData
                {
                    positionFromStartTenths = mark.PositionFromStart.TenthsOfMillimeter,
                    measuredFrom = (int)mark.MeasuredFrom,
                    face = (int)mark.Face
                };
            }

            BoardDimensions dimensions = piece.Dimensions;
            return new BoardData
            {
                id = id,
                speciesId = piece.Species.Id,
                lengthTenths = dimensions.Length.TenthsOfMillimeter,
                widthTenths = dimensions.Width.TenthsOfMillimeter,
                thicknessTenths = dimensions.Thickness.TenthsOfMillimeter,
                marks = markData
            };
        }

        /// <summary>Save data of the ledger and the fastener stock.</summary>
        public static EconomyData ToData(Ledger ledger, FastenerStock fasteners)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            if (fasteners == null) throw new ArgumentNullException(nameof(fasteners));

            var transactions = new TransactionData[ledger.Entries.Count];
            for (int i = 0; i < transactions.Length; i++)
            {
                Transaction entry = ledger.Entries[i];
                transactions[i] = new TransactionData
                {
                    kind = (int)entry.Kind, amountCents = entry.Amount.Cents, description = entry.Description
                };
            }

            var ids = new List<string>(fasteners.All.Keys);
            ids.Sort(string.CompareOrdinal);
            var counts = new FastenerCountData[ids.Count];
            for (int i = 0; i < counts.Length; i++)
            {
                counts[i] = new FastenerCountData { fastenerId = ids[i], count = fasteners.Count(ids[i]) };
            }

            return new EconomyData { started = ledger.IsStarted, transactions = transactions, fasteners = counts };
        }

        /// <summary>
        /// Restores (validated) economy data. Returns false, changing nothing, if the data was never started
        /// (a format-1 save): the caller then gives the starting funds.
        /// </summary>
        public static bool TryRestore(EconomyData data, Ledger ledger, FastenerStock fasteners)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            if (fasteners == null) throw new ArgumentNullException(nameof(fasteners));
            if (data == null || !data.started)
            {
                return false;
            }

            var history = new List<Transaction>();
            foreach (TransactionData entry in data.transactions ?? Array.Empty<TransactionData>())
            {
                history.Add(new Transaction((TransactionKind)entry.kind, Money.FromCents(entry.amountCents), entry.description));
            }

            var counts = new List<KeyValuePair<string, int>>();
            foreach (FastenerCountData entry in data.fasteners ?? Array.Empty<FastenerCountData>())
            {
                counts.Add(new KeyValuePair<string, int>(entry.fastenerId, entry.count));
            }

            ledger.Restore(history);
            fasteners.Restore(counts);
            return true;
        }

        /// <summary>Recreates the piece and its marks from (validated) save data.</summary>
        public static TimberPiece ToPiece(BoardData data, WoodSpecies species, out BoardMarks marks)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (species == null) throw new ArgumentNullException(nameof(species));

            var piece = new TimberPiece(species, new BoardDimensions(
                Length.FromTenthsOfMillimeter(data.lengthTenths),
                Length.FromTenthsOfMillimeter(data.widthTenths),
                Length.FromTenthsOfMillimeter(data.thicknessTenths)));

            marks = new BoardMarks(piece.Dimensions.Length);
            foreach (MarkData mark in data.marks ?? Array.Empty<MarkData>())
            {
                marks.Add(new PencilMark(Length.FromTenthsOfMillimeter(mark.positionFromStartTenths),
                    (BoardEnd)mark.measuredFrom, (BoardFace)mark.face));
            }

            return piece;
        }

        private static void ValidateEconomy(EconomyData economy, List<string> errors)
        {
            TransactionData[] transactions = economy.transactions ?? Array.Empty<TransactionData>();
            FastenerCountData[] fasteners = economy.fasteners ?? Array.Empty<FastenerCountData>();
            if (!economy.started)
            {
                Require(transactions.Length == 0 && fasteners.Length == 0,
                    "Economy: not started, but has transactions or fasteners.", errors);
                return;
            }

            long balance = 0;
            foreach (TransactionData entry in transactions)
            {
                if (entry == null || !Enum.IsDefined(typeof(TransactionKind), entry.kind))
                {
                    errors.Add("Economy: a transaction is empty or of an unknown kind.");
                    return;
                }

                balance += entry.amountCents;
            }

            Require(transactions.Length > 0, "Economy: started, but has no transactions.", errors);
            Require(balance >= 0, $"Economy: the balance is negative ({Money.FromCents(balance)}).", errors);

            var ids = new HashSet<string>();
            foreach (FastenerCountData entry in fasteners)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.fastenerId) || !ids.Add(entry.fastenerId) ||
                    entry.count < 0)
                {
                    errors.Add($"Economy: fastener count '{entry?.fastenerId}' is empty, duplicated or negative.");
                }
            }
        }

        private static void Require(bool condition, string message, List<string> errors)
        {
            if (!condition)
            {
                errors.Add(message);
            }
        }

        private static void RequireFinite(PoseData pose, string label, List<string> errors)
        {
            float[] values = { pose.px, pose.py, pose.pz, pose.rx, pose.ry, pose.rz, pose.rw };
            foreach (float value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    errors.Add($"{label}: position or rotation is not a finite number.");
                    return;
                }
            }
        }
    }
}
