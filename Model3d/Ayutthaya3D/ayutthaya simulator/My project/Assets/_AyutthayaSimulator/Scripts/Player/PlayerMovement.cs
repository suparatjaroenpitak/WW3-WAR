using UnityEngine;

namespace AyutthayaSimulator
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        public float WalkSpeed = 4.2f;
        public float RunSpeed = 7.0f;
        public float CrouchSpeed = 2.0f;
        public float JumpHeight = 1.1f;
        public float Gravity = -18f;
        public float StandingHeight = 1.8f;
        public float CrouchingHeight = 1.15f;

        CharacterController controller;
        float verticalVelocity;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.height = StandingHeight;
            controller.center = new Vector3(0f, StandingHeight * 0.5f, 0f);
            controller.radius = 0.3f;
            controller.stepOffset = 0.3f;
        }

        public void Tick(PlayerInputFrame input, PlayerState state)
        {
            if (state.Mode != PlayerMode.OnFoot || state.Paused)
            {
                state.Velocity = Vector3.zero;
                return;
            }

            if (input.CrouchPressed) SetCrouch(state, !state.Crouching);
            var grounded = controller.isGrounded;
            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (grounded && input.Jump && !state.Crouching)
                verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
            verticalVelocity += Gravity * Time.deltaTime;

            state.Running = input.Run && !state.Crouching;
            var speed = state.Crouching ? CrouchSpeed : (state.Running ? RunSpeed : WalkSpeed);
            var planar = transform.right * input.Move.x + transform.forward * input.Move.y;
            if (planar.sqrMagnitude > 1f) planar.Normalize();
            controller.Move((planar * speed + Vector3.up * verticalVelocity) * Time.deltaTime);
            state.Grounded = controller.isGrounded;
            state.Velocity = controller.velocity;
        }

        public void SetCrouch(PlayerState state, bool crouch)
        {
            state.Crouching = crouch;
            var height = crouch ? CrouchingHeight : StandingHeight;
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
        }

        public void Teleport(Vector3 position, float yaw)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            controller.enabled = true;
            verticalVelocity = 0f;
        }
    }
}
