Shader "AfterYou/CloneGhost"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
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
            // 이 include 없이는 CloneAlpha(0.5) 규약이 셰이더에 도달하지 않는다.
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

            // SRP Batcher 호환: 머티리얼 프로퍼티는 전부 UnityPerMaterial에 넣는다(레이아웃 분기 금지).
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half  _ScanlineStrength;
                half  _ScanlinePeriod;
                half  _JitterStrength;
            CBUFFER_END

            Varyings CloneGhostVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                SetUpSpriteInstanceProperties();

                // flipX/flipY 반영만 한다. 고스트 연출용 위치 변형은 금지 —
                // 클론의 궤적 위치가 곧 퍼즐의 판정 근거라 렌더 위치가 어긋나면 안 된다.
                float3 positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                o.positionCS = TransformObjectToHClip(positionOS);
                o.uv = input.uv;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 CloneGhostFragment(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // RGB = 정체성 색(TintColor), A = CloneAlpha(0.5) x revealProgress. 둘 다 여기서 보존된다.
                half4 baseColor = tex * input.color;
                half alpha = baseColor.a;

                // 밝기 지터: _Time.y를 24Hz로 스텝화해 프레임레이트와 무관하게 같은 속도로 깜빡인다.
                // 연속 sin을 쓰면 고주사율에서 지터가 사라져 보이므로 반드시 스텝화한다.
                float tick = floor(_Time.y * 24.0);
                float noise = frac(sin(tick * 12.9898) * 43758.5453);
                half jitter = (half)(noise * 2.0 - 1.0) * _JitterStrength;

                // 스캔라인: 스크린 공간 y(픽셀 좌표) 기준. SV_POSITION은 프래그먼트에서 픽셀 좌표를 준다.
                // 오브젝트 공간이 아니라 스크린 공간이라 카메라가 움직여도 라인이 화면에 고정된다.
                float scan = saturate(sin(input.positionCS.y * (6.2831853 / max(_ScanlinePeriod, 2.0))) * 0.5 + 0.5);

                // 가산항에 alpha를 곱하는 것이 핵심이다. 안 곱하면 알파 0(스폰 은폐 상태) 클론 자리에
                // 스캔라인 사각형만 떠 보인다.
                half3 rgb = baseColor.rgb * (1.0h + jitter) + scan * _ScanlineStrength * alpha;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
