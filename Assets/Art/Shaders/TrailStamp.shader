Shader "AfterYou/TrailStamp"
{
    // 트레일 스탬프 전용: 겹치는 스탬프 중 "가장 먼저 그려진 것(= 최신)"만 픽셀을 차지한다.
    // 스텐실 선점으로 반투명 이중 누적(방향 전환·점프에서 주름처럼 보이는 진한 겹침)을 원천 차단한다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
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

        // 첫 스탬프(정렬상 먼저 = 최신)가 픽셀을 차지하면 이후 스탬프는 그리지 않는다.
        // 스텐실은 카메라가 매 프레임 클리어하므로 프레임 간 잔류 없음.
        Stencil
        {
            Ref 1
            Comp NotEqual
            Pass Replace
        }

        // LightMode 태그 없는 패스 = SRPDefaultUnlit. URP 2D Renderer가 그대로 렌더한다.
        Pass
        {
            HLSLPROGRAM
            #pragma vertex TrailStampVertex
            #pragma fragment TrailStampFragment
            #pragma multi_compile_instancing

            // unity_SpriteColor / unity_SpriteProps(flipX,flipY) / UnityFlipSprite 정의를 가져온다.
            // SpriteRenderer.color는 SRP Batcher 경로에서 버텍스 컬러가 아니라 unity_SpriteColor로 들어오므로
            // 이 include 없이는 스탬프별 페이드 알파가 셰이더에 도달하지 않는다(CloneGhost와 동일 함정).
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
            CBUFFER_END

            Varyings TrailStampVertex(Attributes input)
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

            half4 TrailStampFragment(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                return tex * i.color;
            }
            ENDHLSL
        }
    }
}
