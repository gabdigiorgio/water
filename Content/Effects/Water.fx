#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_3
#endif

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
    
    float2 distortion1 = tex2D(distortionSampler, float2(input.TextureCoordinates.x + MoveFactor, input.TextureCoordinates.y)) * WaveStrength;
    float2 distortion2 = tex2D(distortionSampler, float2(- input.TextureCoordinates.x + MoveFactor, input.TextureCoordinates.y + MoveFactor)) * WaveStrength;
    float2 totalDistortion = distortion1 + distortion2;
    
    reflectionTex += totalDistortion;
    refractionTex += totalDistortion;
    
    float4 reflectionColor = tex2D(reflectionSampler, reflectionTex);
    float4 refractionColor = tex2D(refractionSampler, refractionTex);
    
    // Fresnel
    float3 viewVector = normalize(CameraPosition - input.WorldPosition.xyz);
    float refractiveFactor = dot(viewVector, normalize(input.Normal.xyz));
    
    // Final calculation
    float4 finalColor = lerp(reflectionColor, refractionColor, refractiveFactor);
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