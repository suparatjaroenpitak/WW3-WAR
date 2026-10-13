using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class VehicleCamera : MonoBehaviour
    {
        public Camera DriveCameraComponent;
        public Transform LookTarget;

        public void SetActiveCamera(bool active)
        {
            if (DriveCameraComponent != null) DriveCameraComponent.enabled = active;
            var listener = DriveCameraComponent != null ? DriveCameraComponent.GetComponent<AudioListener>() : null;
            if (listener != null) listener.enabled = active;
        }

        void LateUpdate()
        {
            if (DriveCameraComponent == null || !DriveCameraComponent.enabled || LookTarget == null) return;
            var desired = LookTarget.position - LookTarget.forward * 7f + Vector3.up * 2.6f;
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * 6f);
            transform.LookAt(LookTarget.position + Vector3.up * 1f);
        }
    }
}
