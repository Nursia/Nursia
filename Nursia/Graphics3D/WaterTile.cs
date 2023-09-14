﻿using System.ComponentModel;
using Microsoft.Xna.Framework;
using Nursia.Utilities;

namespace Nursia.Graphics3D
{
	public class WaterTile: ItemWithId
	{
		[Category("Position")]
		public float X { get; set; }

		[Category("Position")]
		public float Z { get; set; }

		[Category("Position")]
		public float Height { get; set; }

		[Category("Position")]
		public float SizeX { get; set; }

		[Category("Position")]
		public float SizeZ { get; set; }

		[Category("Behavior")]
		public bool Waves { get; set; } = true;

		[Category("Behavior")]
		public bool Specular { get; set; } = true;

		[Category("Behavior")]
		public bool Reflection { get; set; } = true;

		[Category("Behavior")]
		public bool Refraction { get; set; } = true;

		[Category("Behavior")]
		public bool Fresnel { get; set; } = true;

		[Category("Behavior")]
		public Color Color { get; set; } = new Vector4(0.5f, 0.79f, 0.75f, 1.0f).ToColor();

		[Category("Behavior")]
		public float WaveTextureScale { get; set; } = 2.5f;

		[Category("Behavior")]
		public float Shininess { get; set; } = 250.0f;

		[Category("Behavior")]
		public float Reflectivity { get; set; } = 1.5f;

		[Category("Behavior")]
		public Vector2 WaveVelocity0 { get; set; } = new Vector2(0.01f, 0.03f);

		[Category("Behavior")]
		public Vector2 WaveVelocity1 { get; set; } = new Vector2(-0.01f, 0.03f);

		internal Vector2 WaveMapOffset0;
		internal Vector2 WaveMapOffset1;


		public WaterTile(float x, float z, float height, float sizeX = 40.0f, float sizeZ = 40.0f)
		{
			X = x;
			Z = z;
			Height = height;
			SizeX = sizeX;
			SizeZ = sizeZ;
		}
	}
}
