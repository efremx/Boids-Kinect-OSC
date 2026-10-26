using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TennisUI — Superhot VR-inspired HUD.
///
/// Style: stark white background, bold black typography, red accent flashes,
/// dramatic word-by-word text reveals, minimal geometry.
///
/// Scene setup:
///   1. Create a Canvas (Screen Space — Overlay, or World Space for VR).
///   2. Attach this script to the Canvas GameObject.
///   3. Wire up GameManager events to the public methods below,
///      OR this script auto-subscribes if GameManager.Instance is found in Start().
///
/// Fonts: Uses TextMeshPro. For best results import a condensed bold font
/// (e.g. "Bebas Neue" or "Barlow Condensed ExtraBold") as a TMP Font Asset
/// and assign to bigFont / smallFont. Falls back to TMP default if null.
/// </summary>
public class TennisUI : MonoBehaviour
{
    // -------------------------------------------------------------------------
    //  Inspector
    // -------------------------------------------------------------------------

    [Header("Font Assets (optional — falls back to TMP default)")]
    public TMP_FontAsset bigFont;    // e.g. Bebas Neue — for scores & big words
    public TMP_FontAsset smallFont;  // e.g. Barlow Condensed — for labels

    [Header("Colours")]
    public Color bgColor        = new Color(0.96f, 0.96f, 0.96f); // near-white
    public Color primaryColor   = new Color(0.06f, 0.06f, 0.06f); // near-black
    public Color accentColor    = new Color(0.92f, 0.08f, 0.08f); // SUPERHOT red
    public Color dimColor       = new Color(0.55f, 0.55f, 0.55f); // muted labels

    [Header("Timing")]
    public float flashDuration    = 0.18f;
    public float wordRevealDelay  = 0.12f;  // seconds between each word reveal
    public float bigTextHoldTime  = 1.8f;

    // -------------------------------------------------------------------------
    //  Private UI references
    // -------------------------------------------------------------------------

    // Root panels
    private GameObject hudPanel;
    private GameObject centerPanel;
    private GameObject overlayFlash;

    // Score
    private TextMeshProUGUI scoreP1Text;
    private TextMeshProUGUI scoreP2Text;
    private TextMeshProUGUI dividerText;
    private TextMeshProUGUI labelP1;
    private TextMeshProUGUI labelP2;
    private Image           scoreBarP1;
    private Image           scoreBarP2;

    // Center / event text
    private TextMeshProUGUI centerBigText;
    private TextMeshProUGUI centerSubText;

    // Serve indicator
    private TextMeshProUGUI serveIndicator;

    // State
    private Canvas canvas;
    private int    cachedP1 = 0;
    private int    cachedP2 = 0;

    // -------------------------------------------------------------------------
    //  Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = GetComponent<CanvasScaler>()
                  ?? gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        BuildUI();
        SubscribeToGameManager();

        // Initial state
        ShowServeIndicator(1);
    }

    private void SubscribeToGameManager()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.onScoreChanged.AddListener(OnScoreChanged);
        GameManager.Instance.onPointScored.AddListener(OnPointScored);
        GameManager.Instance.onGameOver.AddListener(OnGameOver);
        GameManager.Instance.onServe.AddListener(OnServe);
        GameManager.Instance.onBallReset.AddListener(OnBallReset);
    }

    // -------------------------------------------------------------------------
    //  Build UI
    // -------------------------------------------------------------------------

    private void BuildUI()
    {
        BuildOverlayFlash();
        BuildHUD();
        BuildCenterPanel();
    }

    // Full-screen flash overlay
    private void BuildOverlayFlash()
    {
        overlayFlash = MakePanel("OverlayFlash", (RectTransform)transform);
        Stretch(overlayFlash.GetComponent<RectTransform>());
        var img = overlayFlash.AddComponent<Image>();
        img.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0f);
        overlayFlash.SetActive(true);
    }

    // Score HUD at top
    private void BuildHUD()
    {
        hudPanel = MakePanel("HUD", (RectTransform)transform);
        var rt = hudPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 120f);
        rt.anchoredPosition = Vector2.zero;

        // Background bar
        var bg = hudPanel.AddComponent<Image>();
        bg.color = primaryColor;

        // P1 score (left)
        scoreP1Text = MakeText("ScoreP1", rt, "0",
            bigFont, 88, TextAlignmentOptions.Left, accentColor);
        var p1rt = scoreP1Text.GetComponent<RectTransform>();
        p1rt.anchorMin = new Vector2(0f, 0f);
        p1rt.anchorMax = new Vector2(0.35f, 1f);
        p1rt.offsetMin = new Vector2(48f, 0f);
        p1rt.offsetMax = new Vector2(0f, 0f);

        // Divider
        dividerText = MakeText("Divider", rt, "—",
            smallFont, 36, TextAlignmentOptions.Center, dimColor);
        var dvrt = dividerText.GetComponent<RectTransform>();
        dvrt.anchorMin = new Vector2(0.35f, 0f);
        dvrt.anchorMax = new Vector2(0.65f, 1f);
        dvrt.offsetMin = dvrt.offsetMax = Vector2.zero;

        // P2 score (right)
        scoreP2Text = MakeText("ScoreP2", rt, "0",
            bigFont, 88, TextAlignmentOptions.Right, bgColor);
        var p2rt = scoreP2Text.GetComponent<RectTransform>();
        p2rt.anchorMin = new Vector2(0.65f, 0f);
        p2rt.anchorMax = new Vector2(1f, 1f);
        p2rt.offsetMin = new Vector2(0f, 0f);
        p2rt.offsetMax = new Vector2(-48f, 0f);

        // Player labels below scores
        labelP1 = MakeText("LabelP1", rt, "PLAYER 1",
            smallFont, 16, TextAlignmentOptions.Left, dimColor);
        var lp1rt = labelP1.GetComponent<RectTransform>();
        lp1rt.anchorMin = new Vector2(0f, 0f);
        lp1rt.anchorMax = new Vector2(0.35f, 0.3f);
        lp1rt.offsetMin = new Vector2(48f, 4f);
        lp1rt.offsetMax = Vector2.zero;

        labelP2 = MakeText("LabelP2", rt, "PLAYER 2",
            smallFont, 16, TextAlignmentOptions.Right, dimColor);
        var lp2rt = labelP2.GetComponent<RectTransform>();
        lp2rt.anchorMin = new Vector2(0.65f, 0f);
        lp2rt.anchorMax = new Vector2(1f, 0.3f);
        lp2rt.offsetMin = Vector2.zero;
        lp2rt.offsetMax = new Vector2(-48f, 4f);

        // Score progress bars (thin red lines under scores)
        scoreBarP1 = MakeBar("BarP1", rt, new Vector2(0f, 0f), new Vector2(0.35f, 0f), true);
        scoreBarP2 = MakeBar("BarP2", rt, new Vector2(0.65f, 0f), new Vector2(1f, 0f), false);

        // Serve indicator
        serveIndicator = MakeText("ServeIndicator", rt, "▶ SERVE",
            smallFont, 18, TextAlignmentOptions.Center, accentColor);
        var sirt = serveIndicator.GetComponent<RectTransform>();
        sirt.anchorMin = new Vector2(0.35f, 0f);
        sirt.anchorMax = new Vector2(0.65f, 0.4f);
        sirt.offsetMin = sirt.offsetMax = Vector2.zero;
    }

    // Center event text panel
    private void BuildCenterPanel()
    {
        centerPanel = MakePanel("CenterPanel", (RectTransform)transform);
        var rt = centerPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.3f);
        rt.anchorMax = new Vector2(0.9f, 0.7f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        centerBigText = MakeText("BigText", rt, "",
            bigFont, 140, TextAlignmentOptions.Center, primaryColor);
        Stretch(centerBigText.GetComponent<RectTransform>());
        centerBigText.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.65f);

        centerSubText = MakeText("SubText", rt, "",
            smallFont, 28, TextAlignmentOptions.Center, dimColor);
        var subrt = centerSubText.GetComponent<RectTransform>();
        subrt.anchorMin = new Vector2(0f, 0f);
        subrt.anchorMax = new Vector2(1f, 0.35f);
        subrt.offsetMin = subrt.offsetMax = Vector2.zero;

        centerPanel.SetActive(false);
    }

    // -------------------------------------------------------------------------
    //  GameManager event handlers
    // -------------------------------------------------------------------------

    public void OnScoreChanged(int p1, int p2)
    {
        cachedP1 = p1;
        cachedP2 = p2;
        UpdateScoreDisplay(p1, p2);
    }

    public void OnPointScored(int scoringPlayer)
    {
        StartCoroutine(PointScoredRoutine(scoringPlayer));
    }

    public void OnGameOver(int winner)
    {
        StartCoroutine(GameOverRoutine(winner));
    }

    public void OnServe()
    {
        ShowServeIndicator(GameManager.Instance != null
            ? GameManager.Instance.ServingPlayer : 1);
    }

    public void OnBallReset()
    {
        HideCenter();
        serveIndicator.gameObject.SetActive(false);
    }

    // -------------------------------------------------------------------------
    //  Coroutines
    // -------------------------------------------------------------------------

    private IEnumerator PointScoredRoutine(int scoringPlayer)
    {
        // Red flash
        yield return StartCoroutine(FlashOverlay(accentColor, flashDuration));

        // Show point text
        string winner = scoringPlayer == 1 ? "PLAYER 1" : "PLAYER 2";
        yield return StartCoroutine(RevealWords(centerBigText, centerSubText,
            "POINT", winner, accentColor, dimColor));

        yield return new WaitForSeconds(bigTextHoldTime * 0.5f);
        HideCenter();
    }

    private IEnumerator GameOverRoutine(int winner)
    {
        yield return StartCoroutine(FlashOverlay(accentColor, flashDuration * 2f));

        string winnerName = winner == 1 ? "PLAYER 1" : "PLAYER 2";
        yield return StartCoroutine(RevealWords(centerBigText, centerSubText,
            "GAME", winnerName + " WINS", accentColor, primaryColor));

        // Pulsing effect on big text
        StartCoroutine(PulseText(centerBigText));
    }

    // Reveal words one by one — Superhot style
    private IEnumerator RevealWords(TextMeshProUGUI big, TextMeshProUGUI sub,
        string bigWord, string subWord, Color bigCol, Color subCol)
    {
        centerPanel.SetActive(true);
        big.text  = "";
        sub.text  = "";
        big.color = bigCol;
        sub.color = new Color(subCol.r, subCol.g, subCol.b, 0f);

        // Scale punch in
        big.transform.localScale = Vector3.one * 1.4f;
        big.text = bigWord;
        yield return StartCoroutine(PunchScale(big.transform, 1.4f, 1f, 0.15f));

        yield return new WaitForSeconds(wordRevealDelay);

        // Sub text fade in
        sub.text = subWord;
        yield return StartCoroutine(FadeIn(sub, 0.2f));
    }

    private IEnumerator FlashOverlay(Color col, float duration)
    {
        var img = overlayFlash.GetComponent<Image>();
        img.color = new Color(col.r, col.g, col.b, 0.45f);
        yield return StartCoroutine(FadeAlpha(img, 0.45f, 0f, duration));
    }

    private IEnumerator PunchScale(Transform t, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float s = Mathf.Lerp(from, to, elapsed / duration);
            t.localScale = Vector3.one * s;
            yield return null;
        }
        t.localScale = Vector3.one * to;
    }

    private IEnumerator FadeIn(TextMeshProUGUI tmp, float duration)
    {
        float elapsed = 0f;
        Color c = tmp.color;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(0f, 1f, elapsed / duration);
            tmp.color = c;
            yield return null;
        }
        c.a = 1f;
        tmp.color = c;
    }

    private IEnumerator FadeAlpha(Graphic g, float from, float to, float duration)
    {
        float elapsed = 0f;
        Color c = g.color;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, elapsed / duration);
            g.color = c;
            yield return null;
        }
        c.a = to;
        g.color = c;
    }

    private IEnumerator PulseText(TextMeshProUGUI tmp)
    {
        while (true)
        {
            yield return StartCoroutine(PunchScale(tmp.transform, 1f, 1.04f, 0.5f));
            yield return StartCoroutine(PunchScale(tmp.transform, 1.04f, 1f, 0.5f));
        }
    }

    // -------------------------------------------------------------------------
    //  Score display
    // -------------------------------------------------------------------------

    private void UpdateScoreDisplay(int p1, int p2)
    {
        if (scoreP1Text) scoreP1Text.text = p1.ToString();
        if (scoreP2Text) scoreP2Text.text = p2.ToString();

        // Update progress bars based on points to win (default 5)
        int ptw = 5;
        if (GameManager.Instance != null)
        {
            // Read via reflection if needed — hardcoded to 5 as fallback
        }
        UpdateBar(scoreBarP1, p1, ptw);
        UpdateBar(scoreBarP2, p2, ptw);

        // Flash the scoring player's number red briefly
        StartCoroutine(FlashScore(p1 > cachedP1 ? scoreP1Text : scoreP2Text));
    }

    private IEnumerator FlashScore(TextMeshProUGUI tmp)
    {
        Color orig = tmp.color;
        tmp.color = accentColor;
        yield return StartCoroutine(PunchScale(tmp.transform, 1.3f, 1f, 0.2f));
        tmp.color = orig;
    }

    private void UpdateBar(Image bar, int score, int max)
    {
        if (bar == null) return;
        var rt = bar.GetComponent<RectTransform>();
        float t = Mathf.Clamp01((float)score / max);
        // Animate width
        StartCoroutine(AnimateBarFill(rt, t));
    }

    private IEnumerator AnimateBarFill(RectTransform rt, float targetFill)
    {
        float elapsed = 0f;
        float start   = rt.anchorMax.x - rt.anchorMin.x;
        float range   = 0.35f; // max bar width (fraction of screen)
        float target  = range * targetFill;

        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            float w = Mathf.Lerp(start, target, elapsed / 0.3f);
            rt.anchorMax = new Vector2(rt.anchorMin.x + w, rt.anchorMax.y);
            yield return null;
        }
    }

    private void ShowServeIndicator(int servingPlayer)
    {
        if (serveIndicator == null) return;
        serveIndicator.gameObject.SetActive(true);

        // Arrow points to serving player's side
        serveIndicator.text = servingPlayer == 1
            ? "◀ SERVE"
            : "SERVE ▶";
    }

    private void HideCenter()
    {
        if (centerPanel) centerPanel.SetActive(false);
        StopCoroutine("PulseText");
        if (centerBigText) centerBigText.transform.localScale = Vector3.one;
    }

    // -------------------------------------------------------------------------
    //  UI factory helpers
    // -------------------------------------------------------------------------

    private GameObject MakePanel(string name, RectTransform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private TextMeshProUGUI MakeText(string name, RectTransform parent,
        string text, TMP_FontAsset font, float size,
        TextAlignmentOptions alignment, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.alignment = alignment;
        tmp.color     = color;
        if (font != null) tmp.font = font;
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    private Image MakeBar(string name, RectTransform parent,
        Vector2 anchorMin, Vector2 anchorMax, bool leftAlign)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = accentColor;
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = new Vector2(anchorMin.x, anchorMax.y + 0.06f); // start at 0 width
        rt.offsetMin        = new Vector2(leftAlign ? 48f : 0f, 0f);
        rt.offsetMax        = new Vector2(leftAlign ? 0f : -48f, 3f);
        rt.anchoredPosition = Vector2.zero;
        return img;
    }

    private void Stretch(RectTransform rt)
    {
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;
    }
}
