using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class VehicleEngine : MonoBehaviour
    {
        public float MotorTorque = 1100f;
        public float BrakeTorque = 2500f;
        public float MaxSteerAngle = 28f;
        public float CurrentSpeedKph { get; private set; }

        public void Apply(VehicleWheelController wheels, VehicleInputFrame input, Rigidbody body)
        {
            if (wheels == null) return;
            wheels.Apply(input.Throttle * MotorTorque, input.Steer * MaxSteerAngle, input.Brake ? BrakeTorque : 0f);
            if (body != null) CurrentSpeedKph = body.linearVelocity.magnitude * 3.6f;
        }
    }
}
