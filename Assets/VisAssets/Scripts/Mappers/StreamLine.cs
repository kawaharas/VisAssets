using System.Collections.Generic;
using UnityEngine;

namespace VisAssets.SciVis.Structured.StreamLines
{
	/// <summary>
	/// Agent (Worker) class responsible for drawing a single streamline.
	/// It delegates 3D vector field calculations to the manager (StreamLines)
	/// and handles its own movement (integration) and mesh updating.
	/// Supports both Line and Ribbon rendering modes.
	/// </summary>
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public class StreamLine : MonoBehaviour
	{
		private StreamLines   manager;
		private bool          isTracing = false;

		private Mesh          mesh;
		private MeshFilter    meshFilter;
		private List<Vector3> vertices  = new List<Vector3>();
		private List<Color>   colors    = new List<Color>();
		private List<Color>   magColors = new List<Color>();
		private List<int>     indices   = new List<int>();

		private Color   lineColor = Color.white;
		private Vector3 currentPosition;
		private int     calculatedSteps = 0;
		private float   h               = 0.005f;

		private GameObject            headSphere;
		private MaterialPropertyBlock propBlock;

		/// <summary>
		/// Initializes the mesh and head sphere components.
		/// </summary>
		private void Awake()
		{
			mesh = new Mesh();
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			mesh.MarkDynamic();

			meshFilter = GetComponent<MeshFilter>();

			if (meshFilter != null)
			{
				meshFilter.mesh      = mesh;
				meshFilter.hideFlags = HideFlags.HideInInspector;
			}

			var meshRenderer = GetComponent<MeshRenderer>();

			if (meshRenderer != null)
			{
				meshRenderer.hideFlags = HideFlags.HideInInspector;
			}

			headSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			Destroy(headSphere.GetComponent<Collider>());
			headSphere.transform.SetParent(transform, false);
			headSphere.SetActive(false);

			propBlock = new MaterialPropertyBlock();
		}

		/// <summary>
		/// Integrates the streamline continuously based on physics steps.
		/// </summary>
		private void FixedUpdate()
		{
			if (!isTracing || manager == null) return;

			for (int i = 0; i < 3; i++)
			{
				if (isTracing)
				{
					RungeKutta();
				}
			}

			UpdateMeshAndSphere();
		}

		/// <summary>
		/// Assigns the materials provided by the manager to the line and head sphere.
		/// </summary>
		public void SetMaterial(Material lineMat, Material sphereMat)
		{
			var meshRenderer = GetComponent<MeshRenderer>();

			if (meshRenderer != null)
			{
				meshRenderer.sharedMaterial = lineMat;
			}

			if (headSphere != null && sphereMat != null)
			{
				var sphereRenderer = headSphere.GetComponent<MeshRenderer>();

				if (sphereRenderer != null)
				{
					sphereRenderer.sharedMaterial = sphereMat;
				}
			}
		}

		/// <summary>
		/// Initializes and starts the streamline calculation from a seed position.
		/// </summary>
		public void StartTrace(Vector3 seedPos, StreamLines mgr, Color color)
		{
			manager         = mgr;
			lineColor       = color;
			currentPosition = seedPos;

			vertices.Clear();
			colors.Clear();
			magColors.Clear();
			indices.Clear();

			calculatedSteps = 0;
			isTracing       = true;
			headSphere.SetActive(false);

			mesh.Clear();
		}

		/// <summary>
		/// 4th-order Runge-Kutta integration to calculate the next position.
		/// Retrieves velocity vectors from the manager's centralized field data.
		/// </summary>
		private void RungeKutta()
		{
			if (calculatedSteps >= manager.maxStep)
			{
				isTracing = false;
				return;
			}

			bool isValid;

			Vector3 v0 = manager.GetVelocityAt(currentPosition, out isValid);

			if (!isValid)
			{
				isTracing = false;
				return;
			}

			Vector3 k1 = h * v0.normalized;

			Vector3 v1 = manager.GetVelocityAt(currentPosition + k1 / 2f, out isValid);

			if (!isValid)
			{
				isTracing = false;
				return;
			}

			Vector3 k2 = h * v1.normalized;

			Vector3 v2 = manager.GetVelocityAt(currentPosition + k2 / 2f, out isValid);

			if (!isValid)
			{
				isTracing = false;
				return;
			}

			Vector3 k3 = h * v2.normalized;

			Vector3 v3 = manager.GetVelocityAt(currentPosition + k3, out isValid);

			if (!isValid)
			{
				isTracing = false;
				return;
			}

			Vector3 k4 = h * v3.normalized;

			Vector3 deltaPosition = (k1 + 2f * k2 + 2f * k3 + k4) / 6f;

			if (deltaPosition.sqrMagnitude < 1e-8f)
			{
				isTracing = false;
				return;
			}

			currentPosition += deltaPosition;

			vertices.Add(currentPosition);
			colors.Add(lineColor);
			magColors.Add(manager.GetMagnitudeColor(v0.magnitude));
			indices.Add(calculatedSteps);

			calculatedSteps++;
		}

		/// <summary>
		/// Stops calculation and clears the current trace data.
		/// </summary>
		public void ClearTrace()
		{
			isTracing = false;
			vertices.Clear();
			mesh.Clear();
			headSphere.SetActive(false);
		}

		/// <summary>
		/// Updates the solid color of the streamline dynamically, altering already drawn segments.
		/// </summary>
		public void UpdateSolidColor(Color newColor)
		{
			lineColor = newColor;

			for (int i = 0; i < colors.Count; i++)
			{
				colors[i] = newColor;
			}

			UpdateMeshAndSphere();
		}

		/// <summary>
		/// Forces a mesh update. Useful when switching between Magnitude and Solid color modes.
		/// </summary>
		public void ForceMeshUpdate()
		{
			UpdateMeshAndSphere();
		}

		/// <summary>
		/// Updates the visual representation of the streamline based on the selected DrawMode.
		/// </summary>
		private void UpdateMeshAndSphere()
		{
			if (vertices.Count < 2) return;

			if (manager.drawMode == StreamLines.DrawMode.LINE)
			{
				UpdateAsLine();
			}
			else
			{
				UpdateAsRibbon();
			}
		}

		/// <summary>
		/// Renders the trace as a 1D LineStrip with a leading sphere.
		/// </summary>
		private void UpdateAsLine()
		{
			headSphere.SetActive(true);
			headSphere.transform.localPosition = vertices[vertices.Count - 1];
			headSphere.transform.localScale    = Vector3.Scale(Vector3.one / 20f, manager.upstreamReciprocalScale);

			var sphereRenderer = headSphere.GetComponent<MeshRenderer>();

			if (sphereRenderer != null)
			{
				Color tipColor = manager.UseMagnitude ? magColors[magColors.Count - 1] : manager.sphereColor;

				sphereRenderer.GetPropertyBlock(propBlock);
				propBlock.SetColor("_Color", tipColor);
				propBlock.SetColor("_BaseColor", tipColor);
				sphereRenderer.SetPropertyBlock(propBlock);
			}

			mesh.Clear();
			mesh.SetVertices(vertices);
			mesh.SetColors(manager.UseMagnitude ? magColors : colors);
			mesh.SetIndices(indices, MeshTopology.LineStrip, 0);
			mesh.RecalculateBounds();
		}

		/// <summary>
		/// Renders the trace as a 3D Ribbon using Parallel Transport to calculate consistent twist/normals.
		/// </summary>
		private void UpdateAsRibbon()
		{
			headSphere.SetActive(false);

			int count = vertices.Count;
			Vector3[] newVerts = new Vector3[count * 2];
			Color[]   newCols  = new Color[count * 2];
			int[]     newInds  = new int[(count - 1) * 6];

			Vector3 currentNormal = Vector3.up;
			float halfWidth = manager.ribbonWidth * 0.5f;

			for (int i = 0; i < count; i++)
			{
				Vector3 tangent;

				if (i == 0)
				{
					tangent = (vertices[1] - vertices[0]).normalized;

					Vector3 up = Vector3.up;

					if (Mathf.Abs(Vector3.Dot(tangent, up)) > 0.99f)
					{
						up = Vector3.right;
					}

					currentNormal = Vector3.Cross(tangent, up).normalized;
				}
				else if (i == count - 1)
				{
					tangent = (vertices[count - 1] - vertices[count - 2]).normalized;
					Vector3 prevTangent = (vertices[i - 1] - vertices[i - 2]).normalized;
					currentNormal = CalculateParallelTransport(prevTangent, tangent, currentNormal);
				}
				else
				{
					tangent = (vertices[i + 1] - vertices[i - 1]).normalized;
					Vector3 prevTangent = (vertices[i] - vertices[i - 1]).normalized;
					currentNormal = CalculateParallelTransport(prevTangent, tangent, currentNormal);
				}

				Vector3 p = vertices[i];
				Vector3 offset = currentNormal * halfWidth;

				newVerts[i * 2]     = p + offset;
				newVerts[i * 2 + 1] = p - offset;

				Color c = manager.UseMagnitude ? magColors[i] : colors[i];
				newCols[i * 2]     = c;
				newCols[i * 2 + 1] = c;

				if (i < count - 1)
				{
					int vIdx = i * 2;
					int iIdx = i * 6;

					newInds[iIdx]     = vIdx;
					newInds[iIdx + 1] = vIdx + 1;
					newInds[iIdx + 2] = vIdx + 2;

					newInds[iIdx + 3] = vIdx + 2;
					newInds[iIdx + 4] = vIdx + 1;
					newInds[iIdx + 5] = vIdx + 3;
				}
			}

			mesh.Clear();
			mesh.SetVertices(newVerts);
			mesh.SetColors(newCols);
			mesh.SetIndices(newInds, MeshTopology.Triangles, 0);
			mesh.RecalculateBounds();
			mesh.RecalculateNormals();
		}

		/// <summary>
		/// Calculates the rotation axis and angle to keep the ribbon flat while following the path's curvature.
		/// </summary>
		private Vector3 CalculateParallelTransport(Vector3 prevTangent, Vector3 currentTangent, Vector3 currentNormal)
		{
			Vector3 axis = Vector3.Cross(prevTangent, currentTangent);

			if (axis.sqrMagnitude > 1e-8f)
			{
				float dot = Mathf.Clamp(Vector3.Dot(prevTangent, currentTangent), -1f, 1f);
				float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;

				return Quaternion.AngleAxis(angle, axis) * currentNormal;
			}

			return currentNormal;
		}
	}
}