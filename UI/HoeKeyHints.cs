using System;
using System.Collections.Generic;

using BetterHoe.Core;
using BetterHoe.History;
using BetterHoe.Terrain;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

using Object = UnityEngine.Object;

namespace BetterHoe.UI;

/// <summary>
/// The key hints while the hoe is out. With the hoe menu closed, the vanilla hint row is hidden and every hint
/// is drawn in one stack at the bottom right instead, built from clones of the vanilla hint parts. Each hint has
/// a fixed row: one that doesn't apply right now is invisible but keeps its space, so nothing moves when the
/// tool or the grid changes. A hint's text only changes when its key does something different. With the hoe
/// menu open, the vanilla row (with its menu hints) is shown, relabelled for the hoe. Everything vanilla is
/// put back when the hoe is put away.
/// </summary>
internal static class HoeKeyHints
{
    private const string ContainerName = "BetterHoe_KeyHints";
    private const string ScrollIcon = "@scroll";

    /// <summary>
    /// In a hint's keys, the configured undo and redo shortcuts, shown as their keys.
    /// </summary>
    private const string UndoKey = "@undo";
    private const string RedoKey = "@redo";

    /// <summary>
    /// In a hint's keys, the configured key that clears the marked region.
    /// </summary>
    private const string ClearRegionKey = "@clearregion";

    private const float LabelFontSize = 16f;
    private const float IconHeight = 32f;
    private const float IconScale = 1.8f;
    private const float RowSpacing = 16f;

    /// <summary>
    /// What a hint's label depends on, worked out once a frame.
    /// </summary>
    private sealed class State
    {
        public Player Player;
        public Piece Piece;

        /// <summary>
        /// The selected piece's terrain tool, whether or not the grid is on; null for the stones.
        /// </summary>
        public HoeTool TerrainTool;

        /// <summary>
        /// The terrain tool when it uses the grid; null with vanilla's brush.
        /// </summary>
        public HoeTool AreaTool;
    }

    private sealed class Hint
    {
        public string[] Keys;

        /// <summary>
        /// The hint's label token, or null to hide it.
        /// </summary>
        public Func<State, string> Label;
        public CanvasGroup Group;
        public TextMeshProUGUI LabelText;
        public string ShownLabel;
    }

    /// <summary>
    /// Top to bottom. The ones that show most often are at the bottom, next to the screen edge.
    /// </summary>
    private static readonly List<Hint> Hints =
    [
        new Hint { Keys = [UndoKey], Label = _ => UndoHistory.CanUndo ? "$betterhoe_hint_undo" : null },
        new Hint { Keys = [RedoKey], Label = _ => UndoHistory.CanRedo ? "$betterhoe_hint_redo" : null },
        new Hint
        {
            Keys = [ClearRegionKey],
            Label = ClearRegionLabel,
        },
        new Hint
        {
            Keys = ["Left Alt", "$KEY_BuildMenu"],
            Label = s => AreaTool.CanMark(s.AreaTool) && !MarkedRegion.IsEmpty ? "$betterhoe_hint_unmark" : null,
        },
        new Hint { Keys = ["Left Alt", "$KEY_Remove"], Label = s => AreaTool.CanMark(s.AreaTool) ? "$betterhoe_hint_mark" : null },
        new Hint { Keys = ["Left Ctrl", ScrollIcon], Label = s => s.AreaTool != null ? "$betterhoe_hint_width" : null },
        new Hint
        {
            Keys = ["Left Shift", ScrollIcon],
            Label = s => s.AreaTool != null && s.AreaTool.Shape != ShapeMode.Ramp ? "$betterhoe_hint_length" : null,
        },
        new Hint
        {
            Keys = ["Left Alt", ScrollIcon],
            Label = s => s.AreaTool?.Shape switch
            {
                ShapeMode.Raise or ShapeMode.Lower => "$betterhoe_hint_step",
                ShapeMode.Ramp => "$betterhoe_hint_blend",
                _ => null,
            },
        },
        new Hint
        {
            Keys = ["Left Ctrl", "$KEY_BuildMenu"],
            Label = s => AreaTool.GridToggleApplies(s.Player) ? "$betterhoe_hint_grid" : null,
        },
        new Hint
        {
            // Vanilla Level ground and Paved road level to the aimed height instead of the player's feet.
            Keys = ["$KEY_AltPlace"],
            Label = s => s.AreaTool == null && s.TerrainTool != null && s.Piece.m_allowAltGroundPlacement ? "$betterhoe_hint_altplace" : null,
        },
        new Hint { Keys = [ScrollIcon], Label = s => s.TerrainTool == null ? "$hud_rotate" : null },
        new Hint
        {
            Keys = ["$KEY_Remove"],
            Label = LockLabel,
        },
        new Hint { Keys = ["$KEY_BuildMenu"], Label = _ => AreaTool.RampStarted ? "$betterhoe_hint_ramp_cancel" : "$betterhoe_hint_menu" },
        new Hint { Keys = ["$KEY_Attack"], Label = PlaceLabel },
    ];

    /// <summary>
    /// Every label a hint can show, so the label column is made wide enough for all of them.
    /// </summary>
    private static readonly string[] AllLabels =
    [
        "$betterhoe_hint_undo", "$betterhoe_hint_redo", "$betterhoe_hint_width", "$betterhoe_hint_length",
        "$betterhoe_hint_step", "$betterhoe_hint_blend", "$betterhoe_hint_grid", "$betterhoe_hint_altplace",
        "$hud_rotate", "$betterhoe_hint_lock", "$betterhoe_hint_unlock", "$betterhoe_hint_ramp_cancel",
        "$betterhoe_hint_menu", "$hud_place", "$betterhoe_hint_ramp_start", "$betterhoe_hint_ramp_end",
        "$betterhoe_hint_flatten", "$betterhoe_hint_raise", "$betterhoe_hint_lower", "$betterhoe_hint_reset_height",
        "$betterhoe_hint_path", "$betterhoe_hint_paved", "$betterhoe_hint_cultivate", "$betterhoe_hint_reset_paint",
        "$betterhoe_hint_mark", "$betterhoe_hint_unmark", "$betterhoe_hint_clear_region", "$betterhoe_hint_restore_region",
    ];

    /// <summary>
    /// Middle-click locks the height for Level ground and the ramp on the grid; with anything else it does nothing.
    /// </summary>
    private static string LockLabel(State state)
    {
        bool locks = state.AreaTool?.Shape is ShapeMode.Flatten or ShapeMode.Ramp;
        string token = AreaTool.HeightLocked ? "$betterhoe_hint_unlock" : "$betterhoe_hint_lock";
        return locks ? token : null;
    }

    /// <summary>
    /// The clear key clears marked ground, or with nothing marked marks the last cleared ground again.
    /// </summary>
    private static string ClearRegionLabel(State state)
    {
        return AreaTool.CanMark(state.AreaTool) switch
        {
            false => null,
            true when !MarkedRegion.IsEmpty => "$betterhoe_hint_clear_region",
            true when MarkedRegion.CanRestore => "$betterhoe_hint_restore_region",
            true => null,
        };
    }

    /// <summary>
    /// What the place key does. The same with the grid on or off, as it does the same thing either way.
    /// </summary>
    private static string PlaceLabel(State state)
    {
        string ramp = AreaTool.RampStarted ? "$betterhoe_hint_ramp_end" : "$betterhoe_hint_ramp_start";
        return state.TerrainTool?.Shape switch
        {
            null => "$hud_place",
            ShapeMode.Ramp => ramp,
            _ => state.TerrainTool.ActionToken,
        };
    }

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

    private static readonly State Current = new();

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

        Current.Player = player;
        Current.Piece = piece;
        Current.TerrainTool = AreaTool.SelectedTerrainTool(player);
        Current.AreaTool = AreaTool.ActiveTool(player);

        // With the menu open, vanilla's row carries the menu's own hints, so it stays, relabelled.
        if (Hud.IsPieceSelectionVisible())
        {
            UpdateVanillaForMenu();
            SetContainerActive(false);
            return;
        }

        foreach (string name in VanillaNames)
        {
            SetVanilla(name, false, null);
        }

        foreach (Hint hint in Hints)
        {
            string label = hint.Label(Current);

            // A hidden hint keeps its space, so the others don't move.
            hint.Group.alpha = label != null ? 1f : 0f;
            if (label != null && label != hint.ShownLabel)
            {
                hint.ShownLabel = label;
                hint.LabelText.text = Localization.instance.Localize(label);
            }
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
    /// With the hoe menu open: the vanilla hints the hoe uses, relabelled, and the rest hidden.
    /// </summary>
    private static void UpdateVanillaForMenu()
    {
        SetVanilla("Place", true, PlaceLabel(Current));
        SetVanilla("Remove", false, null);
        SetVanilla("BuildMenu", true, "$betterhoe_hint_menu");
        SetVanilla("AltPlace", false, null);
        SetVanilla("Snap", false, null);
        SetVanilla("Copy", false, null);
        SetVanilla("rotate", false, null);
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

        if (hint.Label == null || !active)
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
    /// The stack's bottom right corner sits where the vanilla row's last hint would end.
    /// </summary>
    private static void Align()
    {
        Vector3[] corners = new Vector3[4];
        _keyboard.GetWorldCorners(corners); // 0 = bottom left, 1 = top left, 2 = top right, 3 = bottom right
        HorizontalLayoutGroup layout = _keyboard.GetComponent<HorizontalLayoutGroup>();
        float right = layout != null ? layout.padding.right * _keyboard.lossyScale.x : 0f;
        float bottom = layout != null ? layout.padding.bottom * _keyboard.lossyScale.y : 0f;
        _stack.position = new Vector3(corners[3].x - right, corners[3].y + bottom, corners[3].z);
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
        HorizontalLayoutGroup rowLayout = copy != null ? copy.GetComponent<HorizontalLayoutGroup>() : null;

        if (labelTemplate == null || plusTemplate == null || keyTemplate == null || iconTemplate == null || rowLayout == null)
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
        string longestLabel = LongestLabel();

        foreach (Hint hint in Hints)
        {
            // A vanilla multi-key hint (for its row settings), emptied: a label, then a group with the keys.
            GameObject item = Object.Instantiate(copy.gameObject, stack, false);
            item.name = "BetterHoe_Hint";
            item.SetActive(true);
            for (int i = item.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(item.transform.GetChild(i).gameObject);
            }

            // Sized with the longest label first, so the column fits every label.
            hint.LabelText = AddText(labelTemplate, item.transform, longestLabel);
            hint.LabelText.alignment = TextAlignmentOptions.Right;
            hint.ShownLabel = null;

            Transform keys = CreateKeyGroup(item.transform, rowLayout);
            string[] keyNames = ExpandShortcuts(hint.Keys);
            for (int i = 0; i < keyNames.Length; i++)
            {
                if (i > 0)
                {
                    _ = AddText(plusTemplate, keys, "+");
                }

                if (keyNames[i] == ScrollIcon)
                {
                    AddScrollIcon(iconTemplate, keys);
                }
                else
                {
                    GameObject key = Object.Instantiate(keyTemplate.gameObject, keys, false);
                    key.SetActive(true);
                    key.GetComponentInChildren<TextMeshProUGUI>().text = Localization.instance.Localize(keyNames[i]);
                }
            }

            hint.Group = item.GetComponent<CanvasGroup>() ?? item.AddComponent<CanvasGroup>();
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(stack);
        EqualiseWidths();
        foreach (Hint hint in Hints)
        {
            hint.LabelText.text = "";
        }

        _container = container.gameObject;
        _stack = stack;
        _keyboard = (RectTransform)keyboard;
        _layoutDirty = true;
        return true;
    }

    private static string LongestLabel()
    {
        string longest = "";
        foreach (string token in AllLabels)
        {
            string text = Localization.instance.Localize(token);
            if (text.Length > longest.Length)
            {
                longest = text;
            }
        }

        return longest;
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
    /// The keys of a row in their own group, laid out like a vanilla hint, starting from the left.
    /// </summary>
    private static Transform CreateKeyGroup(Transform row, HorizontalLayoutGroup template)
    {
        GameObject group = new("Keys", typeof(RectTransform));
        group.transform.SetParent(row, false);
        HorizontalLayoutGroup layout = group.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = template.spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = template.childControlWidth;
        layout.childControlHeight = template.childControlHeight;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // As tall as a key box, whether or not the row controls its children's heights.
        ((RectTransform)group.transform).sizeDelta = new Vector2(0f, IconHeight);
        LayoutElement size = group.AddComponent<LayoutElement>();
        size.minHeight = size.preferredHeight = IconHeight;
        return group.transform;
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
    /// The undo, redo and clear region keybinds as their modifier and key, named as the vanilla hints name them.
    /// </summary>
    private static string[] ExpandShortcuts(string[] keys)
    {
        List<string> expanded = [];
        foreach (string key in keys)
        {
            KeyBinding binding = key switch
            {
                UndoKey => UndoHistory.UndoKey,
                RedoKey => UndoHistory.RedoKey,
                ClearRegionKey => AreaTool.ClearRegionKey,
                _ => null,
            };
            if (binding == null)
            {
                expanded.Add(key);
            }
            else
            {
                expanded.AddRange(binding.HintKeys());
            }
        }

        return [.. expanded];
    }

    /// <summary>
    /// Gives every row's label and key group the same widths, so labels line up on the right and keys on the
    /// left, like a form.
    /// </summary>
    private static void EqualiseWidths()
    {
        for (int part = 0; part < 2; part++)
        {
            float width = 0f;
            foreach (Hint hint in Hints)
            {
                width = Mathf.Max(width, LayoutUtility.GetPreferredWidth((RectTransform)hint.Group.transform.GetChild(part)));
            }

            // The label with the most letters isn't always the widest, so measure every label.
            if (part == 0 && Hints.Count > 0)
            {
                TextMeshProUGUI measure = Hints[0].LabelText;
                foreach (string token in AllLabels)
                {
                    width = Mathf.Max(width, measure.GetPreferredValues(Localization.instance.Localize(token)).x);
                }
            }

            foreach (Hint hint in Hints)
            {
                GameObject child = hint.Group.transform.GetChild(part).gameObject;
                LayoutElement layout = child.GetComponent<LayoutElement>() ?? child.AddComponent<LayoutElement>();
                layout.minWidth = width;
                layout.preferredWidth = width;
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
            hint.LabelText = null;
            hint.ShownLabel = null;
        }
    }
}
