using UnityEngine;

namespace AyutthayaSimulator
{
    /// <summary>
    /// Interior streaming hook. The demo tile has no surveyed interior, so Load reports that honestly.
    /// </summary>
    public sealed class BuildingInteriorLoader : MonoBehaviour
    {
        public bool HasInteriorAsset;
        public string Status = "no_interior_asset";

        public bool LoadInterior()
        {
            if (!HasInteriorAsset)
            {
                Status = "no_interior_asset";
                return false;
            }
            Status = "loaded";
            return true;
        }
    }
}
