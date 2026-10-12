namespace WW3War.Core
{
    public static class CombatRules
    {
        // Arcade model: no penetration tables or real weapon specifications.
        public const float CoverMultiplier = 0.65f;

        public static float ShotDamage(UnitKind attacker, UnitKind defender, bool inCover)
        {
            var attack = UnitCatalog.Get(attacker);
            var defense = UnitCatalog.Get(defender);
            return attack.Damage * (1f - defense.Armor) * (inCover ? CoverMultiplier : 1f);
        }
    }
}
