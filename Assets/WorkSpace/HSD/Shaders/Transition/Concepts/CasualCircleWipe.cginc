// UV origin is bottom-left. The wave always travels top-left -> bottom-right.
float CasualCircleMask(float2 uv, float aspect, float progress)
{
    if (progress <= 0 || progress >= 1) return 0;
    if (abs(progress - 0.5) < 0.00001) return 1;
    float reveal = step(0.5, progress);
    float t = reveal > 0 ? (progress - 0.5) * 2 : progress * 2;
    float2 grid = uv * float2(aspect, 1) * 11;
    float2 cell = frac(grid) - 0.5;
    float2 center = (floor(grid) + 0.5) / (float2(aspect, 1) * 11);
    float delay = saturate((center.x + 1 - center.y) * 0.5);
    float radius = saturate(t * 2.15 - delay * 1.15) * 0.74;
    float aa = max(fwidth(length(cell)), 0.002);
    float filled = 1 - smoothstep(radius - aa, radius + aa, length(cell));
    return reveal > 0 ? 1 - filled : filled;
}
