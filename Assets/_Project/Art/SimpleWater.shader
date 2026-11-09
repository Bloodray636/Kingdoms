Shader "Custom/SimpleWater"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.2, 0.5, 0.8, 1)
        _WaveSpeed ("Wave Speed", Float) = 1.0
        _WaveFrequency ("Wave Frequency", Float) = 2.0
        _WaveAmplitude ("Wave Amplitude", Float) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "WaterUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _WaveSpeed;
                float _WaveFrequency;
                float _WaveAmplitude;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                // Переводим координаты в мировое пространство
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

                // Первая волна
                float waveA = sin((positionWS.x + positionWS.z * 0.35) * _WaveFrequency + _Time.y * _WaveSpeed);
                
                // Вторая волна под другим углом
                float waveB = sin((positionWS.z - positionWS.x * 0.2) * (_WaveFrequency * 0.7) - _Time.y * _WaveSpeed * 0.8);

                // Объединяем волны
                positionWS.y += (waveA * 0.6 + waveB * 0.4) * _WaveAmplitude;

                OUT.positionCS = TransformWorldToHClip(positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}