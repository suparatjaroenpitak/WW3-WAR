using System;
using System.Collections.Generic;

namespace WW3War.Core
{
    public static class CampaignRules
    {
        public const int ActionsPerTurn = 3;
        public const int MaxArmySize = 12;
        public const int MaximumCredits = 1000000;

        public static CampaignState NewCampaign(int seed = 2026)
        {
            return new CampaignState
            {
                Turn = 1, Credits = 420, Supply = 85, Actions = ActionsPerTurn,
                ArmyProvinceId = "harbor", Seed = seed, RecruitmentSequence = 3,
                Army = new List<FormationState>
                {
                    new FormationState("P1", UnitKind.Infantry),
                    new FormationState("P2", UnitKind.Armor),
                    new FormationState("P3", UnitKind.Artillery)
                },
                Provinces = CreateMap()
            };
        }

        public static ProvinceState GetProvince(CampaignState state, string id)
        {
            if (state == null || state.Provinces == null || id == null) return null;
            foreach (var province in state.Provinces)
                if (province != null && province.Id == id) return province;
            return null;
        }

        public static bool MoveArmy(CampaignState state, string id, out string message)
        {
            if (!CanAct(state, out message)) return false;
            var target = GetProvince(state, id);
            if (!IsAdjacent(state, id)) return Fail("Choose a connected neighboring province.", out message);
            if (target.Owner != 1) return Fail("Attack this province before moving into it.", out message);
            if (state.Supply < 3) return Fail("Moving requires 3 supply. End the turn to replenish.", out message);
            state.ArmyProvinceId = id;
            state.Actions--;
            state.Supply -= 3;
            message = "Army moved to " + target.Name + ".";
            return true;
        }

        public static bool Recruit(CampaignState state, UnitKind kind, out string message)
        {
            if (!CanAct(state, out message)) return false;
            if (!UnitCatalog.IsValid(kind)) return Fail("Unknown formation type.", out message);
            if (state.Army.Count >= MaxArmySize) return Fail("The army is at its 12-formation limit.", out message);
            var location = GetProvince(state, state.ArmyProvinceId);
            if (location == null || location.Owner != 1) return Fail("Recruit in a friendly province.", out message);
            var stats = UnitCatalog.Get(kind);
            if (state.Credits < stats.Cost) return Fail("Insufficient credits for " + stats.Name + ".", out message);
            if (state.RecruitmentSequence >= int.MaxValue) return Fail("Formation identifier limit reached.", out message);
            state.RecruitmentSequence++;
            state.Army.Add(new FormationState("P" + state.RecruitmentSequence, kind));
            state.Credits -= stats.Cost;
            state.Actions--;
            message = stats.Name + " recruited.";
            return true;
        }

        public static string EndTurn(CampaignState state)
        {
            string blocked;
            if (!CanContinue(state, out blocked)) return blocked;
            if (state.Turn >= 1000000) return "Campaign turn limit reached.";
            int income = 0, upkeep = 0;
            foreach (var province in state.Provinces)
                if (province.Owner == 1) income += province.Income;
            foreach (var unit in state.Army) upkeep += UnitCatalog.Get(unit.Kind).Upkeep;
            bool unpaid = state.Credits + income < upkeep;
            state.Credits = Math.Max(0, Math.Min(MaximumCredits, state.Credits + income - upkeep));
            int supplyNeed = state.Army.Count * 2;
            bool unsupplied = state.Supply + 18 < supplyNeed;
            state.Supply = Math.Max(0, Math.Min(100, state.Supply + 18 - supplyNeed));
            float change = unpaid || unsupplied ? -0.18f : 0.08f;
            for (int i = state.Army.Count - 1; i >= 0; i--)
            {
                state.Army[i].Health = Math.Min(1f, state.Army[i].Health + change);
                if (state.Army[i].Health <= 0f) state.Army.RemoveAt(i);
            }
            state.Turn++;
            state.Actions = ActionsPerTurn;
            UpdateOutcome(state);
            string expansion = state.Defeat || state.Victory ? "" : ExpandEnemy(state);
            UpdateOutcome(state);
            return "Turn " + state.Turn + ": +" + income + " income, -" + upkeep + " upkeep. "
                + (unpaid || unsupplied ? "Shortages caused 18% army attrition. " : "Army recovered 8% health. ")
                + expansion + (state.Defeat ? "The expedition has been defeated." : "");
        }

        public static bool TryBeginAttack(CampaignState state, string id, out BattleSpec battle, out string message)
        {
            battle = null;
            if (!CanAct(state, out message)) return false;
            if (!IsAdjacent(state, id)) return Fail("Attack a connected neighboring province.", out message);
            var target = GetProvince(state, id);
            if (target.Owner == 1) return Fail("This province is already friendly.", out message);
            if (state.Supply < 8) return Fail("Attacking requires 8 supply. End the turn to replenish.", out message);
            if (state.BattleSequence >= int.MaxValue) return Fail("Battle identifier limit reached.", out message);
            int sequence = state.BattleSequence + 1;
            int battleSeed = MixSeed(state.Seed, state.Turn, sequence);
            uint random = unchecked((uint)battleSeed);
            var pending = new BattleSpec
            {
                BattleId = state.Seed + ":" + sequence + ":" + state.Turn + ":" + id,
                ProvinceId = id, OriginProvinceId = state.ArmyProvinceId,
                Name = "Operation " + target.Name, Seed = battleSeed
            };
            foreach (var unit in state.Army) pending.PlayerUnits.Add(unit.Copy());
            int count = target.Owner == 0 ? 2 : 2 + (int)(Next(ref random) % 2);
            if (id == "citadel") count++;
            for (int i = 0; i < count; i++)
            {
                UnitKind kind = i == 0 ? UnitKind.Infantry : (UnitKind)(Next(ref random) % 3);
                pending.EnemyUnits.Add(new FormationState("E" + sequence + "_" + i, kind));
            }
            state.BattleSequence = sequence;
            state.Actions--;
            state.Supply -= 8;
            state.PendingBattle = pending;
            battle = pending.Copy();
            message = pending.Name + " ready.";
            return true;
        }

        public static bool ResolveBattle(CampaignState state, BattleResolution resolution)
        {
            string ignored;
            return ResolveBattle(state, resolution, out ignored);
        }

        public static bool ResolveBattle(CampaignState state, BattleResolution resolution, out string message)
        {
            if (state == null || state.PendingBattle == null)
                return Fail("No battle is awaiting a result.", out message);
            var pending = state.PendingBattle;
            if (resolution == null || resolution.BattleId != pending.BattleId || resolution.ProvinceId != pending.ProvinceId)
                return Fail("This result belongs to a different or completed battle.", out message);
            if (!Finite(resolution.DurationSeconds) || resolution.DurationSeconds < 0f)
                return Fail("Invalid battle duration.", out message);
            if (resolution.Survivors == null || resolution.Survivors.Count > pending.PlayerUnits.Count)
                return Fail("Invalid survivor roster.", out message);
            var survivors = new List<FormationState>();
            var ids = new HashSet<string>();
            foreach (var unit in resolution.Survivors)
            {
                if (!ValidUnit(unit) || !ids.Add(unit.Id)) return Fail("Invalid or duplicate survivor.", out message);
                var original = FindUnit(pending.PlayerUnits, unit.Id);
                if (original == null || original.Kind != unit.Kind || unit.Health > original.Health + 0.00001f)
                    return Fail("Survivors must match the committed army and cannot gain health in battle.", out message);
                survivors.Add(unit.Copy());
            }
            if (resolution.PlayerWon && survivors.Count == 0)
                return Fail("A victory requires at least one surviving formation.", out message);
            var target = GetProvince(state, pending.ProvinceId);
            if (target == null || target.Owner == 1 || state.ArmyProvinceId != pending.OriginProvinceId)
                return Fail("Campaign no longer matches the committed battle.", out message);
            state.Army = survivors;
            if (resolution.PlayerWon)
            {
                target.Owner = 1;
                state.ArmyProvinceId = target.Id;
                state.Credits = Math.Min(MaximumCredits, state.Credits + 45);
            }
            state.PendingBattle = null;
            UpdateOutcome(state);
            message = resolution.PlayerWon ? target.Name + " secured. +45 credits." : "Operation failed. Survivors withdrew.";
            if (state.Victory) message += " Campaign victory: all opposing provinces secured.";
            if (state.Defeat) message += " The expedition has been defeated.";
            return true;
        }

        // Run this before accepting a deserialized save. No Unity runtime dependency.
        public static bool Validate(CampaignState state, out string message)
        {
            if (state == null) return Fail("Save contains no campaign.", out message);
            if (state.Turn < 1 || state.Turn > 1000000 || state.Credits < 0 || state.Credits > MaximumCredits
                || state.Supply < 0 || state.Supply > 100 || state.Actions < 0 || state.Actions > ActionsPerTurn
                || state.BattleSequence < 0 || state.RecruitmentSequence < 3)
                return Fail("Save contains invalid campaign counters.", out message);
            if (state.Provinces == null || state.Provinces.Count != 10)
                return Fail("Save must contain all ten provinces.", out message);
            var canonical = CreateMap();
            var ids = new HashSet<string>();
            int friendly = 0, enemy = 0;
            foreach (var province in state.Provinces)
            {
                if (province == null || !ValidId(province.Id) || !ids.Add(province.Id)
                    || string.IsNullOrWhiteSpace(province.Name) || province.Name.Length > 100
                    || province.Owner < 0 || province.Owner > 2 || province.Income < 0 || province.Income > 1000
                    || !Finite(province.MapX) || !Finite(province.MapY) || province.MapX < 0f || province.MapX > 1f
                    || province.MapY < 0f || province.MapY > 1f || province.Neighbors == null)
                    return Fail("Save contains invalid province data.", out message);
                ProvinceState expected = canonical.Find(p => p.Id == province.Id);
                if (expected == null || province.Neighbors.Length != expected.Neighbors.Length)
                    return Fail("Save contains an unknown province or altered connections.", out message);
                var links = new HashSet<string>();
                foreach (string neighbor in province.Neighbors)
                    if (!links.Add(neighbor) || Array.IndexOf(expected.Neighbors, neighbor) < 0)
                        return Fail("Save contains invalid province connections.", out message);
                if (province.Owner == 1) friendly++;
                if (province.Owner == 2) enemy++;
            }
            var location = GetProvince(state, state.ArmyProvinceId);
            if (location == null || location.Owner != 1)
                return Fail("Army must be based in a friendly province.", out message);
            if (!ValidRoster(state.Army, true, MaxArmySize, out message)) return false;
            foreach (var unit in state.Army)
            {
                int number;
                if (!unit.Id.StartsWith("P", StringComparison.Ordinal) || !int.TryParse(unit.Id.Substring(1), out number)
                    || number < 1 || number > state.RecruitmentSequence)
                    return Fail("Save contains an invalid player formation identifier.", out message);
            }
            bool shouldLose = state.Army.Count == 0 || friendly == 0;
            bool shouldWin = !shouldLose && enemy == 0;
            if (state.Defeat != shouldLose || state.Victory != shouldWin)
                return Fail("Save outcome does not match the campaign.", out message);
            if (state.PendingBattle != null)
            {
                var pending = state.PendingBattle;
                var target = GetProvince(state, pending.ProvinceId);
                if (state.Defeat || state.Victory || state.BattleSequence < 1 || !ValidId(pending.BattleId)
                    || pending.BattleId != state.Seed + ":" + state.BattleSequence + ":" + state.Turn + ":" + pending.ProvinceId
                    || pending.Seed != MixSeed(state.Seed, state.Turn, state.BattleSequence)
                    || pending.OriginProvinceId != state.ArmyProvinceId || target == null || target.Owner == 1
                    || !IsAdjacent(state, pending.ProvinceId) || string.IsNullOrWhiteSpace(pending.Name))
                    return Fail("Save contains an invalid pending battle.", out message);
                if (!ValidRoster(pending.PlayerUnits, false, MaxArmySize, out message)
                    || !ValidRoster(pending.EnemyUnits, false, 6, out message)) return false;
                if (pending.PlayerUnits.Count != state.Army.Count)
                    return Fail("Pending battle army differs from the saved army.", out message);
                foreach (var unit in pending.PlayerUnits)
                {
                    var actual = FindUnit(state.Army, unit.Id);
                    if (actual == null || actual.Kind != unit.Kind || Math.Abs(actual.Health - unit.Health) > 0.00001f)
                        return Fail("Pending battle army differs from the saved army.", out message);
                }
            }
            message = "Campaign is valid.";
            return true;
        }

        public static bool IsAdjacent(CampaignState state, string id)
        {
            var current = state == null ? null : GetProvince(state, state.ArmyProvinceId);
            return current != null && current.Neighbors != null && GetProvince(state, id) != null
                && Array.IndexOf(current.Neighbors, id) >= 0;
        }

        private static bool CanContinue(CampaignState state, out string message)
        {
            if (state == null) return Fail("Create or load a campaign first.", out message);
            if (state.Victory) return Fail("The campaign has already been won.", out message);
            if (state.Defeat || state.Army == null || state.Army.Count == 0) return Fail("The campaign has ended in defeat.", out message);
            if (state.PendingBattle != null) return Fail("Finish the pending battle first.", out message);
            message = "";
            return true;
        }

        private static bool CanAct(CampaignState state, out string message)
        {
            if (!CanContinue(state, out message)) return false;
            if (state.Actions <= 0) return Fail("No actions remain. End the turn to continue.", out message);
            return true;
        }

        private static string ExpandEnemy(CampaignState state)
        {
            if (state.Turn % 2 != 0) return "";
            var candidates = new List<ProvinceState>();
            foreach (var province in state.Provinces)
            {
                if (province.Owner == 2 || province.Id == state.ArmyProvinceId) continue;
                foreach (string neighborId in province.Neighbors)
                {
                    var neighbor = GetProvince(state, neighborId);
                    if (neighbor != null && neighbor.Owner == 2)
                    {
                        candidates.Add(province);
                        break;
                    }
                }
            }
            if (candidates.Count == 0) return "";
            candidates.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            uint random = unchecked((uint)MixSeed(state.Seed, state.Turn, 913));
            var captured = candidates[(int)(Next(ref random) % (uint)candidates.Count)];
            captured.Owner = 2;
            return "Opposing forces occupied " + captured.Name + ". ";
        }

        private static void UpdateOutcome(CampaignState state)
        {
            bool friendly = false, enemy = false;
            foreach (var province in state.Provinces)
            {
                friendly |= province.Owner == 1;
                enemy |= province.Owner == 2;
            }
            state.Defeat = state.Army.Count == 0 || !friendly;
            state.Victory = !state.Defeat && !enemy;
        }

        private static FormationState FindUnit(List<FormationState> roster, string id)
        {
            if (roster == null) return null;
            foreach (var unit in roster) if (unit != null && unit.Id == id) return unit;
            return null;
        }

        private static bool ValidRoster(List<FormationState> roster, bool allowEmpty, int max, out string message)
        {
            if (roster == null || (!allowEmpty && roster.Count == 0) || roster.Count > max)
                return Fail("Save contains an invalid formation roster.", out message);
            var ids = new HashSet<string>();
            foreach (var unit in roster)
                if (!ValidUnit(unit) || !ids.Add(unit.Id)) return Fail("Save contains an invalid or duplicate formation.", out message);
            message = "";
            return true;
        }

        private static bool ValidUnit(FormationState unit)
        {
            return unit != null && ValidId(unit.Id) && UnitCatalog.IsValid(unit.Kind)
                && Finite(unit.Health) && unit.Health > 0f && unit.Health <= 1f;
        }

        private static bool ValidId(string id) { return !string.IsNullOrWhiteSpace(id) && id.Length <= 96; }
        private static bool Finite(float number) { return !float.IsNaN(number) && !float.IsInfinity(number); }
        private static bool Fail(string reason, out string message) { message = reason; return false; }
        private static int MixSeed(int seed, int turn, int sequence)
        {
            unchecked { return (seed * 73856093) ^ (turn * 19349663) ^ (sequence * 83492791); }
        }
        private static uint Next(ref uint state)
        {
            if (state == 0) state = 0x9E3779B9u;
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return state;
        }

        private static List<ProvinceState> CreateMap()
        {
            return new List<ProvinceState>
            {
                Province("harbor", "Aster Harbor", 1, 65, .12f, .25f, "vale", "ridge"),
                Province("vale", "Meridian Vale", 1, 40, .12f, .68f, "harbor", "crossroads"),
                Province("ridge", "Kestrel Ridge", 0, 35, .32f, .19f, "harbor", "crossroads", "basin"),
                Province("crossroads", "Orion Crossing", 0, 50, .34f, .5f, "vale", "ridge", "delta", "works"),
                Province("delta", "Cobalt Delta", 0, 45, .35f, .82f, "crossroads", "pass"),
                Province("basin", "Helix Basin", 2, 50, .55f, .19f, "ridge", "works", "citadel"),
                Province("works", "Vesper Works", 2, 65, .56f, .49f, "crossroads", "basin", "pass", "citadel"),
                Province("pass", "Ember Pass", 2, 35, .59f, .82f, "delta", "works", "reach"),
                Province("citadel", "Obsidian Citadel", 2, 80, .82f, .3f, "basin", "works", "reach"),
                Province("reach", "Solstice Reach", 2, 45, .85f, .73f, "pass", "citadel")
            };
        }

        private static ProvinceState Province(string id, string name, int owner, int income, float x, float y, params string[] neighbors)
        {
            return new ProvinceState { Id = id, Name = name, Owner = owner, Income = income,
                MapX = x, MapY = y, Neighbors = neighbors };
        }
    }
}
