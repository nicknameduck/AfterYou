Shader "AfterYou/HatchedObstacle"
{
    // 공통 옵션 블록(상단 9개) — CharacterBody/CloneGhost/EnvironmentBoil/HatchedObstacle 4종이
    // 같은 이름·순서·단위·기본값을 공유한다(2026-08-17 통일). 이후는 빗금 전용 옵션.
    // 구 _BorderWidth는 _OutlineWidth로 개명(머티리얼 값 이관 완료).
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
        _HatchStrength ("Hatch Strength", Range(0, 1)) = 0.3
        _HatchPeriod ("Hatch Period (screen px)", Range(2, 32)) = 8.0
        _HatchWidth ("Hatch Line Width (px)", Range(1, 16)) = 3.0
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
            #pragma vertex HatchedObstacleVertex
            #pragma fragment HatchedObstacleFragment
            #pragma multi_compile_instancing

            // unity_SpriteColor / unity_SpriteProps(flipX,flipY) / UnityFlipSprite 정의를 가져온다.
            // SpriteRenderer.color는 SRP Batcher 경로에서 버텍스 컬러가 아니라 unity_SpriteColor로 들어오므로
            // 이 include 없이는 프리팹의 장애물 색이 셰이더에 도달하지 않는다.
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
                half  _HatchStrength;
                half  _HatchPeriod;
                half  _HatchWidth;
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

            Varyings HatchedObstacleVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                SetUpSpriteInstanceProperties();

                float3 positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                // 쿼드 픽셀 기반 확장 + 월드 크기 보정 — EnvironmentBoil.shader와 동일 기법
                // (양방향 보일 마진 확보: 안쪽 전용은 잘린 느낌 — 사용자 피드백).
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

            half4 HatchedObstacleFragment(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 baseColor = tex * input.color;
                half alpha = baseColor.a;

                // 45° 사선 빗금: 스크린 공간(픽셀 좌표) 기준 — 카메라가 움직여도 빗금이 화면에 고정된다
                // (클론 스캔라인과 동일 규약). x+y에 0.7071(1/√2)을 곱해 좌표를 사선 수직 거리로 환산,
                // period로 접어 스트라이프 중심으로부터의 픽셀 거리 d를 얻는다.
                float coord = (input.positionCS.x + input.positionCS.y) * 0.7071068;
                float t = frac(coord / max(_HatchPeriod, 2.0));
                float d = abs(t - 0.5) * _HatchPeriod;

                // 중심 거리 d가 선 반폭 이내면 1, 경계에서 1px 소프트 에지(계단 현상 방지).
                half line01 = saturate(_HatchWidth * 0.5 - d + 0.5);

                // 확장 쿼드 UV → 콜라이더 기준 UV(uvC): 0..1이 콜라이더 경계.
                float2 uvC = (input.uv - 0.5) * input.expand + 0.5;

                // 테두리: 콜라이더 가장자리로부터의 거리를 fwidth로 화면 픽셀 단위 환산.
                // UV 기준 폭을 쓰면 비균등 스케일(문 0.4×6 등)에서 축마다 두께가 달라지므로
                // 반드시 픽셀 환산을 거친다. 테두리 영역은 빗금을 끄고 원래 색만 남긴다.
                float2 uvDist = min(uvC, 1.0 - uvC);
                float2 pxDist = uvDist / max(fwidth(uvC), 1e-5);
                float edgePx = min(pxDist.x, pxDist.y);

                // 코너 라운딩(화면 픽셀 단위) — EnvironmentBoil과 동일 기법. 모서리 영역(두 변
                // 모두 반지름 이내)에서만 호 거리로 대체 — 무조건 min이면 내부가 반지름으로 캡되어
                // 테두리 밴드 판정에 걸리고 빗금·내부에 노이즈가 얼룩진다(실측 버그).
                if (_CornerRadius > 0.01 && pxDist.x < _CornerRadius && pxDist.y < _CornerRadius)
                {
                    float2 q = _CornerRadius - pxDist;
                    edgePx = _CornerRadius - length(q);
                }

                // 경계 보일: 양방향(±amp) — 캐릭터·환경과 동일. 테두리 판정도 같은 흔들린
                // 거리(edgePxW)를 쓰므로 테두리 밴드가 경계를 따라 함께 출렁인다.
                // 틱은 64로 순환 — seed 무한 증가 시 Hash21 frac 정밀도 붕괴로 수 분 뒤 보일 정지(빌드 실측).
                float boilSeed = _WobbleSeed + input.instSeed + fmod(floor(_Time.y * _BoilRate), 64.0) * 7.31;
                float wobble = (ValueNoise(uvC * input.sizeWS * _WobbleFreq + boilSeed) - 0.5) * 2.0;
                float edgePxW = edgePx + wobble * _WobbleAmp;

                alpha *= saturate(edgePxW + 0.5);
                half interior = saturate(edgePxW - _OutlineWidth + 0.5);

                // 테두리 명도: 기본값은 머티리얼에서 1(원색 유지 — 빗금 장애물 고유 룩).
                // 슬라이더 로직 자체는 4종 공통(몸통 색 × 계수).
                // 가산항에 alpha를 곱해 투명 픽셀 자리에 빗금만 떠 보이는 것을 방지(클론 셰이더와 동일 이유).
                half3 rgb = baseColor.rgb * lerp(_OutlineDarken, 1.0h, interior)
                          + line01 * _HatchStrength * alpha * interior;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
