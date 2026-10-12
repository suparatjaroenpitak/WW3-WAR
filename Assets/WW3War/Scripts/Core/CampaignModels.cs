using System;
using System.Collections.Generic;

namespace WW3War.Core
{
    public enum UnitKind { Infantry, Armor, Artillery }

    [Serializable]
    public sealed class FormationState
    {
        public string Id;
        public UnitKind Kind;
        public float Health = 1f;

        public FormationState() { }
        public FormationState(string id, UnitKind kind, float health = 1f)
        {
            Id = id;
            Kind = kind;
            Health = health;
        }
        public FormationState Copy() { return new FormationState(Id, Kind, Health); }
    }

    [Serializable]
    public sealed class ProvinceState
    {
        public string Id;
        public string Name;
        public int Owner;
        public int Income;
        public float MapX;
        public float MapY;
        public string[] Neighbors;
    }

    [Serializable]
    public sealed class CampaignState
    {
        public int Turn;
        public int Credits;
        public int Supply;
        public int Actions;
        public string ArmyProvinceId;
        public List<ProvinceState> Provinces = new List<ProvinceState>();
        public List<FormationState> Army = new List<FormationState>();
        public int Seed;
        public bool Victory;
        public bool Defeat;
        public int BattleSequence;
        public int RecruitmentSequence;
        public BattleSpec PendingBattle;
    }

    [Serializable]
    public sealed class BattleSpec
    {
        public string BattleId;
        public string ProvinceId;
        public string OriginProvinceId;
        public string Name;
        public int Seed;
        public List<FormationState> PlayerUnits = new List<FormationState>();
        public List<FormationState> EnemyUnits = new List<FormationState>();

        public BattleSpec Copy()
        {
            var copy = new BattleSpec
            {
                BattleId = BattleId, ProvinceId = ProvinceId,
                OriginProvinceId = OriginProvinceId, Name = Name, Seed = Seed
            };
            foreach (var unit in PlayerUnits) copy.PlayerUnits.Add(unit.Copy());
            foreach (var unit in EnemyUnits) copy.EnemyUnits.Add(unit.Copy());
            return copy;
        }
    }

    [Serializable]
    public sealed class BattleResolution
    {
        public string BattleId;
        public string ProvinceId;
        public bool PlayerWon;
        public List<FormationState> Survivors = new List<FormationState>();
        public float DurationSeconds;
    }

    public sealed class UnitStats
    {
        public string Name;
        public int Cost;
        public int Upkeep;
        public float MaxHealth;
        public float Speed;
        public float Range;
        public float Damage;
        public float Reload;
        public float Armor;
    }
}
