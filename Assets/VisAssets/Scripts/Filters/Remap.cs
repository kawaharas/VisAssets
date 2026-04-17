using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets.SciVis.Structured.Remap
{
	using FieldType = DataElement.FieldType;
	using ModuleState = Activation.ModuleState;

	// =========================================================================
	// Editor Extension
	// =========================================================================
#if UNITY_EDITOR
	[CustomEditor(typeof(Remap))]
	public class RemapEditor : Editor
	{
		SerializedProperty targetDims;
		SerializedProperty splatRadius;
		private bool pendingUpdate = false;

		private void OnEnable()
		{
			targetDims  = serializedObject.FindProperty("targetDims");
			splatRadius = serializedObject.FindProperty("splatRadius");
		}

		public override void OnInspectorGUI()
		{
			var remap = target as Remap;

			serializedObject.Update();

			EditorGUI.BeginChangeCheck();

			GUILayout.Space(5f);

			// Grid Resolution
			// Restrict the minimum value to 2 to prevent errors when the text field is temporarily empty.
			Vector3Int currentDims = targetDims.vector3IntValue;
			currentDims.x = EditorGUILayout.IntSlider("Grid X", currentDims.x, 2, 256);

			GUILayout.Space(5f);

			currentDims.y = EditorGUILayout.IntSlider("Grid Y", currentDims.y, 2, 256);

			GUILayout.Space(5f);

			currentDims.z = EditorGUILayout.IntSlider("Grid Z", currentDims.z, 2, 256);
			targetDims.vector3IntValue = currentDims;

			GUILayout.Space(5f);

			// Splat Radius
			EditorGUILayout.PropertyField(splatRadius, new GUIContent("Splat Radius (Hole Filling)"));

			GUILayout.Space(5f);

			EditorGUILayout.HelpBox("Setting Splat Radius to 1 or higher splats points into surrounding voxels, smoothly interpolating gaps (stripes) during volume rendering.", MessageType.Info);

			GUILayout.Space(5f);

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "Remap");
				EditorUtility.SetDirty(target);

				if (EditorApplication.isPlaying)
				{
					pendingUpdate = true;
				}
			}

			EditorGUILayout.PropertyField(serializedObject.FindProperty("UIPrefab"), new GUIContent("UI Prefab"));

			serializedObject.ApplyModifiedProperties();

			if (pendingUpdate && GUIUtility.hotControl == 0)
			{
				pendingUpdate = false;

				if (remap.activation != null)
				{
					remap.activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
				}
			}
		}
	}
#endif

	// =========================================================================
	// Main Class
	// =========================================================================
	[System.Serializable]
	public class Remap : FilterModuleTemplate
	{
		[SerializeField]
		[Tooltip("Resolution of the orthogonal grid after remapping")]
		public Vector3Int targetDims = new Vector3Int(64, 64, 64);

		[SerializeField, Range(0, 3)]
		[Tooltip("Splat radius for distributing values to surrounding voxels (effective for filling gaps)")]
		public int splatRadius = 1;

		[HideInInspector]
		public List<int> activeElements = new List<int>();

		public override void InitModule()
		{
			activeElements = new List<int>();
		}

		public override int BodyFunc()
		{
			if (activeElements.Count > 0)
			{
				for (int i = 0; i < activeElements.Count; i++)
				{
					ProcessElement(activeElements[i]);
				}
			}

			return 1;
		}

		public override void ReSetParameters()
		{
			df.DisposeElements();
			df.CreateElements(pdf.elements.Length);

			for (int i = 0; i < pdf.elements.Length; i++)
			{
				df.elements[i] = pdf.elements[i].Clone();
			}

			df.coordinateSystem = pdf.coordinateSystem;
			df.upAxis = pdf.upAxis;
			df.scale  = pdf.scale;
			df.offset = pdf.offset;

			CheckActiveElements();
		}

		public override void SetParameters()
		{
		}

		public override void ResetUI()
		{
		}

		/// <summary>
		/// Checks which data elements are currently active and updates their status in the output DataField.
		/// </summary>
		private void CheckActiveElements()
		{
			activeElements.Clear();

			for (int i = 0; i < pdf.elements.Length; i++)
			{
				if (pdf.elements[i].isActive)
				{
					df.elements[i].isActive = true;
					activeElements.Add(i);
				}
				else
				{
					df.elements[i].isActive = false;
				}
			}
		}

		/// <summary>
		/// Processes a single DataElement, remapping it from an IRREGULAR grid to a UNIFORM grid.
		/// Applies splatting using Inverse Distance Weighting (IDW) to smoothly interpolate values and fill gaps.
		/// </summary>
		/// <param name="index">The index of the data element to process.</param>
		private void ProcessElement(int index)
		{
			DataElement src = pdf.elements[index];
			DataElement dst =  df.elements[index];

			if (src.fieldType != FieldType.IRREGULAR)
			{
				return;
			}

			// Enforce a minimum resolution of 2 for safety in internal calculations.
			int nx = Mathf.Max(2, targetDims.x);
			int ny = Mathf.Max(2, targetDims.y);
			int nz = Mathf.Max(2, targetDims.z);
			int newTotalSize = nx * ny * nz;

			float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
			float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

			int originalCount = src.values.Length;

			// 1. Calculate spatial bounds
			if (src.coords.Length > 3 && src.coords[3].Length > 0)
			{
				for (int i = 0; i < originalCount; i++)
				{
					float x = src.coords[3][i * 3 + 0];
					float y = src.coords[3][i * 3 + 1];
					float z = src.coords[3][i * 3 + 2];

					if (x < minX) minX = x;
					if (x > maxX) maxX = x;
					if (y < minY) minY = y;
					if (y > maxY) maxY = y;
					if (z < minZ) minZ = z;
					if (z > maxZ) maxZ = z;
				}
			}
			else
			{
				Debug.LogError("[Remap] Spatial coordinates do not exist in coords[3].");
				dst.isActive = false;
				return;
			}

			float rangeX = Mathf.Max(maxX - minX, 0.0001f);
			float rangeY = Mathf.Max(maxY - minY, 0.0001f);
			float rangeZ = Mathf.Max(maxZ - minZ, 0.0001f);

			// 2. Arrays for weight addition (IDW algorithm)
			double[] sumValues = new double[newTotalSize];
			double[] weightSum = new double[newTotalSize];

			// 3. Splatting process
			for (int i = 0; i < originalCount; i++)
			{
				if (src.useUndef && src.values[i] == src.undef) continue;

				float px = src.coords[3][i * 3 + 0];
				float py = src.coords[3][i * 3 + 1];
				float pz = src.coords[3][i * 3 + 2];

				float u = (px - minX) / rangeX;
				float v = (py - minY) / rangeY;
				float w = (pz - minZ) / rangeZ;

				// Floating-point index on the new grid
				float fx = u * (nx - 1);
				float fy = v * (ny - 1);
				float fz = w * (nz - 1);

				int cx = Mathf.RoundToInt(fx);
				int cy = Mathf.RoundToInt(fy);
				int cz = Mathf.RoundToInt(fz);

				float val = src.values[i];

				if (splatRadius == 0)
				{
					// If the radius is 0, drop to only one point as before (Nearest Neighbor)
					int ix = Mathf.Clamp(cx, 0, nx - 1);
					int iy = Mathf.Clamp(cy, 0, ny - 1);
					int iz = Mathf.Clamp(cz, 0, nz - 1);
					int targetIndex = (iz * nx * ny) + (iy * nx) + ix;

					sumValues[targetIndex] += val;
					weightSum[targetIndex] += 1.0;
				}
				else
				{
					// Splatting using Inverse Distance Weighting (IDW) to smoothly interpolate values and fill gaps.
					for (int sz = -splatRadius; sz <= splatRadius; sz++)
					{
						for (int sy = -splatRadius; sy <= splatRadius; sy++)
						{
							for (int sx = -splatRadius; sx <= splatRadius; sx++)
							{
								int ix = cx + sx;
								int iy = cy + sy;
								int iz = cz + sz;

								if (ix >= 0 && ix < nx && iy >= 0 && iy < ny && iz >= 0 && iz < nz)
								{
									// Calculate the squared distance from the voxel center
									float distSq = (fx - ix) * (fx - ix) + (fy - iy) * (fy - iy) + (fz - iz) * (fz - iz);

									// Ignore the four corners outside the radius (spherical splatting)
									if (distSq > splatRadius * splatRadius) continue;

									// Weighting by the reciprocal of the distance (the closer, the greater the weight)
									double weight = 1.0 / (distSq + 0.01);

									int targetIndex = (iz * nx * ny) + (iy * nx) + ix;
									sumValues[targetIndex] += val * weight;
									weightSum[targetIndex] += weight;
								}
							}
						}
					}
				}
			}

			// 4. Generate new Values array (weighted average)
			List<float> newValues = new List<float>(newTotalSize);

			// Force UNDEF processing since remapping always creates gaps (UNDEF) without data
			float undef = src.useUndef ? src.undef : -9999f;
			dst.useUndef = true;
			dst.undef = undef;

			float vMin = float.MaxValue;
			float vMax = float.MinValue;

			for (int i = 0; i < newTotalSize; i++)
			{
				if (weightSum[i] > 0)
				{
					float avgVal = (float)(sumValues[i] / weightSum[i]);
					newValues.Add(avgVal);

					// Manually update Min/Max
					if (avgVal < vMin) vMin = avgVal;
					if (avgVal > vMax) vMax = avgVal;
				}
				else
				{
					// Voxels that received no data are made transparent (UNDEF)
					newValues.Add(undef);
				}
			}

			// 5. Generate new Coords for UNIFORM
			List<float>[] newCoords = new List<float>[4];

			for (int i = 0; i < 4; i++)
			{
				newCoords[i] = new List<float>();
			}

			float dx = rangeX / Mathf.Max(1, nx - 1);
			float dy = rangeY / Mathf.Max(1, ny - 1);
			float dz = rangeZ / Mathf.Max(1, nz - 1);

			for (int i = 0; i < nx; i++) newCoords[0].Add(minX + dx * i);
			for (int i = 0; i < ny; i++) newCoords[1].Add(minY + dy * i);
			for (int i = 0; i < nz; i++) newCoords[2].Add(minZ + dz * i);

			for (int k = 0; k < nz; k++)
			{
				for (int j = 0; j < ny; j++)
				{
					for (int i = 0; i < nx; i++)
					{
						newCoords[3].Add(newCoords[0][i]);
						newCoords[3].Add(newCoords[1][j]);
						newCoords[3].Add(newCoords[2][k]);
					}
				}
			}

			// 6. Update DataElement
			dst.SetDims(nx, ny, nz);
			dst.SetCoords(newCoords);
			dst.SetValues(newValues);
			dst.fieldType = FieldType.UNIFORM;

			// Apply manually calculated Min/Max
			dst.min = vMin != float.MaxValue ? vMin : 0f;
			dst.max = vMax != float.MinValue ? vMax : 0f;
		}
	}
}