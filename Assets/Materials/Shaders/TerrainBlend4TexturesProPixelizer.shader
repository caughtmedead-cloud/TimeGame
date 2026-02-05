// ProPixelizer-integrated terrain shader with color grading support
Shader "Thelos/TerrainBlend4TexturesProPixelizer"
{
    Properties
    {
        [Header(Texture 1 Red Channel)]
        _MainTex ("Texture 1 (Red)", 2D) = "white" {}
        _MainTexNormal ("Normal Map 1", 2D) = "bump" {}
        _Smoothness1 ("Smoothness 1", Range(0,1)) = 0.5
        
        [Header(Texture 2 Green Channel)]
        _Texture2 ("Texture 2 (Green)", 2D) = "white" {}
        _Texture2Normal ("Normal Map 2", 2D) = "bump" {}
        _Smoothness2 ("Smoothness 2", Range(0,1)) = 0.5
        
        [Header(Texture 3 Blue Channel)]
        _Texture3 ("Texture 3 (Blue)", 2D) = "white" {}
        _Texture3Normal ("Normal Map 3", 2D) = "bump" {}
        _Smoothness3 ("Smoothness 3", Range(0,1)) = 0.5
        
        [Header(Texture 4 Alpha Channel)]
        _Texture4 ("Texture 4 (Alpha)", 2D) = "white" {}
        _Texture4Normal ("Normal Map 4", 2D) = "bump" {}
        _Smoothness4 ("Smoothness 4", Range(0,1)) = 0.5
        
        [Header(Tiling and Blending)]
        _Tiling ("Texture Tiling", Float) = 10.0
        _BlendSharpness ("Blend Sharpness", Range(0, 5)) = 1.0
        
        [Header(Lighting)]
        _Metallic ("Metallic", Range(0,1)) = 0.0
        
        [Header(ProPixelizer Settings)]
        _PixelSize ("Pixel Size", Float) = 4.0
        [Toggle(USE_OBJECT_POSITION_FOR_PIXEL_GRID)] _UseObjectPosition ("Use Object Position for Grid", Float) = 0
        _AlphaClipThreshold ("Alpha Clip Threshold", Range(0, 1)) = 0.5
        
        [Header(Color Grading and Dithering)]
        [Toggle(USE_COLOR_GRADING)] _UseColorGrading ("Use Color Grading LUT", Float) = 0
        _ColorGradingLUT ("Color Grading LUT", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        
        LOD 200
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ USE_OBJECT_POSITION_FOR_PIXEL_GRID_ON
            #pragma shader_feature USE_COLOR_GRADING_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            #include "../../ProPixelizer/SRP/ShaderLibrary/PixelUtils.hlsl"
            
            #define MAXCOLOR 16.0
            #define RES 16.0
            #define DITHER_SIZE 16.0
            
            inline float GetDitherPaletteOffset(float2 ditherUV) {
                return (((uint(frac(ditherUV.x / 4) * 4)) * 4 + uint(frac(ditherUV.y / 4) * 4)) / DITHER_SIZE);
            }
            
            inline float mapToCell(float input, float size) {
                return (clamp(input * RES, 0, RES-1)+0.5) / (RES*size);
            }
            
            inline void InternalColorGrade(TEXTURE2D(_palette), SAMPLER(sampler_palette), float4 orig, float2 ditherUV, out float4 graded)
            {
            #ifndef UNITY_COLORSPACE_GAMMA
                orig = pow(orig, 0.454545);
            #endif
                orig = clamp(orig, 0.0, 0.9999);
                
                float u = mapToCell(orig.r, RES);
                float v = mapToCell(orig.g, DITHER_SIZE);
                
                float cell = round(clamp(orig.b * MAXCOLOR, 0.0, RES - 1.0));
                u = cell / RES + u;
                
                v = v + GetDitherPaletteOffset(ditherUV);
                
                graded = SAMPLE_TEXTURE2D(_palette, sampler_palette, float2(u, v));
            }
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float4 vertexColor : COLOR;
                float3 viewDirWS : TEXCOORD4;
                float4 screenPosition : TEXCOORD5;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MainTexNormal);
            SAMPLER(sampler_MainTexNormal);
            
            TEXTURE2D(_Texture2);
            SAMPLER(sampler_Texture2);
            TEXTURE2D(_Texture2Normal);
            SAMPLER(sampler_Texture2Normal);
            
            TEXTURE2D(_Texture3);
            SAMPLER(sampler_Texture3);
            TEXTURE2D(_Texture3Normal);
            SAMPLER(sampler_Texture3Normal);
            
            TEXTURE2D(_Texture4);
            SAMPLER(sampler_Texture4);
            TEXTURE2D(_Texture4Normal);
            SAMPLER(sampler_Texture4Normal);
            
            TEXTURE2D(_ColorGradingLUT);
            SAMPLER(sampler_ColorGradingLUT);
            
            CBUFFER_START(UnityPerMaterial)
                float _Tiling;
                float _BlendSharpness;
                float _Smoothness1;
                float _Smoothness2;
                float _Smoothness3;
                float _Smoothness4;
                float _Metallic;
                float _PixelSize;
                float _AlphaClipThreshold;
            CBUFFER_END
            
            float3 _PixelGridOrigin;
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.uv = input.uv;
                output.vertexColor = input.color;
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.screenPosition = ComputeScreenPos(output.positionCS);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                float4 screenParams;
                GetScaledScreenParameters_float(screenParams);
                
                float2 posCS = float2(input.screenPosition.xy / input.screenPosition.w * screenParams.xy);
                
                #if defined(USE_OBJECT_POSITION_FOR_PIXEL_GRID_ON)
                    float3 objectCentreWS = UNITY_MATRIX_M._m03_m13_m23;
                #else
                    float3 objectCentreWS = _PixelGridOrigin.xyz;
                #endif
                
                float alpha_in = 1.0;
                float alpha_out;
                float2 ditherUV;
                
                PixelClipAlpha_float(
                    UNITY_MATRIX_VP,
                    objectCentreWS,
                    screenParams,
                    float4(posCS, 0, 0),
                    _PixelSize,
                    alpha_in,
                    _AlphaClipThreshold,
                    alpha_out,
                    ditherUV
                );
                
                if (alpha_out < 0.5)
                {
                    discard;
                }
                
                float2 tiledUV = input.uv * _Tiling;
                
                float4 weights = input.vertexColor;
                weights = pow(weights, _BlendSharpness);
                
                float totalWeight = weights.r + weights.g + weights.b + weights.a;
                if (totalWeight > 0.001)
                {
                    weights /= totalWeight;
                }
                else
                {
                    weights = float4(0.25, 0.25, 0.25, 0.25);
                }
                
                half4 tex1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, tiledUV);
                half4 tex2 = SAMPLE_TEXTURE2D(_Texture2, sampler_Texture2, tiledUV);
                half4 tex3 = SAMPLE_TEXTURE2D(_Texture3, sampler_Texture3, tiledUV);
                half4 tex4 = SAMPLE_TEXTURE2D(_Texture4, sampler_Texture4, tiledUV);
                
                half4 albedo = 
                    tex1 * weights.r +
                    tex2 * weights.g +
                    tex3 * weights.b +
                    tex4 * weights.a;
                
                half3 normal1 = UnpackNormal(SAMPLE_TEXTURE2D(_MainTexNormal, sampler_MainTexNormal, tiledUV));
                half3 normal2 = UnpackNormal(SAMPLE_TEXTURE2D(_Texture2Normal, sampler_Texture2Normal, tiledUV));
                half3 normal3 = UnpackNormal(SAMPLE_TEXTURE2D(_Texture3Normal, sampler_Texture3Normal, tiledUV));
                half3 normal4 = UnpackNormal(SAMPLE_TEXTURE2D(_Texture4Normal, sampler_Texture4Normal, tiledUV));
                
                half3 blendedNormal = 
                    normal1 * weights.r +
                    normal2 * weights.g +
                    normal3 * weights.b +
                    normal4 * weights.a;
                
                blendedNormal = normalize(blendedNormal);
                
                half smoothness = 
                    _Smoothness1 * weights.r +
                    _Smoothness2 * weights.g +
                    _Smoothness3 * weights.b +
                    _Smoothness4 * weights.a;
                
                float3 bitangentWS = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);
                float3 normalWS = TransformTangentToWorld(blendedNormal, tangentToWorld, true);
                normalWS = normalize(normalWS);
                
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = SafeNormalize(input.viewDirWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.bakedGI = SampleSH(normalWS);
                
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.alpha = 1.0;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = blendedNormal;
                surfaceData.occlusion = 1.0;
                surfaceData.emission = 0;
                
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                
                #if defined(USE_COLOR_GRADING_ON)
                    float4 gradedColor;
                    InternalColorGrade(_ColorGradingLUT, sampler_ColorGradingLUT, color, ditherUV, gradedColor);
                    color = gradedColor;
                #endif
                
                return color;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Lit"
}
