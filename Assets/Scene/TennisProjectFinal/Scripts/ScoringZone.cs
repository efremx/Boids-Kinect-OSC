using UnityEngine;

/// <summary>
/// Attach to a trigger collider. When the TennisBall enters this zone,
/// the specified player is awarded a point via GameManager.
///
/// Scene setup (side-view court, P1 left / P2 right):
///
///   ┌──────────────┬───────┬──────────────┐
///   │  P2 scores   │  NET  │  P1 scores   │
///   │  (left wall  │       │  (right wall │
///   │  + left      │       │  + right     │
///   │  floor)      │       │  floor)      │
///   └──────────────┴───────┴──────────────┘
///
/// You'll need four ScoringZone objects total:
///   1. Left back wall     → scoringPlayer = 2
///   2. Left floor half    → scoringPlayer = 2
///   3. Right back wall    → scoringPlayer = 1
///   4. Right floor half   → scoringPlayer = 1
///
/// Optionally add a "ceiling" kill zone (either player can use scoringPlayer=0
/// to just reset without scoring, or pick whichever side the ball came from).
/// </summary>
[RequireComponent(typeof(Collider))]
public class ScoringZone : MonoBehaviour
{
    [Tooltip("Which player scores when the ball enters this zone.\n" +
             "1 = Player 1 scores  |  2 = Player 2 scores")]
    public int scoringPlayer = 1;

    [Header("Gizmo (editor only)")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.2f, 0.2f, 0.25f);

    private void Awake()
    {
        // Make sure the collider is a trigger
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"ScoringZone on '{name}': Collider must be a Trigger. Forcing isTrigger = true.");
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("TennisBall")) return;

        if (GameManager.Instance == null)
        {
            Debug.LogWarning("ScoringZone: No GameManager found in scene.");
            return;
        }

        // Only register during an active rally
        if (GameManager.Instance.CurrentState != GameManager.GameState.Rally) return;

        GameManager.Instance.RegisterPoint(scoringPlayer);
    }

    // ── Editor visualisation ─────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = gizmoColor;
        Gizmos.matrix = transform.localToWorldMatrix;

        if (col is BoxCollider box)
        {
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Show a label only when this object is selected in the hierarchy,
        // using plain GUI so there is no UnityEditor assembly dependency issue.
        Gizmos.color = Color.white;
        Gizmos.DrawIcon(transform.position, "", false);
    }
}