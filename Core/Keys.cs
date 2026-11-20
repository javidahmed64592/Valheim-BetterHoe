using UnityEngine;

namespace BetterHoe.Core;

/// <summary>
/// Modifier keys, read straight from the keyboard so they work while moving.
/// </summary>
internal static class Keys
{
    public static bool Ctrl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

    public static bool Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    public static bool Alt => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

    public static bool AnyModifier => Ctrl || Shift || Alt;
}
