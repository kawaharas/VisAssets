using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VisAssets
{
public class ResizeCollider : MonoBehaviour
{
	BoxCollider   boxCollider;
	RectTransform rectTransform;
 
	void Awake()
	{
		boxCollider   = GetComponent<BoxCollider>();
		rectTransform = GetComponent<RectTransform>();
	}

	void Update()
	{
		if (rectTransform != null)
		{
			float height = rectTransform.rect.height;
			float width  = rectTransform.rect.width;
			var size = boxCollider.size;
			size.x = width;
			size.y = height;
			boxCollider.size = size;
		}
	}
}
}