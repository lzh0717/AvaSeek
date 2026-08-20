using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class IVARenderer : MonoBehaviour
{
    [Range(0f, 1f)] public float face_width = 0.5f;
    [Range(0f, 1f)] public float face_height = 0.5f;
    [Range(3f, 100f)] public float face_sides = 100f;
    [Range(0f, 1f)] public float temple_width    = 0.5f;
    [Range(0f, 1f)] public float cheekbone_width = 0.5f;
    [Range(0f, 1f)] public float jaw_width       = 0.5f;
    [Range(0f, 1f)] public float eye_width = 0.15f;
    [Range(0f, 1f)] public float eye_height = 0.08f;
    [Range(0f, 1f)] public float eye_spacing = 0.3f;
    [Range(0f, 1f)] public float eye_position_y = 0.6f;
    [Range(0f, 1f)] public float eye_inner_curve = 0.5f;
    [Range(0f, 1f)] public float eye_outer_curve = 0.5f;
    [Range(0f, 1f)] public float eye_tilt = 0.5f;
    [Range(0f, 1f)] public float eye_roundness = 0f;
    [Range(0f, 1f)] public float pupil_size = 0f;
    [Range(0f, 1f)] public float mouth_width = 0.3f;
    [Range(0f, 1f)] public float mouth_curve = 0.5f;
    [Range(0f, 1f)] public float mouth_position_y = 0.3f;
    [Range(0f, 1f)] public float ear_width = 0.1f;
    [Range(0f, 1f)] public float ear_height = 0.2f;
    [Range(0f, 1f)] public float ear_curve = 1f;
    [Range(0f, 1f)] public float ear_position_y = 0.5f;

    [Header("Style")]
    [Range(0f, 1f)] public float stroke_width   = 0.2f;
    [Range(0f, 1f)] public float wave_amplitude = 0f;
    [Range(0f, 3f)] public float wave_speed     = 1f;
    [Range(0f, 1f)] public float glow_intensity = 0f;

    [Header("Colour (continuous MOBO design parameters)")]
    // The avatar's colour is a continuous 3-parameter space, so a MOBO sweep can move smoothly
    // from plain WHITE → one stable vivid colour → the full flowing Siri rainbow. There is no
    // boolean colour switch any more: every stroke always takes the gradient path (see
    // ApplyLineColor), because color_spread=0 already collapses the gradient to one flat colour
    // and color_saturation=0 makes that colour white.
    [Tooltip("0 = pure WHITE (whatever the hue/spread are), 1 = fully saturated.")]
    [Range(0f, 1f)] public float color_saturation = 1f;
    [Tooltip("0 = ONE uniform, time-STABLE colour (no scroll/shimmer). 1 = full rainbow gradient flowing around the stroke.")]
    [Range(0f, 1f)] public float color_spread     = 1f;
    [Tooltip("Rotates the palette around the hue wheel; at low color_spread this picks the single colour.")]
    [Range(0f, 1f)] public float color_hue        = 0f;

    [Header("Siri Wave (when form base is flat)")]
    // The authentic ios9 Siri waveform (faithful port of kopiro/siriwave), rendered
    // as additive colored mesh lobes. It is the form base's DEGENERATE state, not a
    // separate element: when the base outline collapses to a flat horizontal form
    // (face_height≈0) and still has a width (face_width>0), the renderer draws THIS
    // wave INSTEAD of the face — the two are mutually exclusive, never both at once.
    // Its horizontal span is read from face_width; energy/speed reuse the Style
    // fields wave_amplitude/wave_speed (amplitude floors so width alone shows a wave).
    [Range(0f, 1f)] public float wave_height      = 1f;   // peak-to-peak (world units); range trimmed 2→1 to match the BO bound (formula response unchanged)

    // ── Fixed internal values (were Inspector knobs; now frozen & hidden) ────────
    // Removed from the Inspector to declutter it. static readonly (not const) for the gate
    // values so the wave/ring code stays live instead of tripping unreachable-code warnings.
    // NOTE: the avatar's colour is no longer a boolean — it is driven by the three continuous
    // color_saturation / color_spread / color_hue design parameters above.
    static readonly bool  faceOutlineWave  = false; // outline stays still: no ripple, no wave rings
    static readonly int   wave_rings       = 0;     // no extra orbiting wave loops
    const           float color_flow_speed = 1f;    // colour-flow scroll speed (no effect at color_spread = 0)
    const           float ring_spacing     = 0.14f; // only relevant when wave_rings > 0
    const           float wave_layer_alpha = 1f;    // per-lobe alpha, only used in full Siri-wave mode

    [System.Serializable]
    public struct WaveCurveDef { public Color color; public bool supportLine; }

    // Library's default ios9 definition: white support line, then blue / red / green.
    public WaveCurveDef[] wave_definitions =
    {
        new WaveCurveDef { color = new Color(1f, 1f, 1f), supportLine = true },
        new WaveCurveDef { color = new Color(15f/255f,  82f/255f, 169f/255f) }, // blue
        new WaveCurveDef { color = new Color(173f/255f, 57f/255f,  76f/255f) }, // red
        new WaveCurveDef { color = new Color(48f/255f, 220f/255f, 155f/255f) }, // green
    };

    [Header("Rendering")]
    public int segments = 64;
    public Color lineColor = Color.cyan;

    [Header("Auto-fit to size_restriction frame")]
    // When on (Play mode), the avatar auto-scales UNIFORMLY to the largest size that still
    // fits inside an invisible reference frame called "size_restriction", whose height is
    // capped to dashboard_02's height. This solves "the avatar is too small": it grows to
    // fill the dashboard slot and adapts as its shape changes each BO iteration. The frame is
    // a pure scale guide — a GameObject named "size_restriction" is created next to the avatar
    // but its renderer is disabled, so it is never seen in Play mode.
    public bool  autoFitToSizeFrame  = true;
    public string dashboardObjectName = "dashboard_02"; // its world-bounds height caps the frame
    public string sizeFrameName       = "size_restriction";
    [Range(0.05f, 1f)] public float frameHeightFraction = 0.8f; // frame height as a fraction of dashboard_02's height (headroom so glow/ears/wave stay inside)
    [Range(0.2f, 4f)]  public float frameAspect         = 0.8f; // frame width / height

    Transform    _sizeFrame;      // the created "size_restriction" reference frame (DontSave)
    LineRenderer _sizeFrameLine;  // its (disabled) wire box
    Renderer     _dashRendererCache; // cached so we don't GameObject.Find() every frame in the huge scene
    float        _dashNextSearchTime; // throttle: retry Find() (~2x/s) until the dashboard is located, then cache forever
    readonly List<LineRenderer> _fitLines = new List<LineRenderer>(); // reused each frame — no GC

    // Modern Siri gradient stops, sampled cyclically so the flow loops seamlessly.
    static readonly Color[] SiriPalette =
    {
        new Color(1.00f, 0.29f, 0.58f), // pink / magenta
        new Color(0.61f, 0.35f, 0.98f), // purple
        new Color(0.26f, 0.55f, 1.00f), // blue
        new Color(0.20f, 0.92f, 0.86f), // teal
    };

    const int MaxRings = 4;
    readonly LineRenderer[] waveRingLines = new LineRenderer[MaxRings];

    // Reused each frame so the animated gradient doesn't allocate per line.
    readonly Gradient           _grad = new Gradient();
    readonly GradientColorKey[] _ck   = new GradientColorKey[8];
    readonly GradientAlphaKey[] _ak   = new GradientAlphaKey[2];

    LineRenderer faceLine;
    LineRenderer leftEyeLine;
    LineRenderer rightEyeLine;
    LineRenderer leftPupilLine;
    LineRenderer rightPupilLine;
    LineRenderer mouthLine;
    LineRenderer leftEarLine;
    LineRenderer rightEarLine;

    // ── ios9 Siri-wave state (integrated; driven by the wave_* fields) ────────
    // Constants and ranges are verbatim from kopiro/siriwave (src/ios9-curve.ts).
    const float WAVE_GRAPH_X      = 25f;
    const float WAVE_AMP_FACTOR   = 0.8f;
    const float WAVE_SPEED_FACTOR = 1f;
    const float WAVE_DEAD_PX      = 2f;
    const float WAVE_ATT_FACTOR   = 4f;
    const float WAVE_DESPAWN      = 0.02f;
    const float WAVE_PIXEL_STEP   = 0.1f;
    const float WAVE_FLAT_EPS     = 0.02f; // face_height at/below this = "form base is flat"
    const float WAVE_OUTLINE_AMP  = 0.22f; // face-outline ripple strength at wave_amplitude=1
    const float WAVE_HEIGHT_FLOOR = 0.30f; // WaveHeightMax at wave_height→0+ (already clearly visible; empty only at exactly 0)
    const float WAVE_HEIGHT_CAP   = 0.70f; // WaveHeightMax at wave_height=2 (top of the [0,2] range)
    const int   GLOW_LAYERS       = 3;     // concentric bloom copies for a soft glow halo
    const float GLOW_SCALE        = 0.06f; // glow_intensity keeps range [0,1] but 1 maps to a much gentler halo (raise→stronger, lower→subtler)

    static readonly Vector2 WAVE_NOOF       = new Vector2(2f, 5f);
    static readonly Vector2 WAVE_AMP        = new Vector2(0.3f, 1f);
    static readonly Vector2 WAVE_OFF        = new Vector2(-3f, 3f);
    static readonly Vector2 WAVE_WID        = new Vector2(1f, 3f);
    static readonly Vector2 WAVE_SPD        = new Vector2(0.5f, 1f);
    static readonly Vector2 WAVE_DESPAWN_MS = new Vector2(500f, 2000f);

    class WaveGroup
    {
        public int   noOfCurves;
        public float spawnAt, prevMaxY;
        public float[] phases, amplitudes, finalAmplitudes, offsets, speeds, widths, verses, despawn;
        public Mesh  mesh;
    }

    WaveGroup[] waveGroups;
    float _lastWaveTime;
    readonly List<Mesh>     _waveMeshes    = new List<Mesh>();
    readonly List<Material> _waveMaterials = new List<Material>();
    Material _lineMaterial; // one shared vertex-color material for every stroke + glow layer
    readonly List<Vector3>  _wv = new List<Vector3>();
    readonly List<Color>    _wc = new List<Color>();
    readonly List<int>      _wt = new List<int>();
    readonly List<Vector3>  _eyePts = new List<Vector3>(64); // reused per-eye outline buffer (drives both the draw and the pupil fit)

    // Wave energy/speed reuse the Style fields so one "voice" drives it. Amplitude
    // floors at 0.6 so a flat form with only a width already shows a live wave.
    // 0 stays empty; any wave_height>0 jumps to WAVE_HEIGHT_FLOOR and ramps up with a steep sqrt
    // curve so even small values are already tall/visible. (Old map was linear ×0.5, which
    // collapsed small wave_height to an invisible flat line.)
    float WaveHeightMax  => wave_height > 0f
        ? Mathf.Lerp(WAVE_HEIGHT_FLOOR, WAVE_HEIGHT_CAP, Mathf.Sqrt(Mathf.Clamp01(wave_height * 0.5f)))
        : 0f;
    float WaveAmp        => 0.6f + wave_amplitude * 2.4f;   // 0.6 .. 3.0
    float WavePhaseSpeed => wave_speed * 0.2f;              // wave_speed 1 ≈ lib default 0.2
    float WaveSpan       => 2f * face_width;                // matches the flat outline extent

    // Wave mode ⟺ the form base has flattened to a horizontal line but still has a
    // width. In this state every facial feature is already collapsed (their heights
    // scale with face_height), so face_height≈0 is a sufficient, robust trigger.
    bool IsWaveMode() => face_width > 0f && face_height <= WAVE_FLAT_EPS;

    // ── Feature layout safety limits ───────────────────────────────────────────
    // Draw-time clamps that guarantee the eyes never overlap each other or the
    // mouth, and never spill outside the face outline. Like the blink/smile
    // layer below, they adjust only the value used for THIS frame's draw call — the
    // BO-assigned fields are never written, so the optimization log stays truthful.
    // eye_width keeps its BO range [0,1], but 1 now maps to a deliberately SMALL eye:
    // rx = eye_width × face_width × EYE_WIDTH_SCALE. Lower this to shrink eyes further,
    // raise it (≤ ~0.42) to enlarge — the whole range stays a clean 1:1 map to size.
    const float EYE_WIDTH_SCALE  = 0.40f;
    // eye_height keeps its BO range [0,1], but 1 now maps to an eye whose vertical radius is
    // 2/3 of the face's half-height — i.e. the eye's max height never exceeds 2/3 of the face
    // length. Lower to shrink further; the whole [0,1] range stays a clean 1:1 map to height.
    const float EYE_HEIGHT_SCALE = 2f / 3f;
    const float EYE_V_EXTENT     = 1.45f; // drawn eye's cubic-bezier bulges to at most √2×ry past its centre; 1.45 ≥ √2 keeps it provably inside the face
    const float EYE_MAX_FRAC     = 0.42f; // hard safety ceiling: eye half-width ≤ this × face half-width at the eye's height
    const float FACE_EDGE_MARGIN = 0.03f; // keep the eye this far (× face_width) inside the outline
    const float CENTER_GAP_MIN   = 0.06f; // min half-gap (× face_width) kept clear on the center column
    const float FEATURE_GAP      = 0.03f; // min vertical gap (× face_height) between stacked features
    const float FACE_USABLE_FRAC = 0.85f; // features stay within ±this × face_height (never poke through the crown/chin)
    // mouth_height was DELETED (redundant — it only scaled the smile arc's depth, the same job
    // mouth_curve already does). The arc's vertical scale is frozen here at the value IVA_Avatar
    // last used, so the look is unchanged and mouth_curve is the sole smile control.
    const float MOUTH_HEIGHT     = 0.368f;
    // mouth_width=1 used to make the mouth span the whole face half-width (too wide, and it touched
    // the sides). Cap it: mouthRx = mouth_width × face_width × MOUTH_WIDTH_SCALE, and clamp with a
    // clear MOUTH_EDGE_MARGIN so the mouth never reaches the face outline.
    const float MOUTH_WIDTH_SCALE = 0.6f;
    const float MOUTH_EDGE_MARGIN = 0.06f;
    // Pupil radius = pupil_size × (eye's inscribed radius) × PUPIL_SAFETY, centred on the eye's
    // outline centroid — so the pupil is provably inside the eye line for ANY tilt / inner-outer
    // curve and never touches it.
    const float PUPIL_SAFETY      = 0.85f;
    // temple_width / cheekbone_width / jaw_width reshape the outline per vertical region.
    // That only reads as a face when the outline has enough segments to render the widening
    // smoothly; on a coarse polygon it just skews the flat edges. This used to require
    // EXACTLY face_sides == 100, which silently turned those three params into no-ops at
    // every other side count. Now: active whenever face_sides > REGION_SCALE_MIN_SIDES.
    // Single source of truth — the draw, the half-width measure, the wave ring and the
    // containment maths must never disagree about whether region scaling is on.
    const int REGION_SCALE_MIN_SIDES = 30;

    // ── Blink / smile expression animation ─────────────────────────────────────
    // Every public field above is a legitimate Bayesian-optimization design
    // parameter — the BO backend may be reading/writing any of them as part of the
    // current proposed design. So this layer NEVER writes into eye_height,
    // mouth_curve, or any other public field; it only nudges the value used for
    // THIS frame's draw call, then relaxes back. The BO-assigned baseline stays
    // exactly what BO set it to, no matter what the face is doing on screen.
    static readonly Vector2 BLINK_INTERVAL = new Vector2(2.5f, 6f);  // seconds between blinks
    const float BLINK_DURATION = 0.5f;                               // seconds, full close-and-open (slow enough to read the arc)
    // Idle-smile animation REMOVED: the mouth is driven ONLY by the BO mouth_curve and never
    // animates. (Blink still animates the eyes; that is a separate, eyes-only motion.)

    float _nextBlinkAt = -1f, _blinkStartedAt = -10f;
    float _blinkAmount; // 0..1, recomputed every Redraw()

    // Triangular pulse eased on both slopes: 0 at start/end, 1 at the midpoint.
    static float Pulse01(float elapsed, float duration)
    {
        if (elapsed < 0f || elapsed > duration) return 0f;
        float t = elapsed / duration;
        float x = t < 0.5f ? t * 2f : (1f - t) * 2f;
        return Mathf.SmoothStep(0f, 1f, x);
    }

    void UpdateExpression()
    {
        float t = WaveTime();
        if (_nextBlinkAt < 0f) _nextBlinkAt = t + Random.Range(BLINK_INTERVAL.x, BLINK_INTERVAL.y);

        if (t >= _nextBlinkAt)
        {
            _blinkStartedAt = t;
            _nextBlinkAt = t + Random.Range(BLINK_INTERVAL.x, BLINK_INTERVAL.y);
        }

        _blinkAmount = Pulse01(t - _blinkStartedAt, BLINK_DURATION);
    }

#if UNITY_EDITOR
    void OnEnable()
    {
        InitLines();
        UnityEditor.EditorApplication.update += EditorTick;
    }

    void OnDisable()
    {
        UnityEditor.EditorApplication.update -= EditorTick;
        Cleanup();
    }

    void OnValidate()
    {
        face_sides   = Mathf.Round(face_sides);
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            InitLines();
            Redraw();
        };
    }

    // Keeps the animated waveform (and the always-on blink/smile idle motion)
    // ticking while the editor is not in Play mode. (During Play mode the normal
    // player-loop Update handles it.)
    double _nextEditorTick;
    void EditorTick()
    {
        if (this == null || Application.isPlaying) return;
        // Throttle to ~20 fps. EditorApplication.update fires 100+ times/sec, and each
        // SceneView.RepaintAll() re-renders the ENTIRE ~500 MB scene — that constant churn
        // spikes editor memory and load, which is what makes GPU scene-view picking crash
        // (native SIGSEGV in Internal_GetClosestPickingID) under memory pressure. 20 fps keeps
        // the preview animating while cutting the idle repaint cost by ~5x+.
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now < _nextEditorTick) return;
        _nextEditorTick = now + 0.05;
        Redraw();
        UnityEditor.SceneView.RepaintAll();
    }
#else
    void OnEnable() => InitLines();
    void OnDisable() => Cleanup();
#endif

    void OnDestroy() => Cleanup();

    void Update() => Redraw();

    // Clock that advances both in Play mode and while previewing in the editor,
    // so the Siri waveform animates in either context.
    float WaveTime()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return (float)UnityEditor.EditorApplication.timeSinceStartup;
#endif
        return Time.time;
    }

    void InitLines()
    {
        faceLine       = GetOrCreateLine("Face");
        leftEyeLine    = GetOrCreateLine("LeftEye");
        rightEyeLine   = GetOrCreateLine("RightEye");
        leftPupilLine  = GetOrCreateLine("LeftPupil");
        rightPupilLine = GetOrCreateLine("RightPupil");
        mouthLine      = GetOrCreateLine("Mouth");
        leftEarLine    = GetOrCreateLine("LeftEar");  leftEarLine.loop  = false;
        rightEarLine   = GetOrCreateLine("RightEar"); rightEarLine.loop = false;
        for (int i = 0; i < MaxRings; i++)
            waveRingLines[i] = GetOrCreateLine("WaveRing" + i);
    }

    void Redraw()
    {
        if (faceLine == null || leftEyeLine == null || rightEyeLine == null ||
            leftPupilLine == null || rightPupilLine == null ||
            mouthLine == null ||
            leftEarLine == null || rightEarLine == null)
            InitLines();

        // Siri-wave mode: the form base has collapsed to a flat horizontal outline
        // with a width — draw the authentic ios9 wave INSTEAD of the face. Mutually
        // exclusive: never the face and the wave at the same time.
        if (IsWaveMode())
        {
            HideFaceLines();
            UpdateSiriWave();
            // Auto-fit runs in wave mode too (the normal path RETURNs before the FitToSizeFrame()
            // call at the end of Redraw, which is why the wave used to stay stuck at the last
            // face-mode / authored scale, i.e. far too small). FitToSizeFrame sizes the wave
            // analytically so it fills the size_restriction frame.
            if (autoFitToSizeFrame && Application.isPlaying) FitToSizeFrame();
            return;
        }
        ClearWaveMeshes();
        UpdateExpression();

        DrawFace(faceLine, face_width, face_height, Mathf.RoundToInt(face_sides));

        // ── Feature geometry, raw as proposed by BO ─────────────────────────────
        float eyeY    = (eye_position_y   - 0.5f) * face_height;
        float mouthY  = (mouth_position_y - 0.5f) * face_height;
        // Full (un-blinked) eye half-height. ALL layout/clamp/mouth math below uses this, so a
        // blink NEVER moves the eye position, eye width, mouth position/width, or face-width
        // reservations. The blink is applied later as ryDraw, used ONLY for the drawn eye + pupil.
        float ry      = eye_height   * face_height * EYE_HEIGHT_SCALE;
        float mouthRx = mouth_width  * face_width * MOUTH_WIDTH_SCALE; // scaled down so mouth_width=1 isn't over-wide
        float mouthRy = MOUTH_HEIGHT * face_height;   // mouth_height deleted — arc scale frozen (see MOUTH_HEIGHT)
        // Mouth is kept STILL — no idle-smile animation. The drawn curve is exactly the
        // BO-assigned mouth_curve.
        // mouth_curve: 0 = flat, 1 = a moderate DOWNWARD (smile) arc — the middle of the mouth
        // NEVER bulges up. DrawMouth reads curvature = (curve-0.5)*2 (0.5 = flat, <0.5 = middle
        // dips down), so remap [0,1] -> [0.5, 0.2]. The 0.3 factor caps the deepest smile at
        // curvature -0.6 so mouth_curve = 1 stays reasonable, not exaggerated.
        float smileCurve = 0.5f - 0.3f * Mathf.Clamp01(mouth_curve);

        // ── Safety clamps (draw-time only — BO fields untouched, log stays honest) ─
        // Guarantee, whatever BO proposes: eyes above the mouth, they never overlap, and
        // every feature stays inside the face outline — sides, top, bottom, AND the flat
        // edges of a low-face_sides polygon / wavy outline (FaceHalfWidthAt already folds
        // in the polygon factor; the vertical range does the same below). Only the DRAWN
        // values change; the BO fields (and the CSV log) stay exactly what BO proposed.
        float polyFactor = FacePolyFactor();               // < 1 for a few-sided / wavy outline
        float gap      =  face_height * FEATURE_GAP;
        float topY     =  face_height * FACE_USABLE_FRAC * polyFactor;   // highest a feature may reach
        float botY     = -face_height * FACE_USABLE_FRAC * polyFactor;   // lowest a feature may reach
        float halfSpan = (topY - botY) * 0.5f;

        // EYES — the top feature. Keep the whole eye inside the face vertically.
        // The drawn eye's cubic-bezier top/bottom bulge PAST the eye centre by up to ~1.33×ry
        // (worst-case tilt/curve at eye_roundness=0), not just ry — so clamp against that true
        // half-extent, otherwise an eye placed high (eye_position_y≈1) pokes through the crown.
        ry   = Mathf.Min(ry, halfSpan / EYE_V_EXTENT);
        float eyeVExt = ry * EYE_V_EXTENT;
        eyeY = Mathf.Clamp(eyeY, botY + eyeVExt, topY - eyeVExt);
        // Narrowest face half-width across the eye's height, so the eye's corners fit.
        float faceHalfEye = Mathf.Min(FaceHalfWidthAt(eyeY - ry), FaceHalfWidthAt(eyeY + ry));
        // Reserve a FIXED center column so the two eyes never touch each other. This is
        // deliberately INDEPENDENT of mouth_width: the mouth sits below the eyes with its own
        // vertical gap, so widening the mouth must not shove the eyes apart. Eye spacing is
        // driven only by eye_spacing + face geometry.
        float centerGap  = face_width * CENTER_GAP_MIN;
        float outerLimit = faceHalfEye - face_width * FACE_EDGE_MARGIN;
        // eye_width=1 maps to a deliberately small eye (EYE_WIDTH_SCALE); then two hard
        // limits: never exceed EYE_MAX_FRAC of the face, and always leave room for both
        // eyes + the center gap inside the outline.
        float rx = eye_width * face_width * EYE_WIDTH_SCALE;
        rx = Mathf.Min(rx, EYE_MAX_FRAC * faceHalfEye);
        rx = Mathf.Min(rx, Mathf.Max(0f, (outerLimit - centerGap) * 0.5f));
        rx = Mathf.Max(0f, rx);
        // eye_spacing spans the whole valid band [centerGap+rx, outerLimit-rx], so it
        // always has a visible effect (0 = eyes in near the center, 1 = out at the edge).
        float loX = centerGap + rx, hiX = outerLimit - rx;
        float eyeX = (hiX >= loX) ? Mathf.Lerp(loX, hiX, Mathf.Clamp01(eye_spacing))
                                  : Mathf.Max(0f, (centerGap + outerLimit) * 0.5f);

        // MOUTH — kept below the eyes and inside the face. The curve rises by mouthUp above
        // its center line (smile) or drops by mouthDown below it (frown).
        float mouthUp   = Mathf.Max(0f, (smileCurve - 0.5f) * 2f) * mouthRy;
        float mouthDown = Mathf.Max(0f, (0.5f - smileCurve) * 2f) * mouthRy;
        // Keep the mouth's whole arc below the eyes' TRUE bottom — eyeY - eyeVExt, i.e. including
        // the cubic-bezier bulge, NOT just eyeY - ry — so the mouth can never overlap the eye.
        float mouthHi = (eyeY - eyeVExt) - gap - mouthUp;   // arc top stays under the eyes' true bottom
        float mouthLo = botY + mouthDown;              // arc bottom stays in the face
        mouthY = (mouthHi >= mouthLo) ? Mathf.Clamp(mouthY, mouthLo, mouthHi) : (mouthHi + mouthLo) * 0.5f;
        float faceHalfMouth = Mathf.Min(FaceHalfWidthAt(mouthY - mouthDown), FaceHalfWidthAt(mouthY + mouthUp))
                              - face_width * MOUTH_EDGE_MARGIN;   // wider margin so the mouth never touches the outline
        mouthRx = Mathf.Clamp(mouthRx, 0f, Mathf.Max(0f, faceHalfMouth));

        // Blink applied HERE ONLY: shrinks the drawn eye radius toward a flat closed lid. All the
        // layout/mouth math above used the full ry, so nothing else moves when the eyes blink.
        float ryDraw = ry * (1f - _blinkAmount);

        // Left eye: inner corner is on the right (toward nose), mirrored = false
        // Right eye: inner corner is on the left (toward nose), mirrored = true
        // eye_height == 0 OR eye_width == 0 ⇒ NO eyes at all (BO may propose either): hide both
        // eyes + pupils instead of drawing a degenerate flat/vertical line. Guards the BO values,
        // not the blink-driven ryDraw, so a blink (which drives ryDraw→0 for a moment) still shows
        // the eyes.
        if (eye_height <= 0f || eye_width <= 0f)
        {
            leftEyeLine.positionCount    = 0;
            rightEyeLine.positionCount   = 0;
            leftPupilLine.positionCount  = 0;
            rightPupilLine.positionCount = 0;
        }
        else
        {
            DrawEye(leftEyeLine,  new Vector3(-eyeX, eyeY, 0f), rx, ryDraw, false, out Vector3 pcL, out float prMaxL);
            DrawEye(rightEyeLine, new Vector3( eyeX, eyeY, 0f), rx, ryDraw, true,  out Vector3 pcR, out float prMaxR);

            // Pupil sits at each eye's centroid and scales pupil_size over the eye's inscribed
            // radius (× PUPIL_SAFETY), so it stays inside the eye line for any tilt / inner-outer curve.
            float prL = pupil_size * prMaxL * PUPIL_SAFETY;
            float prR = pupil_size * prMaxR * PUPIL_SAFETY;
            DrawEllipse(leftPupilLine,  pcL, prL, prL);
            DrawEllipse(rightPupilLine, pcR, prR, prR);
        }

        DrawMouth(mouthLine, new Vector3(0f, mouthY, 0f), mouthRx, mouthRy, smileCurve);

        // Ears span [center-halfSpread, center+halfSpread] and the two are mirror images across
        // x=0. Requiring center-halfSpread >= 90°+gap keeps the whole left ear in x<=0 and the
        // right in x>=0, so they can NEVER overlap. Wide ears therefore can't climb as high toward
        // the crown — exactly the case that used to collide.
        // Ears are HIDDEN when either:
        //  - the outline is ACTUALLY rippling (faceOutlineWave on AND wave_amplitude > 0), or
        //  - the face is a coarse polygon (face_sides <= 8), where ears anchored to the flat
        //    edges look wrong.
        // Otherwise (a still, rounder face) ears show normally. (face_height→0 full wave-mode
        // already hides ears via HideFaceLines().)
        if ((faceOutlineWave && wave_amplitude > 0f) || Mathf.RoundToInt(face_sides) <= 8)
        {
            leftEarLine.positionCount = 0;
            rightEarLine.positionCount = 0;
        }
        else
        {
            const float EAR_MIN_GAP_DEG = 4f;
            float halfSpreadDeg = ear_width * 45f;
            float topDeg        = Mathf.Min(180f, 90f + halfSpreadDeg + EAR_MIN_GAP_DEG);
            float leftCenterDeg = Mathf.Lerp(180f, topDeg, ear_position_y);
            DrawEar(leftEarLine,  leftCenterDeg, halfSpreadDeg, false);
            DrawEar(rightEarLine, leftCenterDeg, halfSpreadDeg, true);
        }

        // Extra waveform loops around the face (the layered "Siri orb"). Activated by
        // wave_amplitude — the master "wave is alive" switch — so wave_amplitude=0
        // means no rings, and wave_rings then chooses how many stack up. The colour
        // parameters only tint them; they never gate whether the rings appear.
        int ringSides = Mathf.RoundToInt(face_sides);
        for (int i = 0; i < MaxRings; i++)
        {
            if (faceOutlineWave && wave_amplitude > 0f && i < wave_rings) DrawWaveRing(waveRingLines[i], i, ringSides);
            else                                                          waveRingLines[i].positionCount = 0;
        }

        // Colour: always the gradient, shaped by color_saturation / color_spread / color_hue.
        ApplyLineColor(faceLine,       0.00f);
        ApplyLineColor(leftEyeLine,    0.05f);
        ApplyLineColor(rightEyeLine,   0.05f);
        ApplyLineColor(leftPupilLine,  0.10f);
        ApplyLineColor(rightPupilLine, 0.10f);
        ApplyLineColor(mouthLine,      0.15f);
        ApplyLineColor(leftEarLine,    0.20f);
        ApplyLineColor(rightEarLine,   0.20f);
        for (int i = 0; i < MaxRings; i++)
            ApplyLineColor(waveRingLines[i], 0.18f * (i + 1));

        ApplyGlow(faceLine);
        ApplyGlow(leftEyeLine);
        ApplyGlow(rightEyeLine);
        ApplyGlow(leftPupilLine);
        ApplyGlow(rightPupilLine);
        ApplyGlow(mouthLine);
        ApplyGlow(leftEarLine);
        ApplyGlow(rightEarLine);
        for (int i = 0; i < MaxRings; i++)
            ApplyGlow(waveRingLines[i]);

        // Grow/shrink the whole avatar to fill the size_restriction frame (Play only, so it
        // never dirties the huge scene while editing). Runs AFTER drawing so the content
        // bounds are up to date.
        if (autoFitToSizeFrame && Application.isPlaying) FitToSizeFrame();
    }

    // Zeroes every face/feature stroke (and its glow) so only the Siri wave shows.
    void HideFaceLines()
    {
        LineRenderer[] all =
        {
            faceLine, leftEyeLine, rightEyeLine, leftPupilLine, rightPupilLine,
            mouthLine, leftEarLine, rightEarLine,
        };
        foreach (var lr in all)
        {
            if (lr == null) continue;
            lr.positionCount = 0;
            ApplyGlow(lr); // clears the glow child now that the source is empty
        }
        for (int i = 0; i < MaxRings; i++)
            if (waveRingLines[i] != null)
            {
                waveRingLines[i].positionCount = 0;
                ApplyGlow(waveRingLines[i]); // also clear the ring's glow child (no ghost loops)
            }
    }

    // ── Siri color flow ──────────────────────────────────────────────────────

    // Cyclic sample of the palette so 0 and 1 wrap to the same color — this lets
    // the gradient loop seamlessly around a closed stroke and scroll over time.
    Color ColorAt(float u)
    {
        u = Mathf.Repeat(u, 1f);
        float f = u * SiriPalette.Length;
        int   i = Mathf.FloorToInt(f);
        float frac = f - i;
        Color a = SiriPalette[i % SiriPalette.Length];
        Color b = SiriPalette[(i + 1) % SiriPalette.Length];
        return Tint(Color.Lerp(a, b, frac));
    }

    // Push a palette colour through the continuous colour parameters: color_hue rotates it
    // around the wheel, color_saturation fades it to WHITE. V is lifted toward 1 as saturation
    // → 0 because the palette's V is only ~0.92-1.0, so without that lift a low saturation
    // would read as GREY rather than white. At color_saturation = 1 and color_hue = 0 this is
    // the identity, so the stock Siri palette is reproduced exactly. Alpha is preserved
    // (Color.HSVToRGB always returns a = 1).
    Color Tint(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        h  = Mathf.Repeat(h + color_hue, 1f);
        s *= color_saturation;
        v  = Mathf.Lerp(1f, v, color_saturation);
        Color outC = Color.HSVToRGB(h, s, v);
        outC.a = c.a;
        return outC;
    }

    // Fills the reused _grad with 8 palette keys (LineRenderer's max) scrolled by
    // phase. Returns the shared instance — assigning it to a LineRenderer's
    // colorGradient copies the keys, so reuse across lines is safe.
    Gradient SiriGradient(float phase, float alpha)
    {
        const int K = 7; // 8 keys: j = 0..7
        // These strokes are CLOSED LOOPS, so the key at pos 0 and the key at pos 1 MUST be the
        // same colour — otherwise the loop's closing segment shows a hard seam: a visible line
        // where the two ends butt together, which also makes the outline read as "broken" there.
        // ColorAt has period 1, so ColorAt(pos + phase) already wraps perfectly (pos 0 and pos 1
        // return the same colour). We therefore fade that full rainbow toward a FIXED base colour
        // by color_spread, rather than scaling the sample position: scaling broke the wrap for
        // every 0 < color_spread < 1 (start sampled at phase·s, end at phase·s + s), which is
        // precisely what produced the seam.
        //   color_spread = 0 → the constant, time-INVARIANT base colour (stable, no shimmer)
        //   color_spread = 1 → the original flowing rainbow, unchanged
        // and every value in between is seamless, continuous and smooth.
        Color baseCol = ColorAt(0f);
        for (int j = 0; j <= K; j++)
        {
            float pos = j / (float)K;
            _ck[j] = new GradientColorKey(Color.Lerp(baseCol, ColorAt(pos + phase), color_spread), pos);
        }
        _ak[0] = new GradientAlphaKey(alpha, 0f);
        _ak[1] = new GradientAlphaKey(alpha, 1f);
        _grad.SetKeys(_ck, _ak);
        return _grad;
    }

    float ColorPhase() => WaveTime() * color_flow_speed * 0.12f;

    // Every stroke ALWAYS takes the gradient path — there is no boolean colour switch any more.
    // color_spread = 0 already collapses the gradient to a single uniform, time-stable colour and
    // color_saturation = 0 turns it white, so the old solid-lineColor branch is redundant (it had
    // become dead code once siri_colors was forced true, which is what made WHITE unreachable).
    void ApplyLineColor(LineRenderer lr, float phaseOffset)
    {
        if (lr == null) return;
        lr.colorGradient = SiriGradient(ColorPhase() + phaseOffset, 1f);
    }

    // Radial wave shared by BOTH the face outline (layer 0) and the stacked orb
    // rings (layer 1, 2, 3…). One formula → the outline and the rings ripple as one
    // system. Returns a >= 0 factor (breathe · half-cos), so applied along the
    // radial direction it only bulges outward and never self-intersects. Each layer
    // travels at its own frequency/direction/phase for an organic, non-repeating look.
    float WaveLayerOffset(float angle, int layer, float t)
    {
        float dir     = (layer % 2 == 0) ? 1f : -1f;
        float freq    = 6f + layer * 3f;
        float breathe = 0.8f - 0.2f * Mathf.Cos(t * 1.4f + layer);
        float h       = 0.5f * (1f - Mathf.Cos(angle * freq - dir * t * (1.6f + 0.4f * layer) + layer * 1.3f));
        return breathe * h;
    }

    void DrawWaveRing(LineRenderer lr, int idx, int sides)
    {
        // Size the ring from the face's ACTUAL extent so it tracks the outline as
        // the face widens (temple/cheekbone/jaw scale x up to 2×).
        float regionMax = (sides > REGION_SCALE_MIN_SIDES)
            ? Mathf.Max(temple_width, Mathf.Max(cheekbone_width, jaw_width)) * 2f
            : 1f;
        float faceX = face_width * Mathf.Max(1f, regionMax);
        float faceY = face_height;

        // Rings OVERLAP rather than spread out: ring idx 0 sits right on the face
        // outline's radius, and each next ring is only ring_spacing further out AND
        // layered a hair in front — so every ring overlaps the one before it (and
        // the outline itself): the layered "Siri orb". Its wave frequency still
        // steps up one layer per ring so the overlapping loops don't move in lockstep.
        int   layer = idx + 1;                 // layer 0 is the face outline itself
        float scl   = 1f + ring_spacing * idx; // idx 0 → 1.0 → coincides with the outline
        float rx    = faceX * scl;
        float ry    = faceY * scl;
        float t     = WaveTime() * wave_speed;
        float amp   = Mathf.Max(wave_amplitude, 0.4f); // rings stay visibly wavy

        // Stack each ring a hair in front of the previous, so overlapping
        // semi-transparent loops composite cleanly instead of z-fighting.
        lr.transform.localPosition = new Vector3(0f, 0f, -0.0006f * layer);

        lr.loop = true;
        lr.positionCount = sides;
        for (int i = 0; i < sides; i++)
        {
            float angle = 2f * Mathf.PI * i / sides;
            float nx = Mathf.Cos(angle);
            float ny = Mathf.Sin(angle);
            float x  = nx * rx;
            float y  = ny * ry;

            float off = amp * 0.14f * WaveLayerOffset(angle, layer, t);
            x += off * nx;
            y += off * ny;

            lr.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    // ── Glow ───────────────────────────────────────────────────────────────

    // ApplyGlow runs for ~12 lines every frame while glow is on; these caches keep
    // it allocation-free and Find-free in the steady state. Position buffers are
    // cached per exact size (SetPositions wants a length-matched array; line sizes
    // only change when `segments` changes), glow-layer transforms per main line.
    readonly Dictionary<int, Vector3[]> _glowPtsBySize = new Dictionary<int, Vector3[]>();
    readonly Dictionary<LineRenderer, Transform[]> _glowLayerCache = new Dictionary<LineRenderer, Transform[]>();

    void ApplyGlow(LineRenderer mainLine)
    {
        Transform parent = mainLine.transform.parent;
        bool needGlow = glow_intensity > 0f && mainLine.positionCount > 1;
        // glow_intensity keeps its BO range [0,1], but the drawn halo is scaled down so
        // even 1 stays tasteful (see GLOW_SCALE). Only the strength is scaled — the
        // on/off decision above still uses the raw field.
        float glowStrength = glow_intensity * GLOW_SCALE;

        Transform[] layers;
        if (!_glowLayerCache.TryGetValue(mainLine, out layers))
        {
            // Retire the old single-layer glow child from earlier versions, if present,
            // so it can't linger as a stale fat outline behind the new layered halo.
            Transform legacy = parent.Find(mainLine.gameObject.name + "_Glow");
            if (legacy != null) SafeDestroy(legacy.gameObject);

            layers = new Transform[GLOW_LAYERS];
            for (int j = 0; j < GLOW_LAYERS; j++)
                layers[j] = parent.Find(mainLine.gameObject.name + "_Glow" + j);
            _glowLayerCache[mainLine] = layers;
        }

        int count = needGlow ? mainLine.positionCount : 0;
        Vector3[] pts = null;
        if (needGlow)
        {
            if (!_glowPtsBySize.TryGetValue(count, out pts))
            {
                pts = new Vector3[count];
                _glowPtsBySize[count] = pts;
            }
            mainLine.GetPositions(pts);
        }

        // Several concentric copies, each wider and fainter than the last, so the
        // falloff reads as a soft halo instead of one fat flat outline: near the
        // stroke every layer overlaps (bright core), while only the wide faint
        // layers reach the outer edge (soft glow). Colors flow with the wave.
        for (int j = 0; j < GLOW_LAYERS; j++)
        {
            Transform glowT = layers[j]; // cached; Unity fake-null after destroy re-creates below

            if (!needGlow)
            {
                if (glowT != null)
                {
                    LineRenderer off = glowT.GetComponent<LineRenderer>();
                    if (off != null) off.positionCount = 0;
                }
                continue;
            }

            if (glowT == null)
            {
                glowT = new GameObject(mainLine.gameObject.name + "_Glow" + j).transform;
                glowT.SetParent(parent, false);
                glowT.gameObject.hideFlags = HideFlags.DontSave; // derived, rebuilt each load
                layers[j] = glowT;
            }

            LineRenderer glow = glowT.GetComponent<LineRenderer>();
            if (glow == null) glow = glowT.gameObject.AddComponent<LineRenderer>();

            glow.useWorldSpace     = mainLine.useWorldSpace;
            glow.loop              = mainLine.loop;
            glow.numCornerVertices = mainLine.numCornerVertices;
            glow.numCapVertices    = mainLine.numCapVertices;

            // Layer j widens and fades as j grows → smooth outward falloff.
            float u          = (j + 1f) / GLOW_LAYERS;                       // 0<..1
            float widthMul   = 1f + glowStrength * Mathf.Lerp(3f, 16f, u);
            float layerAlpha = glowStrength * 0.33f * Mathf.Pow(0.5f, j);    // scaled by GLOW_SCALE

            float w = mainLine.startWidth * widthMul;
            glow.startWidth = w;
            glow.endWidth   = w;

            // Same single gradient path as ApplyLineColor, keeping this layer's alpha.
            glow.colorGradient = SiriGradient(ColorPhase(), layerAlpha);

            AssignLineMaterial(glow);

            glow.positionCount = count;
            glow.SetPositions(pts);

            // Widest/faintest layer furthest back; all behind the crisp main stroke.
            glowT.localPosition = new Vector3(0f, 0f, 0.001f * (j + 2));
        }
    }

    // Assigns ONE shared vertex-color material to every stroke — all face lines and
    // all glow layers. They differ only in per-LineRenderer color/gradient/width,
    // never in material state, so a single instance is correct; and it means we
    // allocate & free exactly one Material instead of one per line (Unity never GCs
    // Materials, so per-line `new Material` would leak). Created lazily; freed in
    // Cleanup. The shader multiplies by vertex color so the flowing Siri gradient
    // and the single lineColor both render.
    void AssignLineMaterial(LineRenderer lr)
    {
        if (_lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");
            if (shader == null) return;
            _lineMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
        }
        if (lr.sharedMaterial != _lineMaterial) lr.sharedMaterial = _lineMaterial;
    }

    // ── Face ───────────────────────────────────────────────────────────────

    void DrawFace(LineRenderer lr, float rx, float ry, int sides)
    {
        // One vertex per side, NO duplicated closing vertex — loop=true joins the
        // last point back to the first, so there's no overlapping seam "blob".
        lr.loop = true;
        lr.positionCount = sides;
        // A dense outline (high face_sides) is already smooth on its own; still rounding each
        // of its very SHORT corners with numCornerVertices=24 makes adjacent corner quads
        // overlap/flip and TEAR the line into gaps at high sides (~86+). Drop the rounding
        // once the polygon is dense enough to look smooth; keep it only for genuinely
        // few-sided shapes. (ApplyGlow copies this to the glow child, so it stays consistent.)
        lr.numCornerVertices = sides >= 32 ? 0 : 24;
        for (int i = 0; i < sides; i++)
        {
            float angle = 2f * Mathf.PI * i / sides;
            float nx    = Mathf.Cos(angle); // -1..1
            float ny    = Mathf.Sin(angle); // -1..1 (1=top, -1=bottom)

            float x = nx * rx;
            float y = ny * ry;

            if (sides > REGION_SCALE_MIN_SIDES)
            {
                // Region weights with smooth transitions
                // top 20%: ny > 0.6, middle 40%: -0.2..0.6, bottom 30%: ny < -0.2
                float wTop = Mathf.SmoothStep(0.5f, 0.7f, ny);
                float wBot = Mathf.SmoothStep(-0.1f, -0.3f, ny);
                float wMid = 1f - wTop - wBot;

                // param * 2 so that default 0.5 → scale 1.0 (no change)
                float scaleX = wTop * (temple_width    * 2f)
                             + wMid * (cheekbone_width * 2f)
                             + wBot * (jaw_width       * 2f);
                x *= scaleX;
            }

            // ── Siri-style animated waveform ─────────────────────────────────
            // The outline IS wave-layer 0 — the exact travelling ripple the stacked
            // orb rings use (see WaveLayerOffset), so the outline and the rings move
            // as one coherent system. The offset is pushed along the point's own
            // radial direction (nx, ny) and kept >= 0, so the outline always bulges
            // outward and can never self-cross as wave_amplitude grows.
            // wave_amplitude=0 gives exactly zero offset (a perfectly still face). Also gated by
            // faceOutlineWave (default OFF) so the outline stays STILL whenever a face is shown.
            if (faceOutlineWave && wave_amplitude > 0f)
            {
                float t = WaveTime() * wave_speed;
                float waveOffset = wave_amplitude * WAVE_OUTLINE_AMP * WaveLayerOffset(angle, 0, t);
                x += waveOffset * nx;
                y += waveOffset * ny;
            }

            lr.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    // Half-width of the face outline at a given local y — mirrors DrawFace's ellipse
    // and its temple/cheekbone/jaw region scaling, so the feature clamps measure
    // against the SAME silhouette the outline actually draws. (Any wave bulge is
    // ignored: it only pushes the outline outward, so ignoring it keeps eyes safely
    // inside.)
    float FaceHalfWidthAt(float y)
    {
        float ny     = (face_height > 1e-6f) ? Mathf.Clamp(y / face_height, -1f, 1f) : 0f;
        float horiz  = Mathf.Sqrt(Mathf.Max(0f, 1f - ny * ny));
        float scaleX = 1f;
        if (Mathf.RoundToInt(face_sides) > REGION_SCALE_MIN_SIDES)
        {
            float wTop = Mathf.SmoothStep(0.5f, 0.7f, ny);
            float wBot = Mathf.SmoothStep(-0.1f, -0.3f, ny);
            float wMid = 1f - wTop - wBot;
            scaleX = wTop * (temple_width * 2f) + wMid * (cheekbone_width * 2f) + wBot * (jaw_width * 2f);
        }
        // × the polygon factor: with few sides the drawn outline is a polygon whose flat
        // edges sit inside the ellipse, so this is the width a feature can safely reach
        // without poking through an edge.
        return face_width * scaleX * horiz * FacePolyFactor();
    }

    // The actual drawn face-outline point at a given angle (radians) — mirrors DrawFace's
    // ellipse + temple/cheekbone/jaw region scaling (the travelling wave ripple is ignored,
    // exactly as FaceHalfWidthAt does). Used to glue the ears onto the real silhouette so they
    // slide along it as those widths change instead of floating off a plain, unscaled ellipse.
    Vector2 FaceOutlinePoint(float angleRad)
    {
        float nx = Mathf.Cos(angleRad);
        float ny = Mathf.Sin(angleRad);
        float x  = nx * face_width;
        float y  = ny * face_height;
        if (Mathf.RoundToInt(face_sides) > REGION_SCALE_MIN_SIDES)
        {
            float wTop = Mathf.SmoothStep(0.5f, 0.7f, ny);
            float wBot = Mathf.SmoothStep(-0.1f, -0.3f, ny);
            float wMid = 1f - wTop - wBot;
            float scaleX = wTop * (temple_width * 2f) + wMid * (cheekbone_width * 2f) + wBot * (jaw_width * 2f);
            x *= scaleX;
        }
        return new Vector2(x, y);
    }

    // For a low-face_sides outline the drawn polygon's edges sit INSIDE the ellipse — the
    // shortest center-to-edge distance is radius·cos(π/sides) (the apothem). Feature
    // extents are scaled by this so they never poke through a flat/wavy edge; it is ≈1 for
    // a smooth (high-sides) face, so round faces are unaffected.
    float FacePolyFactor()
    {
        int sides = Mathf.Max(3, Mathf.RoundToInt(face_sides));
        return Mathf.Cos(Mathf.PI / sides);
    }

    // ── Auto-fit to the size_restriction frame ───────────────────────────────

    // Scales the avatar uniformly so its drawn content fits maximally inside the invisible
    // "size_restriction" frame (height <= dashboard_02). Content bounds are measured in LOCAL
    // space (the strokes are drawn independently of transform.localScale), so this converges
    // in a single step with no feedback loop. Fails safe: if the dashboard can't be found or
    // nothing is drawn yet, it leaves the avatar's current scale untouched.
    void FitToSizeFrame()
    {
        // 1) Local content half-extents over the STATIC strokes (scale-independent). Reuse a
        //    list (no per-frame GC) and skip the time-animated wave rings / Siri-wave / glow
        //    children, so the measured size — and hence the fit scale — does not oscillate as
        //    the wave breathes. (The face-outline ripple is bounded and negligible here.)
        float halfW = 0f, halfH = 0f;
        if (IsWaveMode())
        {
            // Wave mode: the face strokes are hidden and the Siri wave is a TIME-ANIMATED mesh, so
            // its extent is derived ANALYTICALLY from the static wave_* params (never the live mesh
            // verts, which breathe every frame and would make the fit oscillate). Horizontal
            // half-width = WaveSpan/2 = face_width — the LARGER extent, so it binds and the wave
            // scales up to FILL the frame width. Vertical is a stable estimate of the peak lobe
            // (WAVE_AMP_FACTOR·WaveHeightMax·WaveAmp·~0.5), floored so the guard/scale stay
            // well-defined as wave_height→0.
            halfW = face_width;
            const float WAVE_YREL_EST = 0.5f;
            halfH = Mathf.Max(0.05f, WAVE_AMP_FACTOR * WaveHeightMax * WaveAmp * WAVE_YREL_EST);
        }
        else
        {
            _fitLines.Clear();
            GetComponentsInChildren<LineRenderer>(true, _fitLines);
            for (int li = 0; li < _fitLines.Count; li++)
            {
                var lr = _fitLines[li];
                if (lr == null || lr.transform == transform) continue;
                string nm = lr.gameObject.name;
                // Skip the time-animated children (wave rings / Siri-wave / glow) AND the face
                // outline itself — the face IS wave-layer-0, so reading its rippled points would
                // make the whole avatar breathe. The face extent is folded in analytically below.
                if (nm == "Face" || nm.StartsWith("WaveRing") || nm.StartsWith("SiriWave") || nm.Contains("_Glow")) continue;
                int n = lr.positionCount;
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = lr.GetPosition(i); // child is at identity, so this is avatar-local
                    float ax = Mathf.Abs(p.x), ay = Mathf.Abs(p.y);
                    if (ax > halfW) halfW = ax;
                    if (ay > halfH) halfH = ay;
                }
            }
            // Fold in the STATIC (un-rippled) face-outline extent analytically (matches DrawFace's
            // max: face_width × the temple/cheekbone/jaw region scale, and face_height). This keeps
            // the fit stable even while the wave outline ripples.
            float faceRegionMax = (Mathf.RoundToInt(face_sides) > REGION_SCALE_MIN_SIDES)
                ? Mathf.Max(temple_width, Mathf.Max(cheekbone_width, jaw_width)) * 2f : 1f;
            halfW = Mathf.Max(halfW, face_width * Mathf.Max(1f, faceRegionMax));
            halfH = Mathf.Max(halfH, face_height);
            // Fold in the (local-space) wave geometry that renders OUTSIDE the measured strokes.
            // Only when the outline actually ripples (faceOutlineWave on); otherwise reserving
            // wave space would shrink a still avatar for no reason.
            if (faceOutlineWave && wave_amplitude > 0f)
            {
                float bulge = wave_amplitude * WAVE_OUTLINE_AMP;
                // The outermost wave ring ripples by Max(wave_amplitude,0.4)*0.14 (DrawWaveRing floors
                // the ring amplitude at 0.4 so rings stay wavy), which exceeds the face ripple for
                // small wave_amplitude — fold in the larger so a ring can't poke past the frame.
                if (wave_rings > 0) bulge = Mathf.Max(bulge, Mathf.Max(wave_amplitude, 0.4f) * 0.14f);
                float ringScale = wave_rings > 0 ? 1f + ring_spacing * (wave_rings - 1) : 1f;
                halfW = halfW * ringScale + bulge;
                halfH = halfH * ringScale + bulge;
            }
        }
        // Safety margin for the glow halo + finite stroke width (both world-space, rendering just
        // beyond the measured centerline), so the avatar never pokes past the frame / dashboard_02.
        const float CONTENT_SAFETY = 1.12f;
        halfW *= CONTENT_SAFETY;
        halfH *= CONTENT_SAFETY;
        if (halfW <= 1e-4f || halfH <= 1e-4f) return; // nothing to fit yet

        // 2) Frame world size, capped to dashboard_02's height.
        float frameH = ResolveFrameWorldHeight();
        if (frameH <= 1e-4f) return; // dashboard not found — leave scale as authored
        float frameW = frameH * Mathf.Max(0.01f, frameAspect);

        // 3) Solve ONE world-space scale k (world units per local unit) and divide it out of the
        //    parent PER AXIS. The avatar hangs off a NON-UNIFORMLY scaled parent (a world-space UI
        //    canvas), so a uniform localScale would render the face STRETCHED. Countering pX and pY
        //    separately makes the avatar isotropic (等比例) in world space while still fitting.
        Vector3 pl = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        float pX = Mathf.Abs(pl.x) < 1e-9f ? 1f : Mathf.Abs(pl.x);
        float pY = Mathf.Abs(pl.y) < 1e-9f ? 1f : Mathf.Abs(pl.y);

        float k = Mathf.Min((frameH * 0.5f) / halfH, (frameW * 0.5f) / halfW); // world units per local unit
        if (!(k > 0f) || float.IsNaN(k) || float.IsInfinity(k)) return;

        // z is visually irrelevant (the face is flat in local x/y); match it to x rather than
        // dividing by the parent's tiny z scale (which would produce a huge value).
        transform.localScale = new Vector3(k / pX, k / pY, k / pX);

        // 4) Keep the invisible reference frame in place (created once, never saved).
        UpdateSizeFrame(frameW, frameH);
    }

    float ResolveFrameWorldHeight()
    {
        // Find dashboard_02 and cache its Renderer. GameObject.Find is O(scene) and this scene is
        // huge, so we DON'T call it every frame — but we must RETRY until found: on the first frames
        // (or right after a scene load / Initialize) the dashboard can still be inactive/absent, and
        // the old "search exactly once" logic then left _dashRendererCache null forever, so the
        // avatar never auto-fit and stayed at its tiny authored scale for the whole session. Throttle
        // the retry to ~2x/sec; once found we cache and never Find() again.
        if (_dashRendererCache == null && Time.unscaledTime >= _dashNextSearchTime)
        {
            _dashNextSearchTime = Time.unscaledTime + 0.5f;
            var dash = GameObject.Find(dashboardObjectName);
            if (dash != null) _dashRendererCache = dash.GetComponentInChildren<Renderer>();
        }
        if (_dashRendererCache == null) return -1f;
        return _dashRendererCache.bounds.size.y * Mathf.Clamp01(frameHeightFraction);
    }

    void UpdateSizeFrame(float frameW, float frameH)
    {
        if (_sizeFrame == null)
        {
            Transform existing = transform.parent != null ? transform.parent.Find(sizeFrameName) : null;
            _sizeFrame = existing != null ? existing : new GameObject(sizeFrameName).transform;
            _sizeFrame.SetParent(transform.parent, true);       // sibling of the avatar (unscaled by it)
            _sizeFrame.gameObject.hideFlags = HideFlags.DontSave; // transient guide — never written to the scene
        }

        _sizeFrameLine = _sizeFrame.GetComponent<LineRenderer>();
        if (_sizeFrameLine == null) _sizeFrameLine = _sizeFrame.gameObject.AddComponent<LineRenderer>();

        // Scale guide ONLY — kept disabled so it is invisible in Play mode.
        _sizeFrameLine.enabled           = false;
        _sizeFrameLine.useWorldSpace     = true;
        _sizeFrameLine.loop              = true;
        _sizeFrameLine.numCornerVertices = 0;
        _sizeFrameLine.numCapVertices    = 0;
        _sizeFrameLine.widthMultiplier   = frameH * 0.004f;
        _sizeFrameLine.positionCount     = 4;
        Vector3 c = transform.position;
        float hw = frameW * 0.5f, hh = frameH * 0.5f;
        _sizeFrameLine.SetPosition(0, c + new Vector3(-hw, -hh, 0f));
        _sizeFrameLine.SetPosition(1, c + new Vector3( hw, -hh, 0f));
        _sizeFrameLine.SetPosition(2, c + new Vector3( hw,  hh, 0f));
        _sizeFrameLine.SetPosition(3, c + new Vector3(-hw,  hh, 0f));
    }

    // ── Eyes ───────────────────────────────────────────────────────────────

    void DrawEye(LineRenderer lr, Vector3 center, float rx, float ry, bool mirror,
                 out Vector3 pupilCenter, out float pupilMaxR)
    {
        // Tilt: outer corner rises/drops. inner corner goes opposite direction.
        float tiltOff = (eye_tilt - 0.5f) * ry;

        // Inner/outer curve: independently shift each corner vertically
        float icOff = (eye_inner_curve - 0.5f) * ry;
        float ocOff = (eye_outer_curve - 0.5f) * ry;

        // For left eye: inner = left (-rx), outer = right (+rx)
        Vector2 inner = new Vector2(-rx, -tiltOff + icOff);
        Vector2 outer = new Vector2( rx,  tiltOff + ocOff);

        // Cubic bezier control points for top and bottom halves
        // Top bulges upward by ry, bottom bulges downward by ry
        Vector2 tc1 = new Vector2(inner.x + rx * 0.5f, inner.y + ry);
        Vector2 tc2 = new Vector2(outer.x - rx * 0.5f, outer.y + ry);
        Vector2 bc1 = new Vector2(outer.x - rx * 0.5f, outer.y - ry);
        Vector2 bc2 = new Vector2(inner.x + rx * 0.5f, inner.y - ry);

        int half = Mathf.Max(4, segments / 2);
        int total = half * 2;

        // Build the outline into a reused buffer first, so the pupil-fit geometry (centroid +
        // inscribed radius) is derived from the SAME points the eye actually draws.
        _eyePts.Clear();
        for (int i = 0; i < half; i++)
        {
            float t = (float)i / half;
            Vector2 bezierP = CubicBezier(inner, tc1, tc2, outer, t);
            float ellipseAngle = Mathf.Lerp(Mathf.PI, 0f, t);
            Vector2 ellipseP = new Vector2(Mathf.Cos(ellipseAngle) * rx, Mathf.Sin(ellipseAngle) * ry);
            Vector2 p = Vector2.Lerp(bezierP, ellipseP, eye_roundness);
            if (mirror) p.x = -p.x;
            _eyePts.Add(new Vector3(p.x, p.y, 0f));
        }
        for (int i = 0; i < half; i++)
        {
            float t = (float)i / half;
            Vector2 bezierP = CubicBezier(outer, bc1, bc2, inner, t);
            float ellipseAngle = Mathf.Lerp(0f, -Mathf.PI, t);
            Vector2 ellipseP = new Vector2(Mathf.Cos(ellipseAngle) * rx, Mathf.Sin(ellipseAngle) * ry);
            Vector2 p = Vector2.Lerp(bezierP, ellipseP, eye_roundness);
            if (mirror) p.x = -p.x;
            _eyePts.Add(new Vector3(p.x, p.y, 0f));
        }

        lr.loop = true;
        lr.positionCount = total;
        for (int i = 0; i < total; i++) lr.SetPosition(i, center + _eyePts[i]);

        // Pupil fit: centre it on the outline's CENTROID (so it tracks tilt / inner-outer-curve
        // shifts) and report the min distance from that centroid to the outline — the largest
        // circle that fits inside. The caller scales pupil_size against this, so the pupil stays
        // provably inside the eye line for ANY tilt / curve and never pokes through it.
        Vector2 c = Vector2.zero;
        for (int i = 0; i < total; i++) c += new Vector2(_eyePts[i].x, _eyePts[i].y);
        c /= total;
        float minD = float.PositiveInfinity;
        for (int i = 0; i < total; i++)
        {
            float d = (new Vector2(_eyePts[i].x, _eyePts[i].y) - c).magnitude;
            if (d < minD) minD = d;
        }
        pupilCenter = center + new Vector3(c.x, c.y, 0f);
        pupilMaxR   = minD;
    }

    Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1f - t;
        return u*u*u*p0 + 3*u*u*t*p1 + 3*u*t*t*p2 + t*t*t*p3;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    void DrawEllipse(LineRenderer lr, Vector3 center, float rx, float ry)
    {
        lr.loop = true;
        lr.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = 2f * Mathf.PI * i / segments;
            lr.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry, 0f));
        }
    }

    void DrawMouth(LineRenderer lr, Vector3 center, float rx, float ry, float curve)
    {
        // rx == 0 (e.g. mouth_width = 0, or the mouth clamped to nothing on a very narrow face)
        // ⇒ NO mouth: hide it instead of drawing a degenerate vertical line.
        if (rx <= 0f) { lr.positionCount = 0; return; }
        // Open arc — loop=false so no straight chord is drawn across the smile.
        lr.loop = false;
        lr.positionCount = segments + 1;
        float curvature = (curve - 0.5f) * 2f;
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float x = Mathf.Lerp(-rx, rx, t);
            float normalizedX = (t - 0.5f) * 2f;
            float y = curvature * ry * (1f - normalizedX * normalizedX);
            lr.SetPosition(i, center + new Vector3(x, y, 0f));
        }
    }

    void DrawEar(LineRenderer lr, float centerDeg, float halfSpreadDeg, bool mirror)
    {
        if (ear_width <= 0f || ear_height <= 0f) { lr.positionCount = 0; return; }

        float a1 = (centerDeg - halfSpreadDeg) * Mathf.Deg2Rad;
        float a2 = (centerDeg + halfSpreadDeg) * Mathf.Deg2Rad;

        // Anchor the ear base ON the actual drawn face outline — the SAME temple/cheekbone/jaw
        // region-scaled silhouette DrawFace draws. So when those widths change, the ear slides
        // along the outline and stays glued to it (never floating off it, never sinking inside).
        Vector2 p1  = FaceOutlinePoint(a1);
        Vector2 p2  = FaceOutlinePoint(a2);
        Vector2 mid = (p1 + p2) * 0.5f;
        // Point the ear straight out from the face centre through its base midpoint, so it
        // protrudes outward from wherever the (possibly widened) outline now sits.
        Vector2 outDir = mid.sqrMagnitude > 1e-8f
            ? mid.normalized
            : new Vector2(Mathf.Cos(centerDeg * Mathf.Deg2Rad), Mathf.Sin(centerDeg * Mathf.Deg2Rad));
        float   earH   = ear_height * Mathf.Max(face_width, face_height);
        Vector2 tip    = mid + outDir * earH;
        Vector2 ctrl   = mid + outDir * earH * 2f;

        // One continuous stroke: turn OFF corner/cap rounding. At tall ear_height the tip is a
        // sharp apex; rounding it (numCornerVertices > 0) makes adjacent corner quads overlap/flip
        // and TEAR the line into segments — the same failure the dense face outline hits. The ear
        // is already a smooth many-segment curve, so it needs no rounding.
        lr.numCornerVertices = 0;
        lr.numCapVertices    = 0;

        lr.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector2 pointed = t <= 0.5f
                ? Vector2.Lerp(p1, tip, t * 2f)
                : Vector2.Lerp(tip, p2, (t - 0.5f) * 2f);
            float   u     = 1f - t;
            Vector2 round = u * u * p1 + 2f * u * t * ctrl + t * t * p2;
            Vector2 pos   = Vector2.Lerp(pointed, round, ear_curve);
            if (mirror) pos.x = -pos.x;
            lr.SetPosition(i, new Vector3(pos.x, pos.y, 0f));
        }
    }

    // ── Line setup ─────────────────────────────────────────────────────────

    LineRenderer GetOrCreateLine(string childName)
    {
        Transform child = transform.Find(childName);
        if (child == null)
            child = new GameObject(childName).transform;

        child.SetParent(transform, false);

        LineRenderer lr = child.GetComponent<LineRenderer>();
        if (lr == null)
            lr = child.gameObject.AddComponent<LineRenderer>();

        float width = Mathf.Lerp(0.001f, 0.04f, stroke_width); // floor lowered (was 0.003) — stroke_width is no longer BO-driven; tune via the Inspector slider in edit mode

        lr.useWorldSpace = false;
        lr.loop          = true;
        lr.startWidth    = width;
        lr.endWidth      = width;
        lr.startColor    = lineColor;
        lr.endColor      = lineColor;
        lr.numCornerVertices  = 24;
        lr.numCapVertices     = 24;

        AssignLineMaterial(lr);

        return lr;
    }

    // ── ios9 Siri wave (integrated port of kopiro/siriwave) ──────────────────

    // Called only in wave mode. Ticks the spawn/despawn state and rebuilds each
    // colored lobe. The library's per-frame (~60fps) rates are made framerate-
    // independent via frames = dt*60.
    void UpdateSiriWave()
    {
        if (wave_definitions == null || wave_definitions.Length == 0)
        {
            ClearWaveMeshes();
            return;
        }

        EnsureWaveObjects();

        float t  = WaveTime();
        float dt = Mathf.Clamp(t - _lastWaveTime, 0f, 0.1f);
        _lastWaveTime = t;
        float frames = dt * 60f;

        for (int g = 0; g < waveGroups.Length; g++)
        {
            var def = wave_definitions[g];
            var grp = waveGroups[g];
            if (def.supportLine) { BuildWaveSupport(grp, def); continue; }

            if (grp.spawnAt == 0f) SpawnWave(grp, t);
            StepWave(grp, t, frames);
            BuildWaveCurve(grp, def);
        }
    }

    // Lazily create one additive mesh child per definition; rebuild if the
    // definition count changed. Children are transient (rebuilt on load).
    void EnsureWaveObjects()
    {
        int n = wave_definitions.Length;
        if (waveGroups != null && waveGroups.Length == n) return;

        DestroyOwnedWave(); // free meshes/materials from the previous set

        waveGroups = new WaveGroup[n];
        var expected = new HashSet<string>();
        for (int g = 0; g < n; g++)
        {
            var def = wave_definitions[g];
            string childName = def.supportLine ? "SiriWaveSupport" : "SiriWaveCurve" + g;
            expected.Add(childName);

            Transform child = transform.Find(childName);
            if (child == null)
            {
                child = new GameObject(childName).transform;
                child.SetParent(transform, false);
                child.gameObject.hideFlags = HideFlags.DontSave;
            }

            var mf = child.GetComponent<MeshFilter>();   if (mf == null) mf = child.gameObject.AddComponent<MeshFilter>();
            var mr = child.GetComponent<MeshRenderer>();  if (mr == null) mr = child.gameObject.AddComponent<MeshRenderer>();

            var mesh = new Mesh { name = childName + "_Mesh", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            mf.sharedMesh = mesh;
            _waveMeshes.Add(mesh);

            var mat = MakeWaveMaterial();
            if (mat != null)
            {
                mat.hideFlags = HideFlags.DontSave;
                mr.sharedMaterial = mat;
                _waveMaterials.Add(mat);
            }

            waveGroups[g] = new WaveGroup { spawnAt = 0f, mesh = mesh };
        }

        PruneWaveChildren(expected);
        _lastWaveTime = WaveTime();
    }

    void PruneWaveChildren(HashSet<string> expected)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c.name.StartsWith("SiriWave") && !expected.Contains(c.name))
                SafeDestroy(c.gameObject);
        }
    }

    void ClearWaveMeshes()
    {
        if (waveGroups == null) return;
        for (int g = 0; g < waveGroups.Length; g++)
            if (waveGroups[g] != null && waveGroups[g].mesh != null) waveGroups[g].mesh.Clear();
    }

    // ── Spawn / step (ports spawn(), spawnSingle(), the per-frame update) ─────
    static float RandW(Vector2 r) => Random.Range(r.x, r.y);

    void SpawnWave(WaveGroup grp, float t)
    {
        grp.spawnAt  = t;
        grp.prevMaxY = 0f;
        int n = Mathf.Max(1, Mathf.FloorToInt(RandW(WAVE_NOOF)));
        grp.noOfCurves = n;

        grp.phases          = new float[n];
        grp.amplitudes      = new float[n];
        grp.finalAmplitudes = new float[n];
        grp.offsets         = new float[n];
        grp.speeds          = new float[n];
        grp.widths          = new float[n];
        grp.verses          = new float[n];
        grp.despawn         = new float[n];

        for (int ci = 0; ci < n; ci++)
        {
            grp.phases[ci]          = 0f;
            grp.amplitudes[ci]      = 0f;
            grp.despawn[ci]         = RandW(WAVE_DESPAWN_MS) / 1000f; // seconds
            grp.offsets[ci]         = RandW(WAVE_OFF);
            grp.speeds[ci]          = RandW(WAVE_SPD);
            grp.finalAmplitudes[ci] = RandW(WAVE_AMP);
            grp.widths[ci]          = RandW(WAVE_WID);
            grp.verses[ci]          = RandW(new Vector2(-1f, 1f));
        }
    }

    void StepWave(WaveGroup grp, float t, float frames)
    {
        for (int ci = 0; ci < grp.noOfCurves; ci++)
        {
            if (grp.spawnAt + grp.despawn[ci] <= t)
                grp.amplitudes[ci] -= WAVE_DESPAWN * frames;
            else
                grp.amplitudes[ci] += WAVE_DESPAWN * frames;

            grp.amplitudes[ci] = Mathf.Clamp(grp.amplitudes[ci], 0f, grp.finalAmplitudes[ci]);
            grp.phases[ci] = Mathf.Repeat(
                grp.phases[ci] + WavePhaseSpeed * grp.speeds[ci] * WAVE_SPEED_FACTOR * frames, 2f * Mathf.PI);
        }
    }

    // ── Math (globalAttFn, yRelativePos, yPos, xPos — verbatim) ───────────────
    static float WaveAtt(float x) => Mathf.Pow(WAVE_ATT_FACTOR / (WAVE_ATT_FACTOR + x * x), WAVE_ATT_FACTOR);

    float WaveYRel(WaveGroup grp, float i)
    {
        float y = 0f;
        int denom = Mathf.Max(1, grp.noOfCurves - 1);
        for (int ci = 0; ci < grp.noOfCurves; ci++)
        {
            float tt = 4f * (-1f + ((float)ci / denom) * 2f) + grp.offsets[ci];
            float k  = 1f / grp.widths[ci];
            float x  = i * k - tt;
            y += Mathf.Abs(grp.amplitudes[ci] * Mathf.Sin(grp.verses[ci] * x - grp.phases[ci]) * WaveAtt(x));
        }
        return y / grp.noOfCurves;
    }

    float WaveYPos(WaveGroup grp, float i) =>
        WAVE_AMP_FACTOR * WaveHeightMax * WaveAmp * WaveYRel(grp, i) * WaveAtt((i / WAVE_GRAPH_X) * 2f);

    // Library xPos maps i∈[-25,25] to [0,span]; we center it on the transform.
    float WaveXPos(float i) => WaveSpan * ((i + WAVE_GRAPH_X) / (WAVE_GRAPH_X * 2f)) - WaveSpan * 0.5f;

    // ── Mesh building ─────────────────────────────────────────────────────────
    void BuildWaveCurve(WaveGroup grp, WaveCurveDef def)
    {
        _wv.Clear(); _wc.Clear(); _wt.Clear();
        // Tint() so the wave lobes obey the same colour parameters as every other stroke —
        // otherwise color_saturation = 0 would whiten the face but leave the wave coloured.
        Color col = Tint(def.color); col.a = wave_layer_alpha;

        float maxY = float.NegativeInfinity;

        // Two mirrored lobes (sign = +1 top, -1 bottom), each filled between the
        // curve and the center line — matching the library's two-pass fill.
        for (int s = 0; s < 2; s++)
        {
            float sign = s == 0 ? 1f : -1f;
            int start = _wv.Count;
            int cols = 0;
            for (float i = -WAVE_GRAPH_X; i <= WAVE_GRAPH_X + 1e-4f; i += WAVE_PIXEL_STEP)
            {
                float x = WaveXPos(i);
                float y = WaveYPos(grp, i);
                if (y > maxY) maxY = y;
                _wv.Add(new Vector3(x, 0f, 0f));         // baseline (center)
                _wv.Add(new Vector3(x, sign * y, 0f));   // curve
                _wc.Add(col); _wc.Add(col);
                cols++;
            }
            for (int cIdx = 0; cIdx < cols - 1; cIdx++)
            {
                int b0 = start + cIdx * 2, c0 = b0 + 1;
                int b1 = start + (cIdx + 1) * 2, c1 = b1 + 1;
                _wt.Add(b0); _wt.Add(c0); _wt.Add(c1);
                _wt.Add(b0); _wt.Add(c1); _wt.Add(b1);
            }
        }

        CommitWave(grp.mesh);

        // Respawn when the group has decayed to nothing (library's DEAD_PX check).
        if (maxY < WAVE_DEAD_PX * (WaveHeightMax / 100f) && grp.prevMaxY > maxY) grp.spawnAt = 0f;
        grp.prevMaxY = maxY;
    }

    void BuildWaveSupport(WaveGroup grp, WaveCurveDef def)
    {
        _wv.Clear(); _wc.Clear(); _wt.Clear();

        float thick = Mathf.Max(0.004f, wave_height * 0.012f);
        float x0 = -WaveSpan * 0.5f, x1 = WaveSpan * 0.5f;
        // Alpha gradient along x: transparent → 0.5 → 0.5 → transparent.
        float[] stops = { 0f, 0.1f, 0.8f, 1f };
        float[] al    = { 0f, 0.5f, 0.5f, 0f };
        for (int k = 0; k < stops.Length; k++)
        {
            float x = Mathf.Lerp(x0, x1, stops[k]);
            Color c = Tint(def.color); c.a = al[k] * wave_layer_alpha;
            _wv.Add(new Vector3(x, -thick * 0.5f, 0f));
            _wv.Add(new Vector3(x,  thick * 0.5f, 0f));
            _wc.Add(c); _wc.Add(c);
        }
        for (int k = 0; k < stops.Length - 1; k++)
        {
            int b0 = k * 2, t0 = b0 + 1, b1 = (k + 1) * 2, t1 = b1 + 1;
            _wt.Add(b0); _wt.Add(t0); _wt.Add(t1);
            _wt.Add(b0); _wt.Add(t1); _wt.Add(b1);
        }
        CommitWave(grp.mesh);
    }

    void CommitWave(Mesh m)
    {
        m.Clear();
        if (_wv.Count == 0) return;
        m.SetVertices(_wv);
        m.SetColors(_wc);
        m.SetTriangles(_wt, 0);
        m.RecalculateBounds();
    }

    // Vertex-coloured, alpha-blended, unlit material for the Siri-wave lobes. It now uses the SAME
    // shader as the face LineRenderers — "Sprites/Default" — which is PROVEN to multiply a mesh's
    // per-vertex colours (the flowing face rainbow renders through it). The URP "Particles/Unlit"
    // shader used before did NOT honour a hand-built mesh's vertex colours, so the blue/red/green
    // lobes rendered as grey/white. Sprites/Default premultiplies alpha in its fragment shader and
    // blends One / OneMinusSrcAlpha (correct straight-alpha compositing), so the lobe colours show
    // on ANY background — dark OR the light dashboard.
    static Material MakeWaveMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default")
                     ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                     ?? Shader.Find("Unlit/Transparent");
        if (shader == null)
        {
            // Mirrors AssignLineMaterial's guard: none of the fallback shaders survived
            // build stripping. new Material(null) would produce a broken/error material.
            Debug.LogError("IVARenderer.MakeWaveMaterial: no suitable shader found; skipping wave material.");
            return null;
        }
        var m = new Material(shader);

        // Sample a solid-white texture so the output is EXACTLY the vertex colour regardless of
        // whether the mesh carries UVs (the wave mesh has none) — an unassigned _MainTex can
        // otherwise sample as black/grey and kill the colour.
        if (m.HasProperty("_MainTex")) m.mainTexture = Texture2D.whiteTexture;
        if (m.HasProperty("_Color"))   m.SetColor("_Color", Color.white);

        // If we ever fall through to the URP particle shader, force alpha (not additive) blending
        // so the lobe colours still survive a light background.
        if (shader.name.Contains("Universal Render Pipeline"))
        {
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            m.SetFloat("_Surface", 1f); // Transparent
            m.SetFloat("_Blend", 0f);   // Alpha (was 2 = Additive)
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        }
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    // ── Teardown ─────────────────────────────────────────────────────────────
    // Unity does not GC meshes/materials, and [ExecuteAlways] re-inits often, so
    // the wave meshes/materials we allocate are destroyed explicitly.
    void Cleanup()
    {
        DestroyOwnedWave();
        SafeDestroy(_lineMaterial); // freed only on real teardown, not on wave rebuild
        _lineMaterial = null;
    }

    void DestroyOwnedWave()
    {
        for (int i = 0; i < _waveMeshes.Count; i++)    SafeDestroy(_waveMeshes[i]);
        for (int i = 0; i < _waveMaterials.Count; i++) SafeDestroy(_waveMaterials[i]);
        _waveMeshes.Clear();
        _waveMaterials.Clear();
        waveGroups = null;
    }

    static void SafeDestroy(Object o)
    {
        if (o == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) { DestroyImmediate(o); return; }
#endif
        Destroy(o);
    }
}
