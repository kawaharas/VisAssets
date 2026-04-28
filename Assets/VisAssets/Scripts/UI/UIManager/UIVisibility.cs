using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace VisAssets
{
	public class UIVisibility : MonoBehaviour
	{
		[SerializeField]
		private GameObject uiPanel;

		[SerializeField]
		private float doubleTapSpeed = 0.2f;

		private float lastClickTime;
		private bool  isVisible = true;

		void Start()
		{
			if (uiPanel == null)
			{
				Debug.LogWarning("UIVisibility: uiPanel is not assigned in the Inspector.");
			}
			else
			{
				uiPanel.SetActive(isVisible);
			}
		}

		void Update()
		{
			if (Input.GetMouseButtonDown(0))
			{
				Vector2 clickPos = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;

				if (IsPointerOverUI(clickPos))
//				if (IsPointerOverUI())
				{
					lastClickTime = 0;

					return;
				}

				float timeSinceLastClick = Time.time - lastClickTime;

				if (timeSinceLastClick <= doubleTapSpeed)
				{
					TogglePanelVisibility();
					lastClickTime = 0;
				}
				else
				{
					lastClickTime = Time.time;
				}
			}
		}

		private void TogglePanelVisibility()
		{
			if (uiPanel == null) return;

			isVisible = !isVisible;
			uiPanel.SetActive(isVisible);
		}
/*
		private bool IsPointerOverUI()
		{
			if (EventSystem.current == null) return false;

			if (Input.touchCount > 0)
			{
				return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
			}

			return EventSystem.current.IsPointerOverGameObject();
		}
*/
		private bool IsPointerOverUI(Vector2 screenPosition)
		{
			if (EventSystem.current == null) return false;

			PointerEventData eventData = new PointerEventData(EventSystem.current);
			eventData.position = screenPosition;
			List<RaycastResult> results = new List<RaycastResult>();
			EventSystem.current.RaycastAll(eventData, results);

			return results.Count > 0;
		}
	}
}