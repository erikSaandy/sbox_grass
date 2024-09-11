using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class SceneGrassComponent : Component
{
	GrassRenderDirectObject Renderer { get; set; }

	[Property] public TextureDisplay TextureDisplay { get; set; }

	protected override void OnAwake()
	{
		base.OnAwake();

		Renderer = new GrassRenderDirectObject( Scene.SceneWorld );

		Renderer.Wind = Texture.CreateRenderTarget()
		.WithSize( 1024, 1024 )
		.WithDepth( 0 )
		.WithFormat( ImageFormat.ARGB8888 )
		.WithDynamicUsage()
		.WithUAVBinding()
		.Create( "texture_grass_wind" );

	}

	protected override void OnStart()
	{
		base.OnStart();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		//TextureDisplay.Texture = Renderer.GenerateWindShader.Attributes.GetTexture( "_WindMap" );
		TextureDisplay.Texture = Renderer.Wind;
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		Renderer.Disable();
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		Gizmo.Draw.Color = Color.Yellow;
		if ( Renderer?.chunks != null )
		{
			for ( int i = 0; i < Renderer.numChunks * Renderer.numChunks; ++i )
			{
				Gizmo.Transform = Gizmo.Transform.WithRotation( Rotation.FromRoll( 90 ) );
				Gizmo.Draw.LineBBox( Renderer.chunks[i].bounds );
			}
		}

	}

}
