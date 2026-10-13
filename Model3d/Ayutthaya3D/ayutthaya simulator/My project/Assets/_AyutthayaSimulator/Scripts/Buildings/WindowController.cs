using UnityEngine;

namespace AyutthayaSimulator
{
    /// <summary>
    /// Bound only when a child mesh was verified. Does not invent window geometry.
    /// </summary>
    public sealed class WindowController : MonoBehaviour
    {
        public bool MeshVerified;
        public bool Open;

        public void SetOpen(bool open)
        {
            if (!MeshVerified) return;
            Open = open;
        }
    }
}
