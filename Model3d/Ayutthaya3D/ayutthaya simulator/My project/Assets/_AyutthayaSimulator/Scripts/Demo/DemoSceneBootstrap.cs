using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class DemoSceneBootstrap : MonoBehaviour
    {
        public bool BuildOnPlay = true;

        void Awake()
        {
            var world = GameObject.Find("WorldRoot");
            if (world == null && BuildOnPlay) world = DemoWorldBuilder.Build();
            EnvironmentSetup.Install(world);
        }
    }
}
