using UnityEngine;

namespace AyutthayaSimulator
{
    /// <summary>
    /// Procedural bob used when no authored Animator controller is assigned.
    /// </summary>
    public sealed class NPCAnimation : MonoBehaviour
    {
        public Transform Visual;
        public bool HasAuthoredController;

        public void Tick(NpcMode mode)
        {
            if (HasAuthoredController || Visual == null) return;
            var bob = mode == NpcMode.Walk ? Mathf.Sin(Time.time * 8f) * 0.04f : 0f;
            Visual.localPosition = new Vector3(0f, 0.9f + bob, 0f);
        }
    }
}
