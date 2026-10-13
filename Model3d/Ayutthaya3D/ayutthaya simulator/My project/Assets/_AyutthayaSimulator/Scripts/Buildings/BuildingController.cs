using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class BuildingController : MonoBehaviour
    {
        public BuildingMetadata Metadata;
        public DoorController[] Doors = System.Array.Empty<DoorController>();
        public bool InteriorLoaded;

        void Awake()
        {
            if (Metadata == null) Metadata = GetComponent<BuildingMetadata>();
            if (Doors == null || Doors.Length == 0) Doors = GetComponentsInChildren<DoorController>(true);
        }

        public int DoorCount => Doors == null ? 0 : Doors.Length;
    }
}
