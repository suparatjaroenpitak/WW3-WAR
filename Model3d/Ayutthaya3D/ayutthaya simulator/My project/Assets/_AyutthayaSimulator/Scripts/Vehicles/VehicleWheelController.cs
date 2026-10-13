using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class VehicleWheelController : MonoBehaviour
    {
        public WheelCollider FrontLeft;
        public WheelCollider FrontRight;
        public WheelCollider RearLeft;
        public WheelCollider RearRight;
        public Transform VisualFrontLeft;
        public Transform VisualFrontRight;
        public Transform VisualRearLeft;
        public Transform VisualRearRight;

        public void Apply(float motorTorque, float steerAngle, float brakeTorque)
        {
            if (FrontLeft == null || FrontRight == null || RearLeft == null || RearRight == null) return;
            FrontLeft.steerAngle = steerAngle;
            FrontRight.steerAngle = steerAngle;
            RearLeft.motorTorque = motorTorque;
            RearRight.motorTorque = motorTorque;
            FrontLeft.brakeTorque = brakeTorque;
            FrontRight.brakeTorque = brakeTorque;
            RearLeft.brakeTorque = brakeTorque;
            RearRight.brakeTorque = brakeTorque;
            Sync(FrontLeft, VisualFrontLeft);
            Sync(FrontRight, VisualFrontRight);
            Sync(RearLeft, VisualRearLeft);
            Sync(RearRight, VisualRearRight);
        }

        static void Sync(WheelCollider collider, Transform visual)
        {
            if (collider == null || visual == null) return;
            collider.GetWorldPose(out var position, out var rotation);
            visual.SetPositionAndRotation(position, rotation);
        }
    }
}
