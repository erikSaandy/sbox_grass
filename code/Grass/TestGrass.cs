﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

public class TestGrass : Component
{

	private struct GrassData
	{
		public Vector4 position;
		public Vector2 uv;
		public float displacement;
	}

	private struct GrassChunk
	{
		public ComputeBuffer<DrawIndirectArguments> argsBuffer;
		public ComputeBuffer<DrawIndirectArguments> argsBufferLOD;
		public ComputeBuffer<GrassData> positionsBuffer;
		public ComputeBuffer<GrassData> culledPositionsBuffer;
		public BBox bounds;
		public Material material;
	}

	GrassChunk[] chunks;
	[Property] public Material grassMaterial { get; set; } = Material.Load( "grass/materials/glass_blade01.vmat" );
	[Property] public ModelRenderer grassMesh { get; set; }
	[Property] public ModelRenderer grassLODMesh { get; set; }
	[Property] public Texture heightMap { get; set; } = Texture.Load( "grass/materials/heightmap_temp.vtex" );
	BBox fieldBounds;

	List<DrawIndirectArguments> args;
	List<DrawIndirectArguments> argsLOD;

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

	protected override void OnStart()
	{
		base.OnStart();

		numInstancesPerChunk = MathX.CeilToInt( fieldSize / ChunkNum ) * chunkDensity;
		chunkDimension = numInstancesPerChunk;
		//numInstancesPerChunk *= numInstancesPerChunk;

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

		args = new() {
			new DrawIndirectArguments() {
				BaseInstance = (uint)grassMesh.SceneObject.Model.GetIndexCount( 0 ),
				BaseVertex = (uint)0,
				IndexCount = (uint)grassMesh.SceneObject.Model.GetIndexStart( 0 ),
				InstanceCount = (uint)grassMesh.SceneObject.Model.GetBaseVertex( 0 ),
				StartIndex = (uint)0
			}
		};

		argsLOD = new() {
			new DrawIndirectArguments(){
				BaseInstance = (uint)grassLODMesh.SceneObject.Model.GetIndexCount( 0 ),
				BaseVertex = (uint)0,
				IndexCount = (uint)grassLODMesh.SceneObject.Model.GetIndexStart( 0 ),
				InstanceCount = (uint)grassLODMesh.SceneObject.Model.GetBaseVertex( 0 ),
				StartIndex = (uint)0
			}
		};

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
				Graphics.DrawModelInstancedIndirect( grassMesh.Model, chunks[i].argsBuffer );
			else
				Graphics.DrawModelInstancedIndirect( grassLODMesh.Model, chunks[i].argsBufferLOD );
		}
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
		chunk.positionsBuffer.Dispose();
		chunk.positionsBuffer = null;
		chunk.culledPositionsBuffer.Dispose();
		chunk.culledPositionsBuffer = null;
		chunk.argsBuffer.Dispose();
		chunk.argsBuffer = null;
		chunk.argsBufferLOD.Dispose();
		chunk.argsBufferLOD = null;
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = Color.Yellow;
		if ( chunks != null )
		{
			for ( int i = 0; i < ChunkNum * ChunkNum; ++i )
			{
				Gizmo.Transform = Gizmo.Transform.WithRotation( Rotation.FromRoll( -90 ) );
				Gizmo.Draw.LineBBox( chunks[i].bounds );
			}
		}
	}

}
