#ifndef WATER_FOAM_INCLUDED
#define WATER_FOAM_INCLUDED

// Neighbor directions 0..5 — must match TileGrid.AxialDirections (world/object XZ).
static const float2 kWaterFoamNeighborDir0 = float2(1.0, 0.0);
static const float2 kWaterFoamNeighborDir1 = float2(0.5, -0.8660254);
static const float2 kWaterFoamNeighborDir2 = float2(0.0, -1.0);
static const float2 kWaterFoamNeighborDir3 = float2(-1.0, 0.0);
static const float2 kWaterFoamNeighborDir4 = float2(-0.5, 0.8660254);
static const float2 kWaterFoamNeighborDir5 = float2(0.0, 1.0);

float2 WaterFoamNeighborDir(int index)
{
    if (index == 0) return kWaterFoamNeighborDir0;
    if (index == 1) return kWaterFoamNeighborDir1;
    if (index == 2) return kWaterFoamNeighborDir2;
    if (index == 3) return kWaterFoamNeighborDir3;
    if (index == 4) return kWaterFoamNeighborDir4;
    return kWaterFoamNeighborDir5;
}

float WaterFoamFlatTopHexEdgeDistance(float2 positionXZ)
{
    float2 p = abs(positionXZ);
    return max(dot(p, float2(0.8660254, 0.5)), p.x);
}

float WaterFoamSampleEdgeMask(float4 edgeMask, float2 edgeMaskB, int edgeIndex)
{
    if (edgeIndex < 4)
        return edgeMask[edgeIndex];
    if (edgeIndex == 4)
        return edgeMaskB.x;
    return edgeMaskB.y;
}

void WaterTileFoamMask_float(
    float3 PositionOS,
    float3 NormalWS,
    float4 FoamEdgeMask,
    float2 FoamEdgeMaskB,
    float HexRadius,
    float FoamWidth,
    out float Mask)
{
    // Reject walls and bevels; only near-horizontal top faces pass.
    float topMask = smoothstep(0.82, 0.98, NormalWS.y);

    float2 p = PositionOS.xz;
    float edgeDistance = HexRadius - WaterFoamFlatTopHexEdgeDistance(p);
    float rimRaw = saturate(1.0 - edgeDistance / max(FoamWidth, 0.001));
    // Sharper rim reads as foam, not soft refraction fringe.
    float rim = pow(rimRaw, 1.4);

    float2 dir = p * rsqrt(max(dot(p, p), 1e-6));
    int edgeIndex = 0;
    float bestAlign = -1.0;

    [unroll]
    for (int i = 0; i < 6; i++)
    {
        float align = dot(dir, WaterFoamNeighborDir(i));
        if (align > bestAlign)
        {
            bestAlign = align;
            edgeIndex = i;
        }
    }

    float neighborMask = WaterFoamSampleEdgeMask(FoamEdgeMask, FoamEdgeMaskB, edgeIndex);
    const float kInteriorFoam = 0.07;
    float edgeFoam = rim * neighborMask;
    Mask = saturate(edgeFoam + kInteriorFoam) * topMask;
}

// Shader Graph compiles half-precision variants too — both are required.
void WaterTileFoamMask_half(
    half3 PositionOS,
    half3 NormalWS,
    half4 FoamEdgeMask,
    half2 FoamEdgeMaskB,
    half HexRadius,
    half FoamWidth,
    out half Mask)
{
    float mask;
    WaterTileFoamMask_float(
        (float3)PositionOS,
        (float3)NormalWS,
        (float4)FoamEdgeMask,
        (float2)FoamEdgeMaskB,
        (float)HexRadius,
        (float)FoamWidth,
        mask);
    Mask = (half)mask;
}

// Aliases — Name in Custom Function node must match prefix (without _float/_half).
void Water_foam_float(
    float3 PositionOS,
    float3 NormalWS,
    float4 FoamEdgeMask,
    float2 FoamEdgeMaskB,
    float HexRadius,
    float FoamWidth,
    out float Mask)
{
    WaterTileFoamMask_float(PositionOS, NormalWS, FoamEdgeMask, FoamEdgeMaskB, HexRadius, FoamWidth, Mask);
}

void Water_foam_half(
    half3 PositionOS,
    half3 NormalWS,
    half4 FoamEdgeMask,
    half2 FoamEdgeMaskB,
    half HexRadius,
    half FoamWidth,
    out half Mask)
{
    WaterTileFoamMask_half(PositionOS, NormalWS, FoamEdgeMask, FoamEdgeMaskB, HexRadius, FoamWidth, Mask);
}

// Your subgraph uses Name: Water_Foam (capital F).
void Water_Foam_float(
    float3 PositionOS,
    float3 NormalWS,
    float4 FoamEdgeMask,
    float2 FoamEdgeMaskB,
    float HexRadius,
    float FoamWidth,
    out float Mask)
{
    WaterTileFoamMask_float(PositionOS, NormalWS, FoamEdgeMask, FoamEdgeMaskB, HexRadius, FoamWidth, Mask);
}

void Water_Foam_half(
    half3 PositionOS,
    half3 NormalWS,
    half4 FoamEdgeMask,
    half2 FoamEdgeMaskB,
    half HexRadius,
    half FoamWidth,
    out half Mask)
{
    WaterTileFoamMask_half(PositionOS, NormalWS, FoamEdgeMask, FoamEdgeMaskB, HexRadius, FoamWidth, Mask);
}

#endif
