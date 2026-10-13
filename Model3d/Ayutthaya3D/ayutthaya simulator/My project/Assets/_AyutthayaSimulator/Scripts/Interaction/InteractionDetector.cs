using UnityEngine;

namespace AyutthayaSimulator
{
    /// <summary>
    /// Scene component that mirrors the player's current interaction target for UI and tests.
    /// </summary>
    public sealed class InteractionDetector : MonoBehaviour
    {
        public PlayerInteraction Source;
        public string CurrentId { get; private set; }
        public string CurrentPrompt { get; private set; }

        void Update()
        {
            if (Source == null) Source = FindFirstObjectByType<PlayerInteraction>();
            var current = Source != null ? Source.Current : null;
            CurrentId = current != null ? current.InteractId : "";
            var language = "th";
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null && player.State != null) language = player.State.Language;
            CurrentPrompt = current != null ? current.GetPrompt(language) : "";
        }
    }
}
