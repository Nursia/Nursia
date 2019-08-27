﻿using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Nursia.ModelImporter.Content
{
	class ModelContent
	{
		private readonly List<MaterialContent> _materials = new List<MaterialContent>();
		private readonly List<MeshContent> _meshes = new List<MeshContent>();

		public List<MeshContent> Meshes
		{
			get
			{
				return _meshes;
			}
		}
		
		public List<MaterialContent> Materials
		{
			get
			{
				return _materials;
			}
		}

		public BoneNodeContent RootBoneNode { get; set; }

		public Matrix Transform { get; set; }
	}
}