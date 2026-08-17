Shader "AfterYou/CharacterBody"
{
    // 공통 옵션 블록 — CharacterBody/CloneGhost/EnvironmentBoil/HatchedObstacle 4종이
    // 같은 이름·순서·단위·기본값을 공유한다(2026-08-17 통일). 수정 시 4종 동기 유지할 것.
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
            #pragma vertex CharacterBodyVertex
            #pragma fragment CharacterBodyFragment
            #pragma multi_compile_instancing

            // unity_SpriteColor / unity_SpriteProps(flipX,flipY) / UnityFlipSprite 정의를 가져온다.
            // SpriteRenderer.color는 SRP Batcher 경로에서 버텍스 컬러가 아니라 unity_SpriteColor로 들어오므로
            // 이 include 없이는 정체성 틴트가 셰이더에 도달하지 않는다.
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

            Varyings CharacterBodyVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                SetUpSpriteInstanceProperties();

                float3 positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                // 쿼드 픽셀 기반 확장 — 요철이 콜라이더 밖으로도 부풀 마진 확보(4종 공통 로직).
                // 직교 투영의 px/유닛 = 0.5×화면높이×P._m11 환산으로 크기 무관 균일 마진.
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

            half4 CharacterBodyFragment(Varyings input) : SV_Target
            {
                // 몸통 형태는 경계 거리 계산이 그린다 — 텍스처는 rgb만 곱해 호환 유지(흰 사각이면 no-op).
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 tint = input.color;
                tint.rgb *= tex.rgb;

                // 확장 쿼드 UV → 콜라이더 기준 UV(uvC): 0..1이 정확히 콜라이더 경계(§4 실루엣≈콜라이더).
                float2 uvC = (input.uv - 0.5) * input.expand + 0.5;

                // 콜라이더 가장자리로부터의 화면 픽셀 거리(부호 있음 — 밖이면 음수).
                float2 uvDist = min(uvC, 1.0 - uvC);
                float2 pxDist = uvDist / max(fwidth(uvC), 1e-5);
                float edgePx = min(pxDist.x, pxDist.y);

                // 코너 라운딩 — 모서리 영역(두 변 모두 반지름 이내)에서만 호 거리로 대체.
                // 무조건 min이면 내부 거리가 반지름으로 캡되어 테두리 밴드 판정에 걸린다(수정된 버그).
                if (_CornerRadius > 0.01 && pxDist.x < _CornerRadius && pxDist.y < _CornerRadius)
                {
                    float2 q = _CornerRadius - pxDist;
                    edgePx = _CornerRadius - length(q);
                }

                // 경계 보일: 양방향(±amp). 시드 스텝화로 패턴을 통째로 교체(연속 시간은 물결이 됨).
                // 틱은 64로 순환 — seed 무한 증가 시 Hash21 frac 정밀도 붕괴로 수 분 뒤 보일 정지(빌드 실측).
                float boilSeed = _WobbleSeed + input.instSeed + fmod(floor(_Time.y * _BoilRate), 64.0) * 7.31;
                float wobble = (ValueNoise(uvC * input.sizeWS * _WobbleFreq + boilSeed) - 0.5) * 2.0;

                // [캐릭터 전용] 접지선 보호: 바닥 근처(높이 하위 18%)는 요철을 바깥쪽(아래)으로만.
                // 안쪽 요철은 발밑 흰 틈 플리커(부양)로 읽힌다. 이 거리 규약에선 양수 = 바깥 팽창.
                float bottomBlend = smoothstep(0.0, 0.18, uvC.y);
                wobble = lerp(max(wobble, 0.0), wobble, bottomBlend);

                float edgePxW = edgePx + wobble * _WobbleAmp;

                // 실루엣 알파(1px 소프트 에지) / 외곽선 = 몸통 색의 어두운 톤.
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
