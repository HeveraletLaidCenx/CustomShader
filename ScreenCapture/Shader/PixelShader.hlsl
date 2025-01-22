Texture2D inputTexture : register(t0);
SamplerState samplerState : register(s0);

struct PS_INPUT
{
    float4 position : SV_POSITION;
    float2 texcoord : TEXCOORD;
};

float Luminance(float3 color)
{
    return 0.299 * color.r + 0.587 * color.g + 0.114 * color.b;
}

float4 PS(PS_INPUT input) : SV_Target
{
    // sample the texture
    float4 color = inputTexture.Sample(samplerState, input.texcoord);

    // since the topmost filter window is not perfect, I prefer to let the content rendered as semi-transparent, allow us to see the content covered by the filter window
    color.a = 0.5;

    // calculate luminance
    float luminance = Luminance(color.rgb);

    if (luminance >= 0.8)
    {
        if (luminance >=0.92)
        {
            // darken the bright part
            color.rgb *= 0.1;
        }
        else
        {
            color.rgb *= 0.4;
        }
    }
    else if (luminance <= 0.02)
    {
        // invert dark color close to black
        color.rgb = 0.8;
        color.a = 1.0;
    }
    else
    {
        // to fit the semi-transparent display, otherwise the original value will be doubled
        color.rgb *= 0.5;
    }

    return color;
}