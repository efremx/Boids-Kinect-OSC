using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Central game state machine for the tennis installation.
///
/// States:
///   WaitingForServe  → ball is frozen at spawn, waiting for serve countdown
///   Rally            → ball is live, players are hitting
///   PointScored      → brief pause after a point before re-serving
///   GameOver         → someone reached pointsToWin
///
/// Scene setup:
///   1. Create an empty GameObject called "GameManager" and attach this script.
///   2. Drag the TennisBall's Rigidbody into ballRigidbody.
///   3. Create an empty Transform at the center of the court (net height) as ballSpawnPoint.
///   4. P1 is on the LEFT (negative X), P2 is on the RIGHT (positive X).
///   5. Add ScoringZone trigger volumes behind each player and on each floor half.
///   6. Wire up onScoreChanged / onGameOver events to your UI if desired.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Game Settings")]
    [SerializeField] private int pointsToWin = 5;

    [Header("Ball")]
    [SerializeField] private Rigidbody ballRigidbody;
    [SerializeField] private Transform ballSpawnPoint;

    [Header("Serve Settings")]
    [Tooltip("Pause (seconds) before each serve after a point or at game start.")]
    [SerializeField] private float serveDelay = 2.5f;
    [Tooltip("Horizontal speed of the served ball (toward the opposite player).")]
    [SerializeField] private float serveHorizontalSpeed = 6f;
    [Tooltip("Upward arc speed on serve.")]
    [SerializeField] private float serveArcSpeed = 5f;
    [Tooltip("Random vertical wobble added to each serve so they're never identical.")]
    [SerializeField] private float serveRandomOffset = 0.8f;

    [Header("Safety / Anti-Stuck")]
    [Tooltip("Ball is reset if it drops below this world Y (e.g. fell through the floor).")]
    [SerializeField] private float killPlaneY = -10f;
    [Tooltip("Ball is reset if it has been in Rally without scoring for this many seconds (0 = disabled).")]
    [SerializeField] private float rallyTimeoutSeconds = 20f;

    [Header("Read-Only State")]
    [SerializeField] private int scoreP1;
    [SerializeField] private int scoreP2;
    [SerializeField] private GameState currentState;
    [SerializeField] private int servingPlayer = 1;

    // ─────────────────────────────────────────────────────────────────────────
    //  Events  (wire these up in the Inspector or subscribe in code)
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Events")]
    /// <summary>Fires whenever scores change. Parameters: (p1Score, p2Score).</summary>
    public UnityEvent<int, int> onScoreChanged;
    /// <summary>Fires when a point is awarded. Parameter: winning player (1 or 2).</summary>
    public UnityEvent<int> onPointScored;
    /// <summary>Fires when a player wins the game. Parameter: winning player (1 or 2).</summary>
    public UnityEvent<int> onGameOver;
    /// <summary>Fires when the serve is launched.</summary>
    public UnityEvent onServe;
    /// <summary>Fires when the ball is reset to spawn (between points).</summary>
    public UnityEvent onBallReset;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public read-only accessors
    // ─────────────────────────────────────────────────────────────────────────

    public GameState CurrentState  => currentState;
    public int       ScoreP1       => scoreP1;
    public int       ScoreP2       => scoreP2;
    public int       ServingPlayer => servingPlayer;

    // ─────────────────────────────────────────────────────────────────────────
    //  Private
    // ─────────────────────────────────────────────────────────────────────────

    private float rallyStartTime;

    public enum GameState { WaitingForServe, Rally, PointScored, GameOver }

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        ResetBallToSpawn();
        StartCoroutine(ServeCountdownRoutine());
    }

    private void Update()
    {
        if (currentState != GameState.Rally) return;

        // Kill-plane: ball fell through the floor
        if (ballRigidbody != null && ballRigidbody.transform.position.y < killPlaneY)
        {
            Debug.Log("GameManager: ball fell below kill-plane, resetting.");
            RegisterPoint(GuessPlayerBehindBall());
            return;
        }

        // Timeout: rally lasted too long (stuck ball)
        if (rallyTimeoutSeconds > 0f && Time.time - rallyStartTime > rallyTimeoutSeconds)
        {
            Debug.Log("GameManager: rally timeout, resetting ball.");
            currentState = GameState.PointScored; // suppress further triggers
            StopAllCoroutines();
            StartCoroutine(ResetAndServeRoutine());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by ScoringZone when the ball enters a scoring trigger.
    /// playerWhoScored: 1 = Player 1 scored, 2 = Player 2 scored.
    /// </summary>
    public void RegisterPoint(int playerWhoScored)
    {
        if (currentState != GameState.Rally) return;

        currentState = GameState.PointScored;

        if (playerWhoScored == 1) scoreP1++;
        else                      scoreP2++;

        Debug.Log($"Point! P1={scoreP1}  P2={scoreP2}  (scored by Player {playerWhoScored})");

        onPointScored?.Invoke(playerWhoScored);
        onScoreChanged?.Invoke(scoreP1, scoreP2);

        // Check win condition
        if (scoreP1 >= pointsToWin || scoreP2 >= pointsToWin)
        {
            int winner = scoreP1 >= pointsToWin ? 1 : 2;
            currentState = GameState.GameOver;
            FreezeBall();
            onGameOver?.Invoke(winner);
            Debug.Log($"=== GAME OVER — Player {winner} wins! ===");
            return;
        }

        // Serve goes to the player who just scored
        servingPlayer = playerWhoScored;
        StopAllCoroutines();
        StartCoroutine(ResetAndServeRoutine());
    }

    /// <summary>Fully resets scores and restarts the game.</summary>
    public void RestartGame()
    {
        scoreP1      = 0;
        scoreP2      = 0;
        servingPlayer = 1;
        StopAllCoroutines();
        ResetBallToSpawn();
        onScoreChanged?.Invoke(scoreP1, scoreP2);
        StartCoroutine(ServeCountdownRoutine());
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator ServeCountdownRoutine()
    {
        currentState = GameState.WaitingForServe;
        yield return new WaitForSeconds(serveDelay);
        LaunchServe();
    }

    private IEnumerator ResetAndServeRoutine()
    {
        onBallReset?.Invoke();
        ResetBallToSpawn();
        yield return new WaitForSeconds(serveDelay);
        LaunchServe();
    }

    private void ResetBallToSpawn()
    {
        if (ballRigidbody == null) return;

        ballRigidbody.linearVelocity  = Vector3.zero;
        ballRigidbody.angularVelocity = Vector3.zero;
        ballRigidbody.isKinematic     = true;

        if (ballSpawnPoint != null)
            ballRigidbody.transform.position = ballSpawnPoint.position;
    }

    private void FreezeBall()
    {
        if (ballRigidbody == null) return;
        ballRigidbody.linearVelocity  = Vector3.zero;
        ballRigidbody.angularVelocity = Vector3.zero;
        ballRigidbody.isKinematic     = true;
    }

    private void LaunchServe()
    {
        if (ballRigidbody == null) return;

        ballRigidbody.isKinematic = false;
        currentState              = GameState.Rally;
        rallyStartTime            = Time.time;

        // P1 is on left (−X), P2 is on right (+X).
        // Serve travels toward the opponent (away from the serving player).
        float dirX = servingPlayer == 1 ? 1f : -1f;

        Vector3 serveVelocity = new Vector3(
            dirX * serveHorizontalSpeed,
            serveArcSpeed + Random.Range(-serveRandomOffset, serveRandomOffset),
            Random.Range(-serveRandomOffset * 0.3f, serveRandomOffset * 0.3f)
        );

        ballRigidbody.linearVelocity = serveVelocity;
        onServe?.Invoke();

        Debug.Log($"Serve! Player {servingPlayer} → velocity {serveVelocity:F2}");
    }

    /// <summary>
    /// Fallback when we can't tell who scored (e.g. kill-plane).
    /// Awards the point to the player whose side the ball is NOT on.
    /// </summary>
    private int GuessPlayerBehindBall()
    {
        if (ballRigidbody == null) return 1;
        // Ball on left half → P1 let it fall → P2 scores
        return ballRigidbody.transform.position.x < 0f ? 2 : 1;
    }

#if UNITY_EDITOR
    // Quick restart shortcut in the editor
    private void OnGUI()
    {
        if (currentState == GameState.GameOver)
        {
            if (GUI.Button(new Rect(Screen.width / 2f - 60, Screen.height / 2f + 60, 120, 36), "Restart"))
                RestartGame();
        }
    }
#endif
}