HEADER
{
	Description = "Template Shader for S&box";
}

FEATURES
{
    #include "common/features.hlsl"
}

COMMON
{
	#include "common/shared.hlsl"

    #pragma vertex vert
	#pragma fragment frag

	#pragma target 4.5
	
	struct VertexData {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
    };

	struct v2f {
        float2 uv : TEXCOORD0;
        float4 vertex : SV_POSITION;
        float4 worldPos : TEXCOORD1;
        float noiseVal : TEXCOORD2;
        float3 chunkNum : TEXCOORD3;
    };

    struct GrassData {
        float4 position;
        float2 uv;
        float displacement;
    };

	Texture2D<float> _WindTex <Attribute("_WindTex"); >;
	float4 _Albedo1 <Attribute("_Albedo1"); >;
	float4 _Albedo2 <Attribute("_Albedo2"); >;
	float4 _AOColor <Attribute("_AOColor"); >;
	float4 _TipColor <Attribute("_TipColor"); >;
	float4 _FogColor <Attribute("_FogColor"); >;

    RWStructuredBuffer<GrassData> positionBuffer < Attribute("_PositionBuffer"); >;

	float _Scale <Attribute("_Scale"); >;
	float _Droop<Attribute("_Droop"); >;
	float _FogDensity<Attribute("_FogDensity"); >;
	float _FogOffset<Attribute("_FogOffset"); >;

	int _ChunkNum<Attribute("_ChunkNum"); >;

}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};



VS
{
	#include "common/vertex.hlsl"
    #pragma vertex vert;
    
	float4 RotateAroundYInDegrees (float4 vertex, float degrees) {
    float alpha = degrees * 3.14159265 / 180.0;
    float sina, cosa;
    sincos(alpha, sina, cosa);
    float2x2 m = float2x2(cosa, -sina, sina, cosa);
    return float4(mul(m, vertex.xz), vertex.yw).xzyw;
    }
            
    float4 RotateAroundXInDegrees (float4 vertex, float degrees) {
        float alpha = degrees * 3.14159265 / 180.0;
        float sina, cosa;
        sincos(alpha, sina, cosa);
        float2x2 m = float2x2(cosa, -sina, sina, cosa);
        return float4(mul(m, vertex.yz), vertex.xw).zxyw;
    }

	v2f vert ( VertexInput v, uint instanceID : SV_INSTANCEID )
	{
        v2f o;
        float4 grassPosition = positionBuffer[instanceID].position;

        float idHash = randValue(abs(grassPosition.x * 10000 + grassPosition.y * 100 + grassPosition.z * 0.05f + 2));
        idHash = randValue(idHash * 100000);

        float4 animationDirection = float4(0.0f, 0.0f, 1.0f, 0.0f);
        animationDirection = normalize(RotateAroundYInDegrees(animationDirection, idHash * 180.0f));

        float4 localPosition = RotateAroundXInDegrees(v.vertex, 90.0f);
        localPosition = RotateAroundYInDegrees(localPosition, idHash * 180.0f);
        localPosition.y += _Scale * v.uv.y * v.uv.y * v.uv.y;
        localPosition.xz += _Droop * lerp(0.5f, 1.0f, idHash) * (v.uv.y * v.uv.y * _Scale) * animationDirection;
                
        float4 worldUV = float4(positionBuffer[instanceID].uv, 0, 0);
                
        float swayVariance = lerp(0.8, 1.0, idHash);
        float movement = v.uv.y * v.uv.y * (tex2Dlod(_WindTex, worldUV).r);
        movement *= swayVariance;
                
        localPosition.xz += movement;
                
        float4 worldPosition = float4(grassPosition.xyz + localPosition, 1.0f);

        worldPosition.y -= positionBuffer[instanceID].displacement;
        worldPosition.y *= 1.0f + positionBuffer[instanceID].position.w * lerp(0.8f, 1.0f, idHash);
        worldPosition.y += positionBuffer[instanceID].displacement;
                
        o.vertex = 1;//UnityObjectToClipPos(worldPosition);
        o.uv = v.uv;
        o.noiseVal = tex2Dlod(_WindTex, worldUV).r;
        o.worldPos = worldPosition;
        o.chunkNum = float3(randValue(_ChunkNum * 20 + 1024), randValue(randValue(_ChunkNum) * 10 + 2048), randValue(_ChunkNum * 4 + 4096));

        return o;
	}
}

//=========================================================================================================================

PS
{
    float DotClamped (float3 a, float3 b)
    {
        return saturate(dot(a, b));
    }

    #pragma fragment frag;
    #include "common/pixel.hlsl"

	float4 frag( v2f i ) : SV_Target
	{
        
		float4 col = lerp(_Albedo1, _Albedo2, i.uv.y);
        float ndotl = DotClamped(normalize(float3(1, 1, -1)), normalize(float3(0, 1, 0)));

        float4 ao = lerp(_AOColor, 1.0f, i.uv.y);
        float4 tip = lerp(0.0f, _TipColor, i.uv.y * i.uv.y * (1.0f + _Scale));
        //return fixed4(i.chunkNum, 1.0f);
        //return i.noiseVal;

        float4 grassColor = (col + tip) * ndotl * ao;

        /* Fog */
        float viewDistance = length(g_vCameraPositionWs - i.worldPos);
        float fogFactor = (_FogDensity / sqrt(log(2))) * (max(0.0f, viewDistance - _FogOffset));
        fogFactor = exp2(-fogFactor * fogFactor);


        return lerp(_FogColor, grassColor, fogFactor);

	}
}
