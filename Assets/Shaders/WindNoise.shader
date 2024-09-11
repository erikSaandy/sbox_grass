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

    #pragma kernel WindNoise

    RWTexture2D<float4> _WindMap < Attribute("_WindMap"); >;

    float _Frequency<Attribute("_Frequency"); >;
    float _Amplitude<Attribute("_Amplitude"); >;
    float _Time<Attribute("_Time"); >;

    [numthreads(8, 8, 1)] 
    void WindNoise(uint3 id : SV_DispatchThreadID) 
    {    
        float xPeriod = 0.05f; // Repetition of lines in x direction
        float yPeriod = 0.1f; // Repitition of lines in y direction
        float turbPower = 2.3f;
        float turbSize = 2.0f;

        float xyValue = id.x * xPeriod + id.y * yPeriod + turbPower * snoise(id * turbSize);
        float sineValue = (sin((xyValue + _Time) * _Frequency) + 1.5f) * _Amplitude;

        _WindMap[id.xy] = sineValue;

    }
}