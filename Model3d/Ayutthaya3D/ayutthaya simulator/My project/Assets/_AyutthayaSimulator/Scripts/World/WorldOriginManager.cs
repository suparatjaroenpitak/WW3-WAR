using System.Collections.Generic;
using AyutthayaSimulator.Core;
using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class WorldOriginManager : MonoBehaviour
    {
        public float ShiftThreshold = 2000f;
        public Vector3 TotalShift { get; private set; }

        readonly List<Transform> roots = new List<Transform>();

        public void Register(Transform root)
        {
            if (root != null && !roots.Contains(root)) roots.Add(root);
        }

        public bool ShiftIfNeeded(Vector3 playerPosition, Transform player)
        {
            if (!TileStreamingPolicy.ShouldShiftOrigin(playerPosition.x, playerPosition.z, ShiftThreshold)) return false;
            var shift = new Vector3(-playerPosition.x, 0f, -playerPosition.z);
            for (var i = 0; i < roots.Count; i++)
            {
                if (roots[i] != null) roots[i].position += shift;
            }
            if (player != null) player.position += shift;
            TotalShift += shift;
            return true;
        }
    }
}
