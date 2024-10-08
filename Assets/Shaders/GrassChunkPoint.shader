MODES
{
    Default();
}

COMMON
{
	#include "common/shared.hlsl"
    #include "grass/shaders/simplex.hlsl"
}

CS
{

    #pragma kernel InitializeGrassChunk

    struct GrassData {
        float4 position;
        float2 uv;
        float displacement;
    };

    RWStructuredBuffer<GrassData> _GrassDataBuffer< Attribute( "_GrassDataBuffer" ); >;
    Texture2D<float4> _HeightMap< Attribute( "_HeightMap" ); >;
    SamplerState sampler_HeightMap;

    int _Dimension< Attribute("_Dimension"); >;
    int _Scale< Attribute("_Scale"); >;
    int _XOffset< Attribute("_XOffset"); >;
    int _YOffset< Attribute("_YOffset"); >;
    int _NumChunks < Attribute("_NumChunks"); >;
    int _ChunkDimension< Attribute("_ChunkDimension"); >;
    float _DisplacementStrength< Attribute("_DisplacementStrngth"); >;

    [numthreads(8, 8, 1)] 
    void InitializeGrassChunk(uint3 id : SV_DispatchThreadID) 
    {
        if (id.x < uint(_ChunkDimension) && id.y < uint(_ChunkDimension)) {
            GrassData grass;
            float4 pos = 0.0f;

            float scale = float(_Scale);
            float dimension = float(_Dimension);
            float chunkDimension = float(_ChunkDimension);
            float scaledDimension = dimension * scale;

            pos.x = (id.x - (chunkDimension * 0.5f * _NumChunks)) + chunkDimension * _XOffset;
            pos.z = (id.y - (chunkDimension * 0.5f * _NumChunks)) + chunkDimension * _YOffset;
            pos.xz *= (1.0f / scale);

            pos.x += snoise(float3(pos.xz + _XOffset + _YOffset, 0.0f) * 3.0f) * 0.05f;
            pos.z += snoise(float3(pos.xz + _XOffset + _YOffset, 0.0f) * 4.0f) * 0.05f;

            float uvX = pos.x + dimension * 0.5f * _NumChunks * (1.0f / _NumChunks);
            float uvY = pos.z + dimension * 0.5f * _NumChunks * (1.0f / _NumChunks);

            float2 uv = float2(uvX, uvY) / dimension;
            uv.y = 1 - uv.y;
            uv.x = 1 - uv.x;

            float4 displacement = _HeightMap.SampleLevel(sampler_HeightMap, uv, 0);

            pos.y += displacement.r * _DisplacementStrength + 0.5f;

            float noise = abs(snoise(float3(pos.xz + _XOffset + _YOffset, 0.0f) * 2.2f));

            pos.w = lerp(0.3f, 0.8f, noise);

            grass.position = pos;
            grass.displacement = displacement.r * _DisplacementStrength;
            grass.uv = uv;

            _GrassDataBuffer[id.x + id.y * _ChunkDimension] = grass;
        }
    }
}