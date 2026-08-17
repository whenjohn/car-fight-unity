Shader "CarFight/GridGround"
{
    Properties
    {
        _GroundColor ("Ground Color", Color) = (0.180, 0.205, 0.210, 1)
        _FineColor ("Fine Color", Color) = (0.255, 0.285, 0.290, 1)
        _MajorColor ("Major Color", Color) = (0.330, 0.370, 0.380, 1)
        _AxisColor ("Axis Color", Color) = (0.390, 0.310, 0.190, 1)
        _FineSpacing ("Fine Spacing", Float) = 1
        _MajorSpacing ("Major Spacing", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GroundColor;
                half4 _FineColor;
                half4 _MajorColor;
                half4 _AxisColor;
                float _FineSpacing;
                float _MajorSpacing;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            float GridLine(float2 position, float spacing, float width)
            {
                float2 coordinate = position / spacing;
                float2 derivative = max(fwidth(coordinate), float2(0.00001, 0.00001));
                float2 distanceToLine = abs(frac(coordinate - 0.5) - 0.5) / derivative;
                float closest = min(distanceToLine.x, distanceToLine.y);
                return 1.0 - smoothstep(width, width + 1.0, closest);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float fine = GridLine(input.positionWS.xz, _FineSpacing, 0.55);
                float major = GridLine(input.positionWS.xz, _MajorSpacing, 0.9);
                float2 axisWidth = max(fwidth(input.positionWS.xz), float2(0.00001, 0.00001)) * 1.25;
                float axisX = 1.0 - smoothstep(axisWidth.y, axisWidth.y * 2.0, abs(input.positionWS.z));
                float axisZ = 1.0 - smoothstep(axisWidth.x, axisWidth.x * 2.0, abs(input.positionWS.x));
                float axis = max(axisX, axisZ);

                half3 color = lerp(_GroundColor.rgb, _FineColor.rgb, fine * 0.22);
                color = lerp(color, _MajorColor.rgb, major * 0.45);
                color = lerp(color, _AxisColor.rgb, axis * 0.40);

                half3 normal = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse = saturate(dot(normal, mainLight.direction));
                half3 lighting = 0.72h + mainLight.color * diffuse * mainLight.shadowAttenuation * 0.42h;
                color *= lighting;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
