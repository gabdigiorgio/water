#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_3
#endif

float KSpecular;
float Shininess;
float3 LightPosition;
float3 LightColor;
float3 CameraPosition;

float MoveFactor;
float2 Tiling;
float WaveStrength;

float4x4 ReflectionView;
float4x4 Projection;
float4x4 WorldViewProjection;
float4x4 World;

texture RefractionTexture;
sampler2D refractionSampler = sampler_state
{
    Texture = (RefractionTexture);
    ADDRESSU = Clamp;
    ADDRESSV = Clamp;
    MINFILTER = Linear;
    MAGFILTER = Linear;
    MIPFILTER = Linear;
};

texture ReflectionTexture;
sampler2D reflectionSampler = sampler_state
{
    Texture = (ReflectionTexture);
    ADDRESSU = Clamp;
    ADDRESSV = Clamp;
    MINFILTER = Linear;
    MAGFILTER = Linear;
    MIPFILTER = Linear;
};

texture DistortionMap;
sampler2D distortionSampler = sampler_state
{
    Texture = (DistortionMap);
    ADDRESSU = WRAP;
    ADDRESSV = WRAP;
    MINFILTER = LINEAR;
    MAGFILTER = LINEAR;
    MIPFILTER = LINEAR;
};

texture NormalMap;
sampler2D normalMapSampler = sampler_state
{
    Texture = (NormalMap);
    ADDRESSU = WRAP;
    ADDRESSV = WRAP;
    MINFILTER = LINEAR;
    MAGFILTER = LINEAR;
    MIPFILTER = LINEAR;
};

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Normal : NORMAL;
    float2 TextureCoordinates : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float2 TextureCoordinates : TEXCOORD0;
    float4 WorldPosition : TEXCOORD1;
    float4 Normal : TEXCOORD2;
    float4 ReflectionPosition : TEXCOORD3;
    float4 RefractionPosition : TEXCOORD4;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput) 0;

    output.Position = mul(input.Position, WorldViewProjection);
    output.WorldPosition = mul(input.Position, World);
    output.Normal = input.Normal;
    output.TextureCoordinates = input.TextureCoordinates * Tiling;
    
    // Reflection
    float4x4 reflectProjectWorld;
    
    reflectProjectWorld = mul(ReflectionView, Projection);
    reflectProjectWorld = mul(World, reflectProjectWorld);
    
    output.ReflectionPosition = mul(input.Position, reflectProjectWorld);
    
    // Refraction
    output.RefractionPosition = output.Position;
	
    return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{     
    // REFRACTION
    float4 refractionTexCoord;
    refractionTexCoord = input.RefractionPosition;
    
    // screen position
    refractionTexCoord.xyz /= refractionTexCoord.w;
    
    // adjust offset
    refractionTexCoord.x = 0.5f * refractionTexCoord.x + 0.5f;
    refractionTexCoord.y = -0.5f * refractionTexCoord.y + 0.5f;
    
	// refract more based on distance from the camera
    refractionTexCoord.z = 0.001f / refractionTexCoord.z;
    float2 refractionTex = refractionTexCoord.xy - refractionTexCoord.z;
    
    // REFLECTION
   
    float4 reflectionTexCoord;
    reflectionTexCoord = input.ReflectionPosition;
    
    // screen position
    reflectionTexCoord.xyz /= reflectionTexCoord.w;
    
    // adjust offset
    reflectionTexCoord.x = 0.5f * reflectionTexCoord.x + 0.5f;
    reflectionTexCoord.y = -0.5f * reflectionTexCoord.y + 0.5f;
    
	// reflect more based on distance from the camera
    reflectionTexCoord.z = 0.001f / reflectionTexCoord.z;
    float2 reflectionTex = reflectionTexCoord.xy + reflectionTexCoord.z;
    
    // REFLECTION AND REFRACTION COLORS
    
    float2 distortedTexCoords = tex2D(distortionSampler, float2(input.TextureCoordinates.x + MoveFactor, input.TextureCoordinates.y)) * 0.01;
    distortedTexCoords = input.TextureCoordinates + float2(distortedTexCoords.x, distortedTexCoords.y + MoveFactor);
    float2 totalDistortion = (tex2D(distortionSampler, distortedTexCoords).rg * 2.0 - 1.0) * WaveStrength;
    
    reflectionTex += totalDistortion;
    refractionTex += totalDistortion;
    
    float4 reflectionColor = tex2D(reflectionSampler, reflectionTex);
    float4 refractionColor = tex2D(refractionSampler, refractionTex);
    
    // Fresnel
    float3 viewDirection = normalize(CameraPosition - input.WorldPosition.xyz);
    float refractiveFactor = dot(viewDirection, normalize(input.Normal.xyz));
    
    // Light 
    float4 normalMapColor = tex2D(normalMapSampler, distortedTexCoords);
    float3 normal = float3(normalMapColor.r * 2.0 - 1.0, normalMapColor.b, normalMapColor.g * 2.0 - 1.0);
    normal = normalize(normal);
    
    float3 lightDirection = normalize(LightPosition - input.WorldPosition.xyz);
    float3 halfVector = normalize(lightDirection + viewDirection);
    
    float NdotL = saturate(dot(normal, lightDirection));
    float NdotH = saturate(dot(normal, halfVector));
    float3 specularLight = sign(NdotL) * KSpecular * LightColor * pow(NdotH, Shininess);
    
    // Final calculation
    float4 finalColor = lerp(reflectionColor, refractionColor, refractiveFactor) + float4(specularLight, 0.0);
    return finalColor;
}

technique Water
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};