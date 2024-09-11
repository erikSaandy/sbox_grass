using Saandy;
using Sandbox.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

public class GrassRenderDirectObject : SceneCustomObject
{

	public struct GrassData
	{
		public Vector4 position;
		public Vector2 uv;
		public float displacement;
	}

	public struct GrassChunk
	{
		public float xOffset, yOffset;
		public Transform[] positionBuffer;
		public BBox bounds;
		public Model grassMesh;
		public Model grassLODMesh;
	}

	public ComputeShader GenerateWindShader { get; private set; }
	public Texture Wind { get; set; }
	private int numWindThreadGroups;

	public GrassChunk[] chunks;
	Material grassMaterial = Material.Load( "grass/materials/glass_blade01.vmat" );
	Model grassMesh = Model.Load( "models/grass_strand01/grass_strand01.vmdl" );
	Model grassLODMesh = Model.Load( "models/grass_strand01/grass_strand01_lod1.vmdl" );
	Texture heightMap = Texture.Load( "grass/materials/heightmap_temp.vtex" );
	BBox fieldBounds;

	[Property] public int fieldSize { get; set; } = 2000;
	[Property] public float chunkDensity { get; set; } = 0.2f;
	[Property] public int numChunks { get; set; } = 5;
	[Property] public float displacementStrength { get; set; } = 200.0f;

	[Range( 0, 1000.0f )]
	[Property] public float lodCutoff { get; set; } = 1000.0f;

	[Range( 0, 1000.0f )]
	[Property] public float distanceCutoff { get; set; } = 1000.0f;

	public float windSpeed = 15.0f;
	public float windFrequency = 15.0f;
	public float windStrength = 15.0f;

	private int numInstancesPerChunk;
	private int chunkDimension;

	float heightVariation = 8f;

	public GrassRenderDirectObject( SceneWorld sceneWorld ) : base( sceneWorld )
	{
		GenerateWindShader = new ComputeShader( "grass/shaders/windnoise.shader" );

		numWindThreadGroups = MathX.CeilToInt( 1024f / 8.0f );

		chunkDimension = MathX.CeilToInt( ( fieldSize / numChunks ) * ( chunkDensity )  );
		numInstancesPerChunk = chunkDimension * chunkDimension;
		InitializeChunks();

		fieldBounds = new BBox( Vector3.Zero, new Vector3( -fieldSize, fieldSize, displacementStrength * 2 ) );

	}

	void InitializeChunks()
	{
		chunks = new GrassChunk[numChunks * numChunks];

		for ( int x = 0; x < numChunks; ++x )
		{
			for ( int y = 0; y < numChunks; ++y )
			{
				chunks[x + y * numChunks] = InitializeGrassChunk( x, y );
			}
		}
	}

	GrassChunk InitializeGrassChunk( int xOffset, int yOffset )
	{
		GrassChunk chunk = new GrassChunk();

		int chunkDim = MathX.CeilToInt( fieldSize / numChunks );

		Vector3 c = new Vector3( 0.0f, 0.0f, 0.0f );

		c.y = 0.0f;
		c.x = -(chunkDim * 0.5f * numChunks) + chunkDim * xOffset;
		c.z = -(chunkDim * 0.5f * numChunks) + chunkDim * yOffset;
		c.x += chunkDim * 0.5f;
		c.z += chunkDim * 0.5f;

		chunk.xOffset = xOffset;
		chunk.yOffset = yOffset;

		chunk.bounds = new BBox( c, new Vector3( -chunkDim, 10, chunkDim ) );

		chunk.positionBuffer = GetChunkPositionBuffer( chunk );

		chunk.grassMesh = Model.Load( grassMesh.ResourcePath );
		chunk.grassLODMesh = Model.Load( grassLODMesh.ResourcePath );

		//chunk.material.Attributes.Set( "positionBuffer", chunk.culledPositionsBuffer );
		Material mat = chunk.grassMesh.Materials.First();
		mat.Attributes.Set( "_DisplacementStrength", displacementStrength );
		mat.Attributes.Set( "_WindTex", Wind );
		mat.Attributes.Set( "_ChunkNum", xOffset + yOffset * numChunks );

		mat = chunk.grassLODMesh.Materials.First();
		mat.Attributes.Set( "_DisplacementStrength", displacementStrength );
		mat.Attributes.Set( "_WindTex", Wind );
		mat.Attributes.Set( "_ChunkNum", xOffset + yOffset * numChunks );


		return chunk;
	}

	Transform[] GetChunkPositionBuffer( GrassChunk chunk )
	{
		List<Transform> result = new();

		for ( int i = 0; i < numInstancesPerChunk; i++ )
		{
			float idX = i % chunkDimension;
			float idY = (int)(i / chunkDimension);

			if ( idY < chunkDimension )	
			{

				Vector3 pos = 0;

				float scaledDimension = chunkDimension * (1 / chunkDensity);
				pos.x = ((1 / chunkDensity) * idX - (scaledDimension * 0.5f * numChunks)) + (scaledDimension * chunk.xOffset );
				pos.y = ((1 / chunkDensity) * idY - (scaledDimension * 0.5f * numChunks)) + (scaledDimension * chunk.yOffset );

				Vector3 noisePos = new Vector2( pos.x + chunk.xOffset, pos.y + chunk.yOffset ) * 64;
				pos.x += SimplexNoise.Noise.CalcPixel3D( (int)noisePos.x, (int)noisePos.y, 0, .05f ) * 0.1f;
				pos.y += SimplexNoise.Noise.CalcPixel3D( (int)noisePos.x, (int)noisePos.y, 0, .05f ) * 0.1f;

				float uvX = pos.x + fieldSize * 0.5f * numChunks * (1.0f / numChunks);
				float uvY = pos.z + fieldSize * 0.5f * numChunks * (1.0f / numChunks);
				Vector2 uv = new Vector2( 1 - uvX, 1 - uvY ) / fieldSize;

				float displacement = 0; // _HeightMap.SampleLevel( sampler_HeightMap, uv, 0 );
				pos.z += displacement * displacementStrength + 0.5f;

				//pos.w = lerp( 0.3f, 0.8f, noise );

				result.Add( new Transform()
				{
					Position = pos,
					Rotation = Rotation.FromYaw(Game.Random.Int(0, 360)),
					Scale = Vector3.One.WithZ(1 + Noise.Perlin( pos.x * 0.05f, pos.y * 0.05f ) * heightVariation ),
				} );
			}

		}

		return result.ToArray();
	}

	void GenerateWind()
	{
		GenerateWindShader.Attributes.Set( "_WindMap", Wind );
		GenerateWindShader.Attributes.Set( "_Time", Time.Now * windSpeed );
		GenerateWindShader.Attributes.Set( "_Frequency", windFrequency );
		GenerateWindShader.Attributes.Set( "_Amplitude", windStrength );
		GenerateWindShader.Dispatch( numWindThreadGroups, numWindThreadGroups, 1 );
	}


	RenderAttributes attributes = new RenderAttributes();

	public override void RenderSceneObject()
	{
		base.RenderSceneObject();

		//Matrix4x4 P = Scene.Camera.ProjectionMatrix();
		//Matrix4x4 V = Scene.Camera.Transform.WorldToLocalMatrix();
		//Matrix4x4 VP = P * V;

		GenerateWind();

		for ( int i = 0; i < numChunks * numChunks; ++i )
		{
			GrassChunk chunk = chunks[i];

			//initializeGrassShader.Attributes.Set( "_GrassDataBuffer", chunk.positionsBuffer );

			float dist = Vector3.DistanceBetween( Game.ActiveScene.Camera.Transform.Position, chunks[i].bounds.Center );
			bool noLOD = dist < lodCutoff;

			//CullGrass( chunk, VP, noLOD );

			if ( noLOD )
			{
				Graphics.DrawModelInstanced( chunk.grassMesh, chunk.positionBuffer );
			}
			else
			{
				Graphics.DrawModelInstanced( chunk.grassLODMesh, chunk.positionBuffer );
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
		//scannedGroupSumBuffer = null;
		//voteBuffer = null;
		//scanBuffer = null;
		//groupSumArrayBuffer = null;

		chunks = null;
		Wind?.Dispose();
		Wind = null;

	}

}
