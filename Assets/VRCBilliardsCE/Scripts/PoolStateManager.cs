using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Rendering;
using UnityEngine.UI;
using VRC.SDKBase;

namespace VRCBilliards
{
    /// <summary>
    /// Main Behaviour for the VRCBilliards 8Ball variant.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PoolStateManager : UdonSharpBehaviour
    {
        /*
         * Constants
         */

#if UNITY_ANDROID
        /// <summary>
        /// Maximum steps/frame ( 5ish )
        /// </summary>
    const float MAX_DELTA = 0.075f;
#else
        /// <summary>
        /// Maximum steps/frame ( 8 )
        /// </summary>
        private const float MAX_DELTA = 0.1f;
#endif

        // Physics calculation constants (measurements are in meters)

        /// <summary>
        /// time step in seconds per iteration
        /// </summary>
        private const float FIXED_TIME_STEP = 0.0125f;
        //private const float FIXED_TIME_STEP = 0.005f;
        /// <summary>
        /// horizontal span of table
        /// </summary>
        private const float TABLE_WIDTH = 1.0668f;
        /// <summary>
        /// vertical span of table
        /// </summary>
        private const float TABLE_HEIGHT = 0.6096f;
        /// <summary>
        /// width of ball
        /// </summary>
        private const float BALL_DIAMETER = 0.06f;
        /// <summary>
        /// break placement X
        /// </summary>
        private const float BALL_PL_X = 0.03f;
        /// <summary>
        /// Break placement Y
        /// </summary>
        private const float BALL_PL_Y = 0.05196152422f;
        /// <summary>
        /// 1 over ball radius
        /// </summary>
        private const float BALL_1OR = 33.3333333333f;
        /// <summary>
        /// ball radius squared
        /// </summary>
        private const float BALL_RADIUS_SQUARED = 0.0009f;
        /// <summary>
        /// ball diameter squared
        /// </summary>
        private const float BALL_DSQR = 0.0036f;
        /// <summary>
        /// ball diameter squared plus epsilon
        /// </summary>
        private const float BALL_DSQRPE = 0.003598f;
        /// <summary>
        /// Full diameter of pockets (exc ball radi)
        /// </summary>
        private const float POCKET_RADIUS = 0.09f;
        /// <summary>
        /// 1 over root 2 (normalize +-1,+-1 vector)
        /// </summary>
        private const float ONE_OVER_ROOT_TWO = 0.70710678118f;
        /// <summary>
        /// 1 over root 5 (normalize +-1,+-2 vector)
        /// </summary>
        private const float ONE_OVER_ROOT_FIVE = 0.4472135955f;
        private const float RANDOMIZE_F = 0.0001f;
        /// <summary>
        /// How far back (roughly) do pockets absorb balls after this point
        /// </summary>
        private const float POCKET_DEPTH = 0.04f;
        /// <summary>
        /// Friction coefficient of sliding
        /// </summary>
        private const float F_SLIDE = 0.2f;
        /// <summary>
        /// First X position of the racked balls
        /// </summary>
        private const float SPOT_POSITION_X = 0.5334f;
        /// <summary>
        /// Spot position for carom mode
        /// </summary>
        private const float SPOT_CAROM_X = 0.8001f;
        /// <summary>
        /// Rack position on Y axis
        /// </summary>
        private const float RACHEIGHT = -0.0702f;
        /// <summary>
        /// Vectors cannot be const.
        /// </summary>
        private Vector3 CONTACT_POINT = new Vector3(0.0f, -0.03f, 0.0f);

        private const float SINA = 0.28078832987f;
        private const float COSA = 0.95976971915f;
        private const float F = 1.72909790282f;

        private const float desktopCursorSpeed = 0.035f;

        private const float DEFAULT_FORCE_MULTIPLIER = 1.5f;
        private float forceMultiplier;

        private Vector3 vectorZero = Vector3.zero;
        private Quaternion quaternionIdentity = Quaternion.identity;

        /*
         * Public Variables
         */

        [Header("Other VRCBilliards Components")]
        public GameObject baseObject;
        public PoolMenu poolMenu;
        public GameObject shadows;

        [Header("Shader Information")]
        public string uniformTableColour = "_EmissionColor";
        public string uniformMarkerColour = "_Color";
        public string uniformCueColour = "_EmissionColor";

        [Header("Options")]
        [Tooltip("Use fake shadows? Fake shadows are high-performance, but they may clash with your world's lighting.")]
        public bool fakeBallShadows = true;
        [Tooltip("Change the length of the intro ball-drop animation. If you set this to zero, the animation will not play at all.")]
        [Range(0f, 5f)]
        public float introAnimationLength = 2.0f;
        [Tooltip("If enabled, worldspace table scales beyond 1 in x or z will increase the force of hits to compensate, making it easier for regular-sized avatars to play.")]
        public bool scaleHitForceWithScaleBeyond1;

        [Header("Table Colours")]
        public Color tableBlue = new Color(0.0f, 0.75f, 1.75f, 1.0f);
        public Color tableOrange = new Color(1.75f, 0.25f, 0.0f, 1.0f);
        public Color tableRed = new Color(1.2f, 0.0f, 0.0f, 1.0f);
        public Color tableWhite = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        public Color tableBlack = new Color(0.01f, 0.01f, 0.01f, 1.0f);
        public Color tableYellow = new Color(2.0f, 1.0f, 0.0f, 1.0f);
        public Color tablePink = new Color(2.0f, 0.0f, 1.5f, 1.0f);
        public Color tableGreen = new Color(0.0f, 2.0f, 0.0f, 1.0f);
        public Color tableLightBlue = new Color(0.3f, 0.6f, 1.0f, 1.0f);
        public Color markerOK = new Color(0.0f, 1.0f, 0.0f, 1.0f);
        public Color markerNotOK = new Color(1.0f, 0.0f, 0.0f, 1.0f);
        public Color gripColourActive = new Color(0.0f, 0.5f, 1.1f, 1.0f);
        public Color gripColourInactive = new Color(0.34f, 0.34f, 0.34f, 1.0f);
        public Color fabricGray = new Color(0.3f, 0.3f, 0.3f, 1.0f);
        public Color fabricRed = new Color(0.9f, 0.2f, 0.1f, 1.0f);
        public Color fabricBlue = new Color(0.1f, 0.6f, 1.0f, 1.0f);
        public Color fabricWhite = new Color(0.8f, 0.8f, 0.8f, 1.0f);
        public Color fabricGreen = new Color(0.15f, 0.75f, 0.3f, 1.0f);
        public Color aimAiming = new Color(0.7f, 0.7f, 0.7f, 1.0f);
        public Color aimLocked = new Color(1.0f, 1.0f, 1.0f, 1.0f);

        [Header("Cues")]
        public PoolCue[] gripControllers;

        /// <summary>
        /// The balls that are used by the table.
        /// The order of the balls is as follows: cue, black, all blue in ascending order, then all orange in ascending order.
        /// If the order of the balls is incorrect, gameplay will not proceed correctly.
        /// </summary>
        [Header("Table Objects")]
        [Tooltip("The balls that are used by the table.\nThe order of the balls is as follows: cue, black, all blue in ascending order, then all orange in ascending order.\nIf the order of the balls is incorrect, gameplay will not proceed correctly.")]
        public Transform[] ballTransforms;
        private Rigidbody[] ballRigidbodies;
        [Tooltip("The shadow object for each ball")]
        public PositionConstraint[] ballShadowPosConstraints;
        private Transform[] ballShadowPosConstraintTransforms;
        public GameObject cueTip;
        private Transform cueTipTransform;
        public GameObject guideline;
        public GameObject devhit;
        public GameObject[] playerTotems;
        public GameObject[] cueTips;
        public GameObject gameTable;
        public GameObject marker;
        private Material markerMaterial;
        public GameObject marker9ball;
        public GameObject tableCollisionParent;
        public GameObject pocketBlockers;
        public GameObject point4Ball;
        private Transform point4BallTransform;
        public MeshRenderer[] cueRenderObjs;
        private Material[] cueMaterials = new Material[2];

        [Header("Materials")]
        public MeshRenderer[] ballRenderers;

        public MeshRenderer tableRenderer;
        private Material tableMaterial;

        public Texture[] sets;
        //public Material guidelineMat;
        public Material[] cueGrips;
        //public Material markerMaterial;

        [Header("Audio")]
        public GameObject audioSourcePoolContainer;
        public AudioSource cueTipSrc;
        public AudioClip introSfx;
        public AudioClip sinkSfx;
        public AudioClip[] hitsSfx;
        public AudioClip newTurnSfx;
        public AudioClip pointMadeSfx;
        public AudioClip buttonSfx;
        public AudioClip spinSfx;
        public AudioClip spinStopSfx;
        public AudioClip hitBallSfx;

        [Header("Reflection Probes")]
        public ReflectionProbe tableReflection;

        [Header("Meshes")]
        public Mesh[] cueballMeshes;
        public Mesh nineBall;
        public Mesh fourBallAdd;
        public Mesh fourBallMinus;

        private Quaternion baseObjectRot;

        /// <summary>
        /// True whilst balls are rolling
        /// </summary>
        [UdonSynced]
        private bool gameIsSimulating;
        /// <summary>
        /// Timer ID 2 bit	{ 0: inf, 1: 10s, 2: 15s, 3: 30s, 4: 60s, 5: undefined }
        /// </summary>
        [UdonSynced]
        private uint timerType;
        /// <summary>
        /// Permission for player to play
        /// </summary>
        [UdonSynced]
        private bool isPlayerAllowedToPlay;
        /// <summary>
        /// Player is hitting
        /// </summary>
        private bool isArmed;
        private int localPlayerID = -1;

        [UdonSynced]
        private bool guideLineEnabled = true;

        [Header("Desktop Stuff")]
        public GameObject desktopCursorObject;
        public GameObject desktopHitPosition;
        public GameObject desktopBase;
        public GameObject desktopQuad;
        public GameObject[] desktopCueParents;
        public GameObject desktopOverlayPower;
        public GameObject desktopEPopup;

        [Header("Networking Stuff")]
        public GameObject[] playerSlotOwners;

        [Header("UI Stuff")]
        //public Text[] lobbyNames;

        /*
         * Private variables
         */

        private AudioSource[] ballPool;
        private Transform[] ballPoolTransforms;
        private AudioSource mainSrc;

        /// <summary>
        /// 18:0 (0xffff)	Each bit represents each ball, if it has been pocketed or not
        /// </summary>
        [UdonSynced]
        private uint ballPocketedState;
        /// <summary>
        /// 19:1 (0x02)		Whos turn is it, 0 or 1
        /// </summary>
        [UdonSynced]
        private bool newIsTeam2Turn;
        /// <summary>
        /// 19:2 (0x04)		End-of-turn foul marker
        /// </summary>
        [UdonSynced]
        private bool isFoul;
        /// <summary>
        /// 19:3 (0x08)		Is the table open?
        /// </summary>
        [UdonSynced]
        private bool isOpen = true;
        /// <summary>
        /// 19:4 (0x10)		What colour the players have chosen
        /// </summary>
        [UdonSynced]
        private bool isPlayer2Blue;
        /// <summary>
        /// 19:5 (0x20)	The game isn't running
        /// </summary>
        [UdonSynced]
        private bool isGameInMenus = true;
        /// <summary>
        /// 19:6 (0x40)		Who won the game if sn_gameover is set
        /// </summary>
        [UdonSynced]
        private bool isTeam2Winner;
        /// <summary>
        /// Represents if the game is joinable
        /// </summary>
        [UdonSynced]
        private bool isTableLocked = true;
        /// <summary>
        /// 19:15 (0x8000)	Teams on/off (1 bit)
        /// </summary>
        [UdonSynced]
        private bool isTeams;
        /// <summary>
        /// 21:0 (0xffff)	Game number
        /// </summary>
        [UdonSynced]
        private uint gameID;

        // Cached data to use when checking for update.

        private uint oldPocketed;
        private bool oldIsTeam2Turn;
        private bool oldOpen;
        private bool oldIsGameInMenus;
        private uint oldGameID;

        /// <summary>
        /// We are waiting for our local simulation to finish, before we unpack data
        /// </summary>
        private bool isUpdateLocked;
        /// <summary>
        /// The first ball to be hit by cue ball
        /// </summary>
        private int isFirstHit;
        private int isSecondHit;
        private int isThirdHit;
        /// <summary>
        /// If the simulation was initiated by us, only set from update
        /// </summary>
        private bool isSimulatedByUs;
        /// <summary>
        /// Ball dropper timer
        /// </summary>
        private float introAminTimer;
        /// <summary>
        /// Tracker variable to see if balls are still on the go
        /// </summary>
        private bool ballsMoving;
        /// <summary>
        /// Repositioner is active
        /// </summary>
        [UdonSynced]
        private bool isRepositioningCueBall;
        /// <summary>
        /// For clamping to table or set lower for kitchen
        /// </summary>
        private float repoMaxX = TABLE_WIDTH;
        /// <summary>
        /// What should the timer run out at
        /// </summary>
        private float timerEnd;
        /// <summary>
        /// 1 over time limit
        /// </summary>
        private float timerRecp;
        private bool isTimerRunning;
        private bool isParticleAlive;
        private float particleTime;
        private bool isMadePoint;
        private bool isMadeFoul;

        [UdonSynced]
        private bool isKorean;

        [UdonSynced]
        private int[] scores = new int[2];

        private bool is8Ball;
        private bool isNineBall;
        private bool isFourBall;
        /// <summary>
        /// Game should run in practice mode
        /// </summary>
        private bool isGameModePractice;
        private bool isInDesktopTopDownView;
        /// <summary>
        /// Interpreted value
        /// </summary>
        private bool playerIsTeam2;

        [UdonSynced]
        private Vector3[] currentBallPositions = new Vector3[16];

        [UdonSynced]
        private Vector3[] currentBallVelocities = new Vector3[16];

        [UdonSynced]
        private Vector3[] currentAngularVelocities = new Vector3[16];

        /// <summary>
        /// Runtime target colour
        /// </summary>
        private Color tableSrcColour = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        /// <summary>
        /// Runtime actual colour
        /// </summary>
        private Color tableCurrentColour = new Color(1.0f, 1.0f, 1.0f, 1.0f);

        /// <summary>
        /// Team 0
        /// </summary>
        private Color pointerColour0;
        /// <summary>
        /// Team 1
        /// </summary>
        private Color pointerColour1;
        /// <summary>
        /// No team / open / 9 ball
        /// </summary>
        private Color pointerColour2;
        private Color pointerColourErr;
        private Color pointerClothColour;
        private Vector3 deskTopCursor = new Vector3(0.0f, 2.0f, 0.0f);
        private Vector3 desktopHitCursor = new Vector3(0.0f, 0.0f, 0.0f);
        private bool isDesktopShootingIn;
        private bool isDesktopSafeRemove = true;
        private Vector3 desktopShootVector;
        private Vector3 desktopSafeRemovePoint;
        private float desktopShootReference;
        private float desktopClampX = TABLE_WIDTH;
        private float desktopClampY = TABLE_HEIGHT;
        private bool isDesktopLocalTurn;
        private bool isEntertingDesktopModeThisFrame;
        /// <summary>
        /// Cue input tracking
        /// </summary>
        private Vector3 localSpacePositionOfCueTip;
        private Vector3 localSpacePositionOfCueTipLastFrame;
        private Vector3 cueLocalForwardDirection;
        private Vector3 cueArmedShotDirection;
        private float cueFDir;
        private Vector3 raySphereOutput;
        private uint lastViewTimer;
        private float timeLast;
        private float accumulation;
        private float shootAmt;
        private int[] rackOrder8Ball = { 9, 2, 10, 11, 1, 3, 4, 12, 5, 13, 14, 6, 15, 7, 8 };
        private int[] rackOrder9Ball = { 2, 3, 4, 5, 9, 6, 7, 8, 1 };
        private int[] brearows_9ball = { 0, 1, 2, 1, 0 };
        /// <summary>
        /// 19:8 (0x700)	Gamemode ID 3 bit	{ 0: 8 ball, 1: 9 ball, 2+: undefined }
        /// </summary>
        [UdonSynced]
        private uint gameMode;

        // Additional synced data added by the port to Manual Sync

        [UdonSynced]
        private int player1ID;
        [UdonSynced]
        private int player2ID;
        [UdonSynced]
        private int player3ID;
        [UdonSynced]
        private int player4ID;

        public FairlySadPanda.Utilities.Logger logger;

        [UdonSynced]
        private bool gameWasReset;

        private float ballShadowOffset;
        private MeshRenderer[] shadowRenders;

        private bool isPlayerInVR;
        private VRCPlayerApi localPlayer;
        private int networkingLocalPlayerID;
        private Transform markerTransform;

        private Camera desktopCamera;

        public void Start()
        {
            localPlayer = Networking.LocalPlayer;
            networkingLocalPlayerID = localPlayer.playerId;

            if (Utilities.IsValid(localPlayer))
            {
                isPlayerInVR = localPlayer.IsUserInVR();
            }

            // Disable the reflection probe on Quest.
#if UNITY_ANDROID
            tableReflection = null;
#endif

            tableMaterial = tableRenderer.material;
            baseObjectRot = baseObject.transform.rotation;

            ballRigidbodies = new Rigidbody[ballTransforms.Length];
            for (int i = 0; i < ballRigidbodies.Length; i++)
            {
                ballRigidbodies[i] = ballTransforms[i].GetComponent<Rigidbody>();
            }

            ballShadowPosConstraintTransforms = new Transform[ballShadowPosConstraints.Length];
            for (int i = 0; i < ballShadowPosConstraints.Length; i++)
            {
                ballShadowPosConstraintTransforms[i] = ballShadowPosConstraints[i].transform;
            }

            mainSrc = GetComponent<AudioSource>();

            if (audioSourcePoolContainer) // Xiexe: Use a pool for audio instead of using the PlayClipAtPoint method because PlayClipAtPoint is buggy and VRC audio controls do not modify it.
            {
                ballPool = audioSourcePoolContainer.GetComponentsInChildren<AudioSource>();
                ballPoolTransforms = new Transform[ballPool.Length];

                for (int i = 0; i < ballPool.Length; i++)
                {
                    ballPoolTransforms[i] = ballPool[i].transform;
                }
            }

            desktopCamera = desktopBase.GetComponentInChildren<Camera>();
            desktopCamera.enabled = false;

            point4BallTransform = point4Ball.transform;
            markerTransform = marker.transform;

            CopyGameStateToOldState();

            markerMaterial = marker.GetComponent<MeshRenderer>().material;
            cueMaterials[0] = cueRenderObjs[0].material;
            cueMaterials[1] = cueRenderObjs[1].material;

            cueMaterials[0].SetColor(uniformCueColour, tableBlack);
            cueMaterials[1].SetColor(uniformCueColour, tableBlack);

            cueTipTransform = cueTip.transform;

            if (tableReflection)
            {
                tableReflection.gameObject.SetActive(true);
                tableReflection.mode = ReflectionProbeMode.Realtime;
                tableReflection.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                tableReflection.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                tableReflection.RenderProbe();
            }

            if (guideline)
            {
                guideline.SetActive(false);
            }

            if (devhit)
            {
                devhit.SetActive(false);
            }

            if (marker)
            {
                marker.SetActive(false);
            }

            if (marker9ball)
            {
                marker9ball.SetActive(false);
            }

            if (point4Ball)
            {
                point4Ball.SetActive(false);
            }

            if (shadows)
            {
                shadows.SetActive(fakeBallShadows);
            }

            ballShadowOffset = ballTransforms[0].position.y - ballShadowPosConstraintTransforms[0].position.y;
            if (logger)
            {
                logger.Log(name, $"ball shadow offset is {ballShadowOffset}");
            }

            shadowRenders = shadows.GetComponentsInChildren<MeshRenderer>();

            Vector3 scale = baseObject.transform.lossyScale;
            if (!(scale.x == scale.y && scale.y == scale.z))
            {
                if (logger)
                {
                    logger.Warning(name, "You appear to have scaled this table in a non-uniform way. VRCBCE makes no guarantees of what might happen when you do this. May God have mercy on your soul.");
                }
                else
                {
                    Debug.LogWarning("You appear to have scaled VRCBCE in a non-uniform way. VRCBCE makes no guarantees of what might happen when you do this. May God have mercy on your soul.");
                }
            }

            if (scaleHitForceWithScaleBeyond1 && scale.x > 1f || scale.z > 1f)
            {
                float scaler = scale.x > scale.z ? scale.x : scale.z;
                forceMultiplier = DEFAULT_FORCE_MULTIPLIER * scaler;
                if (logger)
                {
                    logger.Log(name, $"Due to increased scale of {scaler} , setting force multiplier of hits to {DEFAULT_FORCE_MULTIPLIER} * {scaler} = {forceMultiplier}");
                }
            }
            else
            {
                forceMultiplier = DEFAULT_FORCE_MULTIPLIER;
            }
        }

        public void Update()
        {
            // Physics step accumulator routine
            float time = Time.timeSinceLevelLoad;
            float timeDelta = time - timeLast;

            timeLast = time;

            if (isParticleAlive)
            {
                float scale, s, v, e;

                // Evaluate time
                particleTime += Time.deltaTime * 0.25f;

                // Sustained step
                s = Mathf.Max(particleTime - 0.1f, 0.0f);
                v = Mathf.Min(particleTime * particleTime * 100.0f, 21.0f * s * Mathf.Exp(-15.0f * s));

                // Exponential step
                e = Mathf.Exp(-17.0f * Mathf.Pow(Mathf.Max(particleTime - 1.2f, 0.0f), 3.0f));

                scale = e * v * 2.0f;

                // Set scale
                point4BallTransform.localScale = new Vector3(scale, scale, scale);

                // Set position
                Vector3 temp = point4BallTransform.localPosition;
                temp.y = particleTime * 0.5f;
                point4BallTransform.localPosition = temp;

                // Particle death
                if (particleTime > 2.0f)
                {
                    isParticleAlive = false;

                    if (point4Ball)
                    {
                        point4Ball.SetActive(false);
                    }
                }
            }

            if (isInDesktopTopDownView)
            {
                HandleUpdatingDesktopViewUI();

                if (Input.GetKeyDown(KeyCode.E))
                {
                    OnDesktopTopDownViewExit();
                }
            }
            else if (canEnterDesktopTopDownView)
            {
                HandleUpdatingCanEnterDestopViewUI();

                if (Input.GetKeyDown(KeyCode.E))
                {
                    OnDesktopTopDownViewStart();
                }
            }

            // Run sim only if things are moving
            if (gameIsSimulating)
            {
                accumulation += timeDelta;

                if (accumulation > MAX_DELTA)
                {
                    accumulation = MAX_DELTA;
                }

                while (accumulation >= FIXED_TIME_STEP)
                {
                    AdvancePhysicsStep();
                    accumulation -= FIXED_TIME_STEP;
                }
            }
            else if (isGameInMenus)
            {
                return;
            }

            // Everything below this line only runs when the game is active.

            // Update rendering objects positions
            uint ballBit = 0x1u;
            for (int i = 0; i < 16; i++)
            {
                if ((ballBit & ballPocketedState) == 0x0u)
                {
                    ballTransforms[i].localPosition = currentBallPositions[i];
                }

                ballBit <<= 1;
            }

            localSpacePositionOfCueTip = transform.InverseTransformPoint(cueTip.transform.position);
            Vector3 copyOfLocalSpacePositionOfCueTip = localSpacePositionOfCueTip;

            // if shot is prepared for next hit
            if (isPlayerAllowedToPlay)
            {
                if (isRepositioningCueBall)
                {
                    // Clamp position to table / kitchen
                    Vector3 temp = markerTransform.localPosition;
                    temp.x = Mathf.Clamp(temp.x, -TABLE_WIDTH, repoMaxX);
                    temp.z = Mathf.Clamp(temp.z, -TABLE_HEIGHT, TABLE_HEIGHT);
                    temp.y = 0.0f;
                    markerTransform.localPosition = temp;
                    markerTransform.localRotation = quaternionIdentity;

                    currentBallPositions[0] = temp;
                    ballTransforms[0].localPosition = temp;

                    if (IsCueContacting())
                    {
                        markerMaterial.SetColor(uniformMarkerColour, markerNotOK);
                    }
                    else
                    {
                        markerMaterial.SetColor(uniformMarkerColour, markerOK);
                    }
                }

                Vector3 cueballPosition = currentBallPositions[0];

                if (isArmed)
                {
                    float sweepTimeBall = Vector3.Dot(cueballPosition - localSpacePositionOfCueTipLastFrame, cueLocalForwardDirection);

                    // Check for potential skips due to low frame rate
                    if (sweepTimeBall > 0.0f && sweepTimeBall < (localSpacePositionOfCueTipLastFrame - copyOfLocalSpacePositionOfCueTip).magnitude)
                    {
                        copyOfLocalSpacePositionOfCueTip = localSpacePositionOfCueTipLastFrame + (cueLocalForwardDirection * sweepTimeBall);
                    }

                    // Hit condition is when cuetip is gone inside ball
                    if ((copyOfLocalSpacePositionOfCueTip - cueballPosition).sqrMagnitude < BALL_RADIUS_SQUARED)
                    {
                        Vector3 horizontalForce = copyOfLocalSpacePositionOfCueTip - localSpacePositionOfCueTipLastFrame;
                        horizontalForce.y = 0.0f;

                        // Compute velocity delta
                        float vel = forceMultiplier * (horizontalForce.magnitude / Time.deltaTime);

                        // Clamp velocity input to 20 m/s ( moderate break speed )
                        currentBallVelocities[0] = cueArmedShotDirection * Mathf.Min(vel, 20.0f);

                        // Angular velocity: L=r(normalized)×p
                        Vector3 r = (raySphereOutput - cueballPosition) * BALL_1OR;
                        Vector3 p = cueLocalForwardDirection * vel;
                        currentAngularVelocities[0] = Vector3.Cross(r, p) * -50.0f;

                        HitBallWithCue();
                    }
                }
                else
                {
                    cueLocalForwardDirection = transform.InverseTransformVector(cueTip.transform.forward);

                    // Get where the cue will strike the ball
                    if (IsIntersectingWithSphere(copyOfLocalSpacePositionOfCueTip, cueLocalForwardDirection, cueballPosition))
                    {
                        if (guideLineEnabled && guideline)
                        {
                            guideline.SetActive(true);
                        }

                        if (devhit)
                        {
                            devhit.SetActive(true);
                            devhit.transform.localPosition = raySphereOutput;
                        }

                        cueArmedShotDirection = cueLocalForwardDirection;
                        cueArmedShotDirection.y = 0.0f;

                        if (!isInDesktopTopDownView)
                        {
                            // Compute deflection in VR mode
                            Vector3 scuffdir = cueballPosition - raySphereOutput;
                            scuffdir.y = 0.0f;
                            cueArmedShotDirection += scuffdir.normalized * 0.17f;
                        }

                        cueFDir = Mathf.Atan2(cueArmedShotDirection.z, cueArmedShotDirection.x);

                        // Update the prediction line direction
                        guideline.transform.localPosition = currentBallPositions[0];
                        guideline.transform.localEulerAngles = new Vector3(0.0f, -cueFDir * Mathf.Rad2Deg, 0.0f);
                    }
                    else
                    {
                        if (devhit)
                        {
                            devhit.SetActive(false);
                        }

                        if (guideline)
                        {
                            guideline.SetActive(false);
                        }
                    }
                }
            }

            localSpacePositionOfCueTipLastFrame = copyOfLocalSpacePositionOfCueTip;

            // Table outline colour
            if (isGameInMenus)
            {
                // Flashing if we won
                tableCurrentColour = tableSrcColour * ((Mathf.Sin(Time.timeSinceLevelLoad * 3.0f) * 0.5f) + 1.0f);
            }
            else
            {
                tableCurrentColour = Color.Lerp(tableCurrentColour, tableSrcColour, Time.deltaTime * 3.0f);
            }

            float timePercentage;

            if (isTimerRunning)
            {
                float timeleft = timerEnd - Time.timeSinceLevelLoad;

                if (timeleft < 0.0f)
                {
                    isTimerRunning = false;

                    // We are holding the stick so propogate the change
                    if (Networking.GetOwner(playerTotems[Convert.ToInt32(newIsTeam2Turn)]) == localPlayer)
                    {
                        Networking.SetOwner(localPlayer, gameObject);
                        OnTurnOverFoul();
                    }
                    else
                    {
                        // All local players freeze until next target
                        // can pick up and propogate timer end
                        isPlayerAllowedToPlay = false;
                    }

                    timePercentage = 0.0f;
                }
                else
                {
                    timePercentage = 1.0f - (timeleft * timerRecp);
                }
            }
            else
            {
                timePercentage = 0.0f;
            }

            tableMaterial.SetColor(uniformTableColour, new Color(tableCurrentColour.r, tableCurrentColour.g, tableCurrentColour.b, timePercentage));

            // Run the intro animation.
            if (introAminTimer > 0.0f)
            {
                introAminTimer -= Time.deltaTime;

                Vector3 temp;
                float atime;
                float aitime;

                if (introAminTimer < 0.0f)
                {
                    introAminTimer = 0.0f;
                }

                // Cueball drops late
                Transform ball = ballTransforms[0];
                temp = ball.localPosition;
                float height = ball.position.y - ballShadowOffset;

                atime = Mathf.Clamp(introAminTimer - 0.33f, 0.0f, 1.0f);
                aitime = 1.0f - atime;
                temp.y = Mathf.Abs(Mathf.Cos(atime * 6.29f)) * atime * 0.5f;
                ball.localPosition = temp;

                Vector3 scale = new Vector3(aitime, aitime, aitime);
                ball.localScale = scale;

                PositionConstraint posCon = ballShadowPosConstraints[0];
                posCon.constraintActive = false;
                Transform posConTrans = ballShadowPosConstraintTransforms[0];
                posConTrans.position = new Vector3(ball.position.x, height, ball.position.z);
                posConTrans.localScale = scale;

                MeshRenderer r = shadowRenders[0];
                Color c = r.material.color;
                r.material.color = new Color(c.r, c.g, c.b, aitime);

                for (int i = 1; i < 16; i++)
                {
                    ball = ballTransforms[i];

                    temp = ball.localPosition;
                    height = ball.position.y - ballShadowOffset;

                    atime = Mathf.Clamp(introAminTimer - 0.84f - (i * 0.03f), 0.0f, 1.0f);
                    aitime = 1.0f - atime;

                    temp.y = Mathf.Abs(Mathf.Cos(atime * 6.29f)) * atime * 0.5f;
                    ball.localPosition = temp;

                    scale = new Vector3(aitime, aitime, aitime);
                    ball.localScale = scale;

                    posCon = ballShadowPosConstraints[i];
                    posCon.constraintActive = false;
                    posConTrans = ballShadowPosConstraintTransforms[i];
                    posConTrans.position = new Vector3(ball.position.x, height, ball.position.z);
                    posConTrans.localScale = scale;

                    r = shadowRenders[i];
                    c = r.material.color;
                    r.material.color = new Color(c.r, c.g, c.b, aitime);
                }
            }
        }

        public void _ReEnableShadowConstraints()
        {
            foreach (PositionConstraint con in ballShadowPosConstraints)
            {
                con.constraintActive = true;
            }
        }

        public override void OnDeserialization()
        {
            // Check if local simulation is in progress, the event will fire off later when physics
            // are settled by the client
            if (gameIsSimulating)
            {
                isUpdateLocked = true;
            }
            else
            {
                // We are free to read this update
                ReadNetworkData();
            }
        }

        /// PUBLIC FUNCTIONS

        /// MENU ACCESSORS

        public void UnlockTable()
        {
            if (logger)
            {
                logger.Log(name, "UnlockTable");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isTableLocked = false;
            RefreshNetworkData(false);
        }

        public void LockTable()
        {
            if (logger)
            {
                logger.Log(name, "LockTable");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isTableLocked = true;
            RefreshNetworkData(false);
        }

        public void JoinGame(int playerNumber)
        {
            if (logger)
            {
                logger.Log(name, $"JoinGame: {playerNumber}");
            }

            Networking.SetOwner(localPlayer, gameObject);
            localPlayerID = playerNumber;

            switch (playerNumber)
            {
                case 0:
                    player1ID = networkingLocalPlayerID;
                    break;
                case 1:
                    player2ID = networkingLocalPlayerID;
                    break;
                case 2:
                    player3ID = networkingLocalPlayerID;
                    break;
                case 3:
                    player4ID = networkingLocalPlayerID;
                    break;
                default:
                    return;
            }

            RefreshNetworkData(false);
        }

        public void LeaveGame()
        {
            if (logger)
            {
                logger.Log(name, "LeaveGame");
            }

            Networking.SetOwner(localPlayer, gameObject);

            switch (localPlayerID)
            {
                case 0:
                    player1ID = 0;
                    break;
                case 1:
                    player2ID = 0;
                    break;
                case 2:
                    player3ID = 0;
                    break;
                case 3:
                    player4ID = 0;
                    break;
                default:
                    return;
            }

            localPlayerID = -1;

            RefreshNetworkData(false);
        }

        private void RemovePlayerFromGame(int playerID)
        {
            Networking.SetOwner(localPlayer, gameObject);

            switch (playerID)
            {
                case 0:
                    player1ID = 0;
                    break;
                case 1:
                    player2ID = 0;
                    break;
                case 2:
                    player3ID = 0;
                    break;
                case 3:
                    player4ID = 0;
                    break;
                default:
                    return;
            }

            RefreshNetworkData(false);
        }

        public void IncreaseTimer()
        {
            if (logger)
            {
                logger.Log(name, "IncreaseTimer");
            }

            Networking.SetOwner(localPlayer, gameObject);

            if (timerType < 4)
            {
                timerType++;
                RefreshNetworkData(false);
            }
        }

        public void DecreaseTimer()
        {
            if (logger)
            {
                logger.Log(name, "DecreaseTimer");
            }

            Networking.SetOwner(localPlayer, gameObject);

            if (timerType > 0)
            {
                timerType--;
                RefreshNetworkData(false);
            }
        }

        public void SelectTeams()
        {
            if (logger)
            {
                logger.Log(name, "SelectTeams");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isTeams = true;
            RefreshNetworkData(false);
        }

        public void DeselectTeams()
        {
            if (logger)
            {
                logger.Log(name, "DeselectTeams");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isTeams = false;
            RefreshNetworkData(false);
        }

        public void EnableGuideline()
        {
            if (logger)
            {
                logger.Log(name, "EnableGuideline");
            }

            Networking.SetOwner(localPlayer, gameObject);
            guideLineEnabled = true;
            RefreshNetworkData(false);
        }

        public void DisableGuideline()
        {
            if (logger)
            {
                logger.Log(name, "DisableGuideline");
            }

            Networking.SetOwner(localPlayer, gameObject);
            guideLineEnabled = false;
            RefreshNetworkData(false);
        }

        /// <summary>
        /// Initialize new match as the host.
        /// </summary>
        public void StartNewGame()
        {
            if (logger)
            {
                logger.Log(name, "StartNewGame");
            }

            if (!isGameInMenus)
            {
                return;
            }

            gameWasReset = false;
            gameID++;
            isPlayerAllowedToPlay = true;

            OnRemoteNewGame();

            newIsTeam2Turn = false;
            oldIsTeam2Turn = false;
            OnRemoteTurnChange();

            // Following is overrides of NewGameLocal, for game STARTER only
            gameIsSimulating = false;
            isOpen = true;
            isGameInMenus = false;
            isPlayer2Blue = false;
            isTeam2Winner = false;

            // Cue ball
            currentBallPositions[0] = new Vector3(-SPOT_POSITION_X, 0.0f, 0.0f);
            currentBallVelocities[0] = vectorZero;

            // Start at spot

            if (isNineBall) // 9 ball
            {
                Initialize9Ball();
            }
            else if (isFourBall) // 4 ball
            {
                Initialize4Ball();
            }
            else // Normal 8 ball modes
            {
                Initialize8Ball();
            }

            oldPocketed = ballPocketedState;

            ApplyTableColour(false);

            Networking.SetOwner(localPlayer, gameObject);

            RefreshNetworkData(false);
        }

        private void Initialize9Ball()
        {
            ballPocketedState = 0xFC00u;

            for (int i = 0, k = 0; i < 5; i++)
            {
                int rown = brearows_9ball[i];
                for (int j = 0; j <= rown; j++)
                {
                    currentBallPositions[rackOrder9Ball[k++]] = new Vector3
                    (
                        SPOT_POSITION_X + (i * BALL_PL_Y) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F),
                        0.0f,
                        ((-rown + (j * 2)) * BALL_PL_X) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F)
                    );

                    currentBallVelocities[k] = vectorZero;
                    currentAngularVelocities[k] = vectorZero;
                }
            }
        }

        private void Initialize4Ball()
        {
            ballPocketedState = 0xFDF2u;

            currentBallPositions[0] = new Vector3(-SPOT_CAROM_X, 0.0f, 0.0f);
            currentBallPositions[9] = new Vector3(SPOT_CAROM_X, 0.0f, 0.0f);
            currentBallPositions[2] = new Vector3(SPOT_POSITION_X, 0.0f, 0.0f);
            currentBallPositions[3] = new Vector3(-SPOT_POSITION_X, 0.0f, 0.0f);

            currentBallVelocities[0] = vectorZero;
            currentBallVelocities[9] = vectorZero;
            currentBallVelocities[2] = vectorZero;
            currentBallVelocities[3] = vectorZero;

            currentAngularVelocities[0] = vectorZero;
            currentAngularVelocities[9] = vectorZero;
            currentAngularVelocities[2] = vectorZero;
            currentAngularVelocities[3] = vectorZero;
        }

        private void Initialize8Ball()
        {
            ballPocketedState = 0x00u; // No balls are pocketed.

            for (int i = 0, k = 0; i < 5; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    currentBallPositions[rackOrder8Ball[k++]] = new Vector3
                    (
                        SPOT_POSITION_X + (i * BALL_PL_Y) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F),
                        0.0f,
                        ((-i + (j * 2)) * BALL_PL_X) + UnityEngine.Random.Range(-RANDOMIZE_F, RANDOMIZE_F)
                    );

                    currentBallVelocities[k] = vectorZero;
                    currentAngularVelocities[k] = vectorZero;
                }
            }
        }

        public void Select8Ball()
        {
            if (logger)
            {
                logger.Log(name, "Select8Ball");
            }

            Networking.SetOwner(localPlayer, gameObject);

            gameMode = 0u;
            RefreshNetworkData(false);
        }

        public void Select9Ball()
        {
            if (logger)
            {
                logger.Log(name, "Select9Ball");
            }

            Networking.SetOwner(localPlayer, gameObject);

            gameMode = 1u;
            RefreshNetworkData(false);
        }

        /// <summary>
        /// Player selected Japanese 4-ball.
        /// </summary>
        public void Select4BallJapanese()
        {
            if (logger)
            {
                logger.Log(name, "Select4BallJapanese");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isKorean = false;

            gameMode = 2u;
            RefreshNetworkData(false);
        }

        /// <summary>
        /// Player selected Korean 4-ball.
        /// </summary>
        public void Select4BallKorean()
        {
            if (logger)
            {
                logger.Log(name, "Select4BallKorean");
            }

            Networking.SetOwner(localPlayer, gameObject);

            isKorean = true;

            gameMode = 2u;
            RefreshNetworkData(false);
        }

        /// CUE ACTIONS

        /// <summary>
        /// Player is holding input trigger
        /// </summary>
        public void StartHit()
        {
            if (logger)
            {
                logger.Log(name, "StartHit");
            }

            // lock aim variables
            bool isOurTurn = ((localPlayerID >= 0) && (playerIsTeam2 == newIsTeam2Turn)) || isGameModePractice;

            if (isOurTurn)
            {
                isArmed = true;
            }
        }

        /// <summary>
        /// Player stopped holding input trigger
        /// </summary>
        public void EndHit()
        {
            if (logger)
            {
                logger.Log(name, "EndHit");
            }

            isArmed = false;
            //guidelineMat.SetColor("_Colour", aimAiming);
        }

        /// <summary>
        /// Player was moving cueball, place it down
        /// </summary>
        public void PlaceBall()
        {
            if (logger)
            {
                logger.Log(name, "PlaceBall");
            }

            if (!IsCueContacting())
            {
                if (logger)
                {
                    logger.Log(name, "disabling marker because the ball hase been placed");
                }

                isRepositioningCueBall = false;

                if (marker)
                {
                    marker.SetActive(false);
                }

                isPlayerAllowedToPlay = true;
                isFoul = false;

                Networking.SetOwner(localPlayer, gameObject);

                // Save out position to remote clients
                RefreshNetworkData(newIsTeam2Turn);
            }
        }

        /// <summary>
        /// Completely reset state
        /// </summary>
        public void _ForceReset()
        {
            if (logger)
            {
                logger.Log(name, "ForceReset");
            }

            if (
                // If you are a player
                networkingLocalPlayerID == player1ID ||
                networkingLocalPlayerID == player2ID ||
                networkingLocalPlayerID == player3ID ||
                networkingLocalPlayerID == player4ID ||
                // The game is in the menu so resetting doesn't matter much
                isGameInMenus ||
                // The game is in a running state, someone has left, and the table has entered an invalid state
                (player1ID > 0 && VRCPlayerApi.GetPlayerById(player1ID) == null) ||
                (player2ID > 0 && VRCPlayerApi.GetPlayerById(player2ID) == null) ||
                (player3ID > 0 && VRCPlayerApi.GetPlayerById(player3ID) == null) ||
                (player4ID > 0 && VRCPlayerApi.GetPlayerById(player4ID) == null)
                )
            {
                Networking.SetOwner(localPlayer, gameObject);

                Reset();
            }
            else if (logger)
            {
                logger.Log(name, "Cannot reset table: You must be a player, or the table must be in an invalid state.");
            }
        }

        private void Reset()
        {
            isGameInMenus = true;
            isPlayerAllowedToPlay = false;
            gameIsSimulating = false;
            newIsTeam2Turn = false;

            RefreshNetworkData(newIsTeam2Turn);

            if (logger)
            {
                logger.Log(name, "Forcing a reset was successful.");
            }
        }

        public void OnDesktopTopDownViewStart()
        {
            if (logger)
            {
                logger.Log(name, "OnDesktopTopDownViewStart");
            }

            isInDesktopTopDownView = true;
            isEntertingDesktopModeThisFrame = true;

            if (desktopBase)
            {
                desktopBase.SetActive(true);
                desktopCamera.enabled = true;
            }

            // Lock player in place
            localPlayer.Immobilize(true);

            gripControllers[0].localPlayerIsInDesktopTopDownView = true;
            gripControllers[1].localPlayerIsInDesktopTopDownView = true;
        }

        /// <summary>
        /// Cue put down local
        /// </summary>
        public void OnPutDownCueLocally()
        {
            if (logger)
            {
                logger.Log(name, "OnPutDownCueLocally");
            }

            OnDesktopTopDownViewExit();
        }

        private void AdvancePhysicsStep()
        {
            ballsMoving = false;

            uint ballBit = 0x1u;

            // Cue angular velocity
            if ((ballPocketedState & 0x1U) == 0) // If cueball is not sunk
            {
                if (!IsCollisionWithCueBallInevitable())
                {
                    // Apply movement
                    currentBallPositions[0] += currentBallVelocities[0] * FIXED_TIME_STEP;
                }

                AdvanceSimulationForBall(0);
            }

            // Run main simulation / inter-ball collision
            for (int i = 1; i < 16; i++)
            {
                ballBit <<= 1;

                if ((ballBit & ballPocketedState) == 0U) // If the ball in question is not sunk
                {
                    currentBallPositions[i] += currentBallVelocities[i] * FIXED_TIME_STEP;

                    AdvanceSimulationForBall(i);
                }
            }

            // Check if simulation has settled
            if (!ballsMoving && gameIsSimulating)
            {
                gameIsSimulating = false;

                // Make sure we only run this from the client who initiated the move
                if (isSimulatedByUs)
                {
                    HandleEndOfTurn();
                }

                // Check if there was a network update on hold
                if (isUpdateLocked)
                {
                    isUpdateLocked = false;

                    ReadNetworkData();
                }

                return;
            }

            if (isFourBall)
            {
                BounceBallOffCushionIfApplicable(0);
                BounceBallOffCushionIfApplicable(2);
                BounceBallOffCushionIfApplicable(3);
                BounceBallOffCushionIfApplicable(9);

                return;
            }

            ballBit = 0x1U;

            // Run edge collision
            for (int index = 0; index < 16; index++)
            {
                if ((ballBit & ballPocketedState) == 0U)
                {
                    float zy, zx, zk, zw, d, k, i, j, l, r;
                    Vector3 A, N;

                    A = currentBallPositions[index];

                    // REGIONS
                    /*
                        *  QUADS:							SUBSECTION:				SUBSECTION:
                        *    zx, zy:							zz:						zw:
                        *
                        *  o----o----o  +:  1			\_________/				\_________/
                        *  | -+ | ++ |  -: -1		     |	    /		              /  /
                        *  |----+----|					  -  |  +   |		      -     /   |
                        *  | -- | +- |						  |	   |		          /  +  |
                        *  o----o----o						  |      |             /       |
                        *
                        */

                    // Setup major regions
                    zx = Mathf.Sign(A.x);
                    zy = Mathf.Sign(A.z);

                    // within pocket regions
                    if ((A.z * zy > (TABLE_HEIGHT - POCKET_RADIUS)) && (A.x * zx > (TABLE_WIDTH - POCKET_RADIUS) || A.x * zx < POCKET_RADIUS))
                    {
                        // Subregions
                        zw = A.z * zy > (A.x * zx) - TABLE_WIDTH + TABLE_HEIGHT ? 1.0f : -1.0f;

                        // Normalization / line coefficients change depending on sub-region
                        if (A.x * zx > TABLE_WIDTH * 0.5f)
                        {
                            zk = 1.0f;
                            r = ONE_OVER_ROOT_TWO;
                        }
                        else
                        {
                            zk = -2.0f;
                            r = ONE_OVER_ROOT_FIVE;
                        }

                        // Collider line EQ
                        d = zx * zy * zk; // Coefficient
                        k = (-(TABLE_WIDTH * Mathf.Max(zk, 0.0f)) + (POCKET_RADIUS * zw * Mathf.Abs(zk)) + TABLE_HEIGHT) * zy; // Konstant

                        // Check if colliding
                        l = zw * zy;
                        if (A.z * l > ((A.x * d) + k) * l)
                        {
                            // Get line normal
                            N.x = zx * zk;
                            N.z = -zy;
                            N.y = 0.0f;
                            N *= zw * r;

                            // New position
                            i = ((A.x * d) + A.z - k) / (2.0f * d);
                            j = (i * d) + k;

                            currentBallPositions[index].x = i;
                            currentBallPositions[index].z = j;

                            // Reflect velocity
                            ApplyBounceCushion(index, N);
                        }
                    }
                    else // edges
                    {
                        if (A.x * zx > TABLE_WIDTH)
                        {
                            currentBallPositions[index].x = TABLE_WIDTH * zx;
                            ApplyBounceCushion(index, Vector3.left * zx);
                        }

                        if (A.z * zy > TABLE_HEIGHT)
                        {
                            currentBallPositions[index].z = TABLE_HEIGHT * zy;
                            ApplyBounceCushion(index, Vector3.back * zy);
                        }
                    }
                }

                ballBit <<= 1;
            }

            ballBit = 0x1U;

            // Run triggers
            for (int i = 0; i < 16; i++)
            {
                if ((ballBit & ballPocketedState) == 0U)
                {
                    float zz, zx;
                    Vector3 A = currentBallPositions[i];

                    // Setup major regions
                    zx = Mathf.Sign(A.x);
                    zz = Mathf.Sign(A.z);

                    // Its in a pocket
                    if (
                        A.z * zz > TABLE_HEIGHT + POCKET_DEPTH ||
                        A.z * zz > (A.x * -zx) + TABLE_WIDTH + TABLE_HEIGHT + POCKET_DEPTH
                    )
                    {
                        uint total = 0U;

                        // Get total for X positioning
                        int count_extent = isNineBall ? 10 : 16;
                        for (int j = 1; j < count_extent; j++)
                        {
                            total += (ballPocketedState >> j) & 0x1U;
                        }

                        // set this for later
                        currentBallPositions[i].x = -0.9847f + (total * BALL_DIAMETER);
                        currentBallPositions[i].z = 0.768f;

                        // This is where we actually save the pocketed/non-pocketed state of balls.
                        ballPocketedState ^= 1U << i;

                        uint bmask = 0x1FCU << ((int)(Convert.ToUInt32(newIsTeam2Turn) ^ Convert.ToUInt32(isPlayer2Blue)) * 7);
                        mainSrc.PlayOneShot(sinkSfx, 1.0f);

                        // If good pocket
                        if (((0x1U << i) & (bmask | (isOpen ? 0xFFFCU : 0x0000U) | ((bmask & ballPocketedState) == bmask ? 0x2U : 0x0U))) > 0)
                        {
                            // Make a bright flash
                            tableCurrentColour *= 1.9f;
                        }
                        else
                        {
                            tableCurrentColour = pointerColourErr;
                        }

                        // VFX ( make ball move )
                        Rigidbody body = ballTransforms[i].GetComponent<Rigidbody>();
                        body.isKinematic = false;
                        body.velocity = baseObjectRot * new Vector3(
                            currentBallVelocities[i].x,
                            0.0f,
                            currentBallVelocities[i].z
                        );
                    }
                }

                ballBit <<= 1;
            }
        }

        private void HandleEndOfTurn()
        {
            isSimulatedByUs = false;

            // We are updating the game state so make sure we are network owner
            Networking.SetOwner(Networking.LocalPlayer, gameObject);

            // Owner state checks

            /*
            FSP note: For clarity, I've moved Harry's bitmasking here to use binary notation (0b) rather than hex (0x).

            Some notes on what's going on below:
            &= is bitwise AND. 0b0101 &= 0b0100 is 0b0100
            |= is bitwise OR. 0b0101 |= 0b0100 is 0b0101
            << is a bitshift leftwards.
            */

            uint bmask = 0b1111111111111100;
            uint emask = 0b0000000000000000;

            // Quash down the mask if table has closed
            if (!isOpen)
            {
                bmask &= 0x1FCu << ((int)(Convert.ToUInt32(isPlayer2Blue) ^ Convert.ToUInt32(newIsTeam2Turn)) * 7);
                emask = 0x1FCu << ((int)(Convert.ToUInt32(isPlayer2Blue) ^ Convert.ToUInt32(!newIsTeam2Turn)) * 7);
            }

            // Common informations
            bool isSetComplete = (ballPocketedState & bmask) == bmask;

            bool isScratch = (ballPocketedState & 0x1U) == 0x1U;

            // Append black to mask if set is done
            if (isSetComplete)
            {
                bmask |= 0x2U;
            }

            // These are the resultant states we can set for each mode
            // then the rest is taken care of
            bool
                isObjectiveSink,
                isOpponentSink,
                winCondition,
                foulCondition,
                deferLossCondition
            ;

            if (is8Ball)    // Standard 8 ball
            {
                isObjectiveSink = (ballPocketedState & bmask) > (oldPocketed & bmask);
                isOpponentSink = (ballPocketedState & emask) > (oldPocketed & emask);
                // Calculate if objective was not hit first
                bool isWrongHit = ((0x1U << isFirstHit) & bmask) == 0;
                bool is8Sink = (ballPocketedState & 0x2U) == 0x2U;
                winCondition = isSetComplete && is8Sink;
                foulCondition = isScratch || isWrongHit;
                deferLossCondition = is8Sink;
            }
            else if (isNineBall)   // 9 ball
            {
                // Rules are from: https://www.youtube.com/watch?v=U0SbHOXCtFw

                // Rule #1: Cueball must strike the lowest number ball, first
                bool isWrongHit = GetLowestNumberedBall(oldPocketed) != isFirstHit;

                // Rule #2: Pocketing cueball, is a foul

                // Win condition: Pocket 9 ball ( at anytime )
                winCondition = (ballPocketedState & 0x200u) == 0x200u;

                // this video is hard to follow so im just gonna guess this is right
                isObjectiveSink = (ballPocketedState & 0x3FEu) > (oldPocketed & 0x3FEu);

                isOpponentSink = false;
                deferLossCondition = false;

                foulCondition = isWrongHit || isScratch;

                // TODO: Implement rail contact requirement
            }
            else if (isFourBall) // 4 ball
            {
                isObjectiveSink = isMadePoint;
                isOpponentSink = isMadeFoul;
                foulCondition = false;
                deferLossCondition = false;

                winCondition = scores[Convert.ToInt32(newIsTeam2Turn)] >= 10;
            }
            else // Unkown mode
            {
                isObjectiveSink = true;
                isOpponentSink = false;
                winCondition = false;
                foulCondition = false;
                deferLossCondition = false;

                if ((ballPocketedState & 0x1u) == 0x1u)
                {
                    isFoul = true;
                    OnRemoteTurnChange();
                }
            }

            HandleTableBallStateAtEndOfTurn(winCondition, foulCondition, deferLossCondition, isObjectiveSink, isOpponentSink);
        }

        private void HandleTableBallStateAtEndOfTurn(bool winCondition, bool foulCondition, bool deferLossCondition, bool isObjectiveSink, bool isOpponentSink)
        {
            if (winCondition)
            {
                if (foulCondition)
                {
                    // Loss
                    OnTurnOverGameWon(!newIsTeam2Turn);
                }
                else
                {
                    // Win
                    OnTurnOverGameWon(newIsTeam2Turn);
                }
            }
            else if (deferLossCondition)
            {
                // Loss
                OnTurnOverGameWon(!newIsTeam2Turn);
            }
            else if (foulCondition)
            {
                // Foul
                OnTurnOverFoul();
            }
            else if (isObjectiveSink && !isOpponentSink)
            {
                // Continue
                // Close table if it was open ( 8 ball specific )
                if (is8Ball && isOpen)
                {
                    uint sunkOranges = 0;
                    uint sunkBlues = 0;
                    uint pmask = ballPocketedState >> 2;

                    for (int i = 0; i < 7; i++)
                    {
                        if ((pmask & 0x1u) == 0x1u)
                        {
                            sunkBlues++;
                        }

                        pmask >>= 1;
                    }
                    for (int i = 0; i < 7; i++)
                    {
                        if ((pmask & 0x1u) == 0x1u)
                        {
                            sunkOranges++;
                        }

                        pmask >>= 1;
                    }

                    if (sunkBlues != sunkOranges)
                    {
                        isPlayer2Blue = (sunkBlues > sunkOranges) ? newIsTeam2Turn : !newIsTeam2Turn;

                        isOpen = false;
                        ApplyTableColour(newIsTeam2Turn);
                    }
                }

                // Keep playing
                isPlayerAllowedToPlay = true;

                RefreshNetworkData(newIsTeam2Turn);
            }
            else
            {
                // Pass
                isPlayerAllowedToPlay = true;

                if (isFourBall)
                {
                    Vector3 temp = currentBallPositions[0];
                    currentBallPositions[0] = currentBallPositions[9];
                    currentBallPositions[9] = temp;
                }

                RefreshNetworkData(!newIsTeam2Turn);
            }
        }

        private void RefreshNetworkData(bool newIsTeam2Playing)
        {
            if (logger)
            {
                logger.Log(name, "RefreshNetworkData");
            }

            newIsTeam2Turn = newIsTeam2Playing;

            Networking.SetOwner(localPlayer, gameObject);
            RequestSerialization();
            ReadNetworkData();
        }

        /// <summary>
        /// Decode networking string
        /// </summary>
        private void ReadNetworkData()
        {
            if (logger)
            {
                logger.Log(name, "ReadNetworkData");
            }

            if (marker)
            {
                marker.SetActive(false);
            }

            // Events ==========================================================================================================

            if (gameID > oldGameID && !isGameInMenus)
            {
                OnRemoteNewGame();

                if (((localPlayerID >= 0) && (playerIsTeam2 == newIsTeam2Turn)) || isGameModePractice)
                {
                    if (logger)
                    {
                        logger.Log(name, "enabling marker because it is the start of the game and we are breaking");
                    }

                    isRepositioningCueBall = true;
                    repoMaxX = -SPOT_POSITION_X;

                    if (marker)
                    {
                        markerTransform.localPosition = currentBallPositions[0];
                        if (!isFourBall)
                        {
                            marker.SetActive(true);
                        }
                        ((VRC_Pickup)marker.gameObject.GetComponent(typeof(VRC_Pickup))).pickupable = true;
                    }
                }
                else
                {
                    markerTransform.localPosition = currentBallPositions[0];

                    if (!isFourBall)
                    {
                        marker.SetActive(true);
                    }

                    ((VRC_Pickup)marker.gameObject.GetComponent(typeof(VRC_Pickup))).pickupable = false;
                }
            }

            if (newIsTeam2Turn != oldIsTeam2Turn)
            {
                OnRemoteTurnChange();
            }

            oldIsTeam2Turn = newIsTeam2Turn;

            if (oldOpen && !isOpen)
            {
                ApplyTableColour(newIsTeam2Turn);
            }

            if (!oldIsGameInMenus && isGameInMenus)
            {
                OnRemoteGameOver();
            }

            CopyGameStateToOldState();

            if (isTableLocked)
            {
                poolMenu.EnableUnlockTableButton();
                ResetScores();
            }
            else if (!isGameInMenus)
            {
                poolMenu.EnableResetButton();
                UpdateScores();
            }
            else
            {
                poolMenu.EnableMainMenu();
                ResetScores();
            }

            poolMenu.UpdateMainMenuView(
                isTeams,
                newIsTeam2Turn,
                (int)gameMode,
                isKorean,
                (int)timerType,
                player1ID,
                player2ID,
                player3ID,
                player4ID,
                guideLineEnabled
            );

            if (isGameInMenus)
            {
                if (lastViewTimer != timerType)
                {
                    mainSrc.PlayOneShot(spinSfx);
                    lastViewTimer = timerType;
                }

                int numberOfPlayers = 0;

                if (!isTableLocked)
                {
                    if (player1ID != 0)
                    {
                        numberOfPlayers++;
                    }

                    if (player2ID != 0)
                    {
                        numberOfPlayers++;
                    }

                    if (player3ID != 0)
                    {
                        numberOfPlayers++;
                    }

                    if (player4ID != 0)
                    {
                        numberOfPlayers++;
                    }
                }

                isGameModePractice = localPlayerID == 0 && numberOfPlayers == 1;
                return;
            }

            // Effects colliders need to be turned off when not simulating
            // to improve pickups being glitchy
            if (gameIsSimulating)
            {
                if (tableCollisionParent)
                {
                    tableCollisionParent.SetActive(true);
                }
            }
            else
            {
                if (tableCollisionParent)
                {
                    tableCollisionParent.SetActive(false);
                }
            }

            if (isFourBall)
            {
                ballPocketedState = 0xFDF2u;
            }

            // Check this every read
            // Its basically 'turn start' event
            if (isPlayerAllowedToPlay)
            {
                if (((localPlayerID >= 0) && (playerIsTeam2 == newIsTeam2Turn)) || isGameModePractice)
                {
                    // Update for desktop
                    isDesktopLocalTurn = true;

                    // Reset hit point
                    desktopHitCursor = vectorZero;
                }
                else
                {
                    isDesktopLocalTurn = false;
                }

                if (isNineBall)
                {
                    int target = GetLowestNumberedBall(ballPocketedState);

                    if (marker9ball)
                    {
                        marker9ball.SetActive(true);
                        marker9ball.transform.localPosition = currentBallPositions[target];
                    }
                }

                RackBalls();

                if (timerType > 0 && !isTimerRunning)
                {
                    ResetTimer();
                }
            }
            else
            {
                if (marker9ball)
                {
                    marker9ball.SetActive(false);
                }

                isTimerRunning = false;
                isMadePoint = false;
                isMadeFoul = false;
                isFirstHit = 0;
                isSecondHit = 0;
                isThirdHit = 0;

                if (devhit)
                {
                    devhit.SetActive(false);
                }

                if (guideline)
                {
                    guideline.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Updates table colour target to appropriate player colour
        /// </summary>
        private void ApplyTableColour(bool isTeam2Turn)
        {
            if (logger)
            {
                logger.Log(name, "ApplyTableColour");
            }

            if (isFourBall)
            {
                if (!newIsTeam2Turn)
                {
                    cueRenderObjs[0].materials[0].SetColor(uniformCueColour, pointerColour0);
                    cueRenderObjs[1].materials[0].SetColor(uniformCueColour, pointerColour1 * 0.5f);
                }
                else
                {
                    cueRenderObjs[0].materials[0].SetColor(uniformCueColour, pointerColour0 * 0.5f);
                    cueRenderObjs[1].materials[0].SetColor(uniformCueColour, pointerColour1);
                }

                tableSrcColour = tableBlack;
            }
            else if (isNineBall)
            {
                cueRenderObjs[Convert.ToInt32(newIsTeam2Turn)].materials[0].SetColor(uniformCueColour, tableWhite);
                cueRenderObjs[Convert.ToInt32(!newIsTeam2Turn)].materials[0].SetColor(uniformCueColour, tableBlack);

                tableSrcColour = pointerColour2;
            }
            else if (!isOpen)
            {
                if (isTeam2Turn)
                {
                    if (isPlayer2Blue)
                    {
                        tableSrcColour = tableBlue;
                        cueRenderObjs[0].material.SetColor(uniformCueColour, tableOrange * 0.33f);
                        cueRenderObjs[1].material.SetColor(uniformCueColour, tableBlue);
                    }
                    else
                    {
                        tableSrcColour = tableOrange;
                        cueRenderObjs[0].material.SetColor(uniformCueColour, tableBlue * 0.33f);
                        cueRenderObjs[1].material.SetColor(uniformCueColour, tableOrange);
                    }
                }
                else
                {
                    if (isPlayer2Blue)
                    {
                        tableSrcColour = tableOrange;
                        cueRenderObjs[0].material.SetColor(uniformCueColour, tableOrange);
                        cueRenderObjs[1].material.SetColor(uniformCueColour, tableBlue * 0.33f);
                    }
                    else
                    {
                        tableSrcColour = tableBlue;
                        cueRenderObjs[0].material.SetColor(uniformCueColour, tableBlue);
                        cueRenderObjs[1].material.SetColor(uniformCueColour, tableOrange * 0.33f);
                    }
                }
            }
            else
            {
                tableSrcColour = pointerColour2;

                cueRenderObjs[Convert.ToInt32(newIsTeam2Turn)].materials[0].SetColor(uniformCueColour, tableWhite);
                cueRenderObjs[Convert.ToInt32(!newIsTeam2Turn)].materials[0].SetColor(uniformCueColour, tableBlack);
            }

            cueGrips[Convert.ToInt32(newIsTeam2Turn)].SetColor(uniformMarkerColour, gripColourActive);
            cueGrips[Convert.ToInt32(!newIsTeam2Turn)].SetColor(uniformMarkerColour, gripColourInactive);
        }

        private void SpawnFloaty(Vector3 pos, Mesh m)
        {
            if (logger)
            {
                logger.Log(name, "SpawnFloaty");
            }

            if (point4Ball)
            {
                point4Ball.SetActive(true);
            }

            isParticleAlive = true;
            particleTime = 0.1f;

            // orient to be looking at player
            Vector3 lpos = localPlayer.GetPosition();
            Vector3 delta = lpos - transform.TransformPoint(pos);
            float r = Mathf.Atan2(delta.x, delta.z);
            point4BallTransform.localRotation = Quaternion.AngleAxis(r * Mathf.Rad2Deg, Vector3.up);

            // set position
            point4BallTransform.localPosition = pos;

            // Set scale
            point4BallTransform.localScale = vectorZero;

            point4Ball.GetComponent<MeshFilter>().sharedMesh = m;
        }

        private void ResetTimer()
        {
            if (logger)
            {
                logger.Log(name, "ResetTimer");
            }

            if (timerType == 0)
            {
                timerEnd = Time.timeSinceLevelLoad + 30.0f;
                timerRecp = 0.03333333333f;
            }
            else
            {
                timerEnd = Time.timeSinceLevelLoad + 60.0f;
                timerRecp = 0.01666666666f;
            }

            isTimerRunning = true;
        }

        private void OnLocalCaromPoint(Vector3 p)
        {
            isMadePoint = true;
            mainSrc.PlayOneShot(pointMadeSfx, 1.0f);

            scores[Convert.ToUInt32(newIsTeam2Turn)]++;

            if (scores[Convert.ToUInt32(newIsTeam2Turn)] > 10)
            {
                scores[Convert.ToUInt32(newIsTeam2Turn)] = 10;
            }

            SpawnFloaty(p, fourBallAdd);
        }

        /// <summary>
        /// End of the game. Both with/loss
        /// </summary>
        private void OnRemoteGameOver()
        {
            if (logger)
            {
                logger.Log(name, "OnRemoteGameOver");
            }

            ApplyTableColour(isTeam2Winner);

            if (gameWasReset)
            {
                poolMenu.GameWasReset();
            }
            else
            {
                poolMenu.TeamWins(isTeam2Winner);
            }

            if (marker9ball)
            {
                marker9ball.SetActive(false);
            }

            if (tableCollisionParent)
            {
                tableCollisionParent.SetActive(false);
            }

            RackBalls();   // To make sure rigidbodies are completely off

            if (logger)
            {
                logger.Log(name, "disabling marker because the game is over");
            }

            isRepositioningCueBall = false;

            if (marker)
            {
                marker.SetActive(false);
            }

            // Remove any access rights
            localPlayerID = -1;
            GrantCueAccess();

            player1ID = 0;
            player2ID = 0;
            player3ID = 0;
            player4ID = 0;

            poolMenu.UpdateMainMenuView(
                isTeams,
                newIsTeam2Turn,
                (int)gameMode,
                isKorean,
                (int)timerType,
                player1ID,
                player2ID,
                player3ID,
                player4ID,
                guideLineEnabled
            );

            poolMenu.EnableMainMenu();
        }

        private void OnRemoteTurnChange()
        {
            if (logger)
            {
                logger.Log(name, "OnRemoteTurnChange");
            }

            // Effects
            ApplyTableColour(newIsTeam2Turn);
            mainSrc.PlayOneShot(newTurnSfx, 1.0f);

            // Register correct cuetip
            cueTip = cueTips[Convert.ToUInt32(newIsTeam2Turn)];

            bool isOurTurn = ((localPlayerID >= 0) && (playerIsTeam2 == newIsTeam2Turn)) || isGameModePractice;
            if (isFourBall) // 4 ball
            {
                if (!newIsTeam2Turn)
                {
                    logger.Log(name, "0 ball is 0 mesh, 9 ball is 1 mesh");

                    ballTransforms[0].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[0];
                    ballTransforms[9].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[1];
                }
                else
                {
                    logger.Log(name, "0 ball is 0 mesh, 9 ball is 1 mesh");

                    ballTransforms[9].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[0];
                    ballTransforms[0].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[1];
                }
            }
            else
            {
                // White was pocketed
                if ((ballPocketedState & 0x1u) == 0x1u)
                {
                    currentBallPositions[0] = vectorZero;
                    currentBallVelocities[0] = vectorZero;
                    currentAngularVelocities[0] = vectorZero;

                    ballPocketedState &= 0xFFFFFFFEu;
                }
            }

            if (isFoul)
            {
                if (isOurTurn)
                {
                    isRepositioningCueBall = true;
                    repoMaxX = TABLE_WIDTH;

                    if (logger)
                    {
                        logger.Log(name, "enabling marker because it is our turn and there was a foul last turn");
                    }

                    if (marker)
                    {
                        marker.SetActive(true);
                        ((VRC_Pickup)marker.gameObject.GetComponent(typeof(VRC_Pickup))).pickupable = true;
                        markerTransform.localPosition = currentBallPositions[0];
                    }
                }
                else
                {
                    marker.SetActive(true);
                    markerTransform.localPosition = currentBallPositions[0];
                    ((VRC_Pickup)marker.gameObject.GetComponent(typeof(VRC_Pickup))).pickupable = false;
                }
            }

            // Force timer reset
            if (timerType > 0)
            {
                ResetTimer();
            }
        }

        /// <summary>
        /// Grant cue access if we are playing
        /// </summary>
        private void GrantCueAccess()
        {
            if (logger)
            {
                logger.Log(name, "GrantCueAccess");
            }

            if (localPlayerID > -1)
            {
                if (isGameModePractice)
                {
                    gripControllers[0].AllowAccess();
                    gripControllers[1].AllowAccess();
                }
                else if (!playerIsTeam2)                       // Local player is 1, or 3
                {
                    gripControllers[0].AllowAccess();
                    gripControllers[1].DenyAccess();
                }
                else                                                            // Local player is 0, or 2
                {
                    gripControllers[1].AllowAccess();
                    gripControllers[0].DenyAccess();
                }
            }
            else
            {
                gripControllers[0].DenyAccess();
                gripControllers[1].DenyAccess();
            }
        }

        // The generic new game setup that runs for everyone.
        private void OnRemoteNewGame()
        {
            if (logger)
            {
                logger.Log(name, "OnRemoteNewGame");
            }

            poolMenu.EnableResetButton();

            is8Ball = gameMode == 0u;
            isNineBall = gameMode == 1u;
            isFourBall = gameMode == 2u;

            if (localPlayerID >= 0)
            {
                playerIsTeam2 = localPlayerID % 2 == 1;
            }

            if (isNineBall)    // 9 Ball / USA colours
            {
                pointerColour0 = tableLightBlue;
                pointerColour1 = tableLightBlue;
                pointerColour2 = tableLightBlue;

                pointerColourErr = tableBlack;    // No error effect
                pointerClothColour = fabricBlue;

                // 9 ball only uses one colourset / cloth colour

                foreach (MeshRenderer renderer in ballRenderers)
                {
                    renderer.material.SetTexture("_MainTex", sets[3]);
                }
            }
            else if (isFourBall)
            {
                pointerColour0 = tableWhite;
                pointerColour1 = tableYellow;

                // Should not be used
                pointerColour2 = tableRed;
                pointerColourErr = tableRed;

                foreach (MeshRenderer renderer in ballRenderers)
                {
                    renderer.material.SetTexture("_MainTex", sets[2]);
                }
                pointerClothColour = fabricGreen;
            }
            else // Standard 8 ball derivatives
            {
                pointerColourErr = tableRed;
                pointerColour2 = tableWhite;

                pointerColour0 = tableBlue;
                pointerColour1 = tableOrange;

                foreach (MeshRenderer renderer in ballRenderers)
                {
                    renderer.material.SetTexture("_MainTex", sets[0]);
                }

                pointerClothColour = fabricGray;
            }

            tableMaterial.SetColor("_ClothColour", pointerClothColour);

            if (tableReflection)
            {
                tableReflection.RenderProbe();
            }

            ApplyTableColour(false);
            GrantCueAccess();

            if (isNineBall)    // 9 ball specific
            {
                if (marker9ball)
                {
                    marker9ball.SetActive(true);
                }
            }
            else
            {
                if (marker9ball)
                {
                    marker9ball.SetActive(false);
                }
            }

            if (isFourBall) // 4 ball specific
            {
                if (pocketBlockers)
                {
                    pocketBlockers.SetActive(true);
                }

                scores[0] = 0;
                scores[1] = 0;

                // Reset mesh filters on balls that change them
                ballTransforms[0].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[0];
                ballTransforms[9].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[1];
            }
            else
            {
                if (pocketBlockers)
                {
                    pocketBlockers.SetActive(false);
                }

                // Reset mesh filters on balls that change them
                ballTransforms[0].GetComponent<MeshFilter>().sharedMesh = cueballMeshes[0];
                ballTransforms[9].GetComponent<MeshFilter>().sharedMesh = nineBall;
            }

            if (isNineBall)
            {
                for (int i = 0; i <= 9; i++)
                {
                    if (ballTransforms[i])
                    {
                        ballTransforms[i].gameObject.SetActive(true);
                    }

                    if (ballShadowPosConstraints[i])
                    {
                        ballShadowPosConstraints[i].gameObject.SetActive(true);
                    }
                }

                for (int i = 10; i < 16; i++)
                {
                    if (ballTransforms[i])
                    {
                        ballTransforms[i].gameObject.SetActive(false);
                    }

                    if (ballShadowPosConstraints[i])
                    {
                        ballShadowPosConstraints[i].gameObject.SetActive(false);
                    }
                }
            }
            else if (isFourBall)
            {
                for (int i = 1; i < 16; i++)
                {
                    if (ballTransforms[i])
                    {
                        ballTransforms[i].gameObject.SetActive(false);
                    }

                    if (ballShadowPosConstraints[i])
                    {
                        ballShadowPosConstraints[i].gameObject.SetActive(false);
                    }
                }

                if (ballTransforms[0])
                {
                    ballTransforms[0].gameObject.SetActive(true);
                }

                if (ballTransforms[2])
                {
                    ballTransforms[2].gameObject.SetActive(true);
                }

                if (ballTransforms[3])
                {
                    ballTransforms[3].gameObject.SetActive(true);
                }

                if (ballTransforms[9])
                {
                    ballTransforms[9].gameObject.SetActive(true);
                }

                if (ballShadowPosConstraints[0])
                {
                    ballShadowPosConstraints[0].gameObject.SetActive(true);
                }

                if (ballShadowPosConstraints[2])
                {
                    ballShadowPosConstraints[2].gameObject.SetActive(true);
                }

                if (ballShadowPosConstraints[3])
                {
                    ballShadowPosConstraints[3].gameObject.SetActive(true);
                }

                if (ballShadowPosConstraints[9])
                {
                    ballShadowPosConstraints[9].gameObject.SetActive(true);
                }
            }
            else
            {
                for (int i = 0; i < 16; i++)
                {
                    if (ballTransforms[i])
                    {
                        ballTransforms[i].gameObject.SetActive(true);
                    }

                    if (ballShadowPosConstraints[i])
                    {
                        ballShadowPosConstraints[i].gameObject.SetActive(true);
                    }
                }
            }

            // Effects
            introAminTimer = introAnimationLength;
            if (introAminTimer > 0.0f)
            {
                mainSrc.PlayOneShot(introSfx, 1.0f);
                SendCustomEventDelayedSeconds(nameof(_ReEnableShadowConstraints), introAnimationLength);
            }

            isTimerRunning = false;

            gripControllers[0].localPlayerIsInDesktopTopDownView = false;
            gripControllers[1].localPlayerIsInDesktopTopDownView = false;

            if (!localPlayer.IsUserInVR())
            {
                gripControllers[0].usingDesktop = true;
                gripControllers[1].usingDesktop = true;
            }
            else
            {
                gripControllers[0].usingDesktop = false;
                gripControllers[1].usingDesktop = false;
            }
        }

        /// <summary>
        /// Finalize positions onto their rack spots
        /// </summary>
        private void RackBalls()
        {
            if (logger)
            {
                logger.Log(name, "RackBalls");
            }

            uint ball_bit = 0x1u;

            for (int i = 0; i < 16; i++)
            {
                ballTransforms[i].GetComponent<Rigidbody>().isKinematic = true;

                if ((ball_bit & ballPocketedState) == ball_bit)
                {
                    ballTransforms[i].localPosition = new Vector3(
                        currentBallPositions[i].x,
                        RACHEIGHT,
                        currentBallPositions[i].z
                    );
                }

                ball_bit <<= 1;
            }
        }

        /// <summary>
        /// Is cue touching another ball?
        /// </summary>
        private bool IsCueContacting()
        {
            // 8 ball, practice, portal
            if (gameMode != 1u)
            {
                // Check all
                for (int i = 1; i < 16; i++)
                {
                    if ((currentBallPositions[0] - currentBallPositions[i]).sqrMagnitude < BALL_DSQR)
                    {
                        return true;
                    }
                }
            }
            else // 9 ball
            {
                // Only check to 9 ball
                for (int i = 1; i <= 9; i++)
                {
                    if ((currentBallPositions[0] - currentBallPositions[i]).sqrMagnitude < BALL_DSQR)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Apply cushion bounce
        /// </summary>
        /// <param name="id"></param>
        /// <param name="N"></param>
        private void ApplyBounceCushion(int id, Vector3 N)
        {
            // Mathematical expressions derived from: https://billiards.colostate.edu/physics_articles/Mathavan_IMechE_2010.pdf
            //
            // (Note): subscript gamma, u, are used in replacement of Y and Z in these expressions because
            // unicode does not have them.
            //
            // f = 2/7
            // f₁ = 5/7
            //
            // Velocity delta:
            //   Δvₓ = −vₓ∙( f∙sin²θ + (1+e)∙cos²θ ) − Rωᵤ∙sinθ
            //   Δvᵧ = 0
            //   Δvᵤ = f₁∙vᵤ + fR( ωₓ∙sinθ - ωᵧ∙cosθ ) - vᵤ
            //
            // Aux:
            //   Sₓ = vₓ∙sinθ - vᵧ∙cosθ+ωᵤ
            //   Sᵧ = 0
            //   Sᵤ = -vᵤ - ωᵧ∙cosθ + ωₓ∙cosθ
            //
            //   k = (5∙Sᵤ) / ( 2∙mRA );
            //   c = vₓ∙cosθ - vᵧ∙cosθ
            //
            // Angular delta:
            //   ωₓ = k∙sinθ
            //   ωᵧ = k∙cosθ
            //   ωᵤ = (5/(2m))∙(-Sₓ / A + ((sinθ∙c∙(e+1)) / B)∙(cosθ - sinθ));
            //
            // These expressions are in the reference frame of the cushion, so V and ω inputs need to be rotated

            // Reject bounce if velocity is going the same way as normal
            // this state means we tunneled, but it happens only on the corner
            // vertexes
            Vector3 source_v = currentBallVelocities[id];
            if (Vector3.Dot(source_v, N) > 0.0f)
            {
                return;
            }

            // Rotate V, W to be in the reference frame of cushion
            Quaternion rq = Quaternion.AngleAxis(Mathf.Atan2(-N.z, -N.x) * Mathf.Rad2Deg, Vector3.up);
            Quaternion rb = Quaternion.Inverse(rq);
            Vector3 V = rq * source_v;
            Vector3 W = rq * currentAngularVelocities[id];

            Vector3 V1;
            Vector3 W1;
            float k, c, s_x, s_z;

            //V1.x = -V.x * ((2.0f/7.0f) * SINA2 + EP1 * COSA2) - (2.0f/7.0f) * BALL_PL_X * W.z * SINA;
            //V1.z = (5.0f/7.0f)*V.z + (2.0f/7.0f) * BALL_PL_X * (W.x * SINA - W.y * COSA) - V.z;
            //V1.y = 0.0f;
            // (baked):
            V1.x = (-V.x * F) - (0.00240675711f * W.z);
            V1.z = (0.71428571428f * V.z) + (0.00857142857f * ((W.x * SINA) - (W.y * COSA))) - V.z;
            V1.y = 0.0f;

            // s_x = V.x * SINA - V.y * COSA + W.z;
            // (baked): y component not used:
            s_x = (V.x * SINA) + W.z;
            s_z = -V.z - (W.y * COSA) + (W.x * SINA);

            // k = (5.0f * s_z) / ( 2 * BALL_MASS * A );
            // (baked):
            k = s_z * 0.71428571428f;

            // c = V.x * COSA - V.y * COSA;
            // (baked): y component not used
            c = V.x * COSA;

            W1.x = k * SINA;

            //W1.z = (5.0f / (2.0f * BALL_MASS)) * (-s_x / A + ((SINA * c * EP1) / B) * (COSA - SINA));
            // (baked):
            W1.z = 15.625f * ((-s_x * 0.04571428571f) + (c * 0.0546021744f));
            W1.y = k * COSA;

            // Unrotate result
            currentBallVelocities[id] += rb * V1;
            currentAngularVelocities[id] += rb * W1;
        }

        /// <summary>
        /// Pocketless table
        /// </summary>
        /// <param name="id"></param>
        private void BounceBallOffCushionIfApplicable(int id)
        {
            float zz, zx;
            Vector3 A = currentBallPositions[id];

            // Setup major regions
            zx = Mathf.Sign(A.x);
            zz = Mathf.Sign(A.z);

            if (A.x * zx > TABLE_WIDTH)
            {
                currentBallPositions[id].x = TABLE_WIDTH * zx;
                ApplyBounceCushion(id, Vector3.left * zx);
            }

            if (A.z * zz > TABLE_HEIGHT)
            {
                currentBallPositions[id].z = TABLE_HEIGHT * zz;
                ApplyBounceCushion(id, Vector3.back * zz);
            }
        }

        /// <summary>
        /// Advance simulation 1 step for ball id
        /// </summary>
        /// <param name="ballID"></param>
        private void AdvanceSimulationForBall(int ballID)
        {
            // Since v1.5.0
            Vector3 V = currentBallVelocities[ballID];
            Vector3 W = currentAngularVelocities[ballID];

            // Equations derived from: http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.89.4627&rep=rep1&type=pdf
            //
            // R: Contact location with ball and floor aka: (0,-r,0)
            // µₛ: Slipping friction coefficient
            // µᵣ: Rolling friction coefficient
            // i: Up vector aka: (0,1,0)
            // g: Planet Earth's gravitation acceleration ( 9.80665 )
            //
            // Relative contact velocity (marlow):
            //   c = v + R✕ω
            //
            // Ball is classified as 'rolling' or 'slipping'. Rolling is when the relative velocity is none and the ball is
            // said to be in pure rolling motion
            //
            // When ball is classified as rolling:
            //   Δv = -µᵣ∙g∙Δt∙(v/|v|)
            //
            // Angular momentum can therefore be derived as:
            //   ωₓ = -vᵤ/R
            //   ωᵧ =  0
            //   ωᵤ =  vₓ/R
            //
            // In the slipping state:
            //   Δω = ((-5∙µₛ∙g)/(2/R))∙Δt∙i✕(c/|c|)
            //   Δv = -µₛ∙g∙Δt(c/|c|)

            // Relative contact velocity of ball and table
            Vector3 cv = V + Vector3.Cross(CONTACT_POINT, W);

            // Rolling is achieved when cv's length is approaching 0
            // The epsilon is quite high here because of the fairly large timestep we are working with
            if (cv.magnitude <= 0.1f)
            {
                //V += -F_ROLL * GRAVITY * FIXED_TIME_STEP * V.normalized;
                // (baked):
                V += -0.00122583125f * V.normalized;

                // Calculate rolling angular velocity
                W.x = -V.z * BALL_1OR;

                if (0.3f > Mathf.Abs(W.y))
                {
                    W.y = 0.0f;
                }
                else
                {
                    W.y -= Mathf.Sign(W.y) * 0.3f;
                }

                W.z = V.x * BALL_1OR;

                // Stopping scenario
                if (V.magnitude < 0.01f && W.magnitude < 0.2f)
                {
                    W = vectorZero;
                    V = vectorZero;
                }
                else
                {
                    ballsMoving = true;
                }
            }
            else // Slipping
            {
                Vector3 nv = cv.normalized;

                // Angular slipping friction
                //W += ((-5.0f * F_SLIDE * 9.8f)/(2.0f * 0.03f)) * FIXED_TIME_STEP * Vector3.Cross( Vector3.up, nv );
                // (baked):
                W += -2.04305208f * Vector3.Cross(Vector3.up, nv);
                V += -F_SLIDE * 9.8f * FIXED_TIME_STEP * nv;

                ballsMoving = true;
            }

            currentAngularVelocities[ballID] = W;
            currentBallVelocities[ballID] = V;

            // FSP [22/03/21]: Use the base object's rotation as a factor in the axis. This stops the balls spinning incorrectly.
            ballTransforms[ballID].Rotate((baseObjectRot * W).normalized, W.magnitude * FIXED_TIME_STEP * -Mathf.Rad2Deg, Space.World);

            uint ball_bit = 0x1U << ballID;

            // ball/ball collisions
            for (int i = ballID + 1; i < 16; i++)
            {
                ball_bit <<= 1;

                // If the ball has been pocketed it cannot be collided with.
                if ((ball_bit & ballPocketedState) != 0U)
                {
                    continue;
                }

                Vector3 delta = currentBallPositions[i] - currentBallPositions[ballID];
                float dist = delta.magnitude;

                if (dist < BALL_DIAMETER)
                {
                    Vector3 normal = delta / dist;

                    Vector3 velocityDelta = currentBallVelocities[ballID] - currentBallVelocities[i];

                    float dot = Vector3.Dot(velocityDelta, normal);

                    if (dot > 0.0f)
                    {
                        Vector3 reflection = normal * dot;
                        currentBallVelocities[ballID] -= reflection;
                        currentBallVelocities[i] += reflection;

                        // Prevent sound spam if it happens
                        if (currentBallVelocities[ballID].sqrMagnitude > 0 && currentBallVelocities[i].sqrMagnitude > 0)
                        {
                            int clip = UnityEngine.Random.Range(0, hitsSfx.Length - 1);
                            float vol = Mathf.Clamp01(currentBallVelocities[ballID].magnitude * reflection.magnitude);
                            ballPoolTransforms[ballID].position = ballTransforms[ballID].position;
                            ballPool[ballID].PlayOneShot(hitsSfx[clip], vol);
                        }

                        // First hit detected
                        if (ballID == 0)
                        {
                            if (isFourBall)
                            {
                                if (isKorean)  // KR 사구 ( Sagu )
                                {
                                    if (i == 9)
                                    {
                                        if (!isMadeFoul)
                                        {
                                            isMadeFoul = true;
                                            scores[Convert.ToUInt32(newIsTeam2Turn)]--;

                                            if (scores[Convert.ToUInt32(newIsTeam2Turn)] < 0)
                                            {
                                                scores[Convert.ToUInt32(newIsTeam2Turn)] = 0;
                                            }

                                            SpawnFloaty(currentBallPositions[i], fourBallMinus);
                                        }
                                    }
                                    else if (isFirstHit == 0)
                                    {
                                        isFirstHit = i;
                                    }
                                    else if (i != isFirstHit)
                                    {
                                        if (isSecondHit == 0)
                                        {
                                            isSecondHit = i;
                                            OnLocalCaromPoint(currentBallPositions[i]);
                                        }
                                    }
                                }
                                else // JP 四つ玉 ( Yotsudama )
                                {
                                    if (isFirstHit == 0)
                                    {
                                        isFirstHit = i;
                                    }
                                    else if (isSecondHit == 0)
                                    {
                                        if (i != isFirstHit)
                                        {
                                            isSecondHit = i;
                                            OnLocalCaromPoint(currentBallPositions[i]);
                                        }
                                    }
                                    else if (isThirdHit == 0)
                                    {
                                        if (i != isFirstHit && i != isSecondHit)
                                        {
                                            isThirdHit = i;
                                            OnLocalCaromPoint(currentBallPositions[i]);
                                        }
                                    }
                                }
                            }
                            else if (isFirstHit == 0)
                            {
                                isFirstHit = i;
                            }
                        }
                    }
                }
            }
        }

        // TODO: This is a single-use function we can refactor. Note that its use is to equate a bool,
        //       so it's more acceptable to hold on to.
        // ( Since v0.2.0a ) Check if we can predict a collision before move update happens to improve accuracy
        private bool IsCollisionWithCueBallInevitable()
        {
            // Get what will be the next position
            Vector3 originalDelta = currentBallVelocities[0] * FIXED_TIME_STEP;
            Vector3 norm = currentBallVelocities[0].normalized;

            Vector3 h;
            float lf, s, nmag;

            // Closest found values
            float minlf = 9999999.0f;
            int minid = 0;
            float mins = 0;

            uint ball_bit = 0x1U;

            // Loop balls look for collisions
            for (int i = 1; i < 16; i++)
            {
                ball_bit <<= 1;

                if ((ball_bit & ballPocketedState) != 0U)
                {
                    continue;
                }

                h = currentBallPositions[i] - currentBallPositions[0];
                lf = Vector3.Dot(norm, h);
                s = BALL_DSQRPE - Vector3.Dot(h, h) + (lf * lf);

                if (s < 0.0f)
                {
                    continue;
                }

                if (lf < minlf)
                {
                    minlf = lf;
                    minid = i;
                    mins = s;
                }
            }

            if (minid > 0)
            {
                nmag = minlf - Mathf.Sqrt(mins);

                // Assign new position if got appropriate magnitude
                if (nmag * nmag < originalDelta.sqrMagnitude)
                {
                    currentBallPositions[0] += norm * nmag;
                    return true;
                }
            }

            return false;
        }

        // TODO: This is a single-use function we can refactor. Note that its use is to equate a bool,
        //       so it's more acceptable to hold on to.
        private bool IsIntersectingWithSphere(Vector3 start, Vector3 dir, Vector3 sphere)
        {
            Vector3 nrm = dir.normalized;
            Vector3 h = sphere - start;
            float lf = Vector3.Dot(nrm, h);
            float s = BALL_RADIUS_SQUARED - Vector3.Dot(h, h) + (lf * lf);

            if (s < 0.0f)
            {
                return false;
            }

            s = Mathf.Sqrt(s);

            if (lf < s)
            {
                if (lf + s >= 0)
                {
                    s = -s;
                }
                else
                {
                    return false;
                }
            }

            raySphereOutput = start + (nrm * (lf - s));
            return true;
        }

        /// <summary>
        /// Find the lowest numbered ball, that isnt the cue, on the table
        /// This function finds the VISUALLY represented lowest ball,
        /// since 8 has id 1, the search needs to be split
        /// </summary>
        /// <param name="field"></param>
        private int GetLowestNumberedBall(uint field)
        {
            for (int i = 2; i <= 8; i++)
            {
                if (((field >> i) & 0x1U) == 0x00U)
                {
                    return i;
                }
            }

            if ((field & 0x2U) == 0x00U)
            {
                return 1;
            }

            for (int i = 9; i < 16; i++)
            {
                if (((field >> i) & 0x1U) == 0x00U)
                {
                    return i;
                }
            }

            // ??
            return 0;
        }

        private void OnTurnOverGameWon(bool newIsTeam2Winner)
        {
            if (logger)
            {
                logger.Log(name, "OnTurnOverGameWon");
            }

            isGameInMenus = true;
            isTeam2Winner = newIsTeam2Winner;

            RefreshNetworkData(newIsTeam2Turn);
        }

        private void OnTurnOverFoul()
        {
            if (logger)
            {
                logger.Log(name, "OnTurnOverFoul");
            }

            isFoul = true;
            isPlayerAllowedToPlay = true;

            RefreshNetworkData(!newIsTeam2Turn);
        }

        /// <summary>
        /// Copy current values to previous values
        /// </summary>
        private void CopyGameStateToOldState()
        {
            oldOpen = isOpen;
            oldIsGameInMenus = isGameInMenus;
            oldGameID = gameID;
        }

        private void OnDesktopTopDownViewExit()
        {
            if (logger)
            {
                logger.Log(name, "OnDesktopTopDownViewExit");
            }

            isInDesktopTopDownView = false;

            if (desktopBase)
            {
                desktopBase.SetActive(false);

                if (desktopCamera == null)
                {
                    desktopCamera = desktopBase.GetComponentInChildren<Camera>();
                }

                desktopCamera.enabled = false;
            }

            gripControllers[0].localPlayerIsInDesktopTopDownView = false;
            gripControllers[1].localPlayerIsInDesktopTopDownView = false;

            Networking.LocalPlayer.Immobilize(false);

            gripControllers[0].EnableConstraints();
            gripControllers[1].EnableConstraints();
        }

        // TODO: Single use function, but it short-circuits so cannot be easily put into its using function.
        private void HandleUpdatingDesktopViewUI()
        {
            if (isEntertingDesktopModeThisFrame)
            {
                isEntertingDesktopModeThisFrame = false;
                return;
            }

            // // Keep UI rendering
            // if (Utilities.IsValid(localPlayer))
            // {
            //     VRCPlayerApi.TrackingData hmd = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            //     desktopQuad.transform.position = hmd.position + (hmd.rotation * Vector3.forward);
            // }

            // desktopEPopup.transform.position = desktopQuad.transform.position;

            deskTopCursor.x = Mathf.Clamp
            (
                deskTopCursor.x + (Input.GetAxis("Mouse X") * desktopCursorSpeed),
                -desktopClampX,
                 desktopClampX
            );
            deskTopCursor.z = Mathf.Clamp
            (
                deskTopCursor.z + (Input.GetAxis("Mouse Y") * desktopCursorSpeed),
                -desktopClampY,
                 desktopClampY
            );

            if (isDesktopLocalTurn)
            {
                Vector3 ncursor = deskTopCursor;
                ncursor.y = 0.0f;
                Vector3 delta = ncursor - currentBallPositions[0];
                GameObject cue = desktopCueParents[Convert.ToUInt32(newIsTeam2Turn)];

                if (Input.GetButton("Fire1"))
                {
                    if (!isDesktopShootingIn)
                    {
                        isDesktopShootingIn = true;

                        // Create shooting vector
                        desktopShootVector = delta.normalized;

                        // Project reference start point
                        desktopShootReference = Vector3.Dot(desktopShootVector, ncursor);

                        // Create copy of cursor for later
                        desktopSafeRemovePoint = deskTopCursor;

                        // Unlock cursor position from table
                        desktopClampX = Mathf.Infinity;
                        desktopClampY = Mathf.Infinity;
                    }

                    // Calculate shoot amount via projection
                    shootAmt = desktopShootReference - Vector3.Dot(desktopShootVector, ncursor);
                    isDesktopSafeRemove = shootAmt < 0.0f;

                    shootAmt = Mathf.Clamp(shootAmt, 0.0f, 0.5f);

                    // Set delta back to dkShootVector
                    delta = desktopShootVector;

                    // Disable cursor in shooting mode
                    if (desktopCursorObject)
                    {
                        desktopCursorObject.SetActive(false);
                    }
                }
                else
                {
                    // Trigger shot
                    if (isDesktopShootingIn)
                    {
                        // Shot cancel
                        if (!isDesktopSafeRemove)
                        {
                            // Fake hit ( kinda )
                            float vel = Mathf.Pow(shootAmt * 2.0f, 1.4f) * 9.0f;

                            currentBallVelocities[0] = desktopShootVector * vel;

                            Vector3 r_1 = (raySphereOutput - currentBallPositions[0]) * BALL_1OR;
                            Vector3 p = desktopShootVector.normalized * vel;
                            currentAngularVelocities[0] = Vector3.Cross(r_1, p) * -25.0f;
                            cue.transform.localPosition = new Vector3(2000.0f, 2000.0f, 2000.0f);
                            isDesktopLocalTurn = false;
                            HitBallWithCue();
                        }

                        // Restore cursor position
                        deskTopCursor = desktopSafeRemovePoint;
                        desktopClampX = TABLE_WIDTH;
                        desktopClampY = TABLE_HEIGHT;

                        // 1-frame override to fix rotation
                        delta = desktopShootVector;
                    }

                    isDesktopShootingIn = false;
                    shootAmt = 0.0f;

                    if (desktopCursorObject)
                    {
                        desktopCursorObject.SetActive(true);
                    }
                }

                if (Input.GetKey(KeyCode.W))
                {
                    desktopHitCursor += Vector3.forward * Time.deltaTime;
                }
                if (Input.GetKey(KeyCode.S))
                {
                    desktopHitCursor += Vector3.back * Time.deltaTime;
                }
                if (Input.GetKey(KeyCode.A))
                {
                    desktopHitCursor += Vector3.left * Time.deltaTime;
                }
                if (Input.GetKey(KeyCode.D))
                {
                    desktopHitCursor += Vector3.right * Time.deltaTime;
                }

                // Clamp in circle
                if (desktopHitCursor.magnitude > 0.90f)
                {
                    desktopHitCursor = desktopHitCursor.normalized * 0.9f;
                }

                desktopHitPosition.transform.localPosition = desktopHitCursor;

                // Create rotation
                Quaternion xr = Quaternion.AngleAxis(10.0f, Vector3.right);
                Quaternion r = Quaternion.AngleAxis(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, Vector3.up);

                Vector3 worldHit = new Vector3(desktopHitCursor.x * BALL_PL_X, desktopHitCursor.z * BALL_PL_X, -0.89f - shootAmt);

                cue.transform.localRotation = r * xr;
                cue.transform.position = gameObject.transform.TransformPoint(currentBallPositions[0] + (r * xr * worldHit));
            }

            desktopCursorObject.transform.localPosition = deskTopCursor;
            desktopOverlayPower.transform.localScale = new Vector3(1.0f - (shootAmt * 2.0f), 1.0f, 1.0f);
        }

        private void HitBallWithCue()
        {
            if (logger)
            {
                logger.Log(name, "disabling marker because the ball hase been hit");
            }
            isRepositioningCueBall = false;

            if (marker)
            {
                marker.SetActive(false);
            }

            if (devhit)
            {
                devhit.SetActive(false);
            }

            if (guideline)
            {
                guideline.SetActive(false);
            }

            // Remove locks
            EndHit();
            isPlayerAllowedToPlay = false;
            isFoul = false;    // In case did not drop foul marker

            // Commit changes
            gameIsSimulating = true;
            oldPocketed = ballPocketedState;

            // Make sure we definately are the network owner
            Networking.SetOwner(localPlayer, gameObject);

            RefreshNetworkData(newIsTeam2Turn);

            isSimulatedByUs = true;

            float vol = Mathf.Clamp(currentBallVelocities[0].magnitude * 0.1f, 0f, 0.6f);
            cueTipSrc.transform.position = cueTipTransform.position;
            cueTipSrc.PlayOneShot(hitBallSfx, vol);
        }

        private void UpdateScores()
        {
            if (logger)
            {
                logger.Log(name, "UpdateScores");
            }

            if (isFourBall)
            {
                poolMenu.SetScore(false, scores[0]);
                poolMenu.SetScore(true, scores[1]);
            }
            else if (isNineBall)
            {
                poolMenu.SetScore(false, -1);
                poolMenu.SetScore(true, -1);
            }
            else
            {
                int[] counters = new int[2];
                uint temp = ballPocketedState;

                for (int i = 0; i < counters.Length; i++)
                {
                    for (int j = 0; j < 7; j++)
                    {
                        if ((temp & 0x4) > 0)
                        {
                            counters[i ^ Convert.ToUInt32(isPlayer2Blue)]++;
                        }

                        temp >>= 1;
                    }
                }

                if (isGameInMenus)
                {
                    counters[Convert.ToUInt32(isTeam2Winner)] += (int)((ballPocketedState & 0x2) >> 1);
                }

                poolMenu.SetScore(false, counters[0]);
                poolMenu.SetScore(true, counters[1]);
            }
        }

        private void ResetScores()
        {
            if (logger)
            {
                logger.Log(name, "ResetScores");
            }

            poolMenu.SetScore(false, 0);
            poolMenu.SetScore(true, 0);
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (!Networking.IsOwner(localPlayer, gameObject) || !Utilities.IsValid(player))
            {
                return;
            }

            int playerID = player.playerId;
            if (playerID == player1ID || playerID == player2ID || playerID == player3ID || playerID == player4ID)
            {
                if (isGameInMenus)
                {
                    RemovePlayerFromGame(playerID);
                }
                else
                {
                    gameWasReset = true;
                    Reset();
                }
            }
        }

        /// <Summary> The object that contains the UI that appears when you can press E to enter the destop top-down view. </Summary>
        [Tooltip("The object that contains the UI that appears when you can press E to enter the destop top-down view")]
        public GameObject pressE;
        /// <Summary> Is the local player near the table? </Summary>
        private bool isNearTable;
        /// <Summary> Can the local player enter the desktop top-down view?
        private bool canEnterDesktopTopDownView;

        [HideInInspector]
        public ushort numberOfCuesHeldByLocalPlayer;

        public void LocalPlayerEnteredAreaNearTable()
        {
            if (!isPlayerInVR)
            {
                isNearTable = true;
                CheckIfCanEnterDesktopTopDownView();
            }
        }

        /// <Summary> Is the local player near the table? </Summary>
        public void LocalPlayerLeftAreaNearTable()
        {
            isNearTable = false;
            CannotEnterDesktopTopDownView();
        }

        public void LocalPlayerPickedUpCue()
        {
            numberOfCuesHeldByLocalPlayer++;
            CheckIfCanEnterDesktopTopDownView();
        }

        public void LocalPlayerDroppedCue()
        {
            numberOfCuesHeldByLocalPlayer--;
            if (numberOfCuesHeldByLocalPlayer == 0)
            {
                CannotEnterDesktopTopDownView();
            }
        }

        private void CheckIfCanEnterDesktopTopDownView()
        {
            if (isNearTable && numberOfCuesHeldByLocalPlayer > 0)
            {
                canEnterDesktopTopDownView = true;

                if (pressE)
                {
                    pressE.SetActive(true);
                }
            }
        }

        private void CannotEnterDesktopTopDownView()
        {
            canEnterDesktopTopDownView = false;

            if (pressE)
            {
                pressE.SetActive(false);
            }
        }

        private void HandleUpdatingCanEnterDestopViewUI()
        {
            if (Utilities.IsValid(localPlayer))
            {
                VRCPlayerApi.TrackingData hmd = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);

                if (pressE)
                {
                    pressE.transform.position = hmd.position + (hmd.rotation * Vector3.forward);
                }
            }

        }
    }
}
