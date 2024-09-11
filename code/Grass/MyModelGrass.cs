using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

public class MyModelGrass : Component
{

	private struct GrassData
	{
		public Vector4 position;
		public Vector2 uv;
		public float displacement;
	}

	private struct GrassChunk
	{
		public int xOffset, yOffset;
		public List<Transform> transforms;
		public BBox bounds;
		public Material material;
	}

	GrassChunk[] chunks;
	[Property] public Material grassMaterial { get; set; } = Material.Load( "grass/materials/glass_blade01.vmat" );
	[Property] public ModelRenderer grassMesh { get; set; }
	[Property] public ModelRenderer grassLODMesh { get; set; }
	[Property] public Texture heightMap { get; set; } = Texture.Load( "grass/materials/heightmap_temp.vtex" );
	BBox fieldBounds;

	private ComputeShader initializeGrassShader;

	[Property] public int fieldSize { get; set; } = 10;
	[Property] public int chunkDensity { get; set; } = 1;
	[Property] public int ChunkNum { get; set; } = 1;
	[Property] public float displacementStrength { get; set; } = 200.0f;

	[Range( 0, 1000.0f )]
	[Property] public float lodCutoff { get; set; } = 1000.0f;

	[Range( 0, 1000.0f )]
	[Property] public float distanceCutoff { get; set; } = 1000.0f;

	private int numInstancesPerChunk;
	private int chunkDimension;

	class Test : SceneCustomObject
	{
		public Test( SceneWorld sceneWorld ) : base( sceneWorld )
		{
		}
		public override void RenderSceneObject()
		{
			RenderOverride?.Invoke( this );

		}

	}

	protected override void OnStart()
	{
		base.OnStart();

		numInstancesPerChunk = MathX.CeilToInt( fieldSize / ChunkNum ) * chunkDensity;
		chunkDimension = numInstancesPerChunk;
		numInstancesPerChunk *= numInstancesPerChunk;

		//args = new uint[5] { 0, 0, 0, 0, 0 };
		//args[0] = (uint)grassMesh.GetIndexCount( 0 );
		//args[1] = (uint)0;
		//args[2] = (uint)grassMesh.GetIndexStart( 0 );
		//args[3] = (uint)grassMesh.GetBaseVertex( 0 );

		//argsLOD = new uint[5] { 0, 0, 0, 0, 0 };
		//args[0] = (uint)grassLODMesh.GetIndexCount( 0 );
		//args[1] = (uint)0;
		//args[2] = (uint)grassLODMesh.GetIndexStart( 0 );
		//args[3] = (uint)grassLODMesh.GetBaseVertex( 0 );

		initializeGrassShader = new( "Grass/Shaders/GrassChunkPoint" );
		initializeGrassShader.Attributes.Set( "_Dimension", fieldSize );
		initializeGrassShader.Attributes.Set( "_ChunkDimension", chunkDimension );
		initializeGrassShader.Attributes.Set( "_Scale", chunkDensity );
		initializeGrassShader.Attributes.Set( "_NumChunks", ChunkNum );
		initializeGrassShader.Attributes.Set( "_HeightMap", heightMap );
		initializeGrassShader.Attributes.Set( "_DisplacementStrength", displacementStrength );

		InitializeChunks();

		fieldBounds = new BBox( Vector3.Zero, new Vector3( -fieldSize, fieldSize, displacementStrength * 2 ) );

	}

	void InitializeChunks()
	{
		chunks = new GrassChunk[ChunkNum * ChunkNum];

		for ( int x = 0; x < ChunkNum; ++x )
		{
			for ( int y = 0; y < ChunkNum; ++y )
			{
				chunks[x + y * ChunkNum] = InitializeGrassChunk( x, y );
			}
		}
	}

	GrassChunk InitializeGrassChunk( int xOffset, int yOffset )
	{
		GrassChunk chunk = new GrassChunk();

		chunk.xOffset = xOffset;
		chunk.yOffset = yOffset;

		int chunkDim = MathX.CeilToInt( fieldSize / ChunkNum );

		chunk.transforms = new();

		Vector3 c = new Vector3( 0.0f, 0.0f, 0.0f );

		c.y = 0.0f;
		c.x = -(chunkDim * 0.5f * ChunkNum) + chunkDim * xOffset;
		c.z = -(chunkDim * 0.5f * ChunkNum) + chunkDim * yOffset;
		c.x += chunkDim * 0.5f;
		c.z += chunkDim * 0.5f;

		chunk.bounds = new BBox( c, new Vector3( -chunkDim, 10.0f, chunkDim ) );

		chunk.material = grassMaterial.CreateCopy();
		//chunk.material.Attributes.Set( "positionBuffer", chunk.culledPositionsBuffer );
		//chunk.material.Attributes.Set( "_DisplacementStrength", displacementStrength );
		//chunk.material.Attributes.Set( "_WindTex", wind );
		//chunk.material.Attributes.Set( "_ChunkNum", xOffset + yOffset * ChunkNum );

		return chunk;
	}

	RenderAttributes attributes = new RenderAttributes();

	protected override void OnUpdate()
	{
		//Matrix4x4 P = Scene.Camera.ProjectionMatrix();
		//Matrix4x4 V = Scene.Camera.Transform.WorldToLocalMatrix();
		//Matrix4x4 VP = P * V;

		//GenerateWind();


		for ( int i = 0; i < ChunkNum * ChunkNum; ++i )
		{
			float dist = Vector3.DistanceBetween( Scene.Camera.Transform.Position, chunks[i].bounds.Center );

			bool noLOD = dist < lodCutoff;

			//CullGrass( chunks[i], VP, noLOD );

			if ( noLOD )
				Graphics.DrawModelInstanced( grassMesh.Model, GetChunkTransforms( chunks[i] ), attributes );
			else
				Graphics.DrawModelInstanced( grassLODMesh.Model, GetChunkTransforms( chunks[i] ), attributes );
		}
	}

	Span<Transform> GetChunkTransforms( GrassChunk chunk )
	{
		List<Transform> result = new();

		for(int i = 0; i < numInstancesPerChunk; i++ )
		{
			Vector3 pos = 0;
			float scale = chunkDensity;
			float dimension = fieldSize;
			float scaledDimension = dimension * scale;

			float idX = i % chunkDimension;
			float idY = (int)(i / chunkDimension);

			pos.x = ((idX - (chunkDimension * 0.5f * ChunkNum)) + chunkDimension * chunk.xOffset) * (1.0f / scale);
			pos.z = ((idY - (chunkDimension * 0.5f * ChunkNum)) + chunkDimension * chunk.yOffset) * (1.0f / scale);
			//pos.x += snoise( float3( pos.xz + _XOffset + _YOffset, 0.0f ) * 3.0f ) * 0.05f;
			//pos.z += snoise( float3( pos.xz + _XOffset + _YOffset, 0.0f ) * 4.0f ) * 0.05f;

			float uvX = pos.x + dimension * 0.5f * ChunkNum * (1.0f / ChunkNum);
			float uvY = pos.z + dimension * 0.5f * ChunkNum * (1.0f / ChunkNum);
			Vector2 uv = new Vector2( 1 - uvX, 1 - uvY ) / dimension;

			//float4 displacement = _HeightMap.SampleLevel( sampler_HeightMap, uv, 0 );
			//pos.y += displacement.r * _DisplacementStrength + 0.5f;

			//pos.w = lerp( 0.3f, 0.8f, noise );


			result.Add( new Transform()
			{
				Position = pos,
				Rotation = Quaternion.Identity,
				Scale = 1,
			} );

		}

		return new Span<Transform>( result.ToArray() );
	}

	void OnDisable()
	{
		//voteBuffer.Dispose();
		//scanBuffer.Dispose();
		//groupSumArrayBuffer.Dispose();
		//scannedGroupSumBuffer.Dispose();
		//wind.Release();
		//wind = null;
		//scannedGroupSumBuffer = null;
		//voteBuffer = null;
		//scanBuffer = null;
		//groupSumArrayBuffer = null;


		for ( int i = 0; i < ChunkNum * ChunkNum; ++i )
		{
			FreeChunk( chunks[i] );
		}

		chunks = null;
	}

	void FreeChunk( GrassChunk chunk )
	{
		chunk.transforms.Clear();
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = Color.Yellow;
		if ( chunks != null )
		{
			foreach ( GrassChunk chunk in chunks )
			{
				Gizmo.Transform = Gizmo.Transform.WithRotation( Rotation.FromRoll( -90 ) );
				Gizmo.Draw.LineBBox( chunk.bounds );
			}
		}
	}

}
