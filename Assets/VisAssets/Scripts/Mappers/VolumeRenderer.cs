using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets.SciVis.Structured.Volume
{
	using ModuleState = Activation.ModuleState;

	// =========================================================================
	// Editor Extension
	// =========================================================================
#if UNITY_EDITOR
	[CustomEditor(typeof(VolumeRenderer))]
	public class VolumeRendererEditor : Editor
	{
		SerializedProperty transferFunction, density, numSteps, threshold, volumeShader, undefMapping;
		SerializedProperty enableClipping, clipAxis, clipPosition, clipRotation1, clipRotation2, invertClip;
		SerializedProperty enableLighting, ambient, diffuse;

		/// <summary>
		/// Initializes serialized properties when the object is selected in the Inspector.
		/// </summary>
		private void OnEnable()
		{
			transferFunction = serializedObject.FindProperty("transferFunction");
			density = serializedObject.FindProperty("density");
			numSteps = serializedObject.FindProperty("numSteps");
			threshold = serializedObject.FindProperty("threshold");
			volumeShader = serializedObject.FindProperty("volumeShader");
			undefMapping = serializedObject.FindProperty("undefMapping");

			enableClipping = serializedObject.FindProperty("enableClipping");
			clipAxis = serializedObject.FindProperty("clipAxis");
			clipPosition = serializedObject.FindProperty("clipPosition");
			clipRotation1 = serializedObject.FindProperty("clipRotation1");
			clipRotation2 = serializedObject.FindProperty("clipRotation2");
			invertClip = serializedObject.FindProperty("invertClip");

			enableLighting = serializedObject.FindProperty("enableLighting");
			ambient = serializedObject.FindProperty("ambient");
			diffuse = serializedObject.FindProperty("diffuse");
		}

		/// <summary>
		/// Renders the custom Inspector GUI for the VolumeRenderer module.
		/// </summary>
		public override void OnInspectorGUI()
		{
			var mapper = target as VolumeRenderer;

			serializedObject.Update();

			EditorGUI.BeginChangeCheck();

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(transferFunction, new GUIContent("Transfer Function (Color Map)"));

			GUILayout.Space(5f);

			EditorGUILayout.Slider(density, 0f, 50f, new GUIContent("Density (Opacity)"));

			GUILayout.Space(5f);

			EditorGUILayout.IntSlider(numSteps, 32, 256, new GUIContent("Ray Steps (Quality)"));

			GUILayout.Space(5f);

			EditorGUILayout.Slider(threshold, 0f, 1f, new GUIContent("Threshold (Skip Empty)"));

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(undefMapping, new GUIContent("UNDEF Mapping Mode"));

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(enableClipping, new GUIContent("Enable Clipping"));

			GUILayout.Space(5f);

			if (enableClipping.boolValue)
			{
				EditorGUI.indentLevel++;

				EditorGUILayout.PropertyField(clipAxis, new GUIContent("Clip Axis"));

				GUILayout.Space(5f);

				EditorGUILayout.PropertyField(clipPosition, new GUIContent("Position"));

				GUILayout.Space(5f);

				EditorGUILayout.PropertyField(clipRotation1, new GUIContent("Rotation 1"));

				GUILayout.Space(5f);

				EditorGUILayout.PropertyField(clipRotation2, new GUIContent("Rotation 2"));

				GUILayout.Space(5f);

				EditorGUILayout.PropertyField(invertClip, new GUIContent("Invert Cut Direction"));

				GUILayout.Space(5f);

				EditorGUI.indentLevel--;
			}

			EditorGUILayout.PropertyField(enableLighting, new GUIContent("Enable Shading"));

			GUILayout.Space(5f);

			if (enableLighting.boolValue)
			{
				EditorGUI.indentLevel++;
				EditorGUILayout.Slider(ambient, 0f, 1f, new GUIContent("Ambient Light"));

				GUILayout.Space(5f);

				EditorGUILayout.Slider(diffuse, 0f, 2f, new GUIContent("Diffuse Light"));

				GUILayout.Space(5f);

				EditorGUI.indentLevel--;
			}

			EditorGUILayout.PropertyField(volumeShader, new GUIContent("Volume Shader"));

			GUILayout.Space(5f);

			EditorGUILayout.PropertyField(serializedObject.FindProperty("UIPrefab"), new GUIContent("UI Prefab"));

			GUILayout.Space(5f);

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(target, "VolumeRenderer");
				EditorUtility.SetDirty(target);

				if (EditorApplication.isPlaying && mapper != null)
				{
					mapper.UpdateMaterialProperties();
					if(mapper.activation != null) mapper.activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
				}
			}
			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	// =========================================================================
	// Main Class
	// =========================================================================
	public class VolumeRenderer : MapperModuleTemplate, IColormapReceiver
	{
		public enum UndefMappingMode { MapTo0_Min, MapTo255_Max }
		public enum ClipAxis { X, Y, Z }

		[HideInInspector] public bool enableClipping = false;
		[HideInInspector] public ClipAxis clipAxis = ClipAxis.Z;
		[HideInInspector] [Range(0f, 1f)] public float clipPosition = 0.5f;
		[HideInInspector] [Range(-90f, 90f)] public float clipRotation1 = 0f;
		[HideInInspector] [Range(-90f, 90f)] public float clipRotation2 = 0f;
		[HideInInspector] public bool invertClip = false;

		[HideInInspector] public bool enableLighting = false;
		[HideInInspector] [Range(0f, 1f)] public float ambient = 0.5f;
		[HideInInspector] [Range(0f, 2f)] public float diffuse = 1.0f;

		[System.Serializable]
		public class ClipSettings
		{
			public float position = 0.5f;
			public float rotation1 = 0f;
			public float rotation2 = 0f;
		}

		[HideInInspector]
		public ClipSettings[] savedClipSettings = new ClipSettings[3] {
			new ClipSettings(), new ClipSettings(), new ClipSettings()
		};

		[HideInInspector]
		[SerializeField] private ClipAxis lastClipAxis = ClipAxis.Z;

		public Shader volumeShader;
		public Gradient transferFunction;
		[Range(0f, 50f)] public float density = 10.0f;
		[Range(32, 256)] public int numSteps = 128;
		[Range(0f, 1f)] public float threshold = 0.01f;
		public UndefMappingMode undefMapping = UndefMappingMode.MapTo0_Min;

		private DataElement element;
		private int[] dims;
		private float[] values;

		private Texture3D volumeTexture;
		private Texture2D transferTexture;
		private Color[] transferColors = new Color[256];

		private Texture2D lutTextureX;
		private Texture2D lutTextureY;
		private Texture2D lutTextureZ;

		private GameObject volumeObject;
		private Material volumeMaterial;

		[HideInInspector]
		public bool useExternalColormap = false;
		[HideInInspector]
		public Color[] externalTransferColors = new Color[256];

		// Properties for downstream access
		public DataElement Element => element;
		public Texture3D VolumeTexture => volumeTexture;
		public Texture2D TransferTexture => transferTexture;
		public Texture2D LutTextureX => lutTextureX;
		public Texture2D LutTextureY => lutTextureY;
		public Texture2D LutTextureZ => lutTextureZ;

#if UNITY_EDITOR
		/// <summary>
		/// Resets component variables to their default values.
		/// </summary>
		protected override void Reset()
		{
			base.Reset();

			density = 10.0f;
			InitializeDefaultGradient();
		}
#endif

		/// <summary>
		/// Initializes a default color gradient mapping for the transfer function.
		/// </summary>
		private void InitializeDefaultGradient()
		{
			if (transferFunction == null) transferFunction = new Gradient();
			transferFunction.SetKeys(
				new GradientColorKey[] {
					new GradientColorKey(new Color(0f, 0f, 1f), 0f),
					new GradientColorKey(new Color(0f, 1f, 0f), 0.5f),
					new GradientColorKey(new Color(1f, 0f, 0f), 1f)
				},
				new GradientAlphaKey[] {
					new GradientAlphaKey(0f, 0f),
					new GradientAlphaKey(128f / 255f, 0.25f),
					new GradientAlphaKey(1f, 0.5f),
					new GradientAlphaKey(1f, 1f)
				}
			);
		}

		/// <summary>
		/// Validates parameters modified in the Inspector, ensuring clip states are correctly preserved across axes.
		/// </summary>
		private void OnValidate()
		{
			if (savedClipSettings == null || savedClipSettings.Length < 3) return;

			if (clipAxis != lastClipAxis)
			{
				// Backup the current settings to the previous axis
				savedClipSettings[(int)lastClipAxis].position = clipPosition;
				savedClipSettings[(int)lastClipAxis].rotation1 = clipRotation1;
				savedClipSettings[(int)lastClipAxis].rotation2 = clipRotation2;

				// Restore the saved settings for the newly selected axis
				clipPosition = savedClipSettings[(int)clipAxis].position;
				clipRotation1 = savedClipSettings[(int)clipAxis].rotation1;
				clipRotation2 = savedClipSettings[(int)clipAxis].rotation2;
				lastClipAxis = clipAxis;
			}
			else
			{
				// Constantly update the current axis backup
				savedClipSettings[(int)clipAxis].position = clipPosition;
				savedClipSettings[(int)clipAxis].rotation1 = clipRotation1;
				savedClipSettings[(int)clipAxis].rotation2 = clipRotation2;
			}
		}

		/// <summary>
		/// Initializes internal states and activates depth texture rendering for correct occlusion.
		/// </summary>
		public override void InitModule()
		{
			dims = new int[3];

			if (Camera.main != null) Camera.main.depthTextureMode |= DepthTextureMode.Depth;

			if (transferFunction == null || transferFunction.colorKeys == null || transferFunction.colorKeys.Length == 0)
			{
				InitializeDefaultGradient();
			}
		}

		/// <summary>
		/// The main execution function that routes data mapping and ensures compatibility with data structures.
		/// </summary>
		public override int BodyFunc()
		{
			if (pdf.dataLoaded && pdf.elements != null && pdf.elements.Length > 0)
			{
				if (pdf.elements[0].fieldType == DataElement.FieldType.IRREGULAR)
				{
					Debug.LogWarning("[VolumeRenderer] IRREGULAR data is not supported natively. Please insert a Remap module before VolumeRenderer.");
					if (volumeObject != null) volumeObject.SetActive(false);
					return 0;
				}

				if (volumeObject != null) volumeObject.SetActive(true);
				SetupVolumeObject();
				UpdateMaterialProperties();
			}
			return 1;
		}

		/// <summary>
		/// Called continuously. Updates dynamic elements like camera depth flags and clipping planes.
		/// </summary>
		public override void IdleFunc()
		{
			if (Camera.main != null)
			{
				Camera.main.depthTextureMode |= DepthTextureMode.Depth;
			}

			UpdateClippingPlane();
		}

		/// <summary>
		/// Applies parameter changes from the UI and recalculates textures if dimensions change.
		/// </summary>
		public override void SetParameters()
		{
			if (!pdf.dataLoaded) return;

			if (dims != null && pdf.elements != null && pdf.elements.Length > 0)
			{
				if (dims[0] != pdf.elements[0].dims[0] ||
					dims[1] != pdf.elements[0].dims[1] ||
					dims[2] != pdf.elements[0].dims[2])
				{
					SoftRebuild();
					return;
				}
			}
			UpdateMaterialProperties();
		}

		/// <summary>
		/// Performs a partial rebuild of volumetric textures without completely resetting user parameters.
		/// </summary>
		private void SoftRebuild()
		{
			element = pdf.elements[0];
			dims = element.dims;
			values = element.values;

			BuildVolumeTexture();
			BuildLUTsIfNeeded();
			SetupVolumeObject();
			UpdateMaterialProperties();
		}

		/// <summary>
		/// Re-initializes module parameters and rebuilds all volumetric textures from parent data.
		/// </summary>
		public override void ReSetParameters()
		{
			if (!pdf.dataLoaded) return;

			element = pdf.elements[0];
			dims = element.dims;
			values = element.values;

			BuildVolumeTexture();
			BuildLUTsIfNeeded();
			SetupVolumeObject();
			UpdateMaterialProperties();
		}

		/// <summary>
		/// Cleans up dynamically generated textures and materials to prevent memory leaks.
		/// </summary>
		private void OnDestroy()
		{
			if (volumeTexture != null) Destroy(volumeTexture);
			if (transferTexture != null) Destroy(transferTexture);

			if (lutTextureX != null) Destroy(lutTextureX);
			if (lutTextureY != null) Destroy(lutTextureY);
			if (lutTextureZ != null) Destroy(lutTextureZ);

			if (volumeMaterial != null) Destroy(volumeMaterial);
			if (volumeObject != null) Destroy(volumeObject);
		}

		/// <summary>
		/// Resets the associated UI components to their default states.
		/// </summary>
		public override void ResetUI() { }

		/// <summary>
		/// Compiles the scalar scalar field data into a unified 3D Texture for GPU raymarching.
		/// </summary>
		private void BuildVolumeTexture()
		{
			if (volumeTexture != null) Destroy(volumeTexture);

			int width = dims[0];
			int height = dims[1];
			int depth = dims[2];

			volumeTexture = new Texture3D(width, height, depth, TextureFormat.R8, false);
			volumeTexture.wrapMode = TextureWrapMode.Clamp;
			volumeTexture.filterMode = FilterMode.Bilinear;

			byte[] volumeData = new byte[width * height * depth];

			float min = element.min;
			float max = element.max;
			float range = max - min;
			if (range == 0) range = 1f;

			bool useUndef = element.useUndef;
			float undef = element.undef;

			for (int i = 0; i < values.Length; i++)
			{
				if (useUndef && values[i] == undef)
				{
					volumeData[i] = (undefMapping == UndefMappingMode.MapTo0_Min) ? (byte)0 : (byte)255;
				}
				else
				{
					float normalized = (values[i] - min) / range;

					if (undefMapping == UndefMappingMode.MapTo0_Min)
					{
						volumeData[i] = (byte)(1 + Mathf.Clamp01(normalized) * 254f);
					}
					else
					{
						volumeData[i] = (byte)(Mathf.Clamp01(normalized) * 254f);
					}
				}
			}

			volumeTexture.SetPixelData(volumeData, 0);
			volumeTexture.Apply();
		}

		/// <summary>
		/// Generates Look-Up Textures (LUTs) for Rectilinear grids to map computational space to physical coordinates.
		/// </summary>
		private void BuildLUTsIfNeeded()
		{
			if (element.fieldType == DataElement.FieldType.RECTILINEAR)
			{
				if (lutTextureX != null) Destroy(lutTextureX);
				if (lutTextureY != null) Destroy(lutTextureY);
				if (lutTextureZ != null) Destroy(lutTextureZ);

				lutTextureX = GenerateCoordinateLUT(element.coords[0]);
				lutTextureY = GenerateCoordinateLUT(element.coords[1]);
				lutTextureZ = GenerateCoordinateLUT(element.coords[2]);
			}
		}

		/// <summary>
		/// Creates a 1D Look-Up Texture mapping physical grid positions to normalized bounds.
		/// </summary>
		private Texture2D GenerateCoordinateLUT(float[] coords, int resolution = 1024)
		{
			Texture2D lut = new Texture2D(resolution, 1, TextureFormat.RFloat, false);
			lut.wrapMode = TextureWrapMode.Clamp;
			lut.filterMode = FilterMode.Bilinear;

			Color[] pixels = new Color[resolution];
			int numCoords = coords.Length;

			bool isAscending = coords[0] < coords[numCoords - 1];
			float minCoord = isAscending ? coords[0] : coords[numCoords - 1];
			float maxCoord = isAscending ? coords[numCoords - 1] : coords[0];
			float range = maxCoord - minCoord;
			if (range == 0) range = 1f;

			for (int x = 0; x < resolution; x++)
			{
				float t = (float)x / (resolution - 1);
				float physVal = minCoord + t * range;

				float mappedUV = 0f;

				for (int i = 0; i < numCoords - 1; i++)
				{
					float c1 = coords[i];
					float c2 = coords[i + 1];

					if ((isAscending && physVal >= c1 && physVal <= c2) ||
						(!isAscending && physVal <= c1 && physVal >= c2))
					{
						float localFraction = (physVal - c1) / (c2 - c1);
						mappedUV = (i + localFraction) / (numCoords - 1);
						break;
					}
				}
				pixels[x] = new Color(mappedUV, 0, 0, 1);
			}

			lut.SetPixels(pixels);
			lut.Apply();
			return lut;
		}

		/// <summary>
		/// Initializes the bounding box cube object acting as the spatial proxy for the raymarching shader.
		/// </summary>
		private void SetupVolumeObject()
		{
			if (volumeObject == null)
			{
				volumeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
				volumeObject.name = "VolumeRenderCube";
				volumeObject.transform.SetParent(this.transform, false);
				Destroy(volumeObject.GetComponent<BoxCollider>());

				if (volumeShader != null) volumeMaterial = new Material(volumeShader);
				volumeObject.GetComponent<MeshRenderer>().sharedMaterial = volumeMaterial;
			}

			var scale = transform.localScale;
			var v0 = Vector3.Scale(element.boundMin, scale);
			var v1 = Vector3.Scale(element.boundMax, scale);

			volumeObject.transform.localPosition = v0 + (v1 - v0) / 2f;
			volumeObject.transform.localScale = v1 - v0;
		}

		/// <summary>
		/// Calculates the geometric plane for cross-section rendering and synchronizes it with the Shader.
		/// </summary>
		private void UpdateClippingPlane()
		{
			if (volumeMaterial == null) return;

			if (enableClipping)
			{
				// Calculate position and rotation in local space bounds (-0.5 to 0.5)
				float localOffset = clipPosition - 0.5f;
				Vector3 localPos = Vector3.zero;
				Quaternion localRot = Quaternion.identity;

				Quaternion tilt = Quaternion.Euler(clipRotation1, clipRotation2, 0);

				switch (clipAxis)
				{
					case ClipAxis.X:
						localPos = new Vector3(localOffset, 0, 0);
						localRot = Quaternion.Euler(0, 90, 0) * tilt;
						break;
					case ClipAxis.Y:
						localPos = new Vector3(0, localOffset, 0);
						localRot = Quaternion.Euler(-90, 0, 0) * tilt;
						break;
					case ClipAxis.Z:
						localPos = new Vector3(0, 0, localOffset);
						localRot = tilt;
						break;
				}

				// Determine normal vector (invert logic flips the visible cut direction)
				Vector3 localNormal = localRot * Vector3.back;
				if (invertClip) localNormal = -localNormal;

				// Offset parameters back to [0, 1] normalized UV space for the shader
				volumeMaterial.SetVector("_ClipPlanePos", localPos + new Vector3(0.5f, 0.5f, 0.5f));
				volumeMaterial.SetVector("_ClipPlaneNormal", localNormal);
				volumeMaterial.SetFloat("_EnableClipping", 1.0f);
			}
			else
			{
				volumeMaterial.SetFloat("_EnableClipping", 0.0f);
			}
		}

		/// <summary>
		/// Synchronizes internal scalar properties, textures, and settings to the GPU Material.
		/// </summary>
		public void UpdateMaterialProperties()
		{
			if (volumeMaterial == null) return;

			if (transferTexture == null)
			{
				transferTexture = new Texture2D(256, 1, TextureFormat.RGBA32, false);
				transferTexture.wrapMode = TextureWrapMode.Clamp;
			}

			if (useExternalColormap)
			{
				System.Array.Copy(externalTransferColors, transferColors, 256);

				if (undefMapping == UndefMappingMode.MapTo0_Min)
				{
					transferColors[0] = new Color(0f, 0f, 0f, 0f);
				}
				else
				{
					transferColors[255] = new Color(0f, 0f, 0f, 0f);
				}

				transferTexture.SetPixels(transferColors);
				transferTexture.Apply();
			}
			else if (transferFunction != null)
			{
				if (undefMapping == UndefMappingMode.MapTo0_Min)
				{
					transferColors[0] = new Color(0f, 0f, 0f, 0f);
					for (int i = 1; i <= 255; i++)
					{
						float t = (i - 1) / 254f;
						transferColors[i] = transferFunction.Evaluate(t);
					}
				}
				else
				{
					for (int i = 0; i <= 254; i++)
					{
						float t = i / 254f;
						transferColors[i] = transferFunction.Evaluate(t);
					}
					transferColors[255] = new Color(0f, 0f, 0f, 0f);
				}

				transferTexture.SetPixels(transferColors);
				transferTexture.Apply();
			}

			if (volumeTexture != null) volumeMaterial.SetTexture("_VolumeTex", volumeTexture);
			if (transferTexture != null) volumeMaterial.SetTexture("_TransferTex", transferTexture);

			if (element != null && element.fieldType == DataElement.FieldType.RECTILINEAR && lutTextureX != null)
			{
				volumeMaterial.SetTexture("_LutTexX", lutTextureX);
				volumeMaterial.SetTexture("_LutTexY", lutTextureY);
				volumeMaterial.SetTexture("_LutTexZ", lutTextureZ);
				volumeMaterial.SetFloat("_UseLUT", 1.0f);
			}
			else
			{
				volumeMaterial.SetFloat("_UseLUT", 0.0f);
			}

			volumeMaterial.SetFloat("_Density", density);
			volumeMaterial.SetInt("_NumSteps", numSteps);
			volumeMaterial.SetFloat("_Threshold", threshold);

			volumeMaterial.SetFloat("_EnableLighting", enableLighting ? 1.0f : 0.0f);
			volumeMaterial.SetFloat("_Ambient", ambient);
			volumeMaterial.SetFloat("_Diffuse", diffuse);
		}

		public void ApplyColormap(Texture2D colormapTexture)
		{
			if (colormapTexture != null)
			{
				Color[] incoming = colormapTexture.GetPixels();
				if (incoming.Length == 256)
				{
					System.Array.Copy(incoming, externalTransferColors, 256);
					useExternalColormap = true;
				}
			}
			else
			{
				useExternalColormap = false;
			}

			UpdateMaterialProperties();

			if (activation != null)
			{
				activation.SetParameterChanged(ModuleState.PARAMETER_CHANGED);
			}
		}

		public Gradient GetGradient()
		{
			return transferFunction;
		}
	}
}