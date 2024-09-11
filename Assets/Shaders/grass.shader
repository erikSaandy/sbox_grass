
HEADER
{
	Description = "";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	VrForward();
	Depth(); 
	ToolsVis( S_MODE_TOOLS_VIS );
	ToolsWireframe( "vr_tools_wireframe.shader" );
	ToolsShadingComplexity( "tools_shading_complexity.shader" );
}

COMMON
{
	#ifndef S_ALPHA_TEST
	#define S_ALPHA_TEST 0
	#endif
	#ifndef S_TRANSLUCENT
	#define S_TRANSLUCENT 0
	#endif
	
	#include "common/shared.hlsl"
	#include "procedural.hlsl"

	#define S_UV2 1
	#define CUSTOM_MATERIAL_INPUTS
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
	float4 vColor : COLOR0 < Semantic( Color ); >;
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
	float3 vPositionOs : TEXCOORD14;
	float3 vNormalOs : TEXCOORD15;
	float4 vTangentUOs_flTangentVSign : TANGENT	< Semantic( TangentU_SignV ); >;
	float4 vColor : COLOR0;
	float4 vTintColor : COLOR1;
};

VS
{
	#include "common/vertex.hlsl"
	
	float g_fl_Frequency < Attribute( "_Frequency" ); Default1( 1 ); >;
	
	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );
		i.vPositionOs = v.vPositionOs.xyz;
		i.vColor = v.vColor;

		ExtraShaderData_t extraShaderData = GetExtraPerInstanceShaderData( v );
		i.vTintColor = extraShaderData.vTint;

		VS_DecodeObjectSpaceNormalAndTangent( v, i.vNormalOs, i.vTangentUOs_flTangentVSign );
		
		float l_0 = g_fl_Frequency;
		float l_1 = l_0 * g_flTime;
		float l_2 = cos( l_1 );
		float l_3 = l_2 * l_2;
		float l_4 = l_3 * 0.65;
		float3 l_5 = i.vPositionWs;
		float3 l_6 = l_5 * float3( 0.001, 0.001, 0.001 );
		float l_7 = Simplex2D( l_6.xy );
		float l_8 = l_7 * 0.5;
		float l_9 = l_4 - l_8;
		float l_10 = l_9 * l_7;
		float l_11 = l_10 * 15;
		float3 l_12 = float3( l_11, 0, l_11 );
		float2 l_13 = i.vTextureCoords.xy * float2( 1, 1 );
		float l_14 = l_13.y;
		float l_15 = 1 - l_14;
		float3 l_16 = l_12 * float3( l_15, l_15, l_15 );
		i.vPositionWs.xyz += l_16;
		i.vPositionPs.xyzw = Position3WsToPs( i.vPositionWs.xyz );
		
		return FinalizeVertex( i );
	}
}

PS
{
	#include "common/pixel.hlsl"
	
	float4 g_v_ColorTop < Attribute( "_ColorTop" ); Default4( 0.47, 1.00, 0.13, 1.00 ); >;
	float4 g_v_ColorBase < Attribute( "_ColorBase" ); Default4( 0.40, 0.47, 0.36, 1.00 ); >;
	
	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::Init();
		m.Albedo = float3( 1, 1, 1 );
		m.Normal = float3( 0, 0, 1 );
		m.Roughness = 1;
		m.Metalness = 0;
		m.AmbientOcclusion = 1;
		m.TintMask = 1;
		m.Opacity = 1;
		m.Emission = float3( 0, 0, 0 );
		m.Transmission = 0;
		
		float4 l_0 = g_v_ColorTop;
		float4 l_1 = g_v_ColorBase;
		float2 l_2 = i.vTextureCoords.xy * float2( 1, 1 );
		float l_3 = l_2.y;
		float4 l_4 = saturate( lerp( l_0, l_1, l_3 ) );
		
		m.Albedo = l_4.xyz;
		m.Opacity = 1;
		m.Roughness = 1;
		m.Metalness = 0;
		m.AmbientOcclusion = 1;
		
		m.AmbientOcclusion = saturate( m.AmbientOcclusion );
		m.Roughness = saturate( m.Roughness );
		m.Metalness = saturate( m.Metalness );
		m.Opacity = saturate( m.Opacity );

		// Result node takes normal as tangent space, convert it to world space now
		m.Normal = TransformNormal( m.Normal, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );

		// for some toolvis shit
		m.WorldTangentU = i.vTangentUWs;
		m.WorldTangentV = i.vTangentVWs;
        m.TextureCoords = i.vTextureCoords.xy;
		
		return ShadingModelStandard::Shade( i, m );
	}
}
