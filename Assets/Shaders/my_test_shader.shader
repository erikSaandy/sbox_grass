MODES
{
    Default();
}

COMMON
{
	#include "common/shared.hlsl"
}

CS
{
    // Output texture
    RWTexture2D<float4> g_tOutput< Attribute( "OutputTexture" ); >;
    float4 color< Attribute("_Color"); >;

    [numthreads(8, 8, 1)] 
    void MainCs( uint uGroupIndex : SV_GroupIndex, uint3 vThreadId : SV_DispatchThreadID )
    {
		g_tOutput[vThreadId.xy] = color;
    }
}