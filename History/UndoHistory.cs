using System.Collections.Generic;

using BepInEx;
using BepInEx.Configuration;

using UnityEngine;

namespace BetterHoe.History;

/// <summary>
/// Undo and redo for the hoe's terrain edits. Each edit keeps the raw terrain data it was about to change.
/// Undoing first captures the data as it is now, then restores the old data; redoing does the same the other
/// way, so both are exact. Nothing happens if any of the terrain isn't loaded or is behind someone else's ward.
/// Undo and redo are free, and the history lasts until logging out.
/// </summary>
internal static class UndoHistory
{
    private sealed class Entry
    {
        public string NameToken;
        public Vector3 Center;
        public float Radius;
        public List<(int x, int z)> Vertices;
        public List<TerrainSnapshot> Restore;
    }

    private static ConfigEntry<KeyboardShortcut> _undoKey;
    private static ConfigEntry<KeyboardShortcut> _redoKey;
    private static ConfigEntry<int> _size;

    private static readonly LinkedList<Entry> Undos = [];
    private static readonly Stack<Entry> Redos = [];

    internal static KeyboardShortcut UndoKey => _undoKey.Value;
    internal static KeyboardShortcut RedoKey => _redoKey.Value;
    internal static bool CanUndo => Undos.Count > 0;
    internal static bool CanRedo => Redos.Count > 0;

    public static void BindConfig(ConfigFile config)
    {
        _undoKey = config.Bind("Keybinds", "Undo", new KeyboardShortcut(KeyCode.Z, KeyCode.LeftControl),
            "Undoes the last terrain change made with the hoe.");
        _redoKey = config.Bind("Keybinds", "Redo", new KeyboardShortcut(KeyCode.Y, KeyCode.LeftControl),
            "Redoes the last terrain change you undid.");
        _size = config.Bind("Undo", "History Size", 50,
            new ConfigDescription("How many terrain changes can be undone.", new AcceptableValueRange<int>(1, 500)));
    }

    public static void Reset()
    {
        Undos.Clear();
        Redos.Clear();
    }

    /// <summary>
    /// Records an edit that was just sent. A new edit clears the redo list.
    /// </summary>
    public static void Record(string nameToken, Vector3 center, float radius, List<(int x, int z)> vertices, List<TerrainSnapshot> before)
    {
        if (before.Count == 0)
        {
            return;
        }

        _ = Undos.AddLast(new Entry
        {
            NameToken = nameToken,
            Center = center,
            Radius = radius,
            Vertices = vertices,
            Restore = [.. before],
        });
        Redos.Clear();
        while (Undos.Count > _size.Value)
        {
            Undos.RemoveFirst();
        }
    }

    /// <summary>
    /// Checks the undo and redo keys while the hoe is out.
    /// </summary>
    public static void UpdateKeys(Player player, System.Func<Player, List<(int x, int z)>, bool> checkAccess)
    {
        if (IsDown(UndoKey))
        {
            Step(player, undo: true, checkAccess);
        }
        else if (IsDown(RedoKey))
        {
            Step(player, undo: false, checkAccess);
        }
    }

    private static void Step(Player player, bool undo, System.Func<Player, List<(int x, int z)>, bool> checkAccess)
    {
        if (undo ? Undos.Count == 0 : Redos.Count == 0)
        {
            player.Message(MessageHud.MessageType.TopLeft, undo ? "$betterhoe_undo_nothing" : "$betterhoe_redo_nothing");
            return;
        }

        Entry entry = undo ? Undos.Last.Value : Redos.Peek();

        List<TerrainComp> compilers = [];
        foreach (TerrainSnapshot snapshot in entry.Restore)
        {
            TerrainComp compiler = snapshot.FindCompiler();
            if (compiler == null)
            {
                player.Message(MessageHud.MessageType.Center, "$betterhoe_undo_too_far");
                return;
            }

            compilers.Add(compiler);
        }

        if (!checkAccess(player, entry.Vertices))
        {
            return;
        }

        // Capture what's there now first: restoring on our own terrain happens straight away.
        List<TerrainSnapshot> current = [];
        for (int i = 0; i < entry.Restore.Count; i++)
        {
            current.Add(entry.Restore[i].CaptureCurrent(compilers[i]));
        }

        for (int i = 0; i < entry.Restore.Count; i++)
        {
            entry.Restore[i].Restore(compilers[i], entry.Center, entry.Radius);
        }

        entry.Restore = current;
        if (undo)
        {
            Undos.RemoveLast();
            Redos.Push(entry);
        }
        else
        {
            _ = Redos.Pop();
            _ = Undos.AddLast(entry);
        }

        player.Message(MessageHud.MessageType.TopLeft,
            Localization.instance.Localize(undo ? "$betterhoe_undone" : "$betterhoe_redone", Localization.instance.Localize(entry.NameToken)));
    }

    /// <summary>
    /// KeyboardShortcut.IsDown is false while any other key is held, so it would never fire while moving. This
    /// only needs the main key pressed and the modifiers held.
    /// </summary>
    private static bool IsDown(KeyboardShortcut shortcut)
    {
        if (shortcut.MainKey == KeyCode.None || !UnityInput.Current.GetKeyDown(shortcut.MainKey))
        {
            return false;
        }

        foreach (KeyCode modifier in shortcut.Modifiers)
        {
            if (!UnityInput.Current.GetKey(modifier))
            {
                return false;
            }
        }

        return true;
    }
}
