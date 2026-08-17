Shader "AfterYou/CloneGhost"
{
    // 공통 옵션 블록(상단 9개) — CharacterBody/CloneGhost/EnvironmentBoil/HatchedObstacle 4종이
    // 같은 이름·순서·단위·기본값을 공유한다(2026-08-17 통일). 이후는 클론 전용 연출 옵션.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width (screen px)", Range(0, 12)) = 5.0
        _OutlineDarken ("Outline Darken (x body color)", Range(0.2, 1)) = 0.55
        _CornerRadius ("Corner Radius (screen px)", Range(0, 16)) = 4.7
        _WobbleAmp ("Wobble Amplitude (px)", Range(0, 4)) = 1.2
        _WobbleFreq ("Wobble Frequency (per world unit)", Range(2, 40)) = 12.0
        _WobbleSeed ("Wobble Seed", Float) = 0
        _BoilRate ("Line Boil Rate (Hz, 0 = static)", Range(0, 12)) = 7.0
        _ScanlineStrength ("Scanline Strength", Range(0, 0.3)) = 0.10
        _ScanlinePeriod ("Scanline Period (screen px)", Range(2, 16)) = 4.0
        _JitterStrength ("Brightness Jitter", Range(0, 0.2)) = 0.05
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

        // LightMode 태그 없는 패스 = SRPDefaultUnlit. URP 2D Renderer가 그대로 렌더한다.
        Pass
        {
            HLSLPROGRAM
            #pragma vertex CloneGhostVertex
            #pragma fragment CloneGhostFragment
            #pragma multi_compile_instancing

            // unity_SpriteColor / unity_SpriteProps(flipX,flipY) / UnityFlipSprite 정의를 가져온다.
            // SpriteRenderer.color는 SRP Batcher 경로에서 버텍스 컬러가 아니라 unity_SpriteColor로 들어오므로
            // 이 include 없이는 CloneAlpha(0.85) 규약이 셰이더에 도달하지 않는다.
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
                float2 sizeWS     : TEXCOORD1;   // 오브젝트 월드 크기(노이즈 밀도 보정용)
                float2 expand     : TEXCOORD2;   // 쿼드 확장 배율(콜라이더 기준 UV 역매핑용)
                float  instSeed   : TEXCOORD3;   // 인스턴스별 시드(월드 위치 해시)
                half4  color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // SRP Batcher 호환: 머티리얼 프로퍼티는 전부 UnityPerMaterial에 넣는다(레이아웃 분기 금지).
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half  _OutlineWidth;
                half  _OutlineDarken;
                half  _CornerRadius;
                half  _WobbleAmp;
                half  _WobbleFreq;
                half  _WobbleSeed;
                half  _BoilRate;
                half  _ScanlineStrength;
                half  _ScanlinePeriod;
                half  _JitterStrength;
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

            Varyings CloneGhostVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                SetUpSpriteInstanceProperties();

                // flipX/flipY 반영만 한다. 고스트 연출용 위치 변형은 금지 —
                // 클론의 궤적 위치가 곧 퍼즐의 판정 근거라 렌더 위치가 어긋나면 안 된다.
                float3 positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                // 쿼드 픽셀 기반 확장 — 위치 이동이 아닌 대칭 마진이라 궤적 판정 규약과 무관(4종 공통).
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
                // 인스턴스별 시드: 월드 위치 0.5유닛 격자 해시 — 동종 오브젝트 패턴 분화(4종 공통).
                o.instSeed = Hash21(floor(float2(m._m03, m._m13) * 2.0)) * 97.0;

                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 CloneGhostFragment(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 tint = input.color;   // RGB = 정체성 색, A = CloneAlpha(0.85) x revealProgress
                tint.rgb *= tex.rgb;

                // ── 몸통 (CharacterBody와 동일 로직) ──
                float2 uvC = (input.uv - 0.5) * input.expand + 0.5;
                float2 uvDist = min(uvC, 1.0 - uvC);
                float2 pxDist = uvDist / max(fwidth(uvC), 1e-5);
                float edgePx = min(pxDist.x, pxDist.y);

                if (_CornerRadius > 0.01 && pxDist.x < _CornerRadius && pxDist.y < _CornerRadius)
                {
                    float2 q = _CornerRadius - pxDist;
                    edgePx = _CornerRadius - length(q);
                }

                // 틱은 64로 순환 — seed 무한 증가 시 Hash21 frac 정밀도 붕괴로 수 분 뒤 보일 정지(빌드 실측).
                float boilSeed = _WobbleSeed + input.instSeed + fmod(floor(_Time.y * _BoilRate), 64.0) * 7.31;
                float wobble = (ValueNoise(uvC * input.sizeWS * _WobbleFreq + boilSeed) - 0.5) * 2.0;

                // [캐릭터와 동일] 접지선 보호: 바닥 근처는 바깥(아래) 방향 요철만 허용.
                float bottomBlend = smoothstep(0.0, 0.18, uvC.y);
                wobble = lerp(max(wobble, 0.0), wobble, bottomBlend);

                float edgePxW = edgePx + wobble * _WobbleAmp;

                // 알파(리빌 은폐 포함)는 실루엣과 틴트 알파의 곱으로만 결정.
                half alpha = tint.a * saturate(edgePxW + 0.5);
                half interior = saturate(edgePxW - _OutlineWidth + 0.5);
                half3 rgb = tint.rgb * lerp(_OutlineDarken, 1.0h, interior);

                // ── 클론 고유 연출 ──
                // 밝기 지터: _Time.y를 24Hz로 스텝화해 프레임레이트와 무관하게 같은 속도로 깜빡인다.
                float tick = fmod(floor(_Time.y * 24.0), 64.0); // 순환 이유는 위 boilSeed 주석 참조
                float noise = frac(sin(tick * 12.9898) * 43758.5453);
                half jitter = (half)(noise * 2.0 - 1.0) * _JitterStrength;

                // 스캔라인: 스크린 공간 y(픽셀 좌표) 기준 — 카메라가 움직여도 라인이 화면에 고정된다.
                float scan = saturate(sin(input.positionCS.y * (6.2831853 / max(_ScanlinePeriod, 2.0))) * 0.5 + 0.5);

                // 가산항에 alpha를 곱하는 것이 핵심이다. 안 곱하면 알파 0(스폰 은폐 상태) 클론 자리에
                // 스캔라인 사각형만 떠 보인다.
                rgb = rgb * (1.0h + jitter) + scan * _ScanlineStrength * alpha;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
