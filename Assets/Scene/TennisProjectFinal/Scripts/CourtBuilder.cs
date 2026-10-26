using UnityEngine;


/// <summary>
/// Procedurally builds a placeholder tennis court so you can test game logic
/// before importing real assets. Creates:
///
///   • A coloured floor (two halves + centre service boxes)
///   • White court lines
///   • A net at centre
///   • Invisible boundary trigger walls and floor triggers → feeds into ScoringZone
///   • Invisible ceiling kill-plane
///
/// Side-view layout  (Y = up, X = left/right, Z = depth):
///
///   P1 (left, −X)  ←  net at X=0  →  P2 (right, +X)
///
/// USAGE:
///   1. Add this component to any GameObject (e.g. "Court").
///   2. Hit Play. The court is built at runtime.
///   3. Click "Build Court In Editor" context-menu item to preview in Edit mode.
///   4. When you import real court assets, disable/delete this component.
/// </summary>
[DisallowMultipleComponent]
public class CourtBuilder : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Court Dimensions")]
    [Tooltip("Total width of the court (X axis). Standard tennis doubles = 10.97 m.")]
    [SerializeField] private float courtWidth  = 11f;   // X total (each half = courtWidth/2)
    [Tooltip("Depth of the court (Z axis). Affects how wide the court looks from the side.")]
    [SerializeField] private float courtDepth  = 8f;    // Z
    [Tooltip("Net height.")]
    [SerializeField] private float netHeight   = 0.9f;
    [Tooltip("Net thickness.")]
    [SerializeField] private float netThickness = 0.05f;

    [Header("Colours")]
    [SerializeField] private Color courtColour    = new Color(0.18f, 0.45f, 0.20f);  // hard green
    [SerializeField] private Color lineColour     = Color.white;
    [SerializeField] private Color netColour      = new Color(0.85f, 0.85f, 0.85f);

    [Header("Boundary Scoring Zones")]
    [Tooltip("Extra space behind each back wall before the scoring trigger fires.")]
    [SerializeField] private float backWallOffset  = 0.5f;
    [Tooltip("Depth of the scoring trigger volumes.")]
    [SerializeField] private float triggerThickness = 1.5f;

    [Header("Ceiling Kill-Plane")]
    [SerializeField] private float ceilingHeight = 15f;

    [Header("Auto-Build")]
    [SerializeField] private bool buildOnStart = true;

    // ─────────────────────────────────────────────────────────────────────────
    //  Runtime
    // ─────────────────────────────────────────────────────────────────────────

    private Transform courtRoot;

    private void Start()
    {
        if (buildOnStart) Build();
    }

    /// <summary>Call this to (re)build the court at any time.</summary>
    public void Build()
    {
        // Tear down any previously built court
        if (courtRoot != null)
            DestroyImmediate(courtRoot.gameObject);

        courtRoot = new GameObject("CourtGeometry").transform;
        courtRoot.SetParent(transform, false);

        BuildFloor();
        BuildLines();
        BuildNet();
        BuildScoringZones();
        BuildCeilingKillPlane();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Floor  (flat Cube — scale.x = width, scale.y = thickness, scale.z = depth)
    // ─────────────────────────────────────────────────────────────────────────

    private const float FloorThickness = 0.1f;

    private void BuildFloor()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(courtRoot, false);
        // Sit the top surface exactly at Y=0
        floor.transform.localPosition = new Vector3(0f, -FloorThickness / 2f, 0f);
        floor.transform.localScale    = new Vector3(courtWidth, FloorThickness, courtDepth);
        SetColour(floor.transform, courtColour);

        // The Cube already has a BoxCollider — just add physics material
        var col = floor.GetComponent<BoxCollider>();
        var pm  = new PhysicsMaterial("CourtSurface");
        pm.bounciness      = 0.75f;
        pm.bounceCombine   = PhysicsMaterialCombine.Maximum;
        pm.dynamicFriction = 0.2f;   // low friction — don't scrub horizontal speed
        pm.staticFriction  = 0.2f;
        pm.frictionCombine = PhysicsMaterialCombine.Minimum;
        col.material       = pm;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Lines  (thin Cubes sitting flush on top of the floor)
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildLines()
    {
        const float lw = 0.05f;    // line width (narrow axis)
        const float lh = 0.005f;   // line height (just proud of the floor)
        float hw = courtWidth / 2f;
        float hd = courtDepth / 2f;
        float sy = lh / 2f;        // centre the cube so its bottom sits at Y=0

        // Baselines (run along Z, positioned at each end)
        MakeLine("Baseline_P1",   new Vector3(-hw,           sy, 0f), new Vector3(lw, lh, courtDepth));
        MakeLine("Baseline_P2",   new Vector3( hw,           sy, 0f), new Vector3(lw, lh, courtDepth));
        // Sidelines (run along X, at front and back)
        MakeLine("Sideline_Front",new Vector3(0f,            sy,  hd), new Vector3(courtWidth, lh, lw));
        MakeLine("Sideline_Back", new Vector3(0f,            sy, -hd), new Vector3(courtWidth, lh, lw));
        // Service lines (parallel to baselines, halfway each side)
        float slx = hw / 2f;
        MakeLine("ServiceLine_P1",new Vector3(-slx,          sy, 0f), new Vector3(lw, lh, courtDepth));
        MakeLine("ServiceLine_P2",new Vector3( slx,          sy, 0f), new Vector3(lw, lh, courtDepth));
        // Centre line
        MakeLine("CentreLine",    new Vector3(0f,            sy, 0f), new Vector3(lw, lh, courtDepth));
    }

    private void MakeLine(string lineName, Vector3 position, Vector3 scale)
    {
        var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = lineName;
        line.transform.SetParent(courtRoot, false);
        line.transform.localPosition = position;
        line.transform.localScale    = scale;
        SetColour(line.transform, lineColour);

        // Lines don't need to interact with physics
        var col = line.GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Net
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildNet()
    {
        var net = GameObject.CreatePrimitive(PrimitiveType.Cube);
        net.name = "Net";
        net.transform.SetParent(courtRoot, false);
        net.transform.localPosition = new Vector3(0f, netHeight / 2f, 0f);
        net.transform.localScale    = new Vector3(netThickness, netHeight, courtDepth);

        SetColour(net.transform, netColour);

        // Net is a solid (non-trigger) collider – the ball should not pass through it
        // (the existing BoxCollider from CreatePrimitive handles this)
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Scoring zones
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildScoringZones()
    {
        float hw = courtWidth / 2f;
        float hd = courtDepth / 2f;
        float tt = triggerThickness;

        // ── Back walls ──────────────────────────────────────────────────────
        // P1 back wall (ball leaves left → P2 scores)
        MakeScoringZone("BackWall_P1side", scoringPlayer: 2,
            center: new Vector3(-(hw + backWallOffset + tt / 2f), netHeight, 0f),
            size:   new Vector3(tt, netHeight * 3f, courtDepth * 2f));

        // P2 back wall (ball leaves right → P1 scores)
        MakeScoringZone("BackWall_P2side", scoringPlayer: 1,
            center: new Vector3( (hw + backWallOffset + tt / 2f), netHeight, 0f),
            size:   new Vector3(tt, netHeight * 3f, courtDepth * 2f));

        // ── Floor halves ────────────────────────────────────────────────────
        // Ball bounces on P1's side without being returned → P2 scores
        MakeScoringZone("Floor_P1side", scoringPlayer: 2,
            center: new Vector3(-(hw / 2f), -0.5f, 0f),
            size:   new Vector3(hw, 1f, courtDepth));

        // Ball bounces on P2's side without being returned → P1 scores
        MakeScoringZone("Floor_P2side", scoringPlayer: 1,
            center: new Vector3( (hw / 2f), -0.5f, 0f),
            size:   new Vector3(hw, 1f, courtDepth));

        // ── Out-of-bounds sides (Z axis) ────────────────────────────────────
        // Ball goes into the "audience" on P1's side
        MakeScoringZone("OutZ_P1side_Front", scoringPlayer: 2,
            center: new Vector3(-(hw / 2f), netHeight, hd + tt / 2f),
            size:   new Vector3(hw, netHeight * 3f, tt));
        MakeScoringZone("OutZ_P1side_Back", scoringPlayer: 2,
            center: new Vector3(-(hw / 2f), netHeight, -(hd + tt / 2f)),
            size:   new Vector3(hw, netHeight * 3f, tt));
        MakeScoringZone("OutZ_P2side_Front", scoringPlayer: 1,
            center: new Vector3( (hw / 2f), netHeight, hd + tt / 2f),
            size:   new Vector3(hw, netHeight * 3f, tt));
        MakeScoringZone("OutZ_P2side_Back", scoringPlayer: 1,
            center: new Vector3( (hw / 2f), netHeight, -(hd + tt / 2f)),
            size:   new Vector3(hw, netHeight * 3f, tt));
    }

    private void MakeScoringZone(string zoneName, int scoringPlayer, Vector3 center, Vector3 size)
    {
        var go = new GameObject(zoneName);
        go.transform.SetParent(courtRoot, false);
        go.transform.localPosition = center;

        var col   = go.AddComponent<BoxCollider>();
        col.size  = size;
        col.isTrigger = true;

        var zone = go.AddComponent<ScoringZone>();
        zone.scoringPlayer = scoringPlayer;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Ceiling kill-plane (resets ball if it flies too high)
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildCeilingKillPlane()
    {
        // We don't need a ScoringZone here – GameManager's killPlaneY handles
        // balls that fall through the floor. The ceiling just reflects the ball
        // back with a solid collider (so it doesn't vanish upward).
        var ceiling = new GameObject("CeilingWall");
        ceiling.transform.SetParent(courtRoot, false);
        ceiling.transform.localPosition = new Vector3(0f, ceilingHeight, 0f);

        var col  = ceiling.AddComponent<BoxCollider>();
        col.size = new Vector3(courtWidth * 3f, 0.2f, courtDepth * 3f);

        // Bouncy physics material so ball comes back down
        var pm = new PhysicsMaterial("CeilingMat");
        pm.bounciness    = 0.4f;
        pm.bounceCombine = PhysicsMaterialCombine.Maximum;
        col.material     = pm;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static void SetColour(Transform t, Color colour)
    {
        var rend = t.GetComponent<Renderer>();
        if (rend == null) return;

        // Instantiate a new material so we don't modify the shared default
        var mat = new Material(rend.sharedMaterial != null ? rend.sharedMaterial : Shader.Find("Standard") ? new Material(Shader.Find("Standard")) : rend.sharedMaterial);
        mat.color = colour;
        rend.material = mat;
    }

#if UNITY_EDITOR
    [ContextMenu("Build Court In Editor")]
    private void BuildInEditor()
    {
        Build();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
}