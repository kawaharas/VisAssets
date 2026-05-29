using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public enum InterpolationMode
{
	FLAT,
	SMOOTH,
	LINEAR,
	STEP
}

public class ChannelEditor : MonoBehaviour
{
	public Color channelColor  = Color.red;
	public bool isAlphaChannel = false;

	public GameObject   handlePrefab;
	public Transform    handleContainer;
	public RawImage     backgroundGradient;
	public LineRenderer lineRenderer;

	private Vector3 worldPos;

	public InterpolationMode interpolationMode = InterpolationMode.FLAT;

	[HideInInspector]
	public AnimationCurve curve = new AnimationCurve();

	private List<CurveHandle> handles = new List<CurveHandle>();
	private Texture2D bgTexture;

	private Action<ChannelEditor> onCurveChanged;
	private ColormapEditorManager manager;

	public void Initialize(ColormapEditorManager mgr, Action<ChannelEditor> callback)
	{
		interpolationMode = InterpolationMode.FLAT;

		this.manager   = mgr;
		onCurveChanged = callback;
		SetupBackgroundClickEvent();
	}

	public void LoadHandles(Vector2[] points)
	{
		foreach(Transform child in handleContainer)
		{
			Destroy(child.gameObject);
		}
		handles.Clear();

		foreach (Vector2 p in points)
		{
			bool isLocked = (p.x <= 0.001f || p.x >= 0.999f);
			CreateHandle(p, isLocked);
		}

		UpdateCurveData();
	}

	public void RefreshVisuals()
	{
		UpdateVisuals();
	}

	public void SetInterpolationMode(InterpolationMode mode)
	{
		interpolationMode = mode;

		UpdateCurveData();
	}

	public void RefreshHandlePositions()
	{
		if (curve != null && curve.length == handles.Count)
		{
			for (int i = 0; i < handles.Count; i++)
			{
				Vector2 trueNormPos = new Vector2(curve.keys[i].time, curve.keys[i].value);
				handles[i].SetNormalizedPosition(trueNormPos);
			}
		}

		UpdateVisuals();
	}

	private void SetupBackgroundClickEvent()
	{
		EventTrigger trigger = backgroundGradient.gameObject.GetComponent<EventTrigger>();

		if (trigger == null)
		{
			trigger = backgroundGradient.gameObject.AddComponent<EventTrigger>();
		}

		trigger.triggers.Clear();
		EventTrigger.Entry entry = new EventTrigger.Entry();
		entry.eventID = EventTriggerType.PointerClick;
		entry.callback.AddListener((data) => { OnBackgroundClick((PointerEventData)data); });
		trigger.triggers.Add(entry);
	}

	private void OnBackgroundClick(PointerEventData data)
	{
		if (data.button != PointerEventData.InputButton.Left) return;

		RectTransform containerRect = handleContainer.GetComponent<RectTransform>();
		Vector2 localPoint;

		if (RectTransformUtility.ScreenPointToLocalPointInRectangle(containerRect, data.position, data.pressEventCamera, out localPoint))
		{
			float x = Mathf.Clamp01(Mathf.InverseLerp(containerRect.rect.min.x, containerRect.rect.max.x, localPoint.x));
			float y = Mathf.Clamp01(Mathf.InverseLerp(containerRect.rect.min.y, containerRect.rect.max.y, localPoint.y));

			CreateHandle(new Vector2(x, y));
			UpdateCurveData();
		}
	}

	private void CreateHandle(Vector2 normPos, bool isXLocked = false)
	{
		GameObject  obj    = Instantiate(handlePrefab, handleContainer);
		CurveHandle handle = obj.GetComponent<CurveHandle>();
		RectTransform rt   = handle.GetComponent<RectTransform>();

		if (rt != null)
		{
			rt.anchorMin = new Vector2(0.5f, 0.5f);
			rt.anchorMax = new Vector2(0.5f, 0.5f);
			rt.pivot     = new Vector2(0.5f, 0.5f);
			rt.sizeDelta = new Vector2(12f, 12f);
		}

		handle.lockX = isXLocked;
		handles.Add(handle);
		handle.SetNormalizedPosition(normPos);
		handle.OnHandleDragged += OnHandleDraggedCallback;
		handle.OnDeleteRequested += RemoveHandle;
	}

	private void OnHandleDraggedCallback(CurveHandle draggedHandle)
	{
		Vector2 pos = draggedHandle.GetNormalizedPosition();

		if (pos.x <= 0.001f && !draggedHandle.lockX)
		{
			CurveHandle oldLeft = handles.Find(h => h != draggedHandle && h.lockX && h.GetNormalizedPosition().x <= 0.001f);

			if (oldLeft != null)
			{
				handles.Remove(oldLeft);
				Destroy(oldLeft.gameObject);
			}

			draggedHandle.lockX = true; draggedHandle.SetNormalizedPosition(new Vector2(0f, pos.y));
		}
		else if (pos.x >= 0.999f && !draggedHandle.lockX)
		{
			CurveHandle oldRight = handles.Find(h => h != draggedHandle && h.lockX && h.GetNormalizedPosition().x >= 0.999f);

			if (oldRight != null)
			{
				handles.Remove(oldRight);
				Destroy(oldRight.gameObject);
			}

			draggedHandle.lockX = true; draggedHandle.SetNormalizedPosition(new Vector2(1f, pos.y));
		}

		UpdateCurveData();
	}

	private void RemoveHandle(CurveHandle handle)
	{
		if (handle.lockX) return;

		Vector2 pos = handle.GetNormalizedPosition();

		if (pos.x <= 0.01f || pos.x >= 0.99f) return;

		handles.Remove(handle);
		Destroy(handle.gameObject);
		UpdateCurveData();
	}

	private void UpdateCurveData()
	{
		handles.RemoveAll(h => h == null);

		Keyframe[] keys = new Keyframe[handles.Count];

		for (int i = 0; i < handles.Count; i++)
		{
			keys[i] = new Keyframe(handles[i].GetNormalizedPosition().x, handles[i].GetNormalizedPosition().y);
		}

		Array.Sort(keys, (a, b) => a.time.CompareTo(b.time));
		curve = new AnimationCurve(keys);

		for (int i = 0; i < curve.length; i++)
		{
			if (interpolationMode == InterpolationMode.SMOOTH)
			{
				curve.SmoothTangents(i, 0);
			}
			else if (interpolationMode == InterpolationMode.LINEAR)
			{
				Keyframe kf = curve[i];

				if (i > 0)
				{
					kf.inTangent = (kf.value - curve[i-1].value) / (kf.time - curve[i-1].time);
				}

				if (i < curve.length - 1)
				{
					kf.outTangent = (curve[i+1].value - kf.value) / (curve[i+1].time - kf.time);
				}

				curve.MoveKey(i, kf);
			}
			else if (interpolationMode == InterpolationMode.STEP)
			{
				Keyframe kf   = curve[i];
				kf.inTangent  = float.PositiveInfinity;
				kf.outTangent = float.PositiveInfinity;
				curve.MoveKey(i, kf);
			}
			else if (interpolationMode == InterpolationMode.FLAT)
			{
				Keyframe kf   = curve[i];
				kf.inTangent  = 0f;
				kf.outTangent = 0f;
				curve.MoveKey(i, kf);
			}
		}

		UpdateVisuals();
		onCurveChanged?.Invoke(this);
	}

	private void UpdateVisuals()
	{
		int width = 256; int height = 128;

		if (bgTexture == null)
		{
			bgTexture = new Texture2D(width, height);
		}

		Color[] pixels = new Color[width * height];

		RectTransform bgRect = backgroundGradient.rectTransform;
		float uiWidth  = bgRect.rect.width  > 0 ? bgRect.rect.width  : 300f;
		float uiHeight = bgRect.rect.height > 0 ? bgRect.rect.height : 100f;
		float tileSize = 16f;

		for (int x = 0; x < width; x++)
		{
			float t = x / (float)(width - 1);
			float value = curve.Evaluate(t);

			for (int y = 0; y < height; y++)
			{
				Color col;

				if (isAlphaChannel)
				{
					float uiX = (x / (float)width) * uiWidth;
					float uiY = (y / (float)height) * uiHeight;
					bool isC1 = (Mathf.FloorToInt(uiX / tileSize) % 2 == 0) ^ (Mathf.FloorToInt(uiY / tileSize) % 2 == 0);
					Color checkerCol = isC1 ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.4f, 0.4f, 0.4f);

					Color rgbCol = manager != null ? manager.GetCombinedRGB(t) : Color.white;
					col = rgbCol * value + checkerCol * (1f - value);
					col.a = 1.0f;
				}
				else
				{
					col = Color.Lerp(Color.black, channelColor, value);
					col.a = 1.0f;
				}

				pixels[y * width + x] = col;
			}
		}

		bgTexture.SetPixels(pixels); bgTexture.Apply();
		backgroundGradient.texture = bgTexture;

		if (lineRenderer != null)
		{
			Canvas canvas = GetComponentInParent<Canvas>();

			if (canvas != null)
			{
				lineRenderer.sortingLayerID = canvas.sortingLayerID;
				lineRenderer.sortingOrder   = canvas.sortingOrder + 10;
			}

			lineRenderer.useWorldSpace = false;
			lineRenderer.startWidth    = 0.0005f;
			lineRenderer.endWidth      = 0.0005f;


			int resolution = 50;
			lineRenderer.positionCount = resolution;
			Rect rect = handleContainer.GetComponent<RectTransform>().rect;

			if (rect.width > 0)
			{
				for (int i = 0; i < resolution; i++)
				{
					float t = i / (float)(resolution - 1);
					float val = curve.Evaluate(t);

					// 1. Calculate local coordinates within the handleContainer.
					Vector3 containerLocalPos = new Vector3(Mathf.Lerp(rect.min.x, rect.max.x, t), Mathf.Lerp(rect.min.y, rect.max.y, val), 0);

					// 2. Convert to world space first, then to local space relative to the LineRenderer.
					worldPos = handleContainer.TransformPoint(containerLocalPos);
					Vector3 lineLocalPos = lineRenderer.transform.InverseTransformPoint(worldPos);

					// 3. Flatten the Z-axis to 0 to prevent depth deviations when the UI is rotated or scaled.
					lineLocalPos.z = 0f;

					lineRenderer.SetPosition(i, lineLocalPos);
				}
			}
		}
	}
}