using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace VIS
{
	public class DragUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
	{
		void Start()
		{
		}

		void Update()
		{
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
	}
}