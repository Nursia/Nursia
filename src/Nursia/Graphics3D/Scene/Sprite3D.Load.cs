﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Nursia.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nursia.Graphics3D.Scene
{
	partial class Sprite3D
	{
		private class AttributeInfo
		{
			public int Size { get; private set; }
			public int ElementsCount { get; private set; }
			public VertexElementFormat Format { get; private set; }
			public VertexElementUsage Usage { get; private set; }

			public AttributeInfo(int size, int elementsCount, 
				VertexElementFormat format, VertexElementUsage usage)
			{
				Size = size;
				ElementsCount = elementsCount;
				Format = format;
				Usage = usage;
			}
		}

		private static readonly Dictionary<string, AttributeInfo> _attributes = new Dictionary<string, AttributeInfo>
		{
			["POSITION"] = new AttributeInfo(12, 3, VertexElementFormat.Vector3, VertexElementUsage.Position),
			["NORMAL"] = new AttributeInfo(12, 3, VertexElementFormat.Vector3, VertexElementUsage.Normal),
			["TEXCOORD"] = new AttributeInfo(8, 2, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate),
			["BLENDWEIGHT"] = new AttributeInfo(8, 2, VertexElementFormat.Vector2, VertexElementUsage.BlendWeight)
		};
		internal const string IdName = "name";

		private static Stream EnsureOpen(Func<string, Stream> streamOpener, string name)
		{
			var result = streamOpener(name);
			if (result == null)
			{
				throw new Exception(string.Format("stream is null for name '{0}'", name));
			}

			return result;
		}

		private static Matrix CreateTransform(Vector3 translation,
			Vector3 scale, Quaternion rotation)
		{
			return Matrix.CreateFromQuaternion(rotation) *
				Matrix.CreateScale(scale) *
				Matrix.CreateTranslation(translation);
		}

		private static Matrix LoadTransform(JObject data)
		{
			var scale = Vector3.One;
			JToken token;
			if (data.TryGetValue("scale", out token))
			{
				scale = token.ToVector3();
			}

			var translation = Vector3.Zero;
			if (data.TryGetValue("translation", out token))
			{
				translation = token.ToVector3();
			}

			var rotation = Vector4.Zero;
			if (data.TryGetValue("rotation", out token))
			{
				rotation = token.ToVector4();
			}

			var quaternion = new Quaternion(rotation.X,
				rotation.Y, rotation.Z, rotation.W);

			return CreateTransform(translation, scale, quaternion);
		}

		private static Bone LoadBone(JObject data)
		{
			if (data == null)
			{
				return null;
			}

			var result = new Bone
			{
				NodeId = data.EnsureString("node"),
				Transform = Matrix.Invert(LoadTransform(data))
			};

			return result;
		}

		private static Node LoadNode(JObject data)
		{
			if (data == null)
			{
				return null;
			}

			Node result;
			if (data.ContainsKey("parts"))
			{
				// Mesh
				var mesh = new MeshNode();

				var partsData = (JArray)data["parts"];
				foreach (JObject partData in partsData)
				{
					var newPart = new MeshPart
					{
						MeshPartId = partData["meshpartid"].ToString(),
						MaterialId = partData["materialid"].ToString()
					};

					if (partData.ContainsKey("bones"))
					{
						var bonesData = (JArray)partData["bones"];
						foreach (JObject boneData in bonesData)
						{
							var bone = LoadBone(boneData);
							newPart.Bones.Add(bone);
						}
					}

					mesh.Parts.Add(newPart);
				}

				result = mesh;
			}
			else
			{
				// Ordinary node
				result = new Node();
			}

			result.Id = data.GetId();
			result.Transform = LoadTransform(data);

			if (data.ContainsKey("children"))
			{
				var children = (JArray)data["children"];
				foreach (JObject child in children)
				{
					var childNode = LoadNode(child);
					childNode.Parent = result;
					result.Children.Add(childNode);
				}
			}

			return result;
		}

		private static VertexDeclaration LoadDeclaration(JArray data, out int elementsPerData)
		{
			elementsPerData = 0;
			var elements = new List<VertexElement>();
			var offset = 0;
			foreach(var elementData in data)
			{
				var name = elementData.ToString();
				var usage = 0;

				// Remove last digit
				var lastChar = name[name.Length - 1];
				if (char.IsDigit(lastChar))
				{
					name = name.Substring(0, name.Length - 1);
					usage = int.Parse(lastChar.ToString());
				}

				AttributeInfo attributeInfo;
				if (!_attributes.TryGetValue(name, out attributeInfo))
				{
					throw new Exception(string.Format("Unknown attribute '{0}'", name));
				}

				var element = new VertexElement(offset, 
					attributeInfo.Format, 
					attributeInfo.Usage, 
					usage);
				elements.Add(element);

				offset += attributeInfo.Size;
				elementsPerData += attributeInfo.ElementsCount;
			}

			return new VertexDeclaration(elements.ToArray());
		}

		private static void LoadFloat(byte[] dest, ref int destIdx, float data)
		{
			var byteData = BitConverter.GetBytes(data);

			var aaa = BitConverter.ToSingle(byteData, 0);
			Array.Copy(byteData, 0, dest, destIdx, byteData.Length);
			destIdx += byteData.Length;
		}

		private static void LoadByte(byte[] dest, ref int destIdx, int data)
		{
			if (data > byte.MaxValue)
			{
				throw new Exception(string.Format("Only byte bone indices suported. {0}", data));
			}

			dest[destIdx] = (byte)data;
			++destIdx;
		}

		private static VertexBuffer LoadVertexBuffer(
			ref VertexDeclaration declaration, 
			int elementsPerRow,
			JArray data)
		{
			var rowsCount = data.Count / elementsPerRow;
			var elements = declaration.GetVertexElements();

			var blendWeightOffset = 0;
			var blendWeightCount = (from e in elements
									where e.VertexElementUsage == VertexElementUsage.BlendWeight
									select e).Count();
			var hasBlendWeight = blendWeightCount > 0;
			if (blendWeightCount > 4)
			{
				throw new Exception("4 is maximum amount of weights per bone");
			}
			if (hasBlendWeight)
			{
				blendWeightOffset = (from e in elements
									 where e.VertexElementUsage == VertexElementUsage.BlendWeight
									 select e).First().Offset;

				var newElements = new List<VertexElement>();
				newElements.AddRange(from e in elements
									 where e.VertexElementUsage != VertexElementUsage.BlendWeight
									 select e);
				newElements.Add(new VertexElement(blendWeightOffset, VertexElementFormat.Byte4, VertexElementUsage.BlendIndices, 0));
				newElements.Add(new VertexElement(blendWeightOffset + 4, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 0));
				declaration = new VertexDeclaration(newElements.ToArray());
			}

			var byteData = new byte[rowsCount * declaration.VertexStride];

			for (var i = 0; i < rowsCount; ++i)
			{
				var destIdx = i * declaration.VertexStride;
				var srcIdx = i * elementsPerRow;
				var weightsCount = 0;
				for (var j = 0; j < elements.Length; ++j)
				{
					var element = elements[j];

					if (element.VertexElementUsage == VertexElementUsage.BlendWeight)
					{
						// Convert from libgdx multiple vector2 blendweight
						// to single int4 blendindices/vector4 blendweight
						if (element.VertexElementFormat != VertexElementFormat.Vector2)
						{
							throw new Exception("Only Vector2 format for BlendWeight supported.");
						}

						var offset = i * declaration.VertexStride + blendWeightOffset + weightsCount;
						LoadByte(byteData, ref offset, (int)(float)data[srcIdx++]);

						offset = i * declaration.VertexStride + blendWeightOffset + 4 + weightsCount * 4;
						LoadFloat(byteData, ref offset, (float)data[srcIdx++]);
						++weightsCount;
						continue;
					}

					switch (element.VertexElementFormat)
					{
						case VertexElementFormat.Vector2:
							LoadFloat(byteData, ref destIdx, (float)data[srcIdx++]);
							LoadFloat(byteData, ref destIdx, (float)data[srcIdx++]);
							break;
						case VertexElementFormat.Vector3:
						case VertexElementFormat.Color:
							LoadFloat(byteData, ref destIdx, (float)data[srcIdx++]);
							LoadFloat(byteData, ref destIdx, (float)data[srcIdx++]);
							LoadFloat(byteData, ref destIdx, (float)data[srcIdx++]);
							break;
						case VertexElementFormat.Vector4:
							LoadFloat(byteData, ref destIdx, (float)data[srcIdx++]);
							LoadFloat(byteData, ref destIdx, (float)data[srcIdx++]);
							LoadFloat(byteData, ref destIdx, (float)data[srcIdx++]);
							LoadFloat(byteData, ref destIdx, (float)data[srcIdx++]);
							break;
						case VertexElementFormat.Byte4:
							LoadByte(byteData, ref destIdx, (int)data[srcIdx++]);
							LoadByte(byteData, ref destIdx, (int)data[srcIdx++]);
							LoadByte(byteData, ref destIdx, (int)data[srcIdx++]);
							LoadByte(byteData, ref destIdx, (int)data[srcIdx++]);
							break;
						default:
							throw new Exception(string.Format("{0} not supported", element.VertexElementFormat));
					}
				}
			}

			var result = new VertexBuffer(Nrs.GraphicsDevice, declaration, rowsCount, BufferUsage.None);
			result.SetData(byteData);

			return result;
		}

		public static Sprite3D LoadFromJson(string json, Func<string, Texture2D> textureGetter)
		{
			var root = JObject.Parse(json);

			var result = new Sprite3D();
			var meshesData = (JArray)root["meshes"];
			var meshes = new Dictionary<string, MeshPart>();
			foreach (JObject meshData in meshesData)
			{
				// Determine vertex type
				int elementsPerRow;
				var declaration = LoadDeclaration((JArray)meshData["attributes"], out elementsPerRow);
				var vertices = (JArray)meshData["vertices"];

				int bonesCount = 0;
				foreach(var element in declaration.GetVertexElements())
				{
					if (element.VertexElementUsage != VertexElementUsage.BlendWeight)
					{
						continue;
					}

					if (element.UsageIndex + 1 > bonesCount)
					{
						bonesCount = element.UsageIndex + 1;
					}
				}
				
				var bonesPerMesh = BonesPerMesh.None;
				if (bonesCount >= 3)
				{
					bonesPerMesh = BonesPerMesh.Four;
				}
				else if (bonesCount == 2)
				{
					bonesPerMesh = BonesPerMesh.Two;
				}
				else if (bonesCount == 1)
				{
					bonesPerMesh = BonesPerMesh.One;
				}

				var vertexBuffer = LoadVertexBuffer(ref declaration, elementsPerRow, vertices);

				var partsData = (JArray)meshData["parts"];
				foreach (JObject partData in partsData)
				{
					var id = partData.GetId();

					// var type = (PrimitiveType)Enum.Parse(typeof(PrimitiveType), partData.EnsureString("type"));
					var indicesData = (JArray)partData["indices"];
					var indices = new short[indicesData.Count];
					for (var i = 0; i < indicesData.Count; ++i)
					{
						indices[i] = Convert.ToInt16(indicesData[i]);
					}

					var indexBuffer = new IndexBuffer(Nrs.GraphicsDevice, IndexElementSize.SixteenBits,
						indices.Length, BufferUsage.None);
					indexBuffer.SetData(indices);

					meshes[id] = new MeshPart
					{
						IndexBuffer = indexBuffer,
						VertexBuffer = vertexBuffer,
						BonesPerMesh = bonesPerMesh
					};
				}
			}

			var materials = (JArray)root["materials"];
			foreach (JObject materialData in materials)
			{
				var material = new Material
				{
					Id = materialData.GetId(),
					DiffuseColor = Color.White
				};

				JToken obj;
				if (materialData.TryGetValue("diffuse", out obj) && obj != null)
				{
					material.DiffuseColor = new Color(obj.ToVector4(1.0f));
				}

				var texturesData = (JArray)materialData["textures"];
				var name = texturesData[0]["filename"].ToString();
				if (!string.IsNullOrEmpty(name))
				{
					material.Texture = textureGetter(name);
				}

				result.Materials.Add(material);
			}

			// Load nodes hierarchy
			var rootNode = (JObject)((JArray)root["nodes"])[0];
			result.RootNode = LoadNode(rootNode);

			if (result.RootNode != null)
			{
				// Update meshes and materials
				result.TraverseNodes(bn =>
				{
					var asMesh = bn as MeshNode;
					if (asMesh == null)
					{
						return;
					}

					foreach (var part in asMesh.Parts)
					{
						var meshPart = meshes[part.MeshPartId];

						part.VertexBuffer = meshPart.VertexBuffer;
						part.IndexBuffer = meshPart.IndexBuffer;
						part.BonesPerMesh = meshPart.BonesPerMesh;

						part.Material = (from m in result.Materials where m.Id == part.MaterialId select m).First();
					}

					result.Meshes.Add(asMesh);
				});

				var NodesDict = new Dictionary<string, Node>();
				result.TraverseNodes(bn =>
				{
					NodesDict[bn.Id] = bn;
				});

				// Set parent nodes
				foreach (var m in result.Meshes)
				{
					foreach (var part in m.Parts)
					{
						foreach (var bone in part.Bones)
						{
							Node bn;
							if (NodesDict.TryGetValue(bone.NodeId, out bn))
							{
								bone.ParentNode = bn;
							}
						}
					}
				}
			}

			// Load animations
			if (root.ContainsKey("animations"))
			{
				var animationsData = (JArray)root["animations"];
				foreach (JObject animationData in animationsData)
				{
					var animation = new Sprite3DAnimation
					{
						Id = animationData.GetId()
					};

					var bonesData = (JArray)animationData["bones"];
					foreach(JObject boneData in bonesData)
					{
						var boneId = boneData["boneId"].ToString();
						var Node = result.FindNodeById(boneId);
						if (Node == null)
						{
							throw new Exception(string.Format("Could not find bone '{0}'.", boneId));
						}

						var boneAnimation = new BoneAnimation
						{
							Node = Node
						};

						Vector3 scale, translation;
						Quaternion rotation;

						Node.Transform.Decompose(out scale,
							out rotation, out translation);

						var keyframesData = (JArray)boneData["keyframes"];
						foreach (JObject keyframeData in keyframesData)
						{
							var keyframe = new AnimationKeyframe
							{
								Time = TimeSpan.FromMilliseconds(keyframeData["keytime"].ToFloat()),
							};

							JToken token;
							if (keyframeData.TryGetValue("scale", out token))
							{
								scale = token.ToVector3();
							}

							if (keyframeData.TryGetValue("translation", out token))
							{
								translation = token.ToVector3();
							}

							if (keyframeData.TryGetValue("rotation", out token))
							{
								var v = token.ToVector4();
								rotation = new Quaternion(v.X, v.Y, v.Z, v.W);
							}

							keyframe.Transform = CreateTransform(translation,
								scale,
								rotation);

							boneAnimation.Frames.Add(keyframe);
						}

						animation.BoneAnimations.Add(boneAnimation);
					}

					result.Animations[animation.Id] = animation;
				}
			}

			return result;
		}
	}
}
