using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class BuildingLODController : MonoBehaviour
    {
        public LODGroup Group;
        public int LodCount { get; private set; }

        public void Configure(Renderer lod0, Renderer lod1)
        {
            if (lod0 == null) return;
            Group = GetComponent<LODGroup>();
            if (Group == null) Group = gameObject.AddComponent<LODGroup>();
            if (lod1 == null)
            {
                Group.SetLODs(new[] { new LOD(0.02f, new[] { lod0 }) });
                LodCount = 1;
            }
            else
            {
                Group.SetLODs(new[]
                {
                    new LOD(0.35f, new[] { lod0 }),
                    new LOD(0.04f, new[] { lod1 })
                });
                LodCount = 2;
            }
            Group.RecalculateBounds();
        }
    }
}
