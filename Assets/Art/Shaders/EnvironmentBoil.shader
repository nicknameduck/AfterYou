Shader "AfterYou/EnvironmentBoil"
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
            #pragma vertex EnvironmentBoilVertex
            #pragma fragment EnvironmentBoilFragment
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
                float2 sizeWS     : TEXCOORD1;   // 오브젝트 월드 크기(노이즈 밀도 보정용)
                float2 expand     : TEXCOORD2;   // 쿼드 확장 배율(콜라이더 기준 UV 역매핑용)
                float  instSeed   : TEXCOORD3;   // 인스턴스별 시드(월드 위치 해시)
                half4  color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half  _WobbleAmp;
                half  _WobbleFreq;
                half  _WobbleSeed;
                half  _BoilRate;
                half  _OutlineWidth;
                half  _OutlineDarken;
                half  _CornerRadius;
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

            Varyings EnvironmentBoilVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                SetUpSpriteInstanceProperties();

                float3 positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                float4x4 m = GetObjectToWorldMatrix();
                float sx = length(float2(m._m00, m._m10));
                float sy = length(float2(m._m01, m._m11));

                // 쿼드 픽셀 기반 확장 — 요철이 콜라이더 "밖으로도" 부풀 수 있는 마진 확보.
                // 안쪽 전용 보일은 영역 밖으로 못 나가 잘린 느낌이 든다(사용자 피드백) — 캐릭터와
                // 동일한 양방향 보일로 통일한다. 캐릭터의 비율 마진(_EdgeMargin 8%)은 지형
                // (40유닛)에서 수 유닛이 되므로 부적합 — 직교 투영의 px/유닛(0.5×화면높이×P._m11)
                // 환산으로 픽셀 고정 마진을 쓴다.
                float pxPerUnit = max(0.5 * _ScreenParams.y * abs(UNITY_MATRIX_P._m11), 1e-3);
                float marginWorld = (_WobbleAmp + 2.0) / pxPerUnit;
                float2 e = float2(marginWorld / max(sx, 1e-4), marginWorld / max(sy, 1e-4));
                positionOS.xy += sign(positionOS.xy) * e;

                o.positionCS = TransformObjectToHClip(positionOS);
                o.uv = input.uv;
                // 노이즈 밀도는 월드 유닛당 고정(비균등 스케일 보정) — 프래그먼트에서 uvC×sizeWS로 환산.
                o.sizeWS = float2(sx, sy);
                o.expand = 1.0 + 2.0 * e;
                // 인스턴스별 시드: 월드 위치를 0.5유닛 격자로 양자화해 해시 — 같은 머티리얼을
                // 공유하는 동종 오브젝트도 서로 다른 요철 패턴을 갖는다(4종 공통). 이동체는
                // 격자를 넘을 때 패턴이 리롤되지만 보일(7Hz) 리듬에 섞여 체감되지 않는다.
                o.instSeed = Hash21(floor(float2(m._m03, m._m13) * 2.0)) * 97.0;

                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 EnvironmentBoilFragment(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 baseColor = tex * input.color;

                // 확장 쿼드 UV → 콜라이더 기준 UV(uvC): 0..1이 정확히 콜라이더 경계(캐릭터와 동일 기법).
                float2 uvC = (input.uv - 0.5) * input.expand + 0.5;

                // 콜라이더 가장자리로부터의 화면 픽셀 거리(부호 있음 — 밖이면 음수).
                float2 uvDist = min(uvC, 1.0 - uvC);
                float2 pxDist = uvDist / max(fwidth(uvC), 1e-5);
                float edgePx = min(pxDist.x, pxDist.y);

                // 코너 라운딩(화면 픽셀 단위 — 오브젝트 크기가 제각각이라 UV 비율은 부적합).
                // 두 변 "모두" 반지름 안쪽인 모서리 영역에서만 호(arc) 거리로 대체한다.
                // ⚠ 무조건 min으로 합치면 내부 거리가 반지름으로 캡되어, 반지름 < 외곽선 폭일 때
                // 내부 전체가 테두리 밴드 판정에 걸리고 요철 노이즈가 몸통 안까지 얼룩진다
                // (실측된 버그 — 플레이테스트 피드백). 모서리 영역 밖은 원래 거리를 유지한다.
                if (_CornerRadius > 0.01 && pxDist.x < _CornerRadius && pxDist.y < _CornerRadius)
                {
                    float2 q = _CornerRadius - pxDist;
                    edgePx = _CornerRadius - length(q);
                }

                // 경계 보일: 양방향(±amp) — 캐릭터와 동일. 바깥 요철은 쿼드 확장 마진에 그려진다.
                // 틱은 64로 순환 — seed가 무한 증가하면 Hash21의 frac 정밀도가 붕괴해 수 분 뒤
                // 보일이 정지한다(빌드 실측). 보일은 틱마다 무작위라 순환 지점은 보이지 않는다.
                float boilSeed = _WobbleSeed + input.instSeed + fmod(floor(_Time.y * _BoilRate), 64.0) * 7.31;
                float wobble = (ValueNoise(uvC * input.sizeWS * _WobbleFreq + boilSeed) - 0.5) * 2.0;

                // 1px 소프트 에지.
                float edgePxW = edgePx + wobble * _WobbleAmp;
                half alpha = baseColor.a * saturate(edgePxW + 0.5);

                // 외곽선 = 몸통 색의 어두운 톤(캐릭터와 동일 문법). 검은 지형은 "검정의 어두운 톤
                // = 검정"이라 외곽선이 자동으로 보이지 않는다 — 색 있는 오브젝트(발판·스위치·박스·
                // 출구 등)에만 테두리가 드러나므로, 지형/오브젝트를 에셋 분리 없이 한 머티리얼로
                // 처리한다. 테두리 판정은 흔들린 거리(edgePxW)를 공유해 경계 따라 출렁인다.
                half interior = saturate(edgePxW - _OutlineWidth + 0.5);
                half3 rgb = baseColor.rgb * lerp(_OutlineDarken, 1.0h, interior);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
