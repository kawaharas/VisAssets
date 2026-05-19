using UnityEngine;

namespace VisAssets.Extensions.Topo
{
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public class SeaSurface : MonoBehaviour
	{
		private MeshFilter filter;
		private Material seaMaterial;

		[HideInInspector]
		public double west, east, north, south;

		public void UpdateSurface(double w, double e, double s, double n)
		{
			west = w; east = e; south = s; north = n;
			CreateSeaSurface();
		}

		private void CreateSeaSurface()
		{
			if (filter == null) filter = GetComponent<MeshFilter>();

			float x0 = (float)west;
			float x1 = (float)east;
			float y0 = (float)south;
			float y1 = (float)north;

			Vector3[] vertices = new Vector3[] {
				new Vector3(x0, y0, 0),
				new Vector3(x0, y1, 0),
				new Vector3(x1, y1, 0),
				new Vector3(x1, y0, 0)
			};

			Color color = new Color(0f, 0f, 0.5f, 0.65f);
			Color[] colors = new Color[] { color, color, color, color };

			int[] indices = new int[] { 0, 2, 1, 0, 3, 2 };

			var mesh = new Mesh();
			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			mesh.SetIndices(indices, MeshTopology.Triangles, 0);
			mesh.RecalculateNormals();

			filter.sharedMesh = mesh;

			SetupMaterial();
		}

		private void SetupMaterial()
		{
			bool isURP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
			string shaderName = isURP ? "Universal Render Pipeline/Particles/Unlit" : "Sprites/Default";
			Shader shader = Shader.Find(shaderName);

			if (shader != null)
			{
				if (seaMaterial == null || seaMaterial.shader != shader)
				{
					seaMaterial = new Material(shader);

					if (isURP)
					{
						seaMaterial.SetFloat("_Surface", 1.0f); // 1 = Transparent
						seaMaterial.SetFloat("_Blend",   0.0f); // 0 = Alpha

						seaMaterial.SetOverrideTag("RenderType", "Transparent");
						seaMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
						seaMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
						seaMaterial.SetInt("_ZWrite", 0);

						seaMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

						seaMaterial.SetShaderPassEnabled("ShadowCaster", false);
					}
				}

				if (TryGetComponent<MeshRenderer>(out var renderer))
				{
					renderer.sharedMaterial = seaMaterial;
				}
			}
		}
	}
}