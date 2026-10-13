using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class PlayerInteraction : MonoBehaviour
    {
        public Camera ViewCamera;
        public float Distance = 3.5f;
        public IInteractable Current { get; private set; }

        public void Tick(PlayerInputFrame input, PlayerState state)
        {
            Current = null;
            if (state.Paused || state.Mode != PlayerMode.OnFoot || ViewCamera == null) return;
            var ray = new Ray(ViewCamera.transform.position, ViewCamera.transform.forward);
            if (Physics.Raycast(ray, out var hit, Distance, ~0, QueryTriggerInteraction.Collide))
            {
                Current = hit.collider.GetComponentInParent<IInteractable>();
            }
            if (input.Interact && Current != null)
            {
                var player = GetComponent<PlayerController>();
                Current.Interact(player);
            }
        }
    }
}
