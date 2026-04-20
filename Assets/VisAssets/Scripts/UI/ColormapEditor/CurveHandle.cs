using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CurveHandle : MonoBehaviour, IDragHandler, IPointerClickHandler
{
	public Action<CurveHandle> OnHandleDragged;
	public Action<CurveHandle> OnDeleteRequested;

	public RectTransform rectTransform;
	private RectTransform parentRect;

	public bool lockX = false;

	private static Sprite ringSprite;

	private void Awake()
	{
		rectTransform = GetComponent<RectTransform>();
		parentRect = transform.parent as RectTransform;

		ApplyRingGraphic();
	}

	private void ApplyRingGraphic()
	{
		Image img = GetComponent<Image>();

		if (img == null) return;

		if (ringSprite == null)
		{
			ringSprite = GenerateRingSprite(8, 2);
		}

		img.sprite = ringSprite;
		img.color  = Color.white;
	}

	private Sprite GenerateRingSprite(int size, int thickness)
	{
		Texture2D tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
		Color clear    = new Color(0, 0, 0, 0);
		float center   = size / 2f;
		float outerRad = center;
		float innerRad = center - thickness;

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));

				if (dist <= outerRad && dist >= innerRad)
				{
					float alpha = 1f;

					if (dist > outerRad - 1f)
					{
						alpha = outerRad - dist;
					}
					else if (dist < innerRad + 1f)
					{
						alpha = dist - innerRad;
					}

					tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
				}
				else
				{
					tex.SetPixel(x, y, clear);
				}
			}
		}

		tex.Apply();

		return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
	}

	public void OnDrag(PointerEventData eventData)
	{
		Vector2 localPoint;

		if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out localPoint))
		{
			float x = Mathf.Clamp(localPoint.x, parentRect.rect.min.x, parentRect.rect.max.x);
			float y = Mathf.Clamp(localPoint.y, parentRect.rect.min.y, parentRect.rect.max.y);

			if (lockX)
			{
				x = rectTransform.localPosition.x;
			}

			rectTransform.localPosition = new Vector3(x, y, rectTransform.localPosition.z);
			OnHandleDragged?.Invoke(this);
		}
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Right)
		{
			OnDeleteRequested?.Invoke(this);
		}
	}

	public Vector2 GetNormalizedPosition()
	{
		float x = Mathf.InverseLerp(parentRect.rect.min.x, parentRect.rect.max.x, rectTransform.localPosition.x);
		float y = Mathf.InverseLerp(parentRect.rect.min.y, parentRect.rect.max.y, rectTransform.localPosition.y);
		return new Vector2(x, y);
	}

	public void SetNormalizedPosition(Vector2 normPos)
	{
		float minX = parentRect.rect.min.x;
		float maxX = parentRect.rect.max.x;
		float minY = parentRect.rect.min.y;
		float maxY = parentRect.rect.max.y;

		float x = Mathf.Lerp(minX, maxX, normPos.x);
		float y = Mathf.Lerp(minY, maxY, normPos.y);
		
		rectTransform.localPosition = new Vector3(x, y, rectTransform.localPosition.z);
	}
}