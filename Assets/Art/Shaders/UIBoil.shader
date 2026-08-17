// UI 이미지(버튼 배경·밑줄·카드 패널 등)용 라인 보일 — UI/Default 기반.
// 버텍스에서 쿼드를 마진만큼 확장(UV 부호로 모서리 방향 판별 — 렉트 크기 정보 불요)하고,
// 프래그먼트에서 "원래 렉트 경계" 기준으로 월드 셰이더와 같은 양방향 보일 + 코너 라운딩 +
// 어두운 톤 테두리를 그린다. 노이즈는 스크린 공간(파장 px) — UI는 화면 고정이라 자연스럽고,
// 요소마다 화면 위치가 달라 패턴도 저절로 달라진다.
// ⚠ 마진은 캔버스 유닛으로 밀고 px로 빼는 근사 — CanvasScaler 1920x1080 기준(1유닛≈1px) 전제.
Shader "AfterYou/UIBoil"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _OutlineWidth ("Outline Width (screen px)", Range(0, 12)) = 5.0
        _OutlineDarken ("Outline Darken (x body color)", Range(0.2, 1)) = 0.55
        _CornerRadius ("Corner Radius (screen px)", Range(0, 16)) = 4.7
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
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _OutlineWidth;
            float _OutlineDarken;
            float _CornerRadius;
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

            float Margin()
            {
                return _WobbleAmp + 1.5; // 바깥 요철 + AA 여유
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // 쿼드 확장 — 단순 Image의 UV(모서리별 0/1)로 바깥 방향을 판별한다.
                // Sliced/Filled 타입은 UV 규약이 달라 비대응(현 씬은 전부 Simple).
                v.vertex.xy += sign(v.texcoord - 0.5) * Margin();

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = IN.color * (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd);

                // 확장 쿼드 경계 기준 px 거리 → 마진을 빼서 "원래 렉트" 기준 부호 거리로.
                float2 uvDist = min(IN.texcoord, 1.0 - IN.texcoord);
                float2 pxD = uvDist / max(fwidth(IN.texcoord), 1e-5) - Margin();
                float edgePx = min(pxD.x, pxD.y);

                // 코너 라운딩 — 모서리 영역 한정(월드 셰이더와 동일: 무조건 min이면 내부 캡 얼룩)
                if (_CornerRadius > 0.01 && pxD.x < _CornerRadius && pxD.y < _CornerRadius)
                {
                    float2 q = _CornerRadius - pxD;
                    edgePx = _CornerRadius - length(q);
                }

                // 경계 보일: 스크린 공간 노이즈, 시드 스텝화(월드와 동일 기법·양방향 ±amp)
                // 틱은 64로 순환 — seed 무한 증가 시 Hash21 frac 정밀도 붕괴로 수 분 뒤 보일 정지(빌드 실측).
                float tick = fmod(floor(_Time.y * _BoilRate), 64.0);
                float2 nc = IN.vertex.xy / max(_WobbleWavelength, 2.0);
                float wobble = (ValueNoise(nc + _WobbleSeed + tick * 7.31) - 0.5) * 2.0;
                float edgePxW = edgePx + wobble * _WobbleAmp;

                // 실루엣 알파 + 어두운 톤 테두리
                color.a *= saturate(edgePxW + 0.5);
                half interior = saturate(edgePxW - _OutlineWidth + 0.5);
                color.rgb *= lerp(_OutlineDarken, 1.0, interior);

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
