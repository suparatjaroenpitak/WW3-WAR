using UnityEngine;
using UnityEngine.InputSystem;

namespace AyutthayaSimulator
{
    public struct VehicleInputFrame
    {
        public float Throttle;
        public float Steer;
        public bool Brake;
        public bool Exit;
    }

    public sealed class VehicleInput : MonoBehaviour
    {
        public bool EnabledInput;

        public VehicleInputFrame Read()
        {
            var frame = new VehicleInputFrame();
            if (!EnabledInput) return frame;
            var keyboard = Keyboard.current;
            if (keyboard == null) return frame;
            frame.Throttle = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            frame.Steer = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            frame.Brake = keyboard.spaceKey.isPressed;
            frame.Exit = keyboard.fKey.wasPressedThisFrame;
            return frame;
        }
    }
}
