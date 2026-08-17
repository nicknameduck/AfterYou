// 라이브 마커 전용 역삼각형(▼) 실루엣 — 위 변 + 아래 꼭짓점, 쿼드를 꽉 채운다.
// EnvironmentBoil은 사각 경계에만 요철을 넣어 삼각형 빗변이 멈춰 보인다 → ExitArch처럼
// 형태를 SDF로 직접 그린다. 보일·테두리 파라미터는 공통 블록과 같은 의미(2026-08-17 통일 체계).
// _CornerRadius는 삼각형 실루엣에선 미사용(블록 순서 유지용).
Shader "AfterYou/LiveMarker"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width (screen px)", Range(0, 12)) = 5.0
        _OutlineDarken ("Outline Darken (x body color)", Range(0.2, 1)) = 0.55
        _CornerRadius ("Corner Radius (unused for marker)", Range(0, 16)) = 4.7
        _WobbleAmp ("Wobble Amplitude (px)", Range(0, 4)) = 1.2
        _WobbleFreq ("Wobble Frequency (per world unit)", Range(2, 40)) = 4.0
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
            #pragma vertex LiveMarkerVertex
            #pragma fragment LiveMarkerFragment
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
                float2 sizeWS     : TEXCOORD1;
                float2 expand     : TEXCOORD2;
                float  instSeed   : TEXCOORD3;
                half4  color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half  _OutlineWidth;
                half  _OutlineDarken;
                half  _CornerRadius;
                half  _WobbleAmp;
                half  _WobbleFreq;
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

            Varyings LiveMarkerVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                SetUpSpriteInstanceProperties();
                float3 positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                // 픽셀 기반 쿼드 확장 + 월드 크기 varying — 4종 공통 로직(EnvironmentBoil 참조).
                float4x4 m = GetObjectToWorldMatrix();
                float sx = length(float2(m._m00, m._m10));
                float sy = length(float2(m._m01, m._m11));
                float pxPerUnit = max(0.5 * _ScreenParams.y * abs(UNITY_MATRIX_P._m11), 1e-3);
                float marginWorld = (_WobbleAmp + 2.0) / pxPerUnit;
                float2 e = float2(marginWorld / max(sx, 1e-4), marginWorld / max(sy, 1e-4));
                positionOS.xy += sign(positionOS.xy) * e;

                o.positionCS = TransformObjectToHClip(positionOS);
                o.uv = input.uv;
                o.sizeWS = float2(sx, sy);
                o.expand = 1.0 + 2.0 * e;
                o.instSeed = Hash21(floor(float2(m._m03, m._m13) * 2.0)) * 97.0;

                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 LiveMarkerFragment(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 tint = input.color;
                tint.rgb *= tex.rgb;

                float2 uvC = (input.uv - 0.5) * input.expand + 0.5;
                float2 fw = max(fwidth(uvC), 1e-5);

                // 콜라이더 기준 픽셀 좌표계(원점 = 좌하단) — 비균등 스케일에서도 형태 유지.
                float2 pPx = uvC / fw;
                float2 sizePx = 1.0 / fw;

                // 역삼각형 SDF: 위 변(y = 높이) + 좌우 빗변(위 모서리 → 아래 중앙 꼭짓점).
                // 볼록 도형이라 세 반평면 거리의 min이 곧 내부 거리(양수 = 내부).
                // 좌우 대칭이므로 x를 중앙 기준으로 접어 빗변 하나만 계산한다.
                float halfW = sizePx.x * 0.5;
                float h = sizePx.y;
                float xF = halfW - abs(pPx.x - halfW); // 중앙 접기: 0 = 바깥쪽, halfW = 중앙
                float dTop = h - pPx.y;
                float dSlope = (halfW * (pPx.y - h) + h * xF) / max(length(float2(halfW, h)), 1e-4);
                float edgePx = min(dTop, dSlope);

                // 경계 보일(양방향) — 4종 공통 기법.
                // 틱은 64로 순환 — seed 무한 증가 시 Hash21 frac 정밀도 붕괴로 수 분 뒤 보일 정지(빌드 실측).
                float boilSeed = _WobbleSeed + input.instSeed + fmod(floor(_Time.y * _BoilRate), 64.0) * 7.31;
                float wobble = (ValueNoise(uvC * input.sizeWS * _WobbleFreq + boilSeed) - 0.5) * 2.0;
                float edgePxW = edgePx + wobble * _WobbleAmp;

                half alpha = tint.a * saturate(edgePxW + 0.5);
                half interior = saturate(edgePxW - _OutlineWidth + 0.5);
                half3 rgb = tint.rgb * lerp(_OutlineDarken, 1.0h, interior);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
