using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace VisAssets
{
	public class DragUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
	{
		void Update()
		{
			if (Input.touchCount > 0)
			{
				Touch t1 = Input.GetTouch(0);
				var isPointerOverUIObject = IsPointerOverUIObject();
				if (t1.phase == TouchPhase.Began)
				{
					if (isPointerOverUIObject == true)
					{
						EnterStatus(!isPointerOverUIObject);
					}
				}
				else if (t1.phase == TouchPhase.Ended)
				{
					EnterStatus(!isPointerOverUIObject);
				}
			}
		}

		public void OnBeginDrag(PointerEventData eventData)
		{
		}

		public void OnDrag(PointerEventData eventData)
		{
//			if (eventData.button != PointerEventData.InputButton.Left) return;
//			this.GetComponent<RectTransform>().anchoredPosition += eventData.delta;
		}

		public void OnEndDrag(PointerEventData eventData)
		{
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			EnterStatus(false);
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			EnterStatus(true);
		}

		public void EnterStatus(bool value)
		{
			GameObject[] gos = GameObject.FindGameObjectsWithTag("VisModule");
			for (int i = 0; i < gos.Length; i++)
			{
				if (gos[i].name.StartsWith("Read"))
				{
					gos[i].GetComponent<CtrlOBJ>().active = value;
				}
			}
		}

		private bool IsPointerOverUIObject()
		{
			PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
			eventDataCurrentPosition.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
			List<RaycastResult> results = new List<RaycastResult>();
			EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

			for (int i = 0; i < results.Count; i++)
			{
				if (results[i].gameObject.name == "MainPanel")
				{
					return true;
				}
			}

			return false;
		}
	}
}