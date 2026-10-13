using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class NPCDialogue : InteractableBase
    {
        public string ThaiLine = "สวัสดี ลองเดินสำรวจถนนจากข้อมูล OpenStreetMap แล้วขึ้นรถทดสอบได้";
        public string EnglishLine = "Hello. Walk the OpenStreetMap roads, then try the test vehicle.";
        public string LastLine { get; private set; }
        public bool Talking { get; private set; }
        float until;

        void Awake() => PromptKey = "talk";

        public override void Interact(PlayerController player)
        {
            var language = player != null && player.State != null ? player.State.Language : "th";
            LastLine = language == "en" ? EnglishLine : ThaiLine;
            Talking = true;
            until = Time.time + 4f;
            if (player != null) player.NotifyInteracted(InteractId);
        }

        void Update()
        {
            if (Talking && Time.time > until) Talking = false;
        }
    }
}
