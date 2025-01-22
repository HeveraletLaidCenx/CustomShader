struct VSInput
{
    float2 position : POSITION; // Vertex position in NDC
    float2 texcoord : TEXCOORD; // Texture coordinates
};

struct VSOutput
{
    float4 position : SV_POSITION; // Position in clip space
    float2 texcoord : TEXCOORD; // Pass-through texture coordinates
};

VSOutput VS(VSInput input)
{
    VSOutput output;
    output.position = float4(input.position, 0.0f, 1.0f); // Add Z and W components
    output.texcoord = input.texcoord; // Pass texture coordinates
    return output;
}
