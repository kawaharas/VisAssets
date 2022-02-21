using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VisAssets
{
	public class Pointer : MonoBehaviour
	{
		public GameObject pointer;

		public void ShowPointer(Vector3 position)
		{
			if (pointer != null)
			{
				var localPosition = pointer.transform.localPosition;
				localPosition.x = position.x;
				localPosition.y = position.y;
				localPosition.z = position.z;
				pointer.transform.localPosition = localPosition;
				Debug.Log("pointer position = " + position);
				pointer.SetActive(true);
			}
		}

		public void HidePointer()
		{
			if (pointer != null)
			{
				pointer.SetActive(false);
			}
		}
	}
}
