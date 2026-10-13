using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class RainController : MonoBehaviour
    {
        public ParticleSystem Rain;
        public AudioSource RainAudio;
        public Transform Follow;
        public float MaximumEmission = 950f;
        Material rainMaterial;
        AudioClip rainClip;

        public void Configure(Transform follow)
        {
            Follow = follow;
            if (Rain == null) Rain = GetComponent<ParticleSystem>();
            if (Rain == null) return;
            Rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = Rain.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 20f;
            main.startSize = 0.022f;
            main.startLifetime = 0.9f;
            main.maxParticles = 2200;
            main.startColor = new Color(0.68f, 0.78f, 0.9f, 0.42f);
            main.gravityModifier = 0.15f;
            var shape = Rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(24f, 24f, 1f);
            var renderer = Rain.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 5f;
            renderer.velocityScale = 0.035f;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader != null)
            {
                rainMaterial = new Material(shader);
                rainMaterial.SetFloat("_Surface", 1f);
                rainMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                rainMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                rainMaterial.SetInt("_ZWrite", 0);
                rainMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                rainMaterial.renderQueue = 3000;
                renderer.sharedMaterial = rainMaterial;
            }
            if (RainAudio == null) RainAudio = gameObject.AddComponent<AudioSource>();
            RainAudio.playOnAwake = false;
            RainAudio.loop = true;
            RainAudio.spatialBlend = 0f;
            rainClip = EnvironmentAudio.CreateRainClip();
            RainAudio.clip = rainClip;
            SetIntensity(0f);
        }

        void LateUpdate()
        {
            if (Follow == null) return;
            // Use world rotation even when the player is parented to a tilted vehicle.
            transform.SetPositionAndRotation(Follow.position + Vector3.up * 12f, Quaternion.Euler(90f, 0f, 0f));
        }

        public void SetRaining(bool raining) => SetIntensity(raining ? 0.55f : 0f);

        public void SetIntensity(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (Rain != null)
            {
                var emission = Rain.emission;
                emission.rateOverTime = MaximumEmission * amount;
                if (amount > 0.001f && !Rain.isPlaying) Rain.Play();
                if (amount <= 0.001f && Rain.isPlaying) Rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (RainAudio == null) return;
            RainAudio.mute = false;
            RainAudio.volume = amount * 0.32f;
            if (amount > 0.001f && !RainAudio.isPlaying) RainAudio.Play();
            if (amount <= 0.001f && RainAudio.isPlaying) RainAudio.Stop();
        }

        void OnDestroy()
        {
            if (rainMaterial != null) Destroy(rainMaterial);
            if (rainClip != null) Destroy(rainClip);
        }
    }
}
