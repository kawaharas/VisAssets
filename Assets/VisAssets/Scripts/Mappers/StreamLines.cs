using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
//using UnityEngine.InputSystem;
//using Unity.XR.CoreUtils;
//using UnityEngine.XR.Interaction.Toolkit.UI;
//using UnityEngine.Experimental.XR.Interaction;
//using UnityEngine.SpatialTracking;
//using UnityEngine.XR.Interaction.Toolkit;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace VisAssets.SciVis.Structured.StreamLines
{
#if UNITY_EDITOR
//	[CanEditMultipleObjects]
	[CustomEditor(typeof(StreamLines))]
	public class StreamLinesEditor : Editor
	{
		SerializedProperty p0;

		private void OnEnable()
		{
//			serializedObject.FindProperty("__dummy__"); // for null value
			p0 = serializedObject.FindProperty("p0");
		}

		public override void OnInspectorGUI()
		{
			var streamlines = target as StreamLines;

			base.DrawDefaultInspector();

			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			GUILayout.Space(10f);

			char baseLabel = 'X';
//			var color = EditorGUILayout.ColorField("Color:", streamlines.color);
//			GUILayout.Space(10f);

//			GUILayout.BeginVertical(GUI.skin.box);

			for (int i = 0; i < 3; i++)
			{
				var label = (char)(Convert.ToUInt16(baseLabel) + i);
				GUILayout.BeginHorizontal();
				EditorGUILayout.LabelField(label.ToString(), GUILayout.Width(20));
				p0.GetArrayElementAtIndex(i).floatValue = EditorGUILayout.Slider(p0.GetArrayElementAtIndex(i).floatValue, 0, 1f);
				GUILayout.EndHorizontal();

				GUILayout.Space(3f);
			}
/*
			GUILayout.Space(3f);
			EditorGUIUtility.labelWidth = 35;
			EditorGUIUtility.fieldWidth = 50;
			var _p0x_tmp = EditorGUILayout.Slider("x:", p0x.floatValue, 0, 1f);
			GUILayout.Space(3f);
			var _p0y_tmp = EditorGUILayout.Slider("y:", p0y.floatValue, 0, 1f);
			GUILayout.Space(3f);
			var _p0z_tmp = EditorGUILayout.Slider("z:", p0z.floatValue, 0, 1f);
			EditorGUIUtility.labelWidth = 0;
			EditorGUIUtility.fieldWidth = 0;
			GUILayout.Space(3f);
*/
//			GUILayout.EndVertical();

			GUILayout.Space(5f);

			if (GUILayout.Button("Add New Seed"))
			{
				streamlines.AddSeed();
			}

			if (EditorGUI.EndChangeCheck())
			{
/*
				Undo.RecordObject(target, "StreamLines");

				streamlines.SetColor(color);


				EditorUtility.SetDirty(target);
*/
			}
			serializedObject.ApplyModifiedProperties();
		}
	}
#endif

	public class StreamLines : MapperModuleTemplate
	{
		#region variables
		List<Vector3> vertices;
		List<Color>   colors;
		List<int>     indices;
		Material material;
		Mesh mesh;

		public bool   IsAnimation;
		public bool   IsRepeat;
		public bool   UseMagnitude = false;
		public int    step;
		public int    maxStep;

		public GameObject linePrefab;
		public List<Vector3>    seeds;
		List<GameObject> lines;
		[SerializeField]
		public Color     lineColor;
		[SerializeField, Range(0, 1f)]
		public float [] p0 = new float[3];

		[SerializeField, ReadOnly]
		DataElement[] elements;
		List<int> activeElements;
		int[] dims;
//		float[][] coords;
		float min;
		float max;
		float undef;
		bool  useUndef;

		Vector3 boundMin;
		Vector3 boundMax;

		float magMin; // minimum magnitude
		float magMax; // maximum magnitude
/*
		public enum ButtonState
		{
			RELEASED,
			PRESSED,
			KEEP_PRESSING
		}

		public ButtonState ButtonTrigger = ButtonState.RELEASED;
*/
		#endregion // variables

		public override void InitModule()
		{
			for (int i = 0; i < 3; i++)
			{
				p0[i] = 0;
			}
			vertices = new List<Vector3>();
			indices  = new List<int>();
			colors   = new List<Color>();
			material = new Material(Shader.Find("Sprites/Default"));

			IsAnimation = false;
			IsRepeat    = false;
			step = 0;
			maxStep = 5000;

			magMin = float.MaxValue;
			magMax = float.MinValue;

			elements = new DataElement[3];
			dims = new int[3] { -1, -1, -1 };
			useUndef = false;

			mesh = new Mesh();
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			var meshFilter = GetComponent<MeshFilter>();
			if (meshFilter != null)
			{
				meshFilter.mesh = mesh;
				meshFilter.hideFlags = HideFlags.HideInInspector;
			}
			var meshRenderer = GetComponent<MeshRenderer>();
			if (meshRenderer != null)
			{
				meshRenderer.material  = material;
				meshRenderer.hideFlags = HideFlags.HideInInspector;
			}

			colors.Add(Color.red);
			colors.Add(Color.red);
			colors.Add(Color.green);
			colors.Add(Color.green);
			colors.Add(Color.blue);
			colors.Add(Color.blue);

			for (int i = 0; i < 6; i++)
			{
				indices.Add(i);
			}

			if (linePrefab == null) return;

			seeds = new List<Vector3>();
			lines = new List<GameObject>();
		}

		public override int BodyFunc()
		{
			if (activeElements.Count > 0)
			{
//				RungeKutta();
			}

			return 1;
		}

		public override void IdleFunc()
		{
/*
			if (XRSettings.enable)
			{
				var inputDevices = new List<InputDevice>();
				InputDevices.GetDevicesAtXRNode(XRNode.RightHand, inputDevices);
				foreach (var device in inputDevices)
				{
					if (device.TryGetFeatureValue(CommonUsages.triggerButton, out inputValue) && inputValue)
					{
						if (ButtonTrigger == ButtonState.RELEASED)
						{
							var position = tip;
							ButtonTrigger = ButtonState.PRESSED;
						}
						else if (ButtonA == ButtonState.PRESSED)
						{
							ButtonTrigger = ButtonState.KEEP_PRESSING;
						}
					}
					else
					{
						laserPointer.SetActive(false);
						ButtonTrigger = ButtonState.RELEASED;
					}
				}
			}
*/
			DrawGuideLines();
/*
			if (IsAnimation)
			{
				step++;
				if (IsRepeat)
				{
					if (step * 2 > indices.Count)
					{
						step = 0;
					}
				}
				else
				{
					Mathf.Clamp(step, 0, indices.Count / 2);
				}
				int[] subIndices = new int[step];
				System.Array.Copy(indices.ToArray(), subIndices, step);
				mesh.SetIndices(subIndices, MeshTopology.Lines, 0);

				if (vertices.Count != 0)
				{
					sphere.SetActive(true);
				}
				else
				{
					sphere.SetActive(false);
				}
				sphere.transform.localPosition = vertices[subIndices.Last()];
			}
*/
		}

		public override void GetParameters()
		{
		}

		public override void ReSetParameters()
		{
			if (pdf.elements.Length != 3)
			{
				// error
			}

			for (int i = 0; i < pdf.elements.Length; i++)
			{
				elements[i] = pdf.elements[i];
			}

			CheckActiveElements();

			boundMin = elements[0].boundMin;
			boundMax = elements[0].boundMax;
			vertices.Clear();
			vertices.Add(new Vector3(boundMin[0], boundMin[1], boundMin[2]));
			vertices.Add(new Vector3(boundMax[0], boundMin[1], boundMin[2]));
			vertices.Add(new Vector3(boundMin[0], boundMin[1], boundMin[2]));
			vertices.Add(new Vector3(boundMin[0], boundMax[1], boundMin[2]));
			vertices.Add(new Vector3(boundMin[0], boundMin[1], boundMin[2]));
			vertices.Add(new Vector3(boundMin[0], boundMin[1], boundMax[2]));

			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			mesh.SetIndices(indices, MeshTopology.Lines, 0);

			var meshFilter = GetComponent<MeshFilter>();
			meshFilter.mesh = mesh;
			
			step = 0;
		}

		public override void SetParameters()
		{
		}

		public override void ResetUI()
		{
		}

		void OnValidate()
		{
			if (!IsDataLoadedToParent()) return;

//			Calc();
//			DrawGuideLines();

			activation.SetParameterChanged(1);
		}

		private void CheckActiveElements()
		{
			// create a list of active elements
			activeElements = new List<int>();
			for (int i = 0; i < 3; i++)
			{
				if (elements[i].isActive)
				{
					activeElements.Add(i);
				}
			}

			// get variables in the first active element
			if (activeElements.Count > 0)
			{
				int ae0 = activeElements[0];
				for (int n = 0; n < 3; n++)
				{
					dims[n] = elements[ae0].dims[n];
				}
				min      = elements[ae0].min;
				max      = elements[ae0].max;
				undef    = elements[ae0].undef;
				useUndef = elements[ae0].useUndef;

				CheckRange();
			}
		}

		private void CheckRange()
		{
			magMin = float.MaxValue;
			magMax = float.MinValue;
			Vector3 vec3 = new Vector3();
			int size = dims[0] * dims[1] * dims[2];
			for (int i = 0; i < size; i++)
			{
				bool IsUndef = false;
				for (int n = 0; n < 3; n++)
				{
					vec3[n] = 0; // initialize by zero

					if (elements[n].isActive)
					{
						float value = elements[n].values[i];
						if (useUndef && (value == undef))
						{
							IsUndef = true;
						}
						else
						{
							vec3[n] = elements[n].values[i];
						}
					}
				}
				if (!IsUndef)
				{
					magMin = Math.Min(magMin, vec3.magnitude);
					magMax = Math.Max(magMax, vec3.magnitude);
				}
			}
		}

		public void SetAnimationState(bool state)
		{
			IsAnimation = state;
		}

		public void AddSeed(Vector3 seed)
		{
			if (activeElements.Count == 0) return;

			seeds.Add(seed);

			if (linePrefab == null) return;

			var line = Instantiate(linePrefab, Vector3.zero, Quaternion.identity);
			line.transform.localScale = new Vector3(1f, 1f, 1f);
			line.transform.SetParent(transform, false);
			line.GetComponent<StreamLine>().SetSeed(seed);
			lines.Add(line);
		}

		public void AddSeed()
		{
			var p = new float[3];
			for (int i = 0; i < 3; i++)
			{
				p[i] = boundMin[i] + (boundMax[i] - boundMin[i]) * p0[i];
			}

			AddSeed(new Vector3(p[0], p[1], p[2]));
		}

		// for vr controllers
		public void AddSeed2(Vector3 seed)
		{
			if (activeElements.Count == 0) return;

			var scale = new Vector3(1f / pdf.scale.x, 1f / pdf.scale.y, 1f / pdf.scale.z);
			if (pdf.coordinateSystem == DataField.CoordinateSystem.RIGHT_HANDED)
			{
				scale = new Vector3(1f / pdf.scale.x, 1f / pdf.scale.y, 1f / -pdf.scale.z);
			}
			var position = Vector3.Scale(seed, scale);
			if (pdf.upAxis == DataField.UpAxis.Z)
			{
				position = Quaternion.Euler(90, 0, 0) * position;
			}

			seed = position;
			seeds.Add(seed);

			if (linePrefab == null) return;

			var line = Instantiate(linePrefab, Vector3.zero, Quaternion.identity);
			line.transform.localScale = new Vector3(1f, 1f, 1f);
			line.transform.SetParent(transform, false);
			line.GetComponent<StreamLine>().SetSeed(seed);
			lines.Add(line);
		}

		public void DrawGuideLines()
		{
			if (!IsDataLoadedToParent()) return;

			var p = new float[3];
			for (int i = 0; i < 3; i++)
			{
				p[i] = boundMin[i] + (boundMax[i] - boundMin[i]) * p0[i];
			}

			vertices[0] = new Vector3(boundMin[0], p[1], p[2]);
			vertices[1] = new Vector3(boundMax[0], p[1], p[2]);
			vertices[2] = new Vector3(p[0], boundMin[1], p[2]);
			vertices[3] = new Vector3(p[0], boundMax[1], p[2]);
			vertices[4] = new Vector3(p[0], p[1], boundMin[2]);
			vertices[5] = new Vector3(p[0], p[1], boundMax[2]);

			mesh.SetVertices(vertices);

			var filter = GetComponent<MeshFilter>();
			// Warningが出ているので要調査
			// SendMessage cannot be called during Awake, CheckConsistency, or OnValidate (StreamLines: OnMeshFilterChanged)
			filter.mesh = mesh;
		}
	}
}