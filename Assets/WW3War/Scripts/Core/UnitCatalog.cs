using System;

namespace WW3War.Core
{
    public static class UnitCatalog
    {
        // Arcade balance values, not measurements of real military equipment.
        public static UnitStats Get(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Infantry:
                    return new UnitStats { Name = "Mechanized Infantry", Cost = 90, Upkeep = 6,
                        MaxHealth = 110f, Speed = 4.8f, Range = 24f, Damage = 13f, Reload = 1.2f, Armor = 0.05f };
                case UnitKind.Armor:
                    return new UnitStats { Name = "Main Battle Armor", Cost = 150, Upkeep = 12,
                        MaxHealth = 230f, Speed = 6.2f, Range = 32f, Damage = 35f, Reload = 2.5f, Armor = 0.3f };
                case UnitKind.Artillery:
                    return new UnitStats { Name = "Mobile Artillery", Cost = 170, Upkeep = 10,
                        MaxHealth = 90f, Speed = 3.7f, Range = 54f, Damage = 51f, Reload = 4.5f, Armor = 0f };
                default:
                    throw new ArgumentOutOfRangeException("kind", kind, "Unknown formation type.");
            }
        }

        public static bool IsValid(UnitKind kind)
        {
            return kind == UnitKind.Infantry || kind == UnitKind.Armor || kind == UnitKind.Artillery;
        }
    }
}
