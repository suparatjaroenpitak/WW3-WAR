using UnityEngine;
using UnityEngine.UI;

namespace AyutthayaSimulator
{
    public sealed class InteractionPrompt : MonoBehaviour
    {
        public Text Label;
        public InteractionDetector Detector;

        void Update()
        {
            if (Label == null) return;
            if (Detector == null) Detector = FindFirstObjectByType<InteractionDetector>();
            var prompt = Detector != null ? Detector.CurrentPrompt : "";
            Label.text = prompt ?? "";
            Label.enabled = !string.IsNullOrEmpty(prompt);
        }
    }
}
