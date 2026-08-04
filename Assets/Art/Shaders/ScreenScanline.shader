Shader "AfterYou/ScreenScanline"
{
    Properties
    {
        _ScanlineStrength ("Scanline Strength", Range(0, 0.2)) = 0.035
        _ScanlinePeriod ("Scanline Period (screen px)", Range(2, 16)) = 7.0
        _Curvature ("Barrel Curvature", Range(0, 0.3)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        Cull Off
        ZTest Always

        // FullScreenPassRendererFeature(Fetch Color Buffer ON)가 이 패스를 실행한다.
        // Blit.hlsl의 Vert가 풀스크린 삼각형을 그리고, 화면 컬러는 _BlitTexture로 들어온다.
        Pass
        {
            Name "ScreenScanline"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            CBUFFER_START(UnityPerMaterial)
                half _ScanlineStrength;
                half _ScanlinePeriod;
                half _Curvature;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 배럴 왜곡: 중심 기준 반경 제곱에 비례해 UV를 바깥으로 밀어
                // 브라운관 유리의 볼록 휨을 흉내낸다. 화면 밖을 참조하게 된 모서리는 검정(베젤).
                float2 uv = input.texcoord * 2.0 - 1.0;
                uv *= 1.0 + _Curvature * dot(uv, uv);
                uv = uv * 0.5 + 0.5;
                if (any(uv < 0.0) || any(uv > 1.0))
                    return half4(0, 0, 0, 1);

                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // 클론 스캔라인(가산·4px·강함)과 달리 감산·7px·약함 — 화면 전체는 은은한 CRT 질감,
                // 클론은 그 위에서 여전히 도드라져야 한다 (상태 가독성 유지).
                float scan = saturate(sin(input.positionCS.y * (6.2831853 / max(_ScanlinePeriod, 2.0))) * 0.5 + 0.5);
                color.rgb *= 1.0 - scan * _ScanlineStrength;

                return color;
            }
            ENDHLSL
        }
    }
}
