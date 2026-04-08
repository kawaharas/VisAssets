using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisAssets
{
	/// <summary>
	/// Controls rotation and scaling of the object via mouse and touch inputs.
	/// </summary>
	public class CtrlOBJ : MonoBehaviour
	{
		[Header("Rotation Speeds")]
		public float rotationSpeedMouse = 1200f;
		public float rotationSpeedTouch = 15f;

		[Header("Scale Speeds")]
		public float scaleSpeedMouse = 0.1f;
		public float scaleSpeedTouch = 0.005f;

		[Header("Scale Limits (Relative to initial size)")]
		public float minScale = 0.1f;
		public float maxScale = 10.0f;

		[Header("Interaction Settings")]
		[Tooltip("Indicates whether interaction is active. Disabled when the pointer is over UI.")]
		public bool isActive = true;

		[Tooltip("Indicates whether the user is currently dragging the object.")]
		public bool isDragging = false;

		// The current scale ratio relative to the initial size (starts at 1.0)
		private float currentScaleRatio = 1.0f;
		private DataField df;

		void Start()
		{
			df = GetComponent<DataField>();
		}

		void Update()
		{
			// Do not process if disabled (e.g., when interacting with UI)
			if (!isActive) return;
			
			// Do not process if the data has not finished loading
			if (df != null && !df.dataLoaded) return;

			HandleInput();
		}

		/// <summary>
		/// Handles both mouse and touch inputs.
		/// </summary>
		private void HandleInput()
		{
			// Prioritize touch processing if supported and at least one finger is touching
			if (Input.touchSupported && Input.touchCount > 0)
			{
				HandleTouch();
			}
			else
			{
				HandleMouse();
			}
		}

		/// <summary>
		/// Handles mouse drag for rotation and scroll wheel for scaling.
		/// </summary>
		private void HandleMouse()
		{
			// Rotation (Drag)
			if (Input.GetMouseButtonDown(0)) isDragging = true;
			if (Input.GetMouseButtonUp(0)) isDragging = false;

			if (isDragging)
			{
				float mouseX = Input.GetAxis("Mouse X") * rotationSpeedMouse * Time.deltaTime;
				float mouseY = Input.GetAxis("Mouse Y") * rotationSpeedMouse * Time.deltaTime;

				RotateObject(mouseX, mouseY);
			}

			// Scaling (Mouse Wheel)
			float scroll = Input.mouseScrollDelta.y;
			if (scroll != 0)
			{
				ApplyScale(scroll * scaleSpeedMouse);
			}
		}

		/// <summary>
		/// Handles single-finger touch for rotation and two-finger pinch for scaling.
		/// </summary>
		private void HandleTouch()
		{
			// Rotation (Single-finger drag)
			if (Input.touchCount == 1)
			{
				Touch touch = Input.GetTouch(0);

				if (touch.phase == TouchPhase.Began) isDragging = true;
				if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) isDragging = false;

				if (touch.phase == TouchPhase.Moved)
				{
					// Use deltaPosition (movement amount from the previous frame)
					float touchX = touch.deltaPosition.x * rotationSpeedTouch * Time.deltaTime;
					float touchY = touch.deltaPosition.y * rotationSpeedTouch * Time.deltaTime;

					RotateObject(touchX, touchY);
				}
			}
			// Scaling (Two-finger pinch in/out)
			else if (Input.touchCount == 2)
			{
				isDragging = false; 

				Touch touch0 = Input.GetTouch(0);
				Touch touch1 = Input.GetTouch(1);

				// Calculate the positions of the two fingers in the previous frame
				Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
				Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

				// Calculate the difference in distance between the fingers across frames
				float prevTouchDeltaMag = (touch0PrevPos - touch1PrevPos).magnitude;
				float touchDeltaMag = (touch0.position - touch1.position).magnitude;

				float deltaMagnitudeDiff = touchDeltaMag - prevTouchDeltaMag;

				if (Mathf.Abs(deltaMagnitudeDiff) > 0.01f)
				{
					ApplyScale(deltaMagnitudeDiff * scaleSpeedTouch);
				}
			}
		}

		/// <summary>
		/// Rotates the object relative to the main camera's orientation.
		/// </summary>
		private void RotateObject(float deltaX, float deltaY)
		{
			if (Camera.main != null)
			{
				transform.Rotate(Camera.main.transform.up, -deltaX, Space.World);
				transform.Rotate(Camera.main.transform.right, deltaY, Space.World);
			}
		}

		/// <summary>
		/// Safely applies scaling while maintaining the current aspect ratio (non-uniform scale).
		/// </summary>
		private void ApplyScale(float deltaScale)
		{
			float prevRatio = currentScaleRatio;
			
			// Add or subtract the scale multiplier and clamp it within limits
			currentScaleRatio += deltaScale;
			currentScaleRatio = Mathf.Clamp(currentScaleRatio, minScale, maxScale);

			if (prevRatio > 0) 
			{
				// Calculate the multiplier ratio and apply it to the current scale
				// This ensures any custom scale balance set by modules (e.g., ReadGrADSMod) is perfectly preserved
				float multiplier = currentScaleRatio / prevRatio;
				transform.localScale *= multiplier;
			}
		}
	}

	// =========================================================================
	// Editor Extension
	// =========================================================================
#if UNITY_EDITOR
	[CustomEditor(typeof(CtrlOBJ))]
	public class CtrlOBJEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			SerializedProperty iterator = serializedObject.GetIterator();
			bool enterChildren = true;

			while (iterator.NextVisible(enterChildren))
			{
				enterChildren = false;

				// Skip the default m_Script property
				if (iterator.name == "m_Script") continue;

				// Make isActive and isDragging read-only in the Inspector
				if (iterator.name == "isActive" || iterator.name == "isDragging")
				{
					EditorGUI.BeginDisabledGroup(true);
					EditorGUILayout.PropertyField(iterator, true);
					EditorGUI.EndDisabledGroup();
				}
				else
				{
					EditorGUILayout.PropertyField(iterator, true);
				}
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif
}