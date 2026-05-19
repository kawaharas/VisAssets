using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using VisAssets.SciVis.Structured.Common;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets.SciVis.Structured.ParticleTracer
{
	using FieldType   = DataElement.FieldType;
	using ModuleState = Activation.ModuleState;

#if UNITY_EDITOR
	[CustomEditor(typeof(ParticleTracer))]
	public class ParticleTracerEditor : Editor
	{
		SerializedProperty axis;
		SerializedProperty slice;
		SerializedProperty restrictToSlice;
		SerializedProperty zOffset;
		SerializedProperty useMagnitudeColor;
		SerializedProperty magnitudeGradient;
		SerializedProperty flowColor;
		SerializedProperty particleCount;
		SerializedProperty integrationMethod;
		SerializedProperty speedScale;
		SerializedProperty trailLength;
//		SerializedProperty flowShader;
		SerializedProperty builtinMaterial;
		SerializedProperty urpMaterial;

		private void OnEnable()
		{
			axis              = serializedObject.FindProperty("sliceHelper.axis");
			slice             = serializedObject.FindProperty("sliceHelper.slice");
			restrictToSlice   = serializedObject.FindProperty("restrictToSlice");
			zOffset           = serializedObject.FindProperty("zOffset");
			useMagnitudeColor = serializedObject.FindProperty("useMagnitudeColor");
			magnitudeGradient = serializedObject.FindProperty("magnitudeGradient");
			flowColor         = serializedObject.FindProperty("flowColor");
			particleCount     = serializedObject.FindProperty("particleCount");
			integrationMethod = serializedObject.FindProperty("integrationMethod");
			speedScale        = serializedObject.FindProperty("speedScale");
			trailLength       = serializedObject.FindProperty("trailLength");
//			flowShader        = serializedObject.FindProperty("flowShader");
			builtinMaterial   = serializedObject.FindProperty("builtinMaterial");
			urpMaterial       = serializedObject.FindProperty("urpMaterial");
		}

		public override void OnInspectorGUI()
		{
			var flow = target as ParticleTracer;

			if (flow == null) return;

			serializedObject.Update();

			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			if (axis != null)
			{
				EditorGUILayout.IntSlider(axis, 0, 2, new GUIContent("Axis: "));
			}

			GUILayout.Space(5f);

			if (slice != null)
			{
				EditorGUILayout.Slider(slice, 0f, 1f, new GUIContent("Slice: "));
			}

			GUILayout.Space(5f);

			if (restrictToSlice != null)
			{
				EditorGUILayout.PropertyField(restrictToSlice, new GUIContent("Restrict to Slice Plane"));
			}

			GUILayout.Space(5f);

			if (flow.restrictToSlice && zOffset != null)
			{
				EditorGUILayout.Slider(zOffset, 0f, 0.1f, new GUIContent("Z Offset"));
			}

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "ParticleTracer");
				serializedObject.ApplyModifiedProperties();
				flow.ReSetParameters();
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			// --- Color Settings ---
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.LabelField("--- Color Settings ---", EditorStyles.boldLabel);

			GUILayout.Space(5f);

			if (useMagnitudeColor != null)
			{
				EditorGUILayout.PropertyField(useMagnitudeColor, new GUIContent("Use Magnitude Color"));
			}

			GUILayout.Space(5f);

			if (flow.useMagnitudeColor)
			{
				if (magnitudeGradient != null) EditorGUILayout.PropertyField(magnitudeGradient, new GUIContent("Magnitude Gradient"));
			}
			else
			{
				if (flowColor != null) EditorGUILayout.PropertyField(flowColor, new GUIContent("Static Flow Color"));
			}

			GUILayout.Space(5f);

			// --- Flow Parameters ---
			EditorGUILayout.LabelField("--- Flow Parameters ---", EditorStyles.boldLabel);

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(integrationMethod, new GUIContent("Integration Method"));

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(particleCount, new GUIContent("Particle Count"));

			GUILayout.Space(5f);

			if (speedScale != null)
			{
				EditorGUILayout.Slider(speedScale, 0f, 20f, new GUIContent("Speed Scale"));
			}

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(trailLength,   new GUIContent("Trail Length"));

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "ParticleTracer");
				serializedObject.ApplyModifiedProperties();
				flow.ReSetParameters();
				EditorUtility.SetDirty(target);
			}
/*
			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			if (flowShader != null)
			{
				EditorGUILayout.PropertyField(flowShader, new GUIContent("Flow Shader"));
			}
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "ParticleTracer");
				serializedObject.ApplyModifiedProperties();
				flow.UpdateMaterial();
				EditorUtility.SetDirty(target);
			}
*/
			GUILayout.Space(5f);

			EditorGUI.BeginChangeCheck();

			EditorGUILayout.PropertyField(builtinMaterial, new GUIContent("Built-in Material"));

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(urpMaterial, new GUIContent("URP Material"));

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "ParticleTracer");
				serializedObject.ApplyModifiedProperties();
				flow.UpdateMaterial();
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(serializedObject.FindProperty("UIPrefab"), new GUIContent("UI Prefab"));

			GUILayout.Space(5f);

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	[DisallowMultipleComponent]
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public class ParticleTracer : MapperModuleTemplate
	{
		private const int TRAIL_SEGMENTS = 15;

		/// <summary>
		/// Integration algorithm for particle tracing.
		/// </summary>
		public enum IntegrationMethod
		{
			EULER,
			RK4
		}

		private class Particle
		{
			public Vector3   pos;
			public Vector3[] history    = new Vector3[TRAIL_SEGMENTS + 1];
			public float[]   magHistory = new float[TRAIL_SEGMENTS + 1];
			public float     life;
			public float     maxLife;
		}

		[SerializeField] public SliceHelper sliceHelper = new SliceHelper();
//		[SerializeField] public Shader flowShader;
		[SerializeField] private Material builtinMaterial;
		[SerializeField] private Material urpMaterial;

		public bool  restrictToSlice = true;
		public float zOffset         = 0.001f;

		public bool     useMagnitudeColor = false;
		public Gradient magnitudeGradient = new Gradient();
		public Color    flowColor         = new Color(1f, 1f, 1f, 0.6f);

		public IntegrationMethod integrationMethod = IntegrationMethod.EULER;
		public int   particleCount = 10000;
		public float speedScale    = 20f;
		public float trailLength   = 1.0f;

		private Particle[] particles;
		private Material   sharedMaterial;

		private Mesh      dynamicMesh;
		private Vector3[] meshVertices;
		private Color[]   meshColors;
		private int[]     meshIndices;

		private DataElement[] elements       = new DataElement[3];
		private List<int>     activeElements = new List<int>();
		private Vector3       boundMin, boundMax;
		private float         magMin, magMax;

		private float recordTimer    = 0f;
		private float recordInterval = 0.02f;

		private static ThreadLocal<System.Random> threadRnd = new ThreadLocal<System.Random>(() => new System.Random());

		private Color[] gradientLut = new Color[256];

#if UNITY_EDITOR
		protected override void Reset()
		{
			base.Reset();

//			EnsureCorrectShader();

			magnitudeGradient = new Gradient();

			magnitudeGradient.SetKeys(
				new GradientColorKey[] { new GradientColorKey(Color.blue, 0f), new GradientColorKey(Color.green, 0.5f), new GradientColorKey(Color.red, 1f) },
				new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
			);

			BuildGradientLut();
		}
#endif
/*
#if UNITY_EDITOR
		/// <summary>
		/// Automatically determines the current render pipeline (Built-in or URP) and assigns the appropriate shader.
		/// </summary>
		private void EnsureCorrectShader()
		{
			bool isURP = GraphicsSettings.renderPipelineAsset != null;
			string expectedShaderName = isURP ? "Universal Render Pipeline/Particles/Unlit" : "Sprites/Default";
			string otherShaderName    = isURP ? "Sprites/Default" : "Universal Render Pipeline/Particles/Unlit";

			if (flowShader == null || flowShader.name == otherShaderName || flowShader.name == "Standard")
			{
				flowShader = Shader.Find(expectedShaderName);
			}
		}
#endif
*/
		public override void InitModule()
		{
			if (sliceHelper == null)
			{
				sliceHelper = new SliceHelper();
			}

			sliceHelper.Init();

			if (dynamicMesh == null)
			{
				dynamicMesh = new Mesh();
				dynamicMesh.indexFormat = IndexFormat.UInt32;
				dynamicMesh.MarkDynamic();
			}

			var meshFilter = GetComponent<MeshFilter>();
			if (meshFilter != null)
			{
				meshFilter.sharedMesh = dynamicMesh;
				meshFilter.hideFlags = HideFlags.HideInInspector;
			}

			var meshRenderer = GetComponent<MeshRenderer>();
			if (meshRenderer != null)
			{
				meshRenderer.hideFlags = HideFlags.HideInInspector;
				meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			}

			BuildGradientLut();
			UpdateMaterial();
			InitializeParticles();
		}

		public override int BodyFunc()
		{
			ReSetParameters();

			return 1;
		}

		public override void IdleFunc()
		{
			if (!IsDataLoadedToParent() || dynamicMesh == null || particles == null) return;

			int maxLimit = Application.platform == RuntimePlatform.Android ? 20000 : 100000;
			int expectedCount = Mathf.Clamp(particleCount, 1, maxLimit);

			if (activeElements.Count == 0 || meshVertices == null || meshVertices.Length != expectedCount * (TRAIL_SEGMENTS + 1))
			{
				ReSetParameters();
				if (activeElements.Count == 0 || particles == null) return;
			}

			float dt = Time.deltaTime;
			bool shiftHistory = false;

			recordTimer -= dt;

			if (recordTimer <= 0f)
			{
				shiftHistory = true;
				recordInterval = Mathf.Clamp(0.01f * trailLength, 0.005f, 0.1f);
				recordTimer = recordInterval;
			}

			int currentAxis = sliceHelper.axis;

			Parallel.For(0, particles.Length, i =>
			{
				var p = particles[i];

				if (p == null) return;

				p.life += dt;

				Vector3 deltaPos   = Vector3.zero;
				float   currentMag = 0f;

				if (integrationMethod == IntegrationMethod.EULER)
				{
					bool isValid;
					Vector3 v = GetVelocityAt(p.pos, out isValid);

					if (isValid)
					{
						v = ApplyRestriction(v, currentAxis);
						deltaPos = v * speedScale * dt;
						currentMag = v.magnitude;
					}
					else
					{
						RespawnParticle(i);
						return;
					}
				}
				else // 4th Order Runge-Kutta
				{
					float h = speedScale * dt;
					bool v1Valid, v2Valid, v3Valid, v4Valid;

					Vector3 k1 = ApplyRestriction(GetVelocityAt(p.pos, out v1Valid), currentAxis);
					Vector3 k2 = ApplyRestriction(GetVelocityAt(p.pos + k1 * h * 0.5f, out v2Valid), currentAxis);
					Vector3 k3 = ApplyRestriction(GetVelocityAt(p.pos + k2 * h * 0.5f, out v3Valid), currentAxis);
					Vector3 k4 = ApplyRestriction(GetVelocityAt(p.pos + k3 * h, out v4Valid), currentAxis);

					if (v1Valid && v2Valid && v3Valid && v4Valid)
					{
						deltaPos = (h / 6.0f) * (k1 + 2.0f * k2 + 2.0f * k3 + k4);
						currentMag = k1.magnitude;
					}
					else
					{
						RespawnParticle(i);

						return;
					}
				}

				if (p.life > p.maxLife || (restrictToSlice && deltaPos.sqrMagnitude < 1e-8f))
				{
					RespawnParticle(i);

					return;
				}

				p.pos += deltaPos;

				if (shiftHistory)
				{
					for (int j = TRAIL_SEGMENTS; j > 0; j--)
					{
						p.history[j]    = p.history[j - 1];
						p.magHistory[j] = p.magHistory[j - 1];
					}
				}
				p.history[0]    = p.pos;
				p.magHistory[0] = currentMag;
			});

			BuildDynamicMesh();
		}

		public override void SetParameters()
		{
		}

		public override void GetParameters()
		{
		}

		public override void ReSetParameters()
		{
			if (pdf == null || pdf.elements == null || pdf.elements.Length < 3)
			{
				activeElements.Clear();

				return;
			}

			for (int i = 0; i < 3; i++)
			{
				elements[i] = pdf.elements[i];
			}

			activeElements.Clear();

			for (int i = 0; i < 3; i++)
			{
				if (elements[i] != null && elements[i].isActive)
				{
					activeElements.Add(i);
				}
			}

			if (activeElements.Count > 0 && elements[activeElements[0]] != null && elements[activeElements[0]].coords != null)
			{
				var active = elements[activeElements[0]];

				boundMin = active.boundMin;
				boundMax = active.boundMax;
				sliceHelper.Reset(active);

				CalculateMagnitudeRange();
			}

			BuildGradientLut();
			UpdateMaterial();
			InitializeParticles();
			ClearAndRespawn();
		}

		public override void ResetUI()
		{
		}

		private void OnValidate()
		{
#if UNITY_EDITOR
//			EnsureCorrectShader();
			BuildGradientLut();
#endif
			if (IsDataLoadedToParent())
			{
				activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
			}
		}

		/// <summary>
		/// Modifies the velocity vector based on the slice plane restriction settings.
		/// </summary>
		private Vector3 ApplyRestriction(Vector3 velocity, int axis)
		{
			if (restrictToSlice)
			{
				if (axis == 0)
				{
					velocity.x = 0f;
				}
				else if (axis == 1)
				{
					velocity.y = 0f;
				}
				else if (axis == 2)
				{
					velocity.z = 0f;
				}
			}
			return velocity;
		}

		/// <summary>
		/// Pre-calculates a 256-color Look-Up Table (LUT) from the magnitude gradient.
		/// </summary>
		private void BuildGradientLut()
		{
			if (magnitudeGradient == null) return;

			for (int i = 0; i < 256; i++)
			{
				gradientLut[i] = magnitudeGradient.Evaluate(i / 255f);
			}
		}

		/// <summary>
		/// Updates or creates the shared material used for rendering the particle flow lines.
		/// </summary>
		public void UpdateMaterial()
		{
/*
#if UNITY_EDITOR
			EnsureCorrectShader();
#endif
			if (flowShader == null) return;

			if (sharedMaterial == null || sharedMaterial.shader != flowShader)
			{
				if (sharedMaterial != null)
				{
					if (Application.isPlaying) Destroy(sharedMaterial); else DestroyImmediate(sharedMaterial);
				}
				sharedMaterial = new Material(flowShader);
			}

			var meshRenderer = GetComponent<MeshRenderer>();

			if (meshRenderer != null)
			{
				meshRenderer.sharedMaterial = sharedMaterial;
			}
*/
			var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline ?? UnityEngine.QualitySettings.renderPipeline;
			bool isURP = pipeline != null;

			Material targetMaterial = isURP ? urpMaterial : builtinMaterial;

			if (targetMaterial != null && TryGetComponent<MeshRenderer>(out var renderer))
			{
				renderer.sharedMaterial = targetMaterial;
			}
	}

		/// <summary>
		/// Initializes the particle array and creates the dynamic mesh structures.
		/// </summary>
		private void InitializeParticles()
		{
			dynamicMesh.Clear();

			int maxLimit = Application.platform == RuntimePlatform.Android ? 20000 : 100000;
			int count = Mathf.Clamp(particleCount, 1, maxLimit);

			// Only allocate memory if the required particle count has changed
			if (particles == null || particles.Length != count)
			{
				particles = new Particle[count];

				for (int i = 0; i < count; i++)
				{
					particles[i] = new Particle();
				}
			}

			int totalVertices = count * (TRAIL_SEGMENTS + 1);
			int totalIndices  = count * TRAIL_SEGMENTS * 2;

			if (meshVertices == null || meshVertices.Length != totalVertices)
			{
				meshVertices = new Vector3[totalVertices];
				meshColors   = new Color[totalVertices];
				meshIndices  = new int[totalIndices];

				int iIdx = 0;

				for (int i = 0; i < count; i++)
				{
					int startVIdx = i * (TRAIL_SEGMENTS + 1);

					for (int j = 0; j < TRAIL_SEGMENTS; j++)
					{
						meshIndices[iIdx++] = startVIdx + j;
						meshIndices[iIdx++] = startVIdx + j + 1;
					}
				}
			}

			dynamicMesh.SetVertices(meshVertices);
			dynamicMesh.SetColors(meshColors);
			dynamicMesh.SetIndices(meshIndices, MeshTopology.Lines, 0);
		}

		/// <summary>
		/// Resets a specific particle's position, life, and history.
		/// </summary>
		private void RespawnParticle(int i)
		{
			if (activeElements.Count == 0 || pdf == null || elements == null) return;

			float r1 = (float)threadRnd.Value.NextDouble();
			float r2 = (float)threadRnd.Value.NextDouble();
			Vector3 pos = Vector3.zero;

			float s = restrictToSlice ? sliceHelper.value + zOffset : sliceHelper.value;

			if (sliceHelper.axis == 0)
			{
				pos = new Vector3(s, boundMin.y + (boundMax.y - boundMin.y) * r1, boundMin.z + (boundMax.z - boundMin.z) * r2);
			}
			else if (sliceHelper.axis == 1)
			{
				pos = new Vector3(boundMin.x + (boundMax.x - boundMin.x) * r1, s, boundMin.z + (boundMax.z - boundMin.z) * r2);
			}
			else
			{
				pos = new Vector3(boundMin.x + (boundMax.x - boundMin.x) * r1, boundMin.y + (boundMax.y - boundMin.y) * r2, s);
			}

			particles[i].pos = pos;
			particles[i].life = 0;
			particles[i].maxLife = 1.0f + (float)threadRnd.Value.NextDouble() * 3.0f;

			bool isValid;
			Vector3 vel = GetVelocityAt(pos, out isValid);
			float initialMag = isValid ? vel.magnitude : 0f;

			for (int j = 0; j <= TRAIL_SEGMENTS; j++)
			{
				particles[i].history[j]    = pos;
				particles[i].magHistory[j] = initialMag;
			}
		}

		/// <summary>
		/// Rebuilds the dynamic mesh vertices and colors based on particle histories.
		/// </summary>
		private void BuildDynamicMesh()
		{
			Parallel.For(0, particles.Length, i =>
			{
				var p = particles[i];

				if (p == null) return;

				int vIdx = i * (TRAIL_SEGMENTS + 1);

				for (int j = 0; j <= TRAIL_SEGMENTS; j++)
				{
					meshVertices[vIdx] = p.history[j];

					Color baseCol = flowColor;

					if (useMagnitudeColor)
					{
						float t = (magMax > magMin) ? Mathf.Clamp01((p.magHistory[j] - magMin) / (magMax - magMin)) : 0f;
						int lutIndex = (int)(t * 255f);

						baseCol = gradientLut[lutIndex];
					}

					float alpha = 1.0f - ((float)j / TRAIL_SEGMENTS);
					baseCol.a *= alpha;

					if (p.life < recordInterval * j)
					{
						baseCol.a = 0;
					}

					if (vIdx < meshColors.Length)
					{
						meshColors[vIdx] = baseCol;
					}
					vIdx++;
				}
			});

			dynamicMesh.SetVertices(meshVertices);
			dynamicMesh.SetColors(meshColors);
			dynamicMesh.RecalculateBounds();
		}

		/// <summary>
		/// Retrieves the interpolated velocity vector at a specific world coordinate.
		/// </summary>
		public Vector3 GetVelocityAt(Vector3 position, out bool isValid)
		{
			isValid = false;

			if (activeElements.Count == 0 || elements == null) return Vector3.zero;

			var element = elements[activeElements[0]];

			if (element == null || element.values == null) return Vector3.zero;

			if (position.x < boundMin.x || position.x > boundMax.x ||
				position.y < boundMin.y || position.y > boundMax.y ||
				position.z < boundMin.z || position.z > boundMax.z) return Vector3.zero;

			int ix = FindIndex(element.coords[0], position.x, element.dims[0]);
			int iy = FindIndex(element.coords[1], position.y, element.dims[1]);
			int iz = FindIndex(element.coords[2], position.z, element.dims[2]);

			if (ix < 0 || iy < 0 || iz < 0) return Vector3.zero;

			Vector3 result = Vector3.zero;

			for (int n = 0; n < 3; n++)
			{
				if (elements[n] != null && elements[n].isActive && elements[n].values != null)
				{
					result[n] = GetValueSimple(elements[n], ix, iy, iz);
				}
			}

			isValid = true;

			return result;
		}

		/// <summary>
		/// Finds the corresponding array index for a given coordinate value.
		/// </summary>
		private int FindIndex(float[] coords, float val, int size)
		{
			if (coords == null || size <= 1) return -1;

			bool isAsc = coords[0] < coords[size - 1];

			for (int i = 0; i < size - 1; i++)
			{
				if (isAsc)
				{
					if (coords[i + 1] >= val) return i;
				}
				else
				{
					if (coords[i + 1] <= val) return i;
				}
			}

			return -1;
		}

		/// <summary>
		/// Fetches the scalar value from the data array at the specified grid indices.
		/// </summary>
		private float GetValueSimple(DataElement el, int x, int y, int z)
		{
			int index = z * el.dims[0] * el.dims[1] + y * el.dims[0] + x;

			if (index < 0 || index >= el.values.Length) return 0f;

			return el.values[index];
		}

		/// <summary>
		/// Sets the target slice axis and respawns particles.
		/// </summary>
		public void SetAxis(int _axis)
		{
			if (sliceHelper.SetAxis(_axis))
			{
				ClearAndRespawn();
			}
		}

		/// <summary>
		/// Sets the normalized slice position and respawns particles.
		/// </summary>
		public void SetSlice(float _slice)
		{
			if (sliceHelper.SetSlice(_slice))
			{
				ClearAndRespawn();
			}
		}

		/// <summary>
		/// Forces all particles to respawn immediately.
		/// Randomizes life values to prevent synchronous flashing.
		/// </summary>
		private void ClearAndRespawn()
		{
			if (particles == null) return;

			for (int i = 0; i < particles.Length; i++)
			{
				if (particles[i] != null)
				{
					RespawnParticle(i);

					// Randomize life across the entire lifespan so they don't all disappear at once
					particles[i].life = (float)threadRnd.Value.NextDouble() * particles[i].maxLife;
				}
			}
		}

		/// <summary>
		/// Determines the minimum and maximum velocity magnitudes across the active field.
		/// </summary>
		private void CalculateMagnitudeRange()
		{
			magMin = float.MaxValue;
			magMax = float.MinValue;

			if (activeElements.Count == 0 || elements[activeElements[0]] == null || elements[activeElements[0]].values == null) return;

			int size = elements[activeElements[0]].values.Length;

			for (int i = 0; i < size; i++)
			{
				Vector3 vec = Vector3.zero;

				for (int n = 0; n < 3; n++)
				{
					if (elements[n] != null && elements[n].isActive && elements[n].values != null && i < elements[n].values.Length)
					{
						vec[n] = elements[n].values[i];
					}
				}

				float mag = vec.magnitude;

				if (mag < magMin)
				{
					magMin = mag;
				}
				if (mag > magMax)
				{
					magMax = mag;
				}
			}

			if (magMin == magMax) magMax = magMin + 1f;
		}
	}
}