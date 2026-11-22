Shader "SurfRyder/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.20, 0.78, 0.88, 1)
        _DeepColor ("Deep Color", Color) = (0.03, 0.20, 0.50, 1)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)

        _WaveAmp ("Wave Height", Float) = 0.6
        _WaveLength ("Wave Length", Float) = 22
        _WaveSpeed ("Wave Speed", Float) = 1.0

        _FoamScale ("Foam Scale", Float) = 0.35
        _FoamCutoff ("Foam Cutoff", Range(0, 1)) = 0.70
        _FoamSoftness ("Foam Softness", Range(0.001, 0.5)) = 0.05
        _FoamSpeed ("Foam Drift Speed", Float) = 0.25

        _GlintStrength ("Sun Glint", Float) = 1.0
        _DarkLevel ("Brightness With No Sun", Range(0, 1)) = 0.35

        _RippleRadius ("Skimmer Ripple Radius", Float) = 4
        _WakeWidth ("Wake Start Width", Float) = 1.2
        _WakeSpread ("Wake Spread", Float) = 0.25
        _WakeLength ("Wake Length", Float) = 14
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define WATER_TAU 6.2831853

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float _WaveAmp;
                float _WaveLength;
                float _WaveSpeed;
                float _FoamScale;
                float _FoamCutoff;
                float _FoamSoftness;
                float _FoamSpeed;
                float _GlintStrength;
                float _DarkLevel;
                float _RippleRadius;
                float _WakeWidth;
                float _WakeSpread;
                float _WakeLength;
            CBUFFER_END

            // Set every frame by WaterFloor.cs
            float4 _SkimmerPos;   // xyz = skimmer world position, w = speed ratio (1 = cruising)
            float4 _WaterScroll;  // xy = how far the world has slid past (x sideways, y forward)
            float _WaterTime;     // shared wave clock so the C# physics samples the same waves

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  height     : TEXCOORD2;
            };

            void AddWave(inout float h, inout float2 grad, float2 p, float2 dir, float lenScale, float ampScale, float speedScale)
            {
                float k = WATER_TAU / (_WaveLength * lenScale);
                float a = _WaveAmp * ampScale;
                float phase = dot(normalize(dir), p) * k + _WaterTime * _WaveSpeed * speedScale;
                h += a * sin(phase);
                grad += a * k * cos(phase) * normalize(dir);
            }

            void Waves(float2 p, out float h, out float2 grad)
            {
                h = 0.0;
                grad = float2(0.0, 0.0);
                AddWave(h, grad, p, float2(0.80, 0.60), 1.00, 1.00, 1.0);
                AddWave(h, grad, p, float2(-0.40, 0.90), 0.60, 0.55, 1.3);
                AddWave(h, grad, p, float2(0.95, -0.30), 0.37, 0.30, 1.7);
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 ws = TransformObjectToWorld(IN.positionOS.xyz);

                float2 p = ws.xz + _WaterScroll.xy;
                float h;
                float2 grad;
                Waves(p, h, grad);

                // No calming near the skimmer: the physics floats on these exact waves.
                ws.y += h;

                OUT.positionWS = ws;
                OUT.normalWS = normalize(float3(-grad.x, 1.0, -grad.y));
                OUT.height = h;
                OUT.positionCS = TransformWorldToHClip(ws);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 ws = IN.positionWS;
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - ws);
                float t = _Time.y;
                float2 p = ws.xz + _WaterScroll.xy;

                // ---- Base color: deep to shallow with wave height, brighter at glancing angles
                float heightT = saturate(IN.height / max(_WaveAmp, 0.0001) * 0.35 + 0.5);
                float3 col = lerp(_DeepColor.rgb, _ShallowColor.rgb, heightT);
                float fresnel = pow(1.0 - saturate(dot(N, V)), 3.0);
                col = lerp(col, _ShallowColor.rgb * 1.15, fresnel * 0.5);

                // ---- Foam: two warped, drifting noise layers
                float2 warp = float2(ValueNoise(p * 0.15 + t * 0.10),
                                     ValueNoise(p * 0.15 + 7.3 - t * 0.10)) * 2.0 - 1.0;
                float layer1 = ValueNoise((p + warp * 3.0) * _FoamScale + float2(t * _FoamSpeed, 0.0));
                float layer2 = ValueNoise((p - warp * 3.0) * _FoamScale * 1.7 + float2(-t * _FoamSpeed * 0.6, t * _FoamSpeed * 0.3));
                float foamNoise = (layer1 + layer2) * 0.5;
                float foam = smoothstep(_FoamCutoff, _FoamCutoff + _FoamSoftness, foamNoise);

                // Whitecaps on the tallest wave crests
                float crest = smoothstep(0.55, 1.0, IN.height / max(_WaveAmp, 0.0001));
                foam = max(foam, crest * smoothstep(0.35, 0.7, foamNoise));

                // ---- Interactive part: wake behind the skimmer + ripple ring around it
                float2 s = _SkimmerPos.xz;
                float speedAmt = saturate(_SkimmerPos.w * 0.7);

                float dx = abs(ws.x - s.x);
                float dz = s.y - ws.z;                       // positive = behind the skimmer
                float wakeW = _WakeWidth + max(dz, 0.0) * _WakeSpread;
                float wake = saturate(1.0 - dx / max(wakeW, 0.01));
                wake *= step(-1.0, dz) * saturate(1.0 - dz / max(_WakeLength, 0.01));
                wake *= smoothstep(0.2, 0.8, layer1 + 0.35) * speedAmt;

                float r = distance(ws.xz, s);
                float ring = sin(r * 3.0 - t * 4.0) * 0.5 + 0.5;
                float ringMask = 1.0 - smoothstep(0.0, _RippleRadius, r);
                float ripple = smoothstep(0.82, 0.95, ring) * ringMask * 0.8;

                foam = saturate(max(foam * 0.9, max(wake, ripple)));
                col = lerp(col, _FoamColor.rgb, foam);

                // ---- Sun: glint on the waves, and darker water when the sun light is switched off
                Light mainLight = GetMainLight();
                float sun = saturate(dot(mainLight.color.rgb, float3(0.30, 0.59, 0.11)));
                float3 H = normalize(mainLight.direction + V);
                float spec = pow(saturate(dot(N, H)), 120.0) * _GlintStrength;
                col *= lerp(_DarkLevel, 1.0, sun);
                col += spec * mainLight.color.rgb;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
