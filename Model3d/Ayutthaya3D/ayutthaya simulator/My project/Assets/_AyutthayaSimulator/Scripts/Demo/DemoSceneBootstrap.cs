using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class DemoSceneBootstrap : MonoBehaviour
    {
        public bool BuildOnPlay = true;

        void Awake()
        {
            if (!BuildOnPlay) return;
            if (GameObject.Find("WorldRoot") != null) return;
            DemoWorldBuilder.Build();
        }
    }
}
