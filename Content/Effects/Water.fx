#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_3
#endif

float Time;
float ScaleTimeFactor;

float4x4 WorldViewProjection;
float4x4 World;

float3 WaterColor;

float3 AmbientColor;
float3 DiffuseColor;
float3 SpecularColor;

float KAmbient;
float KDiffuse;
float KSpecular;
float Shininess;

float3 LightPosition;
float3 EyePosition;
float2 Tiling;

texture NormalTexture;
sampler2D normalSampler = sampler_state
{
    Texture = (NormalTexture);
    ADDRESSU = WRAP;
    ADDRESSV = WRAP;
    MINFILTER = LINEAR;
    MAGFILTER = LINEAR;
    MIPFILTER = LINEAR;
};

float3 getNormalFromMap(float2 textureCoordinates, float3 worldPosition, float3 worldNormal)
{
    float3 tangentNormal = tex2D(normalSampler, textureCoordinates).xyz * 2.0 - 1.0;

    float3 Q1 = ddx(worldPosition);
    float3 Q2 = ddy(worldPosition);
    float2 st1 = ddx(textureCoordinates);
    float2 st2 = ddy(textureCoordinates);

    worldNormal = normalize(worldNormal.xyz);
    float3 T = normalize(Q1 * st2.y - Q2 * st1.y);
    float3 B = -normalize(cross(worldNormal, T));
    float3x3 TBN = float3x3(T, B, worldNormal);

    return normalize(mul(tangentNormal, TBN));
}

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
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput) 0;

    output.Position = mul(input.Position, WorldViewProjection);
    output.WorldPosition = mul(input.Position, World);
    output.Normal = input.Normal;
    output.TextureCoordinates = input.TextureCoordinates * Tiling;
	
    return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    // Base vectors
    float3 lightDirection = normalize(LightPosition - input.WorldPosition.xyz);
    float3 viewDirection = normalize(EyePosition - input.WorldPosition.xyz);
    float3 halfVector = normalize(lightDirection + viewDirection);
    
    float2 displacedTextureCoordinates1 = input.TextureCoordinates + float2(Time / ScaleTimeFactor, 0.0);
    displacedTextureCoordinates1 = frac(displacedTextureCoordinates1);
    float3 normal1 = getNormalFromMap(displacedTextureCoordinates1, input.WorldPosition.xyz, normalize(input.Normal.xyz));

    float2 displacedTextureCoordinates2 = input.TextureCoordinates + float2(0.0, Time / ScaleTimeFactor);
    displacedTextureCoordinates2 = frac(displacedTextureCoordinates2);
    float3 normal2 = getNormalFromMap(displacedTextureCoordinates2, input.WorldPosition.xyz, normalize(input.Normal.xyz));

    float3 normal = normalize(normal1 + normal2);
    
     // Ambient
    float3 ambientLight = KAmbient * AmbientColor;
    
    // Diffuse
    float NdotL = saturate(dot(normal, lightDirection));
    float3 diffuseLight = KDiffuse * DiffuseColor * NdotL;
    
    // Specular
    float NdotH = saturate(dot(normal, halfVector));
    float3 specularLight = NdotL * KSpecular * SpecularColor * pow(NdotH, Shininess);
    
    // Final calculation
    float4 finalColor = float4(saturate(ambientLight + diffuseLight) * WaterColor + specularLight, 1.0);
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