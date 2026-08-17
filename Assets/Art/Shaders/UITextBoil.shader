// UI 텍스트용 라인 보일 — UI/Default 기반. 스크린 공간 노이즈로 폰트 아틀라스 샘플 UV를
// 픽셀 단위로 비틀어 글자 "획의 윤곽"이 우글거리게 한다(정점 방식은 글자가 통째로 흔들리는
// 떨림이라 폐기 — 2026-08-17 사용자 피드백). 진폭은 아틀라스 인접 글리프 번짐을 피하기 위해
// 소폭(±1.2px)으로 제한한다. 보일 파라미터는 월드 셰이더 4종과 같은 의미·기본값.
Shader "AfterYou/UITextBoil"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _WobbleAmp ("Wobble Amplitude (px)", Range(0, 4)) = 1.2
        _WobbleWavelength ("Wobble Wavelength (screen px)", Range(2, 64)) = 12.0
        _WobbleSeed ("Wobble Seed", Float) = 0
        _BoilRate ("Line Boil Rate (Hz, 0 = static)", Range(0, 12)) = 7.0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _WobbleAmp;
            float _WobbleWavelength;
            float _WobbleSeed;
            float _BoilRate;

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

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 스크린 픽셀 좌표 기반 노이즈 — 획 위 픽셀마다 다른 방향으로 샘플이 비틀려
                // 윤곽이 우글거린다. 시드 스텝화(보일)는 월드 셰이더와 동일 기법.
                // 틱은 64로 순환 — seed 무한 증가 시 Hash21 frac 정밀도 붕괴로 수 분 뒤 보일 정지(빌드 실측).
                float tick = fmod(floor(_Time.y * _BoilRate), 64.0);
                float2 nc = IN.vertex.xy / max(_WobbleWavelength, 2.0);
                float seedT = _WobbleSeed + tick * 7.31;
                float2 n = float2(ValueNoise(nc + seedT), ValueNoise(nc + seedT + 37.7)) - 0.5;

                // 진폭(스크린 px)을 아틀라스 텍셀로 환산. 동적 폰트는 화면 크기 그대로 굽히므로
                // 1 텍셀 ≈ 1 화면 px — 진폭을 크게 올리면 인접 글리프가 번져 들어오니 주의.
                float2 uvOffset = n * 2.0 * _WobbleAmp * _MainTex_TexelSize.xy;

                half4 color = IN.color * (tex2D(_MainTex, IN.texcoord + uvOffset) + _TextureSampleAdd);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
