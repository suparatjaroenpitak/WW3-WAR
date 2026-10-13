using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class DoorController : InteractableBase
    {
        public bool PivotVerified;
        public float OpenAngle = 95f;
        public bool IsOpen { get; private set; }

        Quaternion closedRotation;
        Quaternion openRotation;

        void Awake()
        {
            PromptKey = "open_door";
            closedRotation = transform.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0f, OpenAngle, 0f);
        }

        public override void Interact(PlayerController player)
        {
            IsOpen = !IsOpen;
            if (player != null) player.NotifyInteracted(InteractId);
        }

        void Update()
        {
            var target = IsOpen ? openRotation : closedRotation;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, target, Time.deltaTime * 6f);
        }
    }
}
