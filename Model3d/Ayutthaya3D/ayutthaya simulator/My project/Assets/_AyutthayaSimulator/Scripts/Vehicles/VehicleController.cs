using UnityEngine;

namespace AyutthayaSimulator
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VehicleController : InteractableBase
    {
        public VehicleInput InputSource;
        public VehicleEngine Engine;
        public VehicleWheelController Wheels;
        public VehicleCamera DriveCamera;
        public VehicleEnterExit EnterExit;
        public VehicleAudio AudioSourceDriver;
        public string VehicleId = "demo_vehicle";
        public bool Occupied { get; private set; }

        Rigidbody body;

        void Awake()
        {
            PromptKey = "enter_vehicle";
            Id = VehicleId;
            body = GetComponent<Rigidbody>();
            body.mass = 1200f;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.centerOfMass = new Vector3(0f, -0.3f, 0f);
        }

        public override void Interact(PlayerController player)
        {
            if (EnterExit != null) EnterExit.Enter(player, this);
        }

        public void SetOccupied(bool occupied)
        {
            Occupied = occupied;
            if (InputSource != null) InputSource.EnabledInput = occupied;
            if (DriveCamera != null) DriveCamera.SetActiveCamera(occupied);
        }

        void FixedUpdate()
        {
            if (!Occupied || Engine == null || InputSource == null) return;
            Engine.Apply(Wheels, InputSource.Read(), body);
        }

        void Update()
        {
            if (!Occupied || InputSource == null || EnterExit == null) return;
            if (InputSource.Read().Exit) EnterExit.Exit();
        }
    }
}
