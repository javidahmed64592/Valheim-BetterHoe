using Jotunn.Managers;

using UnityEngine;
using UnityEngine.UI;

namespace BetterHoe.UI;

/// <summary>
/// A few lines of text under the crosshair: the tool, the area size and the height or step it uses.
/// </summary>
internal sealed class Readout
{
    private static readonly Vector2 Offset = new(0f, -70f);

    private GameObject _object;
    private Text _text;

    public void Show(string text)
    {
        if (!Ensure())
        {
            return;
        }

        if (_text.text != text)
        {
            _text.text = text;
        }

        if (!_object.activeSelf)
        {
            _object.SetActive(true);
        }
    }

    public void Hide()
    {
        if (_object != null && _object.activeSelf)
        {
            _object.SetActive(false);
        }
    }

    public void Destroy()
    {
        if (_object != null)
        {
            Object.Destroy(_object);
        }

        _object = null;
        _text = null;
    }

    private bool Ensure()
    {
        if (_object != null)
        {
            return true;
        }

        // The GUI root is rebuilt with each scene, taking the old label with it.
        GameObject parent = GUIManager.CustomGUIFront;
        if (parent == null || GUIManager.Instance == null)
        {
            return false;
        }

        _object = GUIManager.Instance.CreateText(
            "", parent.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Offset,
            GUIManager.Instance.AveriaSerifBold, 18, Color.white, outline: true, new Color(0f, 0f, 0f, 0.9f),
            width: 500f, height: 90f, addContentSizeFitter: false);
        _object.name = "BetterHoe_Readout";
        _text = _object.GetComponent<Text>();
        _text.alignment = TextAnchor.UpperCenter;
        _text.supportRichText = true;
        _text.raycastTarget = false;
        _object.SetActive(false);
        return true;
    }
}
