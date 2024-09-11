using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class GrassRenderObject : SceneCustomObject
{

	public struct GrassData
	{
		public Vector4 position;
		public Vector2 uv;
		public float displacement;
	}

	public struct GrassChunk
	{
		public ComputeBuffer<DrawIndirectArguments> argsBuffer;
		public ComputeBuffer<DrawIndirectArguments> argsBufferLOD;
		public ComputeBuffer<GrassData> positionsBuffer;
		public ComputeBuffer<GrassData> culledPositionsBuffer;
		public BBox bounds;
		public Material material;
	}

	public GrassChunk[] chunks;
	Material grassMaterial= Material.Load( "grass/materials/glass_blade01.vmat" );
	Model grassMesh = Model.Load( "grass/grass_strand01.vmdl" );
	Model grassLODMesh = Model.Load( "grass/grass_strand01.vmdl" );
	Texture heightMap = Texture.Load( "grass/materials/heightmap_temp.vtex" );
	BBox fieldBounds;

	List<DrawIndirectArguments> args;
	List<DrawIndirectArguments> argsLOD;

	private ComputeShader initializeGrassShader;

	[Property] public int fieldSize { get; set; } = 500;
	[Property] public int chunkDensity { get; set; } = 1;
	[Property] public int ChunkNum { get; set; } = 5;
	[Property] public float displacementStrength { get; set; } = 200.0f;

	[Range( 0, 1000.0f )]
	[Property] public float lodCutoff { get; set; } = 1000.0f;

	[Range( 0, 1000.0f )]
	[Property] public float distanceCutoff { get; set; } = 1000.0f;

	private int numInstancesPerChunk;
	private int chunkDimension;

	public GrassRenderObject( SceneWorld sceneWorld ) : base( sceneWorld )
	{

		numInstancesPerChunk = MathX.CeilToInt( fieldSize / ChunkNum ) * chunkDensity;
		chunkDimension = numInstancesPerChunk;

		args = new() {
			new DrawIndirectArguments() {
				BaseInstance = (uint)grassMesh.GetIndexCount( 0 ),
				BaseVertex = (uint)0,
				IndexCount = (uint)grassMesh.GetIndexStart( 0 ),
				InstanceCount = (uint)grassMesh.GetBaseVertex( 0 ),
				StartIndex = (uint)0
			}
		};

		argsLOD = new() {
			new DrawIndirectArguments(){
				BaseInstance = (uint)grassLODMesh.GetIndexCount( 0 ),
				BaseVertex = (uint)0,
				IndexCount = (uint)grassLODMesh.GetIndexStart( 0 ),
				InstanceCount = (uint)grassLODMesh.GetBaseVertex( 0 ),
				StartIndex = (uint)0
			}
		};


		initializeGrassShader = new( "Grass/Shaders/GrassChunkPoint.shader" );
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

		chunk.argsBuffer = new ComputeBuffer<DrawIndirectArguments>( 1, ComputeBufferType.IndirectDrawArguments );
		chunk.argsBufferLOD = new ComputeBuffer<DrawIndirectArguments>( 1, ComputeBufferType.IndirectDrawArguments );

		chunk.argsBuffer.SetData( args );
		chunk.argsBufferLOD.SetData( argsLOD );

		chunk.positionsBuffer = new ComputeBuffer<GrassData>( numInstancesPerChunk );
		chunk.culledPositionsBuffer = new ComputeBuffer<GrassData>( numInstancesPerChunk );
		int chunkDim = MathX.CeilToInt( fieldSize / ChunkNum );

		Vector3 c = new Vector3( 0.0f, 0.0f, 0.0f );

		c.y = 0.0f;
		c.x = -(chunkDim * 0.5f * ChunkNum) + chunkDim * xOffset;
		c.z = -(chunkDim * 0.5f * ChunkNum) + chunkDim * yOffset;
		c.x += chunkDim * 0.5f;
		c.z += chunkDim * 0.5f;

		chunk.bounds = new BBox( c, new Vector3( -chunkDim, 10.0f, chunkDim ) );

		initializeGrassShader.Attributes.Set( "_XOffset", xOffset );
		initializeGrassShader.Attributes.Set( "_YOffset", yOffset );
		initializeGrassShader.Attributes.Set( "_GrassDataBuffer", chunk.positionsBuffer );
		initializeGrassShader.Dispatch( MathX.CeilToInt( fieldSize / ChunkNum ) * chunkDensity, MathX.CeilToInt( fieldSize / ChunkNum ) * chunkDensity, 1 );

		chunk.material = grassMaterial.CreateCopy();
		//chunk.material.Attributes.Set( "positionBuffer", chunk.culledPositionsBuffer );
		//chunk.material.Attributes.Set( "_DisplacementStrength", displacementStrength );
		//chunk.material.Attributes.Set( "_WindTex", wind );
		//chunk.material.Attributes.Set( "_ChunkNum", xOffset + yOffset * ChunkNum );

		return chunk;
	}


	RenderAttributes attributes = new RenderAttributes();

	public override void RenderSceneObject()
	{
		base.RenderSceneObject();

		//Matrix4x4 P = Scene.Camera.ProjectionMatrix();
		//Matrix4x4 V = Scene.Camera.Transform.WorldToLocalMatrix();
		//Matrix4x4 VP = P * V;

		//GenerateWind();


		for ( int i = 0; i < ChunkNum * ChunkNum; ++i )
		{
			GrassChunk chunk = chunks[i];

			//initializeGrassShader.Attributes.Set( "_GrassDataBuffer", chunk.positionsBuffer );

			float dist = Vector3.DistanceBetween( Game.ActiveScene.Camera.Transform.Position, chunks[i].bounds.Center );
			bool noLOD = dist < lodCutoff;

			//CullGrass( chunk, VP, noLOD );

			attributes.Set( "_ArgsBuffer", noLOD ? chunk.argsBuffer : chunk.argsBufferLOD );


			if ( noLOD )
			{
				chunk.argsBuffer.SetData( args );
				Graphics.DrawModelInstancedIndirect( grassMesh, chunk.argsBuffer );
			}
			else
			{
				chunk.argsBufferLOD.SetData( argsLOD );
				Graphics.DrawModelInstancedIndirect( grassLODMesh, chunk.argsBufferLOD );
			}
		}

		//Graphics.DrawModelInstanced( grassMesh, new Span<Transform>(new List<Transform>() { new Transform(0, Rotation.Identity, 1), }.ToArray() ), attributes: attributes );

	}
	
	public void Disable()
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
		chunk.positionsBuffer.Dispose();
		chunk.positionsBuffer = null;
		chunk.culledPositionsBuffer.Dispose();
		chunk.culledPositionsBuffer = null;
		chunk.argsBuffer.Dispose();
		chunk.argsBuffer = null;
		chunk.argsBufferLOD.Dispose();
		chunk.argsBufferLOD = null;
	}

}
