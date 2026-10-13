using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AyutthayaSimulator
{
    public struct PlayerInputFrame
    {
        public Vector2 Move;
        public Vector2 Look;
        public bool Run;
        public bool CrouchPressed;
        public bool Jump;
        public bool Interact;
        public bool ExitVehicle;
        public bool Pause;
        public bool Save;
        public bool Load;
        public bool ToggleLanguage;
        public bool ToggleWeather;
        public bool Screenshot;
    }

    /// <summary>
    /// Reads the Input System devices directly so gameplay does not depend on a generated input asset.
    /// </summary>
    public sealed class PlayerInputHandler : MonoBehaviour
    {
        public float LookSensitivity = 0.08f;

        public PlayerInputFrame Read(bool paused, bool cursorLocked)
        {
            var frame = new PlayerInputFrame();
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null) return frame;

            frame.Pause = keyboard.escapeKey.wasPressedThisFrame;
            frame.Save = keyboard.f5Key.wasPressedThisFrame;
            frame.Load = keyboard.f9Key.wasPressedThisFrame;
            frame.ToggleLanguage = keyboard.lKey.wasPressedThisFrame;
            frame.ToggleWeather = keyboard.rKey.wasPressedThisFrame;
            frame.Screenshot = keyboard.pKey.wasPressedThisFrame;
            if (paused) return frame;

            var x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            var z = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            frame.Move = Vector2.ClampMagnitude(new Vector2(x, z), 1f);
            frame.Run = keyboard.leftShiftKey.isPressed;
            frame.CrouchPressed = keyboard.cKey.wasPressedThisFrame || keyboard.leftCtrlKey.wasPressedThisFrame;
            frame.Jump = keyboard.spaceKey.wasPressedThisFrame;
            frame.Interact = keyboard.eKey.wasPressedThisFrame;
            frame.ExitVehicle = keyboard.fKey.wasPressedThisFrame;
            if (cursorLocked && mouse != null)
                frame.Look = mouse.delta.ReadValue() * LookSensitivity;
            return frame;
        }
    }
}
