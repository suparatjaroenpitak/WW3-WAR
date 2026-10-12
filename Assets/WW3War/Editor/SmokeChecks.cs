using System;
using UnityEditor;
using UnityEngine;
using WW3War.Core;

namespace WW3War.Editor
{
    public static class SmokeChecks
    {
        [MenuItem("WW3 War/Run Smoke Checks")]
        public static void Run()
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(BuildCommands.MainScene) != null, "Main scene is missing.");
            Require(Resources.Load<Shader>("FieldSurface") != null, "FieldSurface shader is missing.");
            var state = CampaignRules.NewCampaign();
            Require(CampaignRules.Validate(state, out string reason), reason);
            Require(CampaignRules.TryBeginAttack(state, "ridge", out var battle, out reason), reason);
            var restored = JsonUtility.FromJson<CampaignState>(JsonUtility.ToJson(state));
            Require(CampaignRules.Validate(restored, out reason), reason);
            Require(restored.PendingBattle.BattleId == battle.BattleId, "JsonUtility lost pending battle identity.");
            var result = new BattleResolution
            {
                BattleId = battle.BattleId, ProvinceId = battle.ProvinceId,
                PlayerWon = true, DurationSeconds = 30f
            };
            foreach (var unit in battle.PlayerUnits) result.Survivors.Add(unit.Copy());
            Require(CampaignRules.ResolveBattle(restored, result, out reason), reason);
            Require(restored.ArmyProvinceId == "ridge", "Winning did not move the army.");
            Require(!CampaignRules.ResolveBattle(restored, result), "Duplicate battle result was accepted.");
            Require(CampaignRules.Validate(restored, out reason), reason);
            var roundTrip = JsonUtility.FromJson<CampaignState>(JsonUtility.ToJson(restored));
            Require(CampaignRules.Validate(roundTrip, out reason), reason);
            Debug.Log("WW3 WAR: Editor smoke checks passed (scene, shader asset, JsonUtility, battle transaction). Play Mode and rendered shaders still require manual verification.");
        }

        static void Require(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException("WW3 WAR smoke check failed: " + reason);
        }
    }
}
