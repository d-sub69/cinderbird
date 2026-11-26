// The glow of buried ember clusters, drawn over the ash as an additive overlay on the chunks that have a cluster near.
// Each chunk gets its nearest few clusters (center, patch radius, intensity, depth, color) through a property block; a
// point of the surface glows by how close it is to a cluster, measured past the ash that covers it, so a deep cluster
// shows as a small bright spot and one dug into as a wide glow. HDR, so bloom carries it.
Shader "Cinderbird/ClusterGlow"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ClusterGlow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_CLUSTERS 8

            // x, y, z: center (world); w: pulse phase.
            float4 _ClusterCenter[MAX_CLUSTERS];
            // x: patch radius; y: intensity; z: depth of ash over the center; w: pulse amount.
            float4 _ClusterGlow[MAX_CLUSTERS];
            float4 _ClusterColor[MAX_CLUSTERS];
            float _ClusterCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 glow = 0;
                int count = (int)_ClusterCount;
                for (int i = 0; i < MAX_CLUSTERS; i++)
                {
                    if (i >= count)
                    {
                        break;
                    }

                    float4 center = _ClusterCenter[i];
                    float4 data = _ClusterGlow[i];
                    // Distance past the covering ash: zero right above a deep cluster, so its spot is small but bright.
                    float past = max(0.0, distance(input.positionWS, center.xyz) - data.z);
                    float t = saturate(1.0 - past / max(data.x, 0.001));
                    float pulse = 1.0 - data.w + data.w * sin(_Time.y * 1.7 + center.w);
                    glow += _ClusterColor[i].rgb * (data.y * t * t * pulse);
                }

                // Additive: fog fades the glow toward nothing rather than toward the fog color.
                glow = MixFogColor(glow, half3(0, 0, 0), input.fogFactor);
                return half4(glow, 1);
            }
            ENDHLSL
        }
    }
}
