using UnityEngine;

namespace WW3War
{
    public sealed class RTSCamera : MonoBehaviour
    {
        public Vector3 Focus = Vector3.zero;
        public bool InputEnabled;
        float distance = 86;
        float yaw = 0;

        void LateUpdate()
        {
            if(InputEnabled)
            {
                var forward=Quaternion.Euler(0,yaw,0)*Vector3.forward;
                var right=Quaternion.Euler(0,yaw,0)*Vector3.right;
                float dx=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0);
                float dz=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0);
                Focus+=(right*dx+forward*dz)*Time.unscaledDeltaTime*distance*.55f;
                if(Input.GetKey(KeyCode.Q)) yaw-=Time.unscaledDeltaTime*55;
                if(Input.GetKey(KeyCode.E)) yaw+=Time.unscaledDeltaTime*55;
                distance=Mathf.Clamp(distance-Input.mouseScrollDelta.y*5,26,125);
                Focus.x=Mathf.Clamp(Focus.x,-70,70); Focus.z=Mathf.Clamp(Focus.z,-48,48);
            }
            transform.rotation=Quaternion.Euler(57,yaw,0);
            transform.position=Focus-transform.forward*distance;
        }

        public void ResetView() { Focus=Vector3.zero; distance=86; yaw=0; }
    }
}
