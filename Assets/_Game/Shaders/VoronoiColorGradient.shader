Shader "Ape/VoronoiColorGradient"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ColorA ("Color A", Color) = (0.10, 0.58, 1.00, 1.0)
        _ColorB ("Color B", Color) = (1.00, 0.26, 0.60, 1.0)
        _ColorC ("Color C", Color) = (1.00, 0.76, 0.18, 1.0)
        _ColorD ("Color D", Color) = (0.12, 0.95, 0.72, 1.0)
        _CellDensity ("Cell Density", Range(1, 14)) = 5
        _CellDensityOffset ("Cell Density Offset", Range(-13, 13)) = 0
        _FlowSpeed ("Flow Speed", Range(0, 3)) = 0.75
        _AnimationOffset ("Animation Offset", Float) = 0
        _WarpStrength ("Warp Strength", Range(0, 0.25)) = 0.08
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.12
        _EdgeGlow ("Edge Glow", Range(0, 2)) = 0.65
        _GradientDrift ("Gradient Drift", Range(0, 4)) = 1.1
        _Opacity ("Opacity", Range(0, 1)) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _TextureSampleAdd;
            fixed4 _Color;
            fixed4 _ColorA;
            fixed4 _ColorB;
            fixed4 _ColorC;
            fixed4 _ColorD;
            float _CellDensity;
            float _CellDensityOffset;
            float _FlowSpeed;
            float _AnimationOffset;
            float _WarpStrength;
            float _EdgeSoftness;
            float _EdgeGlow;
            float _GradientDrift;
            float _Opacity;
            float4 _ClipRect;

            float hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float2 hash22(float2 p)
            {
                float2 q = float2(
                    dot(p, float2(127.1, 311.7)),
                    dot(p, float2(269.5, 183.3))
                );

                return frac(sin(q) * 43758.5453123);
            }

            float3 SamplePalette(float seed, float drift)
            {
                float blendA = smoothstep(0.0, 1.0, frac(seed + drift));
                float blendB = smoothstep(0.0, 1.0, frac(seed * 0.73 + drift + 0.35));
                float blendC = 0.5 + 0.5 * sin((seed + drift * 0.8) * 6.2831853);

                float3 firstBand = lerp(_ColorA.rgb, _ColorB.rgb, blendA);
                float3 secondBand = lerp(_ColorC.rgb, _ColorD.rgb, blendB);

                return lerp(firstBand, secondBand, blendC);
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float cellDensity = clamp(_CellDensity + _CellDensityOffset, 1.0, 14.0);
                float time = _Time.y * _FlowSpeed + _AnimationOffset;

                float2 uv = i.uv;
                float2 warp = float2(
                    sin((uv.y + time * 0.27) * 6.2831853),
                    cos((uv.x - time * 0.21) * 6.2831853)
                );
                uv += warp * _WarpStrength;

                float2 scaledUv = uv * cellDensity;
                float2 baseCell = floor(scaledUv);
                float2 localUv = frac(scaledUv);

                float nearestDist = 10.0;
                float secondDist = 10.0;
                float nearestSeed = 0.0;
                float2 nearestDelta = float2(0.0, 0.0);

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 cellOffset = float2(x, y);
                        float2 cell = baseCell + cellOffset;
                        float2 randomPoint = hash22(cell);
                        float2 animatedPoint = cellOffset + 0.5 + 0.35 * sin(time + randomPoint * 6.2831853 + float2(0.0, 1.57));
                        float2 delta = animatedPoint - localUv;
                        float dist = dot(delta, delta);

                        if (dist < nearestDist)
                        {
                            secondDist = nearestDist;
                            nearestDist = dist;
                            nearestSeed = hash21(cell + 19.19);
                            nearestDelta = delta;
                        }
                        else if (dist < secondDist)
                        {
                            secondDist = dist;
                        }
                    }
                }

                float minDistance = sqrt(nearestDist);
                float edgeDistance = max(sqrt(secondDist) - minDistance, 0.0);
                float edgeMask = 1.0 - smoothstep(0.0, _EdgeSoftness, edgeDistance);
                float centerGlow = 1.0 - smoothstep(0.0, 0.9, minDistance * 1.8);

                float drift = dot(uv, float2(0.35, 0.7)) * _GradientDrift + time * 0.18;
                float3 cellColor = SamplePalette(nearestSeed, drift);
                float3 accentColor = SamplePalette(frac(nearestSeed + 0.37), drift + 0.27);

                float cellPulse = 0.85 + 0.15 * sin(time * 1.7 + nearestSeed * 11.0 + minDistance * 9.0);
                float interiorBlend = saturate(length(nearestDelta) * 1.5);
                float3 finalRgb = lerp(cellColor, accentColor, interiorBlend * 0.45);
                finalRgb *= cellPulse;
                finalRgb += accentColor * centerGlow * 0.12;
                finalRgb += lerp(accentColor, float3(1.0, 1.0, 1.0), 0.35) * edgeMask * _EdgeGlow;

                fixed4 spriteSample = tex2D(_MainTex, i.uv) + _TextureSampleAdd;
                fixed alpha = spriteSample.a * i.color.a * _Opacity;
                fixed3 rgb = saturate(finalRgb) * i.color.rgb;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                clip(alpha - 0.001);

                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
