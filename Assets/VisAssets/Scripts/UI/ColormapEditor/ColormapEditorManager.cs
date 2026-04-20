using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VisAssets;

[System.Serializable]
public class ColorMapPreset
{
	public string presetName;
	public Vector2[] redPoints;
	public Vector2[] greenPoints;
	public Vector2[] bluePoints;
	public Vector2[] alphaPoints;
}

public class ColormapEditorManager : MonoBehaviour
{
	[Header("Panel Layout")]
	public RectTransform mainPanel;
	private float lastPanelWidth;

	[Header("Channels")]
	public ChannelEditor redEditor;
	public ChannelEditor greenEditor;
	public ChannelEditor blueEditor;
	public ChannelEditor alphaEditor;

	[Header("UI Controls")]
	public TMP_Dropdown interpolationDropdown;

	[Header("Presets")]
	public Transform presetContainer;
	public GameObject presetButtonPrefab;
	public int presetColumns = 4;
	public float presetSpacing = 5f;

	public bool showAlphaOnPresetButtons = true;

	public List<ColorMapPreset> presets = new List<ColorMapPreset>();

	[Header("Layout Ratios")]
	public float editorAspectRatio = 3.0f;
	public float presetButtonAspectRatio = 5.0f;
	public float dropdownAspectRatio = 8.0f;

	[HideInInspector]
	public Texture2D finalTexture;

	private bool lastShowAlphaState;

	private IColormapReceiver targetModule;
	private bool isInitialized = false;

	IEnumerator Start()
	{
		yield return null;

		finalTexture = new Texture2D(256, 1, TextureFormat.RGBA32, false);

		redEditor.Initialize(this, OnChannelUpdated);
		greenEditor.Initialize(this, OnChannelUpdated);
		blueEditor.Initialize(this, OnChannelUpdated);
		if (alphaEditor != null)
		{
			alphaEditor.Initialize(this, OnChannelUpdated);
		}

		UIPanel uiPanel = GetComponentInParent<UIPanel>();
		if (uiPanel != null && uiPanel.TargetModule != null)
		{
			targetModule = uiPanel.TargetModule.GetComponent<IColormapReceiver>();
		}

		bool loadedFromTarget = false;
		if (targetModule == null)
		{
			Gradient currentGrad = targetModule.GetGradient();
			if (currentGrad != null)
			{
				LoadFromGradient(currentGrad);
				loadedFromTarget = true;
			}
		}

		if (!loadedFromTarget && presets.Count > 0)
		{
			ApplyPreset(presets[0]);
		}

		SyncWithTargetOrDefault();

		EnforceAspectRatios();

		if (mainPanel != null)
		{
			lastPanelWidth = mainPanel.rect.width;
		}

		Canvas.ForceUpdateCanvases();

		SetupDropdown();

		if (presets.Count == 0)
		{
			InitializeDefaultPresets();
		}

		GeneratePresetButtons();

		SyncDropdownItemHeight();

		lastShowAlphaState = showAlphaOnPresetButtons;
/*
		if (presets.Count > 0)
		{
			ApplyPreset(presets[0]);
		}
*/
		isInitialized = true;
	}

	void OnEnable()
	{
		if (isInitialized)
		{
			SyncWithTargetOrDefault();
		}
	}

	void Update()
	{
		if (lastShowAlphaState != showAlphaOnPresetButtons)
		{
			lastShowAlphaState = showAlphaOnPresetButtons;
			RefreshPresetButtonsAppearance();
		}

		if (mainPanel != null)
		{
			float currentWidth = mainPanel.rect.width;

			if (Mathf.Abs(currentWidth - lastPanelWidth) > 0.1f)
			{
				lastPanelWidth = currentWidth;

				Canvas.ForceUpdateCanvases();
				UpdatePresetLayout();

				SyncDropdownItemHeight();

				redEditor.RefreshHandlePositions();
				greenEditor.RefreshHandlePositions();
				blueEditor.RefreshHandlePositions();
				if (alphaEditor != null)
				{
					alphaEditor.RefreshHandlePositions();
				}
			}
		}
	}

	private void SyncDropdownItemHeight()
	{
		if (interpolationDropdown == null || interpolationDropdown.template == null) return;

		float currentHeight = interpolationDropdown.GetComponent<RectTransform>().rect.height;
		int displayCount = interpolationDropdown.options.Count;

		RectTransform templateRect = interpolationDropdown.template;
		templateRect.anchorMin = new Vector2(0, 0);
		templateRect.anchorMax = new Vector2(1, 0);
		templateRect.pivot = new Vector2(0.5f, 1);
		templateRect.anchoredPosition = new Vector2(0, 0);
		templateRect.sizeDelta = new Vector2(templateRect.sizeDelta.x, currentHeight * displayCount);

		Image templateImage = templateRect.GetComponent<Image>();
		if (templateImage != null)
		{
			templateImage.type = Image.Type.Simple;
		}

		Transform itemTransform = interpolationDropdown.template.Find("Viewport/Content/Item");
		if (itemTransform != null)
		{
			RectTransform itemRect = itemTransform.GetComponent<RectTransform>();
			itemRect.pivot = new Vector2(0.5f, 1f);
			itemRect.anchorMin = new Vector2(0f, 1f);
			itemRect.anchorMax = new Vector2(1f, 1f);
			itemRect.anchoredPosition = new Vector2(0f, 0f);
			itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, currentHeight);
		}

		Transform viewportTransform = interpolationDropdown.template.Find("Viewport");
		if (viewportTransform != null)
		{
			RectTransform viewportRect = viewportTransform.GetComponent<RectTransform>();
			viewportRect.offsetMin = new Vector2(viewportRect.offsetMin.x, 0f);
			viewportRect.offsetMax = new Vector2(viewportRect.offsetMax.x, 0f);
		}

		Transform contentTransform = interpolationDropdown.template.Find("Viewport/Content");
		if (contentTransform != null)
		{
			RectTransform contentRect = contentTransform.GetComponent<RectTransform>();
			contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, currentHeight * displayCount);
		}
	}

	public void SetShowAlphaOnPresetButtons(bool show)
	{
		showAlphaOnPresetButtons = show;
	}

	private void RefreshPresetButtonsAppearance()
	{
		if (presetContainer == null) return;

		int index = 0;

		foreach (Transform child in presetContainer)
		{
			if (index >= presets.Count) break;

			RawImage img = child.GetComponentInChildren<RawImage>();

			if (img != null)
			{
				if (img.texture != null)
				{
					Destroy(img.texture);
				}

				img.texture = CreateTextureFromPreset(presets[index]);
			}

			index++;
		}
	}

	private void EnforceAspectRatios()
	{
		ApplyAspectRatio(redEditor   != null ? redEditor.GetComponent<RectTransform>()   : null, editorAspectRatio);
		ApplyAspectRatio(greenEditor != null ? greenEditor.GetComponent<RectTransform>() : null, editorAspectRatio);
		ApplyAspectRatio(blueEditor  != null ? blueEditor.GetComponent<RectTransform>()  : null, editorAspectRatio);
		if (alphaEditor != null)
		{
			ApplyAspectRatio(alphaEditor.GetComponent<RectTransform>(), editorAspectRatio);
		}

		if (interpolationDropdown != null && interpolationDropdown.transform.parent != null)
		{
			ApplyAspectRatio(interpolationDropdown.transform.parent.GetComponent<RectTransform>(), dropdownAspectRatio);
		}
	}

	private void ApplyAspectRatio(RectTransform rt, float ratio)
	{
		if (rt == null) return;

		AspectRatioFitter fitter = rt.GetComponent<AspectRatioFitter>();

		if (fitter == null)
		{
			fitter = rt.gameObject.AddComponent<AspectRatioFitter>();
		}

		fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
		fitter.aspectRatio = ratio;
	}

	private void InitializeDefaultPresets()
	{
		presets.Add(new ColorMapPreset() {
			presetName  = "Rainbow",
			redPoints   = new Vector2[] { new Vector2(0f, 0f), new Vector2(1f, 1f) },
			greenPoints = new Vector2[] { new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(1f, 0f) },
			bluePoints  = new Vector2[] { new Vector2(0f, 1f), new Vector2(1f, 0f) },
			alphaPoints = new Vector2[] { new Vector2(0f, 1f), new Vector2(1f, 1f) }
		});

		presets.Add(new ColorMapPreset() {
			presetName  = "Grayscale",
			redPoints   = new Vector2[] { new Vector2(0f, 0f), new Vector2(1f, 1f) },
			greenPoints = new Vector2[] { new Vector2(0f, 0f), new Vector2(1f, 1f) },
			bluePoints  = new Vector2[] { new Vector2(0f, 0f), new Vector2(1f, 1f) },
			alphaPoints = new Vector2[] { new Vector2(0f, 1f), new Vector2(1f, 1f) }
		});

		presets.Add(new ColorMapPreset() {
			presetName  = "Hot",
			redPoints   = new Vector2[] { new Vector2(0f, 0f), new Vector2(0.3f, 1f), new Vector2(1f, 1f) },
			greenPoints = new Vector2[] { new Vector2(0f, 0f), new Vector2(0.3f, 0f), new Vector2(0.8f, 1f), new Vector2(1f, 1f) },
			bluePoints  = new Vector2[] { new Vector2(0f, 0f), new Vector2(0.8f, 0f), new Vector2(1f, 1f) },
			alphaPoints = new Vector2[] { new Vector2(0f, 1f), new Vector2(1f, 1f) }
		});

		presets.Add(new ColorMapPreset() {
			presetName  = "Fade To Blue",
			redPoints   = new Vector2[] { new Vector2(0f, 0f), new Vector2(1f, 0f) },
			greenPoints = new Vector2[] { new Vector2(0f, 0f), new Vector2(1f, 0f) },
			bluePoints  = new Vector2[] { new Vector2(0f, 1f), new Vector2(1f, 1f) },
			alphaPoints = new Vector2[] { new Vector2(0f, 0f), new Vector2(1f, 1f) }
		});
	}
/*
	private void InitializeDefaultPresets()
	{
		presets.Clear();

		// Preset 1: rainbow1
		presets.Add(new ColorMapPreset() {
			presetName  = "rainbow1",
			redPoints   = new Vector2[] { new Vector2(0.00f, 0.00f), new Vector2(0.33f, 0.00f), new Vector2(0.50f, 0.00f), new Vector2(0.67f, 1.00f), new Vector2(1.00f, 1.00f) },
			greenPoints = new Vector2[] { new Vector2(0.00f, 0.00f), new Vector2(0.33f, 1.00f), new Vector2(0.50f, 1.00f), new Vector2(0.67f, 1.00f), new Vector2(1.00f, 0.00f) },
			bluePoints  = new Vector2[] { new Vector2(0.00f, 1.00f), new Vector2(0.33f, 1.00f), new Vector2(0.50f, 0.00f), new Vector2(0.67f, 0.00f), new Vector2(1.00f, 0.00f) },
			alphaPoints = new Vector2[] { new Vector2(0.00f, 0.10f), new Vector2(0.33f, 0.10f), new Vector2(0.50f, 0.10f), new Vector2(0.67f, 0.10f), new Vector2(1.00f, 0.10f) }
		});

		// Preset 2: (No name provided in cpp, generic)
		presets.Add(new ColorMapPreset() {
			presetName  = "preset2",
			redPoints   = new Vector2[] { new Vector2(0.00f, 0.00f), new Vector2(0.50f, 0.50f), new Vector2(1.00f, 1.00f) },
			greenPoints = new Vector2[] { new Vector2(0.00f, 0.00f), new Vector2(0.50f, 1.00f), new Vector2(1.00f, 0.00f) },
			bluePoints  = new Vector2[] { new Vector2(0.00f, 1.00f), new Vector2(0.50f, 0.50f), new Vector2(1.00f, 0.00f) },
			alphaPoints = new Vector2[] { new Vector2(0.00f, 1.00f), new Vector2(0.50f, 1.00f), new Vector2(1.00f, 1.00f) }
		});

		// Preset 3: rainbow2
		presets.Add(new ColorMapPreset() {
			presetName  = "rainbow2",
			redPoints   = new Vector2[] { new Vector2(0.00f, 1.00f), new Vector2(0.20f, 0.00f), new Vector2(0.40f, 0.00f), new Vector2(0.60f, 0.00f), new Vector2(0.80f, 1.00f), new Vector2(1.00f, 1.00f) },
			greenPoints = new Vector2[] { new Vector2(0.00f, 0.00f), new Vector2(0.20f, 0.00f), new Vector2(0.40f, 1.00f), new Vector2(0.60f, 1.00f), new Vector2(0.80f, 1.00f), new Vector2(1.00f, 0.00f) },
			bluePoints  = new Vector2[] { new Vector2(0.00f, 1.00f), new Vector2(0.20f, 1.00f), new Vector2(0.40f, 1.00f), new Vector2(0.60f, 0.00f), new Vector2(0.80f, 0.00f), new Vector2(1.00f, 0.00f) },
			alphaPoints = new Vector2[] { new Vector2(0.00f, 0.10f), new Vector2(0.20f, 0.10f), new Vector2(0.40f, 0.10f), new Vector2(0.60f, 0.10f), new Vector2(0.80f, 0.10f), new Vector2(1.00f, 0.10f) }
		});

		// Preset 4: rainbow3
		presets.Add(new ColorMapPreset() {
			presetName  = "rainbow3",
			redPoints   = new Vector2[] { new Vector2(0.00f, 0.00f), new Vector2(0.17f, 0.00f), new Vector2(0.33f, 0.00f), new Vector2(0.50f, 0.00f), new Vector2(0.67f, 1.00f), new Vector2(0.83f, 1.00f), new Vector2(1.00f, 1.00f) },
			greenPoints = new Vector2[] { new Vector2(0.00f, 0.00f), new Vector2(0.17f, 0.00f), new Vector2(0.33f, 1.00f), new Vector2(0.50f, 1.00f), new Vector2(0.67f, 1.00f), new Vector2(0.83f, 0.00f), new Vector2(1.00f, 1.00f) },
			bluePoints  = new Vector2[] { new Vector2(0.00f, 0.00f), new Vector2(0.17f, 1.00f), new Vector2(0.33f, 1.00f), new Vector2(0.50f, 0.00f), new Vector2(0.67f, 0.00f), new Vector2(0.83f, 0.00f), new Vector2(1.00f, 1.00f) },
			alphaPoints = new Vector2[] { new Vector2(0.00f, 0.10f), new Vector2(0.17f, 0.10f), new Vector2(0.33f, 0.10f), new Vector2(0.50f, 0.10f), new Vector2(0.67f, 0.10f), new Vector2(0.83f, 0.10f), new Vector2(1.00f, 0.10f) }
		});

		// Preset 5: monochrome
		presets.Add(new ColorMapPreset() {
			presetName  = "monochrome",
			redPoints   = new Vector2[] { new Vector2(0.00f, 0.73f), new Vector2(0.08f, 0.80f), new Vector2(0.15f, 1.00f), new Vector2(1.00f, 1.00f) },
			greenPoints = new Vector2[] { new Vector2(0.00f, 0.73f), new Vector2(0.08f, 0.80f), new Vector2(0.15f, 1.00f), new Vector2(1.00f, 1.00f) },
			bluePoints  = new Vector2[] { new Vector2(0.00f, 0.73f), new Vector2(0.08f, 0.80f), new Vector2(0.15f, 1.00f), new Vector2(1.00f, 1.00f) },
			alphaPoints = new Vector2[] { new Vector2(0.00f, 0.00f), new Vector2(0.08f, 0.31f), new Vector2(0.15f, 0.31f), new Vector2(1.00f, 0.31f) } // Alpha values derived from 0x00 and 0x50
		});

		// Preset 6: blue_white_red
		presets.Add(new ColorMapPreset() {
			presetName  = "blue_white_red",
			redPoints   = new Vector2[] { new Vector2(0.00f, 0.00f), new Vector2(0.50f, 1.00f), new Vector2(1.00f, 1.00f) },
			greenPoints = new Vector2[] { new Vector2(0.00f, 0.00f), new Vector2(0.50f, 1.00f), new Vector2(1.00f, 0.00f) },
			bluePoints  = new Vector2[] { new Vector2(0.00f, 1.00f), new Vector2(0.50f, 1.00f), new Vector2(1.00f, 0.00f) },
			alphaPoints = new Vector2[] { new Vector2(0.00f, 0.10f), new Vector2(0.50f, 0.10f), new Vector2(1.00f, 0.10f) }
		});

		// Preset 7: brown_white_green
		presets.Add(new ColorMapPreset() {
			presetName  = "brown_white_green",
			redPoints   = new Vector2[] { new Vector2(0.00f, 0.60f), new Vector2(0.50f, 1.00f), new Vector2(1.00f, 0.00f) },
			greenPoints = new Vector2[] { new Vector2(0.00f, 0.30f), new Vector2(0.50f, 1.00f), new Vector2(1.00f, 1.00f) },
			bluePoints  = new Vector2[] { new Vector2(0.00f, 0.00f), new Vector2(0.50f, 1.00f), new Vector2(1.00f, 0.00f) },
			alphaPoints = new Vector2[] { new Vector2(0.00f, 0.10f), new Vector2(0.50f, 0.10f), new Vector2(1.00f, 0.10f) }
		});

		// Preset 8: purple_green_orange
		presets.Add(new ColorMapPreset() {
			presetName  = "purple_green_orange",
			redPoints   = new Vector2[] { new Vector2(0.00f, 0.50f), new Vector2(0.50f, 0.00f), new Vector2(1.00f, 1.00f) },
			greenPoints = new Vector2[] { new Vector2(0.00f, 0.05f), new Vector2(0.50f, 0.40f), new Vector2(1.00f, 0.50f) },
			bluePoints  = new Vector2[] { new Vector2(0.00f, 0.50f), new Vector2(0.50f, 0.15f), new Vector2(1.00f, 0.00f) },
			alphaPoints = new Vector2[] { new Vector2(0.00f, 0.10f), new Vector2(0.50f, 0.10f), new Vector2(1.00f, 0.10f) }
		});

		// Preset 9: blue_red_yellow (Note: typo in original cpp preserved in name)
		presets.Add(new ColorMapPreset() {
			presetName  = "blue_red_yeallow",
			redPoints   = new Vector2[] { new Vector2(0.00f, 0.05f), new Vector2(0.50f, 1.00f), new Vector2(1.00f, 1.00f) },
			greenPoints = new Vector2[] { new Vector2(0.00f, 0.00f), new Vector2(0.50f, 0.00f), new Vector2(1.00f, 1.00f) },
			bluePoints  = new Vector2[] { new Vector2(0.00f, 0.70f), new Vector2(0.50f, 1.00f), new Vector2(1.00f, 0.00f) },
			alphaPoints = new Vector2[] { new Vector2(0.00f, 0.10f), new Vector2(0.50f, 0.10f), new Vector2(1.00f, 0.10f) }
		});

		// Preset 10: yellow_purple_orange_blue
		presets.Add(new ColorMapPreset() {
			presetName  = "yellow_purple_orange_blue",
			redPoints   = new Vector2[] { new Vector2(0.00f, 0.95f), new Vector2(0.33f, 0.45f), new Vector2(0.66f, 1.00f), new Vector2(1.00f, 0.00f) },
			greenPoints = new Vector2[] { new Vector2(0.00f, 0.90f), new Vector2(0.33f, 0.10f), new Vector2(0.66f, 0.50f), new Vector2(1.00f, 0.15f) },
			bluePoints  = new Vector2[] { new Vector2(0.00f, 0.00f), new Vector2(0.33f, 0.45f), new Vector2(0.66f, 0.00f), new Vector2(1.00f, 0.50f) },
			alphaPoints = new Vector2[] { new Vector2(0.00f, 0.10f), new Vector2(0.33f, 0.10f), new Vector2(0.66f, 0.10f), new Vector2(1.00f, 0.10f) }
		});

		// Preset 11: chrome
		presets.Add(new ColorMapPreset() {
			presetName  = "chrome",
			redPoints   = new Vector2[] { new Vector2(0.00f, 0.15f), new Vector2(0.50f, 1.00f), new Vector2(0.52f, 0.55f), new Vector2(0.64f, 0.85f), new Vector2(1.00f, 1.00f) },
			greenPoints = new Vector2[] { new Vector2(0.00f, 0.55f), new Vector2(0.50f, 1.00f), new Vector2(0.52f, 0.45f), new Vector2(0.64f, 0.60f), new Vector2(1.00f, 1.00f) },
			bluePoints  = new Vector2[] { new Vector2(0.00f, 0.80f), new Vector2(0.50f, 1.00f), new Vector2(0.52f, 0.00f), new Vector2(0.64f, 0.00f), new Vector2(1.00f, 1.00f) },
			alphaPoints = new Vector2[] { new Vector2(0.00f, 0.10f), new Vector2(0.50f, 0.10f), new Vector2(0.52f, 0.10f), new Vector2(0.64f, 0.10f), new Vector2(1.00f, 0.10f) }
		});

		// Preset 12: blue_red
		presets.Add(new ColorMapPreset() {
			presetName  = "blue_red",
			redPoints   = new Vector2[] { new Vector2(0.00f, 0.00f), new Vector2(0.50f, 0.00f), new Vector2(0.52f, 1.00f), new Vector2(1.00f, 1.00f) },
			greenPoints = new Vector2[] { new Vector2(0.00f, 0.00f), new Vector2(0.50f, 0.00f), new Vector2(0.52f, 0.00f), new Vector2(1.00f, 0.00f) },
			bluePoints  = new Vector2[] { new Vector2(0.00f, 1.00f), new Vector2(0.50f, 1.00f), new Vector2(0.52f, 0.00f), new Vector2(1.00f, 0.00f) },
			alphaPoints = new Vector2[] { new Vector2(0.00f, 0.10f), new Vector2(0.50f, 0.10f), new Vector2(0.52f, 0.10f), new Vector2(1.00f, 0.10f) }
		});
	}
*/
	private void GeneratePresetButtons()
	{
		if (presetContainer == null || presetButtonPrefab == null) return;

		foreach (Transform child in presetContainer)
		{
			Destroy(child.gameObject);
		}

		foreach (var preset in presets)
		{
			ColorMapPreset p = preset;

			GameObject btnObj = Instantiate(presetButtonPrefab, presetContainer);

			RawImage img = btnObj.GetComponentInChildren<RawImage>();

			if (img != null)
			{
				img.texture = CreateTextureFromPreset(p);
			}

			Button btn = btnObj.GetComponent<Button>();

			if (btn != null)
			{
				btn.onClick.AddListener(() => ApplyPreset(p));
			}
		}

		UpdatePresetLayout();
	}

	private void UpdatePresetLayout()
	{
		if (presetContainer == null) return;

		GridLayoutGroup grid = presetContainer.GetComponent<GridLayoutGroup>();
		if (grid != null)
		{
			RectTransform containerRect = presetContainer.GetComponent<RectTransform>();
			float totalWidth = containerRect.rect.width;

			if (totalWidth <= 0.1f) return;

			float paddingX = grid.padding.left + grid.padding.right;
			float totalSpacing = presetSpacing * (presetColumns - 1);

			float cellWidth = (totalWidth - paddingX - totalSpacing) / presetColumns;
			float cellHeight = cellWidth / presetButtonAspectRatio;

			grid.cellSize = new Vector2(cellWidth, cellHeight);
			grid.spacing  = new Vector2(presetSpacing, presetSpacing);

			int rows = Mathf.CeilToInt((float)presets.Count / presetColumns);

			float totalHeight = grid.padding.top + grid.padding.bottom +
								(cellHeight * rows) +
								(presetSpacing * Mathf.Max(0, rows - 1));

			LayoutElement layoutElement = presetContainer.GetComponent<LayoutElement>();

			if (layoutElement == null)
			{
				layoutElement = presetContainer.gameObject.AddComponent<LayoutElement>();
			}

			layoutElement.minHeight = totalHeight;
			layoutElement.preferredHeight = totalHeight;
		}
	}

	private Texture2D CreateTextureFromPreset(ColorMapPreset preset)
	{
		int width = 128;
		int height = 16;
		Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
		tex.filterMode = FilterMode.Point;

		AnimationCurve rC = CreateCurve(preset.redPoints);
		AnimationCurve gC = CreateCurve(preset.greenPoints);
		AnimationCurve bC = CreateCurve(preset.bluePoints);
		AnimationCurve aC = CreateCurve(preset.alphaPoints);

		float uiWidth = 100f;
		float uiHeight = uiWidth / presetButtonAspectRatio;
		float tileSize = 6f;

		for (int x = 0; x < width; x++)
		{
			float t = x / (float)(width - 1);
			float r = rC != null ? rC.Evaluate(t) : 0f;
			float g = gC != null ? gC.Evaluate(t) : 0f;
			float b = bC != null ? bC.Evaluate(t) : 0f;

			float a = (showAlphaOnPresetButtons && aC != null) ? aC.Evaluate(t) : 1f;

			Color rgbCol = new Color(r, g, b, 1f);

			for (int y = 0; y < height; y++)
			{
				if (showAlphaOnPresetButtons)
				{
					float uiX = (x / (float)width)  * uiWidth;
					float uiY = (y / (float)height) * uiHeight;

					bool isC1 = (Mathf.FloorToInt(uiX / tileSize) % 2 == 0) ^ (Mathf.FloorToInt(uiY / tileSize) % 2 == 0);
					Color checkerCol = isC1 ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.4f, 0.4f, 0.4f);

					Color finalCol = rgbCol * a + checkerCol * (1f - a);
					finalCol.a = 1f;

					tex.SetPixel(x, y, finalCol);
				}
				else
				{
					tex.SetPixel(x, y, rgbCol);
				}
			}
		}

		tex.Apply();
		return tex;
	}

	private AnimationCurve CreateCurve(Vector2[] points)
	{
		if (points == null || points.Length == 0) return null;

		Keyframe[] keys = new Keyframe[points.Length];

		for (int i = 0; i < points.Length; i++)
		{
			keys[i] = new Keyframe(points[i].x, points[i].y);
		}

		System.Array.Sort(keys, (a, b) => a.time.CompareTo(b.time));
		AnimationCurve curve = new AnimationCurve(keys);

		for (int i = 0; i < curve.length; i++)
		{
			curve.SmoothTangents(i, 0);
		}

		return curve;
	}

	public void ApplyPreset(ColorMapPreset preset)
	{
		if (preset.redPoints != null)
		{
			redEditor.LoadHandles(preset.redPoints);
		}

		if (preset.greenPoints != null)
		{
			greenEditor.LoadHandles(preset.greenPoints);
		}

		if (preset.bluePoints != null)
		{
			blueEditor.LoadHandles(preset.bluePoints);
		}

		if (alphaEditor != null && preset.alphaPoints != null)
		{
			alphaEditor.LoadHandles(preset.alphaPoints);
		}

		Canvas.ForceUpdateCanvases();

		redEditor.RefreshHandlePositions();
		greenEditor.RefreshHandlePositions();
		blueEditor.RefreshHandlePositions();
		if (alphaEditor != null)
		{
			alphaEditor.RefreshHandlePositions();
		}

		OnChannelUpdated(null);
	}

	public Color GetCombinedRGB(float t)
	{
		if (redEditor == null || greenEditor == null || blueEditor == null)
		{
			return Color.white;
		}

		float r = redEditor.curve.Evaluate(t);
		float g = greenEditor.curve.Evaluate(t);
		float b = blueEditor.curve.Evaluate(t);

		return new Color(r, g, b, 1.0f);
	}

	private void SetupDropdown()
	{
		if (interpolationDropdown == null) return;

		interpolationDropdown.ClearOptions();

		List<string> options = new List<string> { "FLAT", "SMOOTH", "LINEAR", "STEP" };

		interpolationDropdown.AddOptions(options);
//		interpolationDropdown.value = (int)redEditor.interpolationMode;
		interpolationDropdown.value = 0;
		interpolationDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
	}

	private void OnDropdownValueChanged(int index)
	{
		InterpolationMode selectedMode = (InterpolationMode)index;

		redEditor.SetInterpolationMode(selectedMode);
		greenEditor.SetInterpolationMode(selectedMode);
		blueEditor.SetInterpolationMode(selectedMode);
		if (alphaEditor != null)
		{
			alphaEditor.SetInterpolationMode(selectedMode);
		}
	}

	void OnChannelUpdated(ChannelEditor source)
	{
		if (redEditor.curve == null || greenEditor.curve == null || blueEditor.curve == null) return;

		int width = 256;
		Color[] pixels = new Color[width];

		for (int i = 0; i < width; i++)
		{
			float t = i / (float)(width - 1);
			Color rgb = GetCombinedRGB(t);
			float a = alphaEditor != null ? alphaEditor.curve.Evaluate(t) : 1.0f;
			pixels[i] = new Color(rgb.r, rgb.g, rgb.b, a);
		}
		finalTexture.SetPixels(pixels);
		finalTexture.Apply();

		if (targetModule != null)
		{
			targetModule.ApplyColormap(finalTexture);
		}

		if (alphaEditor != null && source != alphaEditor)
		{
			alphaEditor.RefreshVisuals();
		}
	}

	public void LoadFromGradient(Gradient grad)
	{
		Vector2[] rPts = new Vector2[grad.colorKeys.Length];
		Vector2[] gPts = new Vector2[grad.colorKeys.Length];
		Vector2[] bPts = new Vector2[grad.colorKeys.Length];
		Vector2[] aPts = new Vector2[grad.alphaKeys.Length];

		for (int i = 0; i < grad.colorKeys.Length; i++)
		{
			float t = grad.colorKeys[i].time;
			Color c = grad.colorKeys[i].color;
			rPts[i] = new Vector2(t, c.r);
			gPts[i] = new Vector2(t, c.g);
			bPts[i] = new Vector2(t, c.b);
		}

		for (int i = 0; i < grad.alphaKeys.Length; i++)
		{
			aPts[i] = new Vector2(grad.alphaKeys[i].time, grad.alphaKeys[i].alpha);
		}

		redEditor.LoadHandles(rPts);
		greenEditor.LoadHandles(gPts);
		blueEditor.LoadHandles(bPts);
		if (alphaEditor != null)
		{
			alphaEditor.LoadHandles(aPts);
		}

		// 表示の更新
		Canvas.ForceUpdateCanvases();
		redEditor.RefreshHandlePositions();
		greenEditor.RefreshHandlePositions();
		blueEditor.RefreshHandlePositions();

		if (alphaEditor != null)
		{
			alphaEditor.RefreshHandlePositions();
		}

		OnChannelUpdated(null);
	}

	private void SyncWithTargetOrDefault()
	{
		bool loadedFromTarget = false;

		if (targetModule == null)
		{
			UIPanel uiPanel = GetComponentInParent<UIPanel>();

			if (uiPanel != null && uiPanel.TargetModule != null)
			{
				targetModule = uiPanel.TargetModule.GetComponent<IColormapReceiver>();
			}
		}

		if (targetModule != null)
		{
			Gradient currentGrad = targetModule.GetGradient();

			if (currentGrad != null)
			{
				LoadFromGradient(currentGrad);
				loadedFromTarget = true;
			}
		}

		if (!loadedFromTarget && presets.Count > 0)
		{
			ApplyPreset(presets[0]);
		}
	}
}