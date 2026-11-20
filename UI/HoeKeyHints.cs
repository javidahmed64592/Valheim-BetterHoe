using System.Collections.Generic;

using BetterHoe.Terrain;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace BetterHoe.UI;

/// <summary>
/// Rewrites the vanilla build key hints while the hoe is out. Hints for things the hoe can't do (removing,
/// copying, snapping, and rotating terrain tools) are hidden, the rest say what they do with the hoe ("Level
/// area" instead of "Place", "Hoe menu" instead of "Build Menu"), and the area controls are added in a stack
/// above the vanilla row on the right, built from clones of the vanilla hint parts like BetterHammer's.
/// Everything is put back when the hoe is put away.
/// </summary>
internal static class HoeKeyHints
{
    private const string ContainerName = "BetterHoe_KeyHints";
    private const string ScrollIcon = "@scroll";

    private const float LabelFontSize = 16f;
    private const float IconHeight = 32f;
    private const float IconScale = 1.8f;
    private const float RowSpacing = 16f;

    /// <summary>
    /// How far the stack sits above the vanilla row, so its scroll icons (drawn bigger than their rows) don't
    /// touch the vanilla hints.
    /// </summary>
    private const float StackLift = 12f;

    private enum Show { Area = 0, Step = 1, GridToggle = 2 }

    private sealed class Hint
    {
        public string Label;
        public string[] Keys;
        public Show Show;
        public CanvasGroup Group;
    }

    private static readonly List<Hint> Hints =
    [
        new Hint { Label = "$betterhoe_hint_width", Keys = ["Left Ctrl", ScrollIcon], Show = Show.Area },
        new Hint { Label = "$betterhoe_hint_length", Keys = ["Left Shift", ScrollIcon], Show = Show.Area },
        new Hint { Label = "$betterhoe_hint_step", Keys = ["Left Alt", ScrollIcon], Show = Show.Step },
        new Hint { Label = "$betterhoe_hint_grid", Keys = ["Left Ctrl", "$button_mouse1"], Show = Show.GridToggle },
    ];

    /// <summary>
    /// A vanilla hint this class changes: its object, its label and the label's vanilla text.
    /// </summary>
    private sealed class VanillaHint
    {
        public GameObject Item;
        public TextMeshProUGUI Label;
        public string OriginalText;
        public bool Changed;
    }

    private static readonly Dictionary<string, VanillaHint> Vanilla = [];
    private static readonly string[] VanillaNames = ["Place", "Remove", "BuildMenu", "AltPlace", "Snap", "Copy", "rotate"];

    private static GameObject _container;
    private static RectTransform _stack;
    private static RectTransform _keyboard;
    private static bool _createFailed;
    private static bool _layoutDirty;

    private static void Update(KeyHints keyHints, Player player)
    {
        Piece piece = AreaTool.SelectedPiece(player);
        bool hoe = piece != null
            && keyHints.m_buildHints != null
            && keyHints.m_buildHints.activeInHierarchy
            && !ZInput.IsGamepadActive();

        if (!hoe)
        {
            RestoreVanilla();
            SetContainerActive(false);
            return;
        }

        if (_container == null && (_createFailed || !Create(keyHints)))
        {
            return;
        }

        HoeTool terrainTool = AreaTool.SelectedTerrainTool(player);
        HoeTool areaTool = AreaTool.ActiveTool(player);
        UpdateVanilla(piece, terrainTool, areaTool);

        bool menuOpen = Hud.IsPieceSelectionVisible();
        foreach (Hint hint in Hints)
        {
            bool shown = !menuOpen && hint.Show switch
            {
                Show.Area => areaTool != null,
                Show.Step => areaTool != null && areaTool.Shape is ShapeMode.Raise or ShapeMode.Lower,
                _ => AreaTool.GridToggleApplies(player),
            };

            // A hidden hint keeps its space, so the others don't move.
            hint.Group.alpha = shown ? 1f : 0f;
        }

        SetContainerActive(true);
        if (_layoutDirty)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_stack);
            _layoutDirty = false;
        }

        Align();
    }

    /// <summary>
    /// Shows, hides and relabels the vanilla hints for the selected hoe piece.
    /// </summary>
    private static void UpdateVanilla(Piece piece, HoeTool terrainTool, HoeTool areaTool)
    {
        // Place: the area action, the tool's own name with the vanilla brush, or "Place" for the stones.
        if (areaTool != null)
        {
            SetVanilla("Place", true, areaTool.ActionToken);
        }
        else if (terrainTool != null)
        {
            SetVanilla("Place", true, piece.m_name);
        }
        else
        {
            SetVanilla("Place", true, null);
        }

        // The hoe can't remove or copy pieces, so middle-click only locks the height for Level ground.
        if (areaTool?.Shape == ShapeMode.Flatten)
        {
            SetVanilla("Remove", true, AreaTool.HeightLocked ? "$betterhoe_hint_unlock" : "$betterhoe_hint_lock");
        }
        else
        {
            SetVanilla("Remove", false, null);
        }

        SetVanilla("BuildMenu", true, "$betterhoe_hint_menu");

        // Shift places vanilla Level ground and Paved road at the aimed height instead of the player's feet.
        // The area always uses the aimed height, and Shift+Scroll sets its length.
        SetVanilla("AltPlace", areaTool == null && terrainTool != null && piece.m_allowAltGroundPlacement, "$betterhoe_hint_altplace");

        // Hoe pieces have no snap points, and terrain tools don't rotate.
        SetVanilla("Snap", false, null);
        SetVanilla("Copy", false, null);
        SetVanilla("rotate", terrainTool == null, null);
    }

    /// <summary>
    /// Sets a vanilla hint's visibility and, if `label` is given, its text. Remembers the vanilla text first.
    /// </summary>
    private static void SetVanilla(string name, bool active, string label)
    {
        if (!Vanilla.TryGetValue(name, out VanillaHint hint) || hint.Item == null)
        {
            return;
        }

        if (hint.Item.activeSelf != active)
        {
            hint.Item.SetActive(active);
            hint.Changed = true;
        }

        if (hint.Label == null)
        {
            return;
        }

        string text = label != null ? Localization.instance.Localize(label) : hint.OriginalText;
        if (hint.Label.text != text)
        {
            hint.Label.text = text;
            hint.Changed = true;
        }
    }

    private static void RestoreVanilla()
    {
        foreach (VanillaHint hint in Vanilla.Values)
        {
            if (!hint.Changed || hint.Item == null)
            {
                continue;
            }

            hint.Item.SetActive(true);
            if (hint.Label != null)
            {
                hint.Label.text = hint.OriginalText;
            }

            hint.Changed = false;
        }
    }

    private static void SetContainerActive(bool active)
    {
        if (_container != null && _container.activeSelf != active)
        {
            _container.SetActive(active);
            _layoutDirty |= active;
        }
    }

    /// <summary>
    /// The stack's bottom right corner sits just above the right end of the vanilla row.
    /// </summary>
    private static void Align()
    {
        Vector3[] corners = new Vector3[4];
        _keyboard.GetWorldCorners(corners); // 0 = bottom left, 1 = top left, 2 = top right, 3 = bottom right
        HorizontalLayoutGroup layout = _keyboard.GetComponent<HorizontalLayoutGroup>();
        float padding = layout != null ? layout.padding.right * _keyboard.lossyScale.x : 0f;
        _stack.position = new Vector3(corners[2].x - padding, corners[2].y + (StackLift * _stack.lossyScale.y), corners[2].z);
    }

    private static bool Create(KeyHints keyHints)
    {
        Transform keyboard = keyHints.m_buildHints.transform.Find("Keyboard");
        Transform copy = keyboard != null ? keyboard.Find("Copy") : null;
        Transform rotate = keyboard != null ? keyboard.Find("rotate") : null;
        Transform labelTemplate = copy != null ? copy.Find("Text") : null;
        Transform plusTemplate = copy != null ? copy.Find("Text (1)") : null;
        Transform keyTemplate = copy != null ? copy.Find("key_bkg") : null;
        Transform iconTemplate = rotate != null ? rotate.Find("mousew_icon") : null;

        if (labelTemplate == null || plusTemplate == null || keyTemplate == null || iconTemplate == null)
        {
            _createFailed = true;
            Jotunn.Logger.LogWarning("Vanilla build key hints layout not as expected, not changing the hoe's key hints.");
            return false;
        }

        Vanilla.Clear();
        foreach (string name in VanillaNames)
        {
            Transform item = keyboard.Find(name);
            if (item == null)
            {
                continue;
            }

            TextMeshProUGUI label = item.Find("Text")?.GetComponent<TextMeshProUGUI>();
            Vanilla[name] = new VanillaHint { Item = item.gameObject, Label = label, OriginalText = label != null ? label.text : null };
        }

        RectTransform container = new GameObject(ContainerName, typeof(RectTransform)).GetComponent<RectTransform>();
        container.SetParent(keyHints.m_buildHints.transform, false);
        RectTransform stack = CreateStack(container);

        foreach (Hint hint in Hints)
        {
            GameObject item = Object.Instantiate(copy.gameObject, stack, false);
            item.name = "BetterHoe_Hint_" + hint.Label.TrimStart('$');
            item.SetActive(true);
            for (int i = item.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(item.transform.GetChild(i).gameObject);
            }

            _ = AddText(labelTemplate, item.transform, Localization.instance.Localize(hint.Label));
            for (int i = 0; i < hint.Keys.Length; i++)
            {
                if (i > 0)
                {
                    _ = AddText(plusTemplate, item.transform, "+");
                }

                if (hint.Keys[i] == ScrollIcon)
                {
                    AddScrollIcon(iconTemplate, item.transform);
                }
                else
                {
                    GameObject key = Object.Instantiate(keyTemplate.gameObject, item.transform, false);
                    key.SetActive(true);
                    key.GetComponentInChildren<TextMeshProUGUI>().text = Localization.instance.Localize(hint.Keys[i]);
                }
            }

            hint.Group = item.GetComponent<CanvasGroup>() ?? item.AddComponent<CanvasGroup>();
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(stack);
        EqualiseWidths();

        _container = container.gameObject;
        _stack = stack;
        _keyboard = (RectTransform)keyboard;
        _layoutDirty = true;
        return true;
    }

    private static RectTransform CreateStack(RectTransform parent)
    {
        RectTransform stack = new GameObject(ContainerName + "_Stack", typeof(RectTransform)).GetComponent<RectTransform>();
        stack.SetParent(parent, false);
        stack.pivot = new Vector2(1f, 0f);

        VerticalLayoutGroup layout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = RowSpacing;
        layout.childAlignment = TextAnchor.LowerRight;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = stack.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return stack;
    }

    /// <summary>
    /// The scroll wheel icon, laid out the size of a key box but drawn bigger, as its sprite is padded.
    /// </summary>
    private static void AddScrollIcon(Transform template, Transform parent)
    {
        GameObject icon = Object.Instantiate(template.gameObject, parent, false);
        icon.SetActive(true);
        Image image = icon.GetComponent<Image>();
        image.preserveAspect = true;
        float aspect = image.sprite != null ? image.sprite.rect.width / image.sprite.rect.height : 0.6f;
        LayoutElement layout = icon.GetComponent<LayoutElement>() ?? icon.AddComponent<LayoutElement>();
        layout.minWidth = layout.preferredWidth = IconHeight * aspect * IconScale;
        layout.minHeight = layout.preferredHeight = IconHeight;
        layout.flexibleWidth = layout.flexibleHeight = 0f;
        icon.transform.localScale = new Vector3(IconScale, IconScale, 1f);
    }

    /// <summary>
    /// Gives matching parts of every row (label, key, "+", key or icon) the same width, so the keys line up.
    /// </summary>
    private static void EqualiseWidths()
    {
        List<Transform> items = [];
        foreach (Hint hint in Hints)
        {
            items.Add(hint.Group.transform);
        }

        int parts = int.MaxValue;
        foreach (Transform item in items)
        {
            parts = Mathf.Min(parts, item.childCount);
        }

        for (int part = 0; part < parts; part++)
        {
            float width = 0f;
            foreach (Transform item in items)
            {
                width = Mathf.Max(width, ((RectTransform)item.GetChild(part)).rect.width);
            }

            foreach (Transform item in items)
            {
                GameObject child = item.GetChild(part).gameObject;
                LayoutElement layout = child.GetComponent<LayoutElement>() ?? child.AddComponent<LayoutElement>();
                layout.minWidth = width;
            }
        }
    }

    private static TextMeshProUGUI AddText(Transform template, Transform parent, string text)
    {
        GameObject clone = Object.Instantiate(template.gameObject, parent, false);
        clone.SetActive(true);

        // Vanilla labels can have a fixed width sized for their own text.
        LayoutElement fixedSize = clone.GetComponent<LayoutElement>();
        if (fixedSize != null)
        {
            Object.DestroyImmediate(fixedSize);
        }

        TextMeshProUGUI label = clone.GetComponent<TextMeshProUGUI>();
        label.enableAutoSizing = false;
        label.fontSize = LabelFontSize;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.text = text;
        return label;
    }

    /// <summary>
    /// Called after vanilla's KeyHints.UpdateHints.
    /// </summary>
    internal static void AfterUpdateHints(KeyHints keyHints)
    {
        // The hints live under the HUD, which is rebuilt with each world load.
        if (!ReferenceEquals(_container, null) && _container == null)
        {
            Reset();
        }

        Update(keyHints, Player.m_localPlayer);
    }

    private static void Reset()
    {
        _container = null;
        _stack = null;
        _keyboard = null;
        _createFailed = false;
        Vanilla.Clear();
        foreach (Hint hint in Hints)
        {
            hint.Group = null;
        }
    }
}
