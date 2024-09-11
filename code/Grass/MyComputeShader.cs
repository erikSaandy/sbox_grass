using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sandbox.Grass
{
	public class MyComputeShader : Component
	{

		[Property][RequireComponent] ModelRenderer Renderer { get; set; }

		ComputeShader computeShader = new ComputeShader( "grass/shaders/my_test_shader.shader" );

		[Property] Color Color { get; set; } = Color.White;

		protected override void OnStart()
		{
			base.OnStart();

			Renderer.SetMaterial( Material.Load( "models/goober/materials/goober.vmat" ).CreateCopy() );

		}

		protected override void OnUpdate()
		{

			// Create a texture for the compute shader to use
			var texture = Texture.Create( 512, 512 )
						  .WithUAVBinding() // Needs to have this if we're using it in a compute shader
						  .WithFormat( ImageFormat.RGBA16161616F ) // Use whatever you need
						  .Finish();

			// Attach texture to OutputTexture attribute in shader
			computeShader.Attributes.Set( "OutputTexture", texture );
			computeShader.Attributes.Set( "_Color", Color );

			// Dispatch 
			computeShader.Dispatch( texture.Width, texture.Height, 1 );

			Renderer.GetMaterial( 0 ).Set( "Color", texture );

		}

	}
}
