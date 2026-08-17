// 배경 전용 빗금 — 흰 배경 위에 살짝 어두운 45° 사선을 긋는다(손그림 노트 질감).
// 장애물 빗금(HatchedObstacle)은 가산(밝게)이라 흰 배경에선 보이지 않으므로 감산 계열로 분리.
// 선은 저주파 노이즈로 구불거리고 시드 스텝화(보일)로 다시 그어진다 — 전체 보일 언어와 동일 리듬.
Shader "AfterYou/BackgroundHatch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        _HatchDarken ("Hatch Darken (x base color)", Range(0.8, 1)) = 0.94
        _HatchPeriod ("Hatch Period (screen px)", Range(4, 64)) = 16.0
        _HatchWidth ("Hatch Line Width (px)", Range(1, 16)) = 3.0
        _WaveAmp ("Line Wave Amplitude (px)", Range(0, 6)) = 1.5
        _WaveLength ("Line Wave Length (screen px)", Range(4, 128)) = 24.0
        _WobbleSeed ("Wobble Seed", Float) = 0
        _BoilRate ("Line Boil Rate (Hz, 0 = static)", Range(0, 12)) = 7.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex BackgroundHatchVertex
            #pragma fragment BackgroundHatchFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half  _HatchDarken;
                half  _HatchPeriod;
                half  _HatchWidth;
                half  _WaveAmp;
                half  _WaveLength;
                half  _WobbleSeed;
                half  _BoilRate;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings BackgroundHatchVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                SetUpSpriteInstanceProperties();
                float3 positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                o.positionCS = TransformObjectToHClip(positionOS);
                o.uv = input.uv;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 BackgroundHatchFragment(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 baseColor = tex * input.color;

                // 45° 사선 좌표(스크린 px) — HatchedObstacle과 동일 환산.
                float coord = (input.positionCS.x + input.positionCS.y) * 0.7071068;

                // 선을 저주파 노이즈로 구불거리게 + 시드 스텝화(보일)로 패턴 교체.
                // 노이즈는 사선의 "진행 방향" 좌표로 샘플해야 선을 따라 흐르는 웨이브가 된다.
                float along = (input.positionCS.x - input.positionCS.y) * 0.7071068;
                // 틱은 64로 순환 — seed 무한 증가 시 Hash21 frac 정밀도 붕괴로 수 분 뒤 보일 정지(빌드 실측).
                float boilSeed = _WobbleSeed + fmod(floor(_Time.y * _BoilRate), 64.0) * 7.31;
                float wave = (ValueNoise(float2(along / max(_WaveLength, 4.0), coord / max(_HatchPeriod, 4.0)) + boilSeed) - 0.5) * 2.0;
                coord += wave * _WaveAmp;

                float t = frac(coord / max(_HatchPeriod, 4.0));
                float d = abs(t - 0.5) * _HatchPeriod;
                half line01 = saturate(_HatchWidth * 0.5 - d + 0.5);

                // 감산 계열: 흰 배경 위 연회색 선(손그림 노트 질감).
                half3 rgb = baseColor.rgb * lerp(1.0h, _HatchDarken, line01);

                return half4(rgb, baseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
