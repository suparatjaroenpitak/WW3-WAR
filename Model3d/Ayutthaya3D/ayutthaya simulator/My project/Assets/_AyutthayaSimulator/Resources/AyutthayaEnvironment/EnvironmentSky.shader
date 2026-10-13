Shader "Ayutthaya/EnvironmentSky"
{
    Properties
    {
        _Daylight ("Daylight", Range(0,1)) = 1
        _Warmth ("Sunset", Range(0,1)) = 0
        _CloudCover ("Clouds", Range(0,1)) = 0.12
        _Night ("Night", Range(0,1)) = 0
        _Horizon ("Horizon", Color) = (0.64,0.73,0.8,1)
        _SunDirection ("Sun", Vector) = (0,1,0,0)
        _MoonDirection ("Moon", Vector) = (0,-1,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };
            float _Daylight, _Warmth, _CloudCover, _Night;
            float4 _Horizon, _SunDirection, _MoonDirection;
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.direction = v.vertex.xyz;
                return o;
            }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1,0)), f.x), lerp(hash(i + float2(0,1)), hash(i + 1), f.x), f.y);
            }
            float4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float height = saturate(d.y);
                float3 top = lerp(float3(0.006,0.012,0.035), float3(0.1,0.34,0.68), _Daylight);
                top *= 1 - _CloudCover * 0.55;
                float3 color = lerp(_Horizon.rgb, top, pow(height, 0.4));
                float sunDot = saturate(dot(d, normalize(_SunDirection.xyz)));
                color += float3(0.8,0.22,0.055) * pow(sunDot, 6) * _Warmth * (1 - height) * (1 - _CloudCover);
                float stars = step(0.998, hash(floor(d.xz / max(0.12, d.y) * 320))) * smoothstep(0,0.2,d.y);
                color += stars * _Night * (1 - _CloudCover) * 0.7;
                color += float3(1,0.82,0.48) * smoothstep(0.99965,0.99985,sunDot) * _Daylight * 3;
                float moon = smoothstep(0.9995,0.99975, dot(d, normalize(_MoonDirection.xyz)));
                color += float3(0.6,0.73,0.95) * moon * _Night * 1.5;
                float2 uv = d.xz / max(0.16, d.y) * 2.4 + _Time.y * float2(0.006,0.003);
                float cloudNoise = noise(uv) * 0.57 + noise(uv * 2.1) * 0.28 + noise(uv * 4.3) * 0.15;
                float cloud = smoothstep(0.72 - _CloudCover * 0.6, 0.88 - _CloudCover * 0.6, cloudNoise);
                cloud *= smoothstep(-0.02,0.15,d.y);
                float3 cloudColor = lerp(float3(0.035,0.045,0.065), lerp(float3(0.95,0.91,0.85), float3(0.28,0.32,0.38), _CloudCover), _Daylight);
                color = lerp(color, cloudColor, cloud * lerp(0.45,0.98,_CloudCover));
                return float4(color,1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
