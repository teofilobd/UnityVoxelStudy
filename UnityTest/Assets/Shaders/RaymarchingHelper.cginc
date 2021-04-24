#ifndef _RAYMARCHING_HELPER_
#define _RAYMARCHING_HELPER_

///
///  Some functionalities adapted from https://www.shadertoy.com/view/Xds3zN
///

struct TracingResult
{
    float distance;
    float m;
    float3 color;
};

float sdot(in float3 v1, in float3 v2) { return saturate(dot(v1, v2)); }

float sdBox(float3 p, float3 b)
{
    float3 d = abs(p) - b;
    return min(max(d.x, max(d.y, d.z)), 0.0) + length(max(d, 0.0));
}

float sdVoxel(float3 p, float size)
{
    return sdBox(p, float3(size, size, size));
}

//------------------------------------------------------------------

TracingResult opU(TracingResult result1, TracingResult result2)
{
    if (result1.distance < result2.distance)
    {
        return result1;
    }
    else
    {
        return result2;
    }
}
#endif