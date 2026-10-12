using System;
using System.Collections.Generic;
using System.Text.Json;
using WW3War.Core;

internal static class Program
{
    static int passed;
    static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { IncludeFields = true };

    static void Main()
    {
        Check("new campaign and symmetric ten-province map", () =>
        {
            var state = CampaignRules.NewCampaign();
            Valid(state);
            Equal(10, state.Provinces.Count);
            Equal(3, state.Army.Count);
            Equal("harbor", state.ArmyProvinceId);
            foreach (var province in state.Provinces)
                foreach (string neighbor in province.Neighbors)
                    Require(Array.IndexOf(CampaignRules.GetProvince(state, neighbor).Neighbors, province.Id) >= 0);
        });
        Check("connected friendly movement spends action and supply", () =>
        {
            var state = CampaignRules.NewCampaign();
            Require(CampaignRules.MoveArmy(state, "vale", out _));
            Equal("vale", state.ArmyProvinceId); Equal(2, state.Actions); Equal(82, state.Supply); Valid(state);
        });
        Check("illegal movement leaves state unchanged", () =>
        {
            var state = CampaignRules.NewCampaign();
            string before = Json(state);
            Require(!CampaignRules.MoveArmy(state, "ridge", out _));
            Require(!CampaignRules.MoveArmy(state, "citadel", out _));
            Require(!CampaignRules.MoveArmy(state, null, out _));
            Equal(before, Json(state));
        });
        Check("all modern formations recruit with distinct IDs and costs", () =>
        {
            var state = CampaignRules.NewCampaign();
            foreach (UnitKind kind in Enum.GetValues(typeof(UnitKind))) Require(CampaignRules.Recruit(state, kind, out _));
            Equal(10, state.Credits); Equal(0, state.Actions); Equal(6, state.Army.Count); Valid(state);
            Require(!CampaignRules.Recruit(state, UnitKind.Infantry, out _));
        });
        Check("unknown unit, insufficient credits and army cap rejected", () =>
        {
            var state = CampaignRules.NewCampaign();
            Require(!CampaignRules.Recruit(state, (UnitKind)99, out _));
            state.Credits = 0;
            Require(!CampaignRules.Recruit(state, UnitKind.Armor, out _));
            state.Credits = 10000;
            for (int i = 4; i <= 12; i++) state.Army.Add(new FormationState("P" + i, UnitKind.Infantry));
            state.RecruitmentSequence = 12;
            Require(!CampaignRules.Recruit(state, UnitKind.Infantry, out _)); Valid(state);
        });
        Check("turn economy, replenishment and enemy expansion", () =>
        {
            var state = CampaignRules.NewCampaign();
            state.Army[0].Health = .5f;
            CampaignRules.EndTurn(state);
            Equal(497, state.Credits); Equal(97, state.Supply); Equal(2, state.Turn); Equal(3, state.Actions);
            Near(.58f, state.Army[0].Health);
            Equal(6, state.Provinces.FindAll(p => p.Owner == 2).Count);
            Equal(1, CampaignRules.GetProvince(state, state.ArmyProvinceId).Owner); Valid(state);
        });
        Check("unpaid army suffers attrition and eventually defeat", () =>
        {
            var state = CampaignRules.NewCampaign();
            foreach (var province in state.Provinces) province.Income = 0;
            state.Credits = 0;
            foreach (var unit in state.Army) unit.Health = .1f;
            CampaignRules.EndTurn(state);
            Require(state.Defeat); Equal(0, state.Army.Count); Valid(state);
        });
        Check("unsupplied army suffers attrition even with credits", () =>
        {
            var state = CampaignRules.NewCampaign();
            for (int i = 4; i <= 12; i++) state.Army.Add(new FormationState("P" + i, UnitKind.Infantry));
            state.RecruitmentSequence = 12; state.Credits = 10000; state.Supply = 0;
            CampaignRules.EndTurn(state);
            Equal(0, state.Supply); Near(.82f, state.Army[0].Health); Valid(state);
        });
        Check("low supply prevents attacks and movement without mutation", () =>
        {
            var state = CampaignRules.NewCampaign(); state.Supply = 2;
            string before = Json(state);
            Require(!CampaignRules.MoveArmy(state, "vale", out _));
            Require(!CampaignRules.TryBeginAttack(state, "ridge", out _, out _));
            Equal(before, Json(state));
        });
        Check("pending battle locks strategic actions and returns an isolated copy", () =>
        {
            var state = CampaignRules.NewCampaign();
            var battle = Attack(state, "ridge");
            Equal(77, state.Supply); Equal(2, state.Actions); Valid(state);
            string before = Json(state);
            Require(!CampaignRules.Recruit(state, UnitKind.Armor, out _));
            Require(!CampaignRules.MoveArmy(state, "vale", out _));
            Require(!CampaignRules.TryBeginAttack(state, "ridge", out _, out _));
            CampaignRules.EndTurn(state); Equal(before, Json(state));
            battle.PlayerUnits[0].Health = .1f;
            Near(1, state.PendingBattle.PlayerUnits[0].Health); Near(1, state.Army[0].Health);
        });
        Check("equal seeds yield equal enemies and battle identifiers", () =>
        {
            var a = CampaignRules.NewCampaign(43); var b = CampaignRules.NewCampaign(43);
            Equal(Json(Attack(a, "ridge")), Json(Attack(b, "ridge")));
        });
        Check("victory captures province and preserves casualties exactly once", () =>
        {
            var state = CampaignRules.NewCampaign(); var battle = Attack(state, "ridge");
            var result = Result(battle, true); result.Survivors[0].Health = .4f;
            Require(CampaignRules.ResolveBattle(state, result, out _));
            Equal("ridge", state.ArmyProvinceId); Equal(465, state.Credits); Near(.4f, state.Army[0].Health);
            Require(state.PendingBattle == null); Valid(state);
            string before = Json(state);
            Require(!CampaignRules.ResolveBattle(state, result)); Equal(before, Json(state));
        });
        Check("withdrawal preserves survivors but never captures territory", () =>
        {
            var state = CampaignRules.NewCampaign(); var battle = Attack(state, "ridge");
            Require(CampaignRules.ResolveBattle(state, Result(battle, false)));
            Equal("harbor", state.ArmyProvinceId); Equal(0, CampaignRules.GetProvince(state, "ridge").Owner);
            Equal(420, state.Credits); Valid(state);
        });
        Check("forged battle IDs, duplicate survivors, healing and NaN rejected atomically", () =>
        {
            var state = CampaignRules.NewCampaign(); var battle = Attack(state, "ridge");
            string before = Json(state);
            var result = Result(battle, true); result.BattleId = "forged";
            Require(!CampaignRules.ResolveBattle(state, result));
            result = Result(battle, true); result.Survivors[1] = result.Survivors[0].Copy();
            Require(!CampaignRules.ResolveBattle(state, result));
            result = Result(battle, true); result.Survivors[0].Health = 1.1f;
            Require(!CampaignRules.ResolveBattle(state, result));
            result = Result(battle, true); result.Survivors[0].Health = float.NaN;
            Require(!CampaignRules.ResolveBattle(state, result));
            result = Result(battle, true); result.DurationSeconds = float.PositiveInfinity;
            Require(!CampaignRules.ResolveBattle(state, result)); Equal(before, Json(state));
        });
        Check("victory needs survivors; total loss ends campaign", () =>
        {
            var state = CampaignRules.NewCampaign(); var battle = Attack(state, "ridge");
            var result = Result(battle, true); result.Survivors.Clear();
            Require(!CampaignRules.ResolveBattle(state, result));
            result.PlayerWon = false; Require(CampaignRules.ResolveBattle(state, result));
            Require(state.Defeat); Require(!state.Victory); Valid(state);
        });
        Check("campaign victory locks actions when last hostile province falls", () =>
        {
            var state = CampaignRules.NewCampaign();
            foreach (var province in state.Provinces) province.Owner = province.Id == "ridge" ? 2 : 1;
            var battle = Attack(state, "ridge"); Require(CampaignRules.ResolveBattle(state, Result(battle, true)));
            Require(state.Victory); Valid(state);
            string before = Json(state);
            Require(!CampaignRules.Recruit(state, UnitKind.Infantry, out _)); CampaignRules.EndTurn(state);
            Equal(before, Json(state));
        });
        Check("field-based JSON round trip validates normal and pending campaigns", () =>
        {
            var state = CampaignRules.NewCampaign();
            Valid(JsonSerializer.Deserialize<CampaignState>(Json(state), JsonOptions));
            Attack(state, "ridge");
            var loaded = JsonSerializer.Deserialize<CampaignState>(Json(state), JsonOptions);
            Valid(loaded); Equal(Json(state), Json(loaded));
        });
        Check("corrupt saves reject counters, map edits, rosters, outcomes and pending mismatch", () =>
        {
            CampaignState state;
            state = CampaignRules.NewCampaign(); state.Credits = -1; Invalid(state);
            state = CampaignRules.NewCampaign(); state.Provinces.RemoveAt(0); Invalid(state);
            state = CampaignRules.NewCampaign(); state.Provinces[0].Neighbors[0] = "citadel"; Invalid(state);
            state = CampaignRules.NewCampaign(); state.Army[0].Health = float.NaN; Invalid(state);
            state = CampaignRules.NewCampaign(); state.Army[1].Id = "P1"; Invalid(state);
            state = CampaignRules.NewCampaign(); state.Victory = true; Invalid(state);
            state = CampaignRules.NewCampaign(); Attack(state, "ridge"); state.PendingBattle.PlayerUnits[0].Health = .2f; Invalid(state);
            Require(!CampaignRules.Validate(null, out _));
        });
        Check("armor and cover damage rules use original arcade balance", () =>
        {
            Near(9.1f, CombatRules.ShotDamage(UnitKind.Infantry, UnitKind.Armor, false));
            Near(9.1f * .65f, CombatRules.ShotDamage(UnitKind.Infantry, UnitKind.Armor, true));
            Near(51f, CombatRules.ShotDamage(UnitKind.Artillery, UnitKind.Artillery, false));
            bool threw = false;
            try { CombatRules.ShotDamage((UnitKind)99, UnitKind.Infantry, false); }
            catch (ArgumentOutOfRangeException) { threw = true; }
            Require(threw);
        });
        Check("many seeds and repeated campaign operations remain valid", () =>
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var state = CampaignRules.NewCampaign(seed);
                for (int step = 0; step < 30 && !state.Victory && !state.Defeat; step++)
                {
                    if (state.Actions == 0) CampaignRules.EndTurn(state);
                    var current = CampaignRules.GetProvince(state, state.ArmyProvinceId);
                    string target = Array.Find(current.Neighbors, id => CampaignRules.GetProvince(state, id).Owner != 1);
                    if (target == null) CampaignRules.MoveArmy(state, current.Neighbors[step % current.Neighbors.Length], out _);
                    else if (CampaignRules.TryBeginAttack(state, target, out var battle, out _))
                        Require(CampaignRules.ResolveBattle(state, Result(battle, true)));
                    else CampaignRules.EndTurn(state);
                    Valid(state);
                }
            }
        });
        Console.WriteLine("PASS: " + passed + " checks. These verify core C# rules, NOT Unity rendering, input or JsonUtility.");
    }

    static BattleSpec Attack(CampaignState state, string target)
    {
        Require(CampaignRules.TryBeginAttack(state, target, out var battle, out string message), message);
        return battle;
    }
    static BattleResolution Result(BattleSpec battle, bool won)
    {
        var result = new BattleResolution { BattleId = battle.BattleId, ProvinceId = battle.ProvinceId, PlayerWon = won, DurationSeconds = 20f };
        foreach (var unit in battle.PlayerUnits) result.Survivors.Add(unit.Copy());
        return result;
    }
    static void Check(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception exception) { Console.Error.WriteLine("FAIL " + name + ": " + exception); Environment.Exit(1); }
    }
    static string Json<T>(T value) { return JsonSerializer.Serialize(value, JsonOptions); }
    static void Valid(CampaignState state) { Require(CampaignRules.Validate(state, out string reason), reason); }
    static void Invalid(CampaignState state) { Require(!CampaignRules.Validate(state, out _)); }
    static void Require(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
    static void Equal<T>(T expected, T actual) { Require(EqualityComparer<T>.Default.Equals(expected, actual), "Expected " + expected + ", got " + actual); }
    static void Near(float expected, float actual) { Require(Math.Abs(expected - actual) < .0001f, "Expected " + expected + ", got " + actual); }
}
