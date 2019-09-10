﻿using Microsoft.Xna.Framework.Graphics;

namespace Nursia.Graphics3D
{
	public class Mesh
	{
		public VertexBuffer VertexBuffer { get; set; }
		public IndexBuffer IndexBuffer { get; set; }

		public int PrimitiveCount
		{
			get
			{
				return IndexBuffer.IndexCount / 3;
			}
		}

		internal static Mesh Create<T>(T[] vertices, 
			short[] indices, 
			PrimitiveType primitiveType = PrimitiveType.TriangleList) where T : struct, IVertexType
		{
			var device = Nrs.GraphicsDevice;
			var vertexBuffer = new VertexBuffer(device, 
				new T().VertexDeclaration, 
				vertices.Length,
				BufferUsage.None);

			vertexBuffer.SetData(vertices);

			var indexBuffer = new IndexBuffer(device, 
				IndexElementSize.SixteenBits, 
				indices.Length, 
				BufferUsage.None);
			indexBuffer.SetData(indices);

			return new Mesh
			{
				VertexBuffer = vertexBuffer,
				IndexBuffer = indexBuffer
			};
		}
	}
}
