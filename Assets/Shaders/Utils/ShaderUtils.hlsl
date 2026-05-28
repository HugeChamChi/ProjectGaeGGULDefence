#ifndef SHADER_UTILS_INCLUDED
#define SHADER_UTILS_INCLUDED

// Rotate UV coordinates around a center point
float2 RotateUV(float2 uv, float rotationDeg, float2 center)
{
    float rad = radians(rotationDeg);
    float s = sin(rad);
    float c = cos(rad);
    float2x2 rotMat = float2x2(c, -s, s, c);
    return mul(rotMat, uv - center) + center;
}

// Optimized Step for antialiasing if needed (Smoothstep version)
float AaStep(float edge, float x)
{
    float df = fwidth(x);
    return smoothstep(edge - df, edge + df, x);
}

#endif // SHADER_UTILS_INCLUDED
