using System;
using System.Linq;
using UnityEngine;

namespace VisAssets.SciVis.Structured.Common
{
	using FieldType = DataElement.FieldType;

	[Serializable]
	public class SliceParams
	{
		public float slider = 0;
		public float value  = 0;
		public float min    = 0;
		public float max    = 1f;
	}

	// =========================================================================
	// Main Class
	// =========================================================================
	/// <summary>
	/// Helper class responsible for calculating slice plane positions, managing UI state, and computing grid indices.
	/// Modified to preserve inspector pre-set values across Play mode and data reloads.
	/// </summary>
	[Serializable]
	public class SliceHelper
	{
		[SerializeField, Range(0, 2)]
		public int axis;
		[SerializeField, Range(0, 1f)]
		public float slice;

		public float value;
		public float value_min;
		public float value_max;
		public SliceParams[] slices;

		[SerializeField, HideInInspector]
		private int   prev_axis;
		[SerializeField, HideInInspector]
		private float prev_slice;
		[SerializeField, HideInInspector]
		private float prev_value;

		/// <summary>
		/// Safely initializes the slices array.
		/// Prevents IndexOutOfRangeException caused by Unity serializing arrays as length 0 instead of null.
		/// </summary>
		public void EnsureSlices()
		{
			if (slices == null || slices.Length < 3)
			{
				var newSlices = new SliceParams[3];

				for (int i = 0; i < 3; i++)
				{
					newSlices[i] = new SliceParams();
				}

				slices = newSlices;
			}
		}

		/// <summary>
		/// Initializes the slice parameters.
		/// Only applies defaults if values haven't been set in the Inspector.
		/// </summary>
		public void Init()
		{
			EnsureSlices();

			// Sync internal array with inspector 'slice' value if it's the first time
			if (slices[axis].slider == 0 && slice != 0)
			{
				slices[axis].slider = slice;
			}

			prev_axis  = axis;
			prev_slice = slice;
			prev_value = value;
		}

		/// <summary>
		/// Updates physical bounds based on loaded data while preserving the user's slider settings.
		/// </summary>
		public void Reset(DataElement element)
		{
			EnsureSlices();

			for (int i = 0; i < 3; i++)
			{
				// Preserve existing slider values (do NOT set to 0)

				if (element.fieldType == FieldType.RECTILINEAR)
				{
					float first = element.coords[i].First();
					float last  = element.coords[i].Last();
					slices[i].min = Mathf.Min(first, last);
					slices[i].max = Mathf.Max(first, last);
				}
				else if ((element.fieldType == FieldType.UNIFORM) ||
						 (element.fieldType == FieldType.IRREGULAR))
				{
					slices[i].min = 0;
					slices[i].max = element.dims[i] - 1;
				}

				// Re-calculate physical value based on existing slider position and new bounds
				slices[i].value = slices[i].min + (slices[i].max - slices[i].min) * slices[i].slider;
			}

			// Ensure the active axis parameters are correctly synced to the inspector values
			value_min = slices[axis].min;
			value_max = slices[axis].max;

			// Use the current inspector 'slice' to define the initial 'value'
			value = prev_value = value_min + (value_max - value_min) * slice;

			// Ensure the tracker array for the current axis is also synced
			slices[axis].slider = slice;
			slices[axis].value  = value;

			prev_slice = slice;
			prev_axis  = axis;
		}

		/// <summary>
		/// Validates and synchronizes slice parameters, ensuring values remain within valid physical limits.
		/// </summary>
		public void Validate()
		{
			EnsureSlices();

			if (axis != prev_axis)
			{
				// Backup current variables before switching
				slices[prev_axis].slider = slice;
				slices[prev_axis].value  = value;
				slices[prev_axis].min    = value_min;
				slices[prev_axis].max    = value_max;

				// Restore variables for the newly selected axis
				slice     = slices[axis].slider;
				value     = slices[axis].value;
				value_min = slices[axis].min;
				value_max = slices[axis].max;

				prev_slice = slice;
				prev_value = value;
				prev_axis  = axis;
			}
			else
			{
				if (value != prev_value)
				{
					var _value = Mathf.Clamp(value, value_min, value_max);
					slice = (_value - value_min) / (value_max - value_min);
					prev_value = _value;

					// Update internal memory
					slices[axis].slider = slice;
					slices[axis].value  = _value;
				}

				if (slice != prev_slice)
				{
					value = value_min + (value_max - value_min) * slice;
					prev_slice = slice;

					// Update internal memory
					slices[axis].slider = slice;
					slices[axis].value  = value;
				}
			}
		}

		/// <summary>
		/// Sets the target axis (X=0, Y=1, Z=2) for the slice plane. Returns true if the axis was changed.
		/// </summary>
		public bool SetAxis(int _axis)
		{
			EnsureSlices();

			if (_axis != prev_axis)
			{
				axis = _axis;
				Validate();
				return true;
			}

			return false;
		}

		/// <summary>
		/// Sets the normalized slice position along the current axis. Returns true if the position was changed.
		/// </summary>
		public bool SetSlice(float _slice)
		{
			EnsureSlices();

			if (_slice != prev_slice)
			{
				slice = _slice;
				Validate(); // Use Validate to sync physical 'value'
				return true;
			}

			return false;
		}

		/// <summary>
		/// Calculates the exact cell index and interpolation ratio where the slicing plane intersects the volume grid.
		/// Handles non-uniform grid intervals properly for RECTILINEAR fields.
		/// </summary>
		public int GetIndexOfCuttingEdge(DataElement element, out float ratio)
		{
			int idx = 0;
			ratio = 0;

			if (element.fieldType == FieldType.RECTILINEAR)
			{
				float[] coord = element.coords[axis];
				int size = element.dims[axis];
				bool isAscending = coord.First() < coord.Last();

				for (int i = 0; i < size - 1; i++)
				{
					if ((isAscending && coord[i + 1] >= value) || (!isAscending && coord[i + 1] <= value))
					{
						idx = i;
						ratio = (value - coord[i]) / (coord[i + 1] - coord[i]);

						if (ratio < 0)
						{
							ratio += 1f;
						}

						if (value == value_max)
						{
							ratio = isAscending ? 1f : 0f;
						}

						break;
					}
				}
			}
			else if ((element.fieldType == FieldType.UNIFORM) ||
					 (element.fieldType == FieldType.IRREGULAR))
			{
				idx = (int)Mathf.Clamp(value, value_min, value_max - 1f);
				ratio = (value % 1);

				if (value == value_max)
				{
					ratio = 1f;
				}
			}

			return idx;
		}

		/// <summary>
		/// Retrieves the width, height, and depth dimensions of the current slice based on the active axis.
		/// </summary>
		public void GetSliceDimensions(DataElement element, out int slice_w, out int slice_h, out int slice_d)
		{
			if (axis == 0)
			{
				slice_w = element.dims[1];
				slice_h = element.dims[2];
				slice_d = element.dims[0];
			}
			else if (axis == 1)
			{
				slice_w = element.dims[0];
				slice_h = element.dims[2];
				slice_d = element.dims[1];
			}
			else
			{
				slice_w = element.dims[0];
				slice_h = element.dims[1];
				slice_d = element.dims[2];
			}
		}

		/// <summary>
		/// Calculates the 1D array indices (idx0, idx1) for the current 3D grid position.
		/// Used for interpolating values between two adjacent data points.
		/// </summary>
		public void GetIndices(int i, int j, int slice_w, int slice_h, int slice_d, int idx, out int idx0, out int idx1)
		{
			if (axis == 0)
			{
				idx0 = slice_d * (slice_w * j + i) + idx;
				idx1 = idx0 + 1;
			}
			else if (axis == 1)
			{
				idx0 = slice_w * slice_d * j + i + slice_w * idx;
				idx1 = idx0 + slice_w;
			}
			else
			{
				idx0 = slice_w * slice_h * idx + slice_w * j + i;
				idx1 = idx0 + slice_w * slice_h;
			}
		}
	}
}