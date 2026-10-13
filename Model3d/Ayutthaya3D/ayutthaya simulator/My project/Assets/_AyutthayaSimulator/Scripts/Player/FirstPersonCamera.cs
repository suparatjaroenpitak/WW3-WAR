using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class FirstPersonCamera : MonoBehaviour
    {
        public Transform CameraTransform;
        public float MinPitch = -85f;
        public float MaxPitch = 85f;
        public bool HeadBobEnabled;
        public float HeadBobAmount = 0.03f;

        float pitch;
        float bob;
        Vector3 cameraLocalOrigin = new Vector3(0f, 1.62f, 0f);

        void Awake()
        {
            if (CameraTransform == null)
            {
                var cameraObject = GetComponentInChildren<Camera>();
                if (cameraObject != null) CameraTransform = cameraObject.transform;
            }
            if (CameraTransform != null) cameraLocalOrigin = CameraTransform.localPosition;
        }

        public void Tick(PlayerInputFrame input, PlayerState state)
        {
            if (CameraTransform == null || state.Paused || state.Mode != PlayerMode.OnFoot) return;
            transform.Rotate(0f, input.Look.x, 0f);
            pitch = Mathf.Clamp(pitch - input.Look.y, MinPitch, MaxPitch);
            CameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            var origin = cameraLocalOrigin;
            if (state.Crouching) origin.y = 1.05f;
            if (HeadBobEnabled && state.Grounded && state.Velocity.sqrMagnitude > 0.2f)
            {
                bob += Time.deltaTime * (state.Running ? 12f : 8f);
                origin.y += Mathf.Sin(bob) * HeadBobAmount;
            }
            CameraTransform.localPosition = origin;
        }

        public void SetYawPitch(float yaw, float newPitch)
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            pitch = Mathf.Clamp(newPitch, MinPitch, MaxPitch);
            if (CameraTransform != null) CameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
