using BepInEx.Configuration;

using Jotunn.Configs;
using Jotunn.Managers;

using UnityEngine;

namespace BetterHoe.Equip;

/// <summary>
/// A hoe that isn't in the inventory: the Equip Hoe key puts it in the player's hand (and takes it away again),
/// like BetterHammer's build menu key does with a hammer. It's built from the vanilla hoe's item data, kept at
/// full durability, and patched in where vanilla expects equipped items and piece tables to come from the
/// inventory (see the Inventory and Humanoid patches).
/// </summary>
internal static class PhantomHoe
{
    private const string HoePrefabName = "Hoe";
    private const string ButtonName = "BetterHoe_EquipHoe";

    private static ConfigEntry<KeyCode> _key;
    private static bool _knownRecipesRefreshed;

    internal static ItemDrop.ItemData Item { get; private set; }

    private static string ButtonQueryName => ButtonName + "!" + BetterHoePlugin.PluginGUID;

    public static void BindConfig(ConfigFile config)
    {
        _key = config.Bind("Keybinds", "Equip Hoe", KeyCode.N,
            "Puts a hoe in your hand, or puts it away, without needing a hoe in your inventory. " +
            "Rebindable from the in-game controls menu.");

        InputManager.Instance.AddButton(BetterHoePlugin.PluginGUID, new ButtonConfig
        {
            Name = ButtonName,
            Config = _key,
        });
    }

    public static void Update(Player player)
    {
        if (player == null)
        {
            Item = null;
            _knownRecipesRefreshed = false;
            return;
        }

        Ensure();
        if (Item == null)
        {
            return;
        }

        // It never wears out or needs repairing.
        Item.m_durability = Item.m_shared.m_maxDurability;

        // Once per session, so the hoe's pieces count as known without a hoe in the inventory.
        if (!_knownRecipesRefreshed)
        {
            _knownRecipesRefreshed = true;
            player.UpdateKnownRecipesList();
        }

        if (!InputBlocked() && ZInput.GetButtonDown(ButtonQueryName))
        {
            Toggle(player);
        }
    }

    private static bool InputBlocked()
    {
        return Menu.IsVisible()
            || InventoryGui.IsVisible()
            || (Chat.instance != null && Chat.instance.HasFocus())
            || Console.IsVisible()
            || TextInput.IsVisible()
            || (Minimap.instance != null && Minimap.instance.m_mode == Minimap.MapMode.Large);
    }

    private static void Toggle(Player player)
    {
        // Checking the hand rather than keeping a flag stays right across death, respawn, or something else
        // unequipping the hoe.
        if (player.GetRightItem() == Item)
        {
            player.UnequipItem(Item);
        }
        else
        {
            // The same path as picking a tool from the hotbar: shows it in hand and opens build mode.
            _ = player.EquipItem(Item);
        }
    }

    private static void Ensure()
    {
        if (Item != null || ObjectDB.instance == null)
        {
            return;
        }

        GameObject prefab = ObjectDB.instance.GetItemPrefab(HoePrefabName);
        ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        if (drop == null)
        {
            return;
        }

        // A fresh ItemData rather than a clone of the prefab's: the prefab's has fields that are never set up
        // (m_customData is null), which SetupVisEquipment trips over.
        Item = new ItemDrop.ItemData
        {
            m_shared = drop.m_itemData.m_shared,
            m_dropPrefab = prefab,
            m_stack = 1,
            m_quality = 1,
            m_variant = 0,
            m_durability = drop.m_itemData.m_shared.m_maxDurability,
            m_customData = [],
        };
    }
}
