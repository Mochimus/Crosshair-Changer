using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using DataCenterCrosshair.Patches;

namespace DataCenterCrosshair;

public sealed class Mod : MelonMod
{
    private const string LabelBgObjectName = "DCrosshair_LabelBg";

    private HarmonyLib.Harmony _harmony;
    private RectTransform _lastTrackedPointerRt;
    private Vector2 _lastPointerOffsetSnapshot = new(float.NaN, float.NaN);

    private bool _loggedReady;
    private bool _warnedMissingImage;
    private bool _warnedBadColor;
    private bool _warnedBadPointerTextColor;
    private bool _warnedBadPointerBgColor;

    private GameObject _labelBgRoot;
    private int _labelBgOwnerId;
    private static Sprite _whiteSprite;

    public override void OnInitializeMelon()
    {
        CrosshairSettings.Initialize(LoggerInstance);

        _harmony = new HarmonyLib.Harmony("DataCenterCrosshair");
        try
        {
            _harmony.PatchAll(typeof(Mod).Assembly);
            MelonLogger.Msg("[DataCenterCrosshair] Harmony: RectTransform.anchoredPosition setter prefix (pointer label lift).");
        }
        catch (System.Exception ex)
        {
            MelonLogger.Warning("[DataCenterCrosshair] Harmony patch failed (label lift may not apply): " + ex);
        }
    }

    /// <summary>Assign Harmony target before most game scripts run so anchoredPosition writes from the game see a non-null target.</summary>
    public override void OnUpdate()
    {
        CrosshairSettings.TickReload();

        var offSnap = new Vector2(CrosshairSettings.PointerTextOffsetX, CrosshairSettings.PointerTextOffsetY);
        if (float.IsNaN(_lastPointerOffsetSnapshot.x) || (offSnap - _lastPointerOffsetSnapshot).sqrMagnitude > 1e-6f)
        {
            _lastPointerOffsetSnapshot = offSnap;
            PointerLabelAnchoredHarmony.ClearWrittenCache();
        }

        RefreshPointerLabelHarmonyTarget(StaticUIElements.instance);
    }

    public override void OnLateUpdate()
    {
        var ui = StaticUIElements.instance;
        if (ui != null
            && CrosshairSettings.PointerTextEnabled
            && ui.txtUnderPointer != null
            && ui.txtUnderPointer.gameObject.activeInHierarchy
            && !string.IsNullOrEmpty(ui.txtUnderPointer.text))
            ApplyUnderPointerStyle(ui.txtUnderPointer);
        else
            SetLabelBackgroundVisible(false);

        if (ui == null)
            return;

        if (ui.imagePointer == null)
            return;

        var go = ui.imagePointer;
        float size = CrosshairSettings.Size;
        string hex = CrosshairSettings.ColorHexRaw;
        if (!ColorUtility.TryParseHtmlString(hex, out var color))
        {
            color = Color.white;
            if (!_warnedBadColor)
            {
                _warnedBadColor = true;
                MelonLogger.Warning($"[DataCenterCrosshair] Invalid CrosshairColor '{hex}', using white. Use #RRGGBB or #RRGGBBAA.");
            }
        }

        var rt = go.transform as RectTransform ?? go.GetComponent<RectTransform>();
        var image = go.GetComponent<Image>() ?? go.GetComponentInChildren<Image>(true);

        bool sizeMismatch = rt != null && (!Mathf.Approximately(rt.sizeDelta.x, size) || !Mathf.Approximately(rt.sizeDelta.y, size));
        bool colorMismatch = image != null && !ApproximatelyColor(image.color, color);
        if (!sizeMismatch && !colorMismatch)
        {
            if (!_loggedReady)
            {
                _loggedReady = true;
                MelonLogger.Msg($"[DataCenterCrosshair] Tracking StaticUIElements.imagePointer (size={size}, color={hex}).");
            }
            return;
        }

        if (rt != null)
            rt.sizeDelta = new Vector2(size, size);

        if (image != null)
            image.color = color;
        else if (!_warnedMissingImage)
        {
            _warnedMissingImage = true;
            MelonLogger.Warning("[DataCenterCrosshair] No UnityEngine.UI.Image on imagePointer; size was still applied if RectTransform exists.");
        }

        if (!_loggedReady)
        {
            _loggedReady = true;
            MelonLogger.Msg($"[DataCenterCrosshair] Patched StaticUIElements.imagePointer (size={size}, color={hex}).");
        }
    }

    private void RefreshPointerLabelHarmonyTarget(StaticUIElements ui)
    {
        RectTransform nextTarget = null;

        if (ui != null
            && CrosshairSettings.PointerTextEnabled
            && ui.txtUnderPointer != null
            && ui.txtUnderPointer.gameObject.activeInHierarchy
            && !string.IsNullOrEmpty(ui.txtUnderPointer.text))
            nextTarget = ui.txtUnderPointer.rectTransform;

        if (!ReferenceEquals(nextTarget, _lastTrackedPointerRt))
            PointerLabelAnchoredHarmony.ClearWrittenCache();

        PointerLabelAnchoredHarmony.Target = nextTarget;
        PointerLabelAnchoredHarmony.TargetInstanceId = nextTarget != null ? nextTarget.GetInstanceID() : int.MinValue;
        _lastTrackedPointerRt = nextTarget;
    }

    private void ApplyUnderPointerStyle(TextMeshProUGUI tmp)
    {
        string pHex = CrosshairSettings.PointerTextColorHexRaw;
        if (!ColorUtility.TryParseHtmlString(pHex, out var textColor))
        {
            textColor = Color.white;
            if (!_warnedBadPointerTextColor)
            {
                _warnedBadPointerTextColor = true;
                MelonLogger.Warning($"[DataCenterCrosshair] Invalid pointerTextColor '{pHex}', using white.");
            }
        }

        tmp.enableAutoSizing = false;
        tmp.fontSize = CrosshairSettings.PointerTextFontSize;
        tmp.color = textColor;

        if (CrosshairSettings.PointerTextBackgroundEnabled)
            UpdateLabelBackground(tmp);
        else
            SetLabelBackgroundVisible(false);
    }

    private void UpdateLabelBackground(TextMeshProUGUI tmp)
    {
        string bgHex = CrosshairSettings.PointerTextBackgroundColorHexRaw;
        if (!ColorUtility.TryParseHtmlString(bgHex, out var bgColor))
        {
            bgColor = new Color(0f, 0f, 0f, 0.7f);
            if (!_warnedBadPointerBgColor)
            {
                _warnedBadPointerBgColor = true;
                MelonLogger.Warning($"[DataCenterCrosshair] Invalid pointerTextBackgroundColor '{bgHex}', using semi-opaque black.");
            }
        }

        int ownerId = tmp.GetInstanceID();
        if (_labelBgRoot == null || _labelBgOwnerId != ownerId || _labelBgRoot.transform.parent != tmp.transform)
        {
            if (_labelBgRoot != null)
                UnityEngine.Object.Destroy(_labelBgRoot);

            _labelBgOwnerId = ownerId;
            _labelBgRoot = new GameObject(LabelBgObjectName);
            _labelBgRoot.transform.SetParent(tmp.transform, false);
            _labelBgRoot.transform.SetAsFirstSibling();

            var bgRt = _labelBgRoot.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = Vector2.zero;

            var img = _labelBgRoot.AddComponent<Image>();
            img.sprite = GetWhiteSprite();
            img.type = Image.Type.Simple;
            img.raycastTarget = false;

            tmp.transform.SetAsLastSibling();
        }

        _labelBgRoot.SetActive(true);

        var image = _labelBgRoot.GetComponent<Image>();
        image.color = bgColor;

        var padX = CrosshairSettings.PointerTextBackgroundPadX;
        var padY = CrosshairSettings.PointerTextBackgroundPadY;
        var bgRt2 = _labelBgRoot.GetComponent<RectTransform>();
        bgRt2.offsetMin = new Vector2(-padX, -padY);
        bgRt2.offsetMax = new Vector2(padX, padY);
        bgRt2.localPosition = new Vector3(
            CrosshairSettings.PointerBackgroundLiftX,
            CrosshairSettings.PointerBackgroundLiftY,
            0f);
    }

    private void SetLabelBackgroundVisible(bool visible)
    {
        if (_labelBgRoot != null)
            _labelBgRoot.SetActive(visible);
    }

    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null)
            return _whiteSprite;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        return _whiteSprite;
    }

    private static bool ApproximatelyColor(Color a, Color b)
    {
        return Mathf.Approximately(a.r, b.r)
            && Mathf.Approximately(a.g, b.g)
            && Mathf.Approximately(a.b, b.b)
            && Mathf.Approximately(a.a, b.a);
    }
}
