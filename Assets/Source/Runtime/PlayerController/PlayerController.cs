using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Utility.Timer;
using UnityEngine.InputSystem;
using Q.Controllers;
using Q.Controllers.Options;
using Q.LevelElements;
using Q.Managers;
using Q.UI;
using Q.QUI.Controller;

namespace Q.Player
{
    [DefaultExecutionOrder(100)]
    public class PlayerController : MonoBehaviour
    {
#pragma warning disable 0414 // private field assigned but not used.
        public Rigidbody2D Rigidbody
        { get; private set; }
        public CircleCollider2D circleCollider
        { get; private set; }
        public BoxCollider2D boxCollider
        { get; private set; }

        public InputMaster input
        { get; set; }
        private SaveObject save;
        
        [SerializeField] private bool silentSpawn = false;
        public bool SilentSpawn
        {
            get { return silentSpawn; }
            set { silentSpawn = value; }
        }
        [SerializeField] private bool resetable = true;
        public bool Resetable
        {
            get { return resetable; }
            set { resetable = value; }
        }
        [SerializeField] private bool forcedCutscene = false;
        public bool ForcedCutscene
        {
            get { return forcedCutscene; }
            set { forcedCutscene = value; }
        }

        private static bool godMode = false;
        public static void SetGodMode(bool enabled)
        {
            if (enabled != godMode)
            {
                godMode = enabled;
                LevelController.instance?.Player?.SetupGodMode();
            }
        }

        public static bool GetGodMode()
        {
            return godMode;
        }
        private void SetupGodMode()
        {
            transform.rotation = Quaternion.identity;
            if (godMode)
                StopMovement();
            SetCollisionEnabled(!godMode);
            SetKinematic(godMode);
        }

        public LayerMask collisionMask;
        public LayerMask spikeDetectionMask;
        public PhysicsMaterial2D frictionMaterial;
        public PhysicsMaterial2D slippyMaterial;

        public float impulseTorque = 3f;
        public float impulseAirTorque = 0.3f;
        public float groundTorque = 100f;
        public float airTorque = 200f;
        public float airSpeed = 60f;
        public float maxMoveSpeed = 8f;
        public float physicsCastOffset = 0.1f;
        public float jumpTestDistance = 0.1f;
        public float jumpHeight = 1.4f;
        public float jumpExtraStrength = 1.45f;
        public float jumpMinTime = 0.1f;
        public float jumpMaxTime = 0.25f;
        public float walljumpTestDistance = 0.3f;
        public float walljumpStrength = 20f;
        public float walljumpShortGravityBoost = 0.75f;
        public float walljumpShortDrag = 2.4f;
        public float walljumpMinTime = 0.1f;
        public float walljumpMaxTime = 0.15f;
        public float ceilingjumpStrength = 2.4f;
        public float superJumpStrength = 50f;
        public float wallRollTime = 0.3f;
        public float ceilingTestDistance = 0.3f;
        public float coyoteTime = 0.075f;
        public float respawnTime = 0.75f;

        public UnityEvent onDeath;
        public UnityEvent onSpawn;
        public UnityEvent onJump;
        public UnityEvent onWaterJump;

        [SerializeField] private UnityAtoms.VoidEvent EventPlayerSpawn = null;
        [SerializeField] private UnityAtoms.VoidEvent EventPlayerDeath = null;
        [SerializeField] private UnityAtoms.VoidEvent EventPlayerJump = null;
        [SerializeField] private UnityAtoms.BoolEvent EventPlayerCollisionSet = null;
        [SerializeField] private UnityAtoms.VoidEvent EventPlayerCheckpoint = null;

        // cached variables
        Vector3 startPosition;
        PhysicsMaterial2D colliderMaterial;

        // local variables
        private bool hasSpawned = false;
        private Timer inputTimer = new Timer(UpdateCycle.FixedUpdate);
        private Timer jumpBufferTimer = new Timer(UpdateCycle.FixedUpdate);
        private Timer groundTimer = new Timer(UpdateCycle.FixedUpdate);
        private bool hasNoInput = true;
        private Timer noInputTime = new Timer(UpdateCycle.FixedUpdate);
        private Timer airTimer = new Timer(UpdateCycle.FixedUpdate);
        private Timer jumpTimer = new Timer(UpdateCycle.FixedUpdate);
        private Timer coyoteTimer = new Timer(UpdateCycle.FixedUpdate);
        private Timer superJumpTimer = new Timer(UpdateCycle.FixedUpdate);
        private Timer superjumpAllowedTimer = new Timer(UpdateCycle.FixedUpdate);
        private Timer sharpTurnTimer = new Timer(UpdateCycle.FixedUpdate);
        private int requestedFlipDir = 0;
        private int queuedFlipDir = 0;
        private Timer wallRollTimer = new Timer(UpdateCycle.FixedUpdate);
        private Timer wallJumpTimer = new Timer(UpdateCycle.FixedUpdate);
        private Timer bunnyJumpTimer = new Timer(UpdateCycle.FixedUpdate);
        private Timer flipGravityTimer = new Timer(UpdateCycle.FixedUpdate);

        public bool hasGroundJumped
        { get; private set; }
        private int normalJumpCount = 0;
        public bool hasWallJumped
        { get; private set; }
        public int walljumpDir
        { get; private set; }
        public bool hasCeilingJumped
        { get; private set; }
        public bool nearGeometry
        { get; private set; }
        public bool almostNearGeometry
        { get; private set; }
        public bool isGrounded
        { get; private set; }

        private Timer ceilingJumpTimer = new Timer(UpdateCycle.FixedUpdate);
        private bool allowOveredge;
        private Timer overedgeTimer = new Timer(UpdateCycle.FixedUpdate);
        public float colliderSphereify
        { get; private set; }
        public bool isDead { get; private set; }
        private Timer deathTimer = new Timer(UpdateCycle.FixedUpdate);
        private Vector2 surfaceNormal;
        private Collider2D groundCollider;
        private Vector2 lastSurfaceNormal;
        private RaycastHit2D wiskersRight;
        private RaycastHit2D wiskersLeft;
        private Vector2 rightWallNormal;
        private Collider2D rightCollider;
        private Vector2 frameRightWallNormal;
        private Vector2 lastFrameRightWallNormal;
        private Vector2 lastRightWallNormal;
        private Vector2 leftWallNormal;
        private Collider2D leftCollider;
        private Vector2 frameLeftWallNormal;
        private Vector2 lastFrameLeftWallNormal;
        private Vector2 lastLeftWallNormal;
        private Vector2 ceilingNormal;
        private Collider2D ceilingCollider;
        private Vector2 lastVelocity;
        private Vector2 debugLastPosition;
        private Vector2 frameVelocity;
        private int wallRollSide = 0;
        private bool canWallroll;
        private bool outsideForce = false;
        private bool allowJump = false;
        private bool disallowJump = false;
        private Timer disallowJumpTimer = new Timer(UpdateCycle.FixedUpdate);
        private bool useJump = false;
        private bool limitAirMovement = false;
        private Timer purgeInputTimer = new Timer(UpdateCycle.FixedUpdate);
        // these indicate the time since the player has willingfully left a surface,
        // thus, even though he might still be in contact, we disregard any contact if the timer is ticking
        private Timer blockedGroundTimer = new Timer(UpdateCycle.FixedUpdate);
        private Timer blockedLeftWallTimer = new Timer(UpdateCycle.FixedUpdate);
        private Timer blockedRightWallTimer = new Timer(UpdateCycle.FixedUpdate);

        private float contactFriction = 10;

        private Timer tipTimer = new Timer(UpdateCycle.FixedUpdate);

        private bool frameGrounded;

        // tilt stuff
        // TODO needs to be cleaned up
        public Vector2 tiltOffset = new Vector2(-0.5f, -0.5f);
        public float tiltSlide = 0.1f;
        public float tiltWall = 0.2f;
        private bool tiltJump;
        private float tiltAngleOffset;
        private Vector2 cornerPivot;
        /*
        private int tiltDir = 0;
        private bool hasJustJumped = false;
        private float tipJumpMult = 1;
        */

        // special abilities
        private static bool abilityWallRoll = true;
        private static bool abilityDownwardWallJump = false;
        private static bool abilityFastFall = true;

        public int AirJumps
        { get; set; }

        public bool invertedGeometry
        { get; set; }

        public bool invertedGravity
        {
            get { return Rigidbody.gravityScale < 0; }
            private set { Rigidbody.gravityScale = value ? -1 : 1; }
        }
        public int gravitySign
        {
            get { return (int)Mathf.Sign(Rigidbody.gravityScale); }
        }
        private Vector2 gravity => Physics2D.gravity * Rigidbody.gravityScale;

        public void InvertGravity()
        {
            SetGravity(!invertedGravity);
        }
        public void SetGravity(bool inverted = false)
        {
            if (invertedGravity != inverted)
            {
                flipGravityTimer.Clear();
                flipGravityTimer.Start(0.2f);
                invertedGravity = inverted;
            }
        }

        // exiting levels
        public Finish LevelExit
        { get; set; }

        [HideInInspector]
        public UnityEvent OnExitingLevel = new UnityEvent();



        #region macros

        public bool justGroundJumped => jumpTimer.isRunning && jumpTimer.time == 0;
        public bool justWallJumped => wallJumpTimer.isRunning && wallJumpTimer.time == 0;
        public bool isCeilingGrounded => !isGrounded && (insideWater || allowJumpEverywhere) && hasCeilingContact && !isWallSupported;
        public bool hasJumped
        { get { return hasGroundJumped || hasWallJumped || hasCeilingJumped; } }
        public float anyJumpTime
        {
            get
            {
                if (wallJumpTimer.isRunning)
                    return wallJumpTimer.time;
                if (ceilingJumpTimer.isRunning)
                    return ceilingJumpTimer.time;
                return jumpTimer.time;
            }
        }
        public bool anyJumpTimerRunning => jumpTimer.isRunning || wallJumpTimer.isRunning || ceilingJumpTimer.isRunning;
        public bool hasLeftSurfaceAfterJump
        {
            get
            {
                return (hasGroundJumped && jumpTimer.time >= 0.1f)
                    || (hasWallJumped && wallJumpTimer.time >= 0.1f)
                    || (hasCeilingJumped && ceilingJumpTimer.time >= 0.1f);
            }
        }
        public bool wasFrameLeftWallSupported
        { get { return lastFrameLeftWallNormal != Vector2.zero; } }
        public bool wasLeftWallSupported
        { get { return lastLeftWallNormal != Vector2.zero; } }
        public bool wasFrameRightWallSupported
        { get { return lastRightWallNormal != Vector2.zero; } }
        public bool wasRightWallSupported
        { get { return lastRightWallNormal != Vector2.zero; } }
        public bool isLeftWallSupported
        { get { return leftWallNormal != Vector2.zero; } }
        public bool isRightWallSupported
        { get { return rightWallNormal != Vector2.zero; } }
        public bool isFrameLeftWallSupported
        { get { return frameLeftWallNormal != Vector2.zero; } }
        public bool isFrameRightWallSupported
        { get { return frameRightWallNormal != Vector2.zero; } }
        public bool isWallSupported
        { get { return isLeftWallSupported || isRightWallSupported; } }
        public bool wasWallSupported
        { get { return wasLeftWallSupported || wasRightWallSupported; } }
        /// <summary>
        /// The player is not in contact with the ground or walls
        /// </summary>
        public bool isNotSupported
        {
            get
            {
                return !isGrounded && !isLeftWallSupported && !isRightWallSupported;
            }
        }
        public bool hasCeilingContact
        {
            get
            {
                return ceilingNormal != Vector2.zero;
            }
        }
        public float frictionCoefficient
        { get { return resetFrictionTimer.isRunning ? 1 : Mathf.Pow(MathHelper.Remap(0, 10, 0, 1, contactFriction), 0.4f); } }

        public bool isCogDecreasing
        {
            get { return Rigidbody.velocity.y < -0.01f; }
        }

        public bool slipperyMovement => contactFriction < 1;

        #endregion


        // input
        private float rawInputH;
        private float rawInputV;

        public bool collisionEnabled
        { get; private set; }
        public InputState inputState
        { get; private set; }
        public float inputH
        { get; private set; }
        public float inputV
        { get; private set; }
        public bool jump
        { get; private set; }
        public bool jumpHold
        { get; private set; }

        private Timer inputCoodownTimer = new Timer(UpdateCycle.FixedUpdate);

        // outside hooks
        private bool jumpImmediate;
        private bool allowJumpEverywhere;
        private Timer resetFrictionTimer = new Timer(UpdateCycle.FixedUpdate);
        private Timer speedRailTimer = new Timer(UpdateCycle.FixedUpdate);

        // input recording
        public struct PlayerInput
        {
            public float inputH;
            public float inputV;
            public bool jump;
            public bool jumpHold;
            public bool suicide;

            public Vector2 position;
            public Vector2 velocity;
            public float angularVelocity;
        }
        List<PlayerInput> inputSequence = new List<PlayerInput>();
        int inputId;
        bool inputPlayback = false;


        // visuals
        public PlayerVisuals visuals
        { get; set; }
        public PlayerSkinController skinController
        { get; private set; }



        private void Awake()
        {
            Rigidbody = GetComponent<Rigidbody2D>();
            circleCollider = GetComponent<CircleCollider2D>();
            boxCollider = GetComponent<BoxCollider2D>();
            visuals = GetComponentInChildren<PlayerVisuals>();
            skinController = GetComponent<PlayerSkinController>();

            input = new InputMaster();
            Options.RemapInput(input);
            Options.OnInputRemap += RemapInput;

            save = GameSettings.global.MainSaveManager.GetCurrentSave();

            // regitser timers
            TimerManager.RegisterTimer(inputTimer);
            TimerManager.RegisterTimer(jumpBufferTimer);
            TimerManager.RegisterTimer(groundTimer);
            TimerManager.RegisterTimer(noInputTime);
            TimerManager.RegisterTimer(airTimer);
            TimerManager.RegisterTimer(jumpTimer);
            TimerManager.RegisterTimer(coyoteTimer);
            TimerManager.RegisterTimer(superJumpTimer);
            TimerManager.RegisterTimer(superjumpAllowedTimer);
            TimerManager.RegisterTimer(sharpTurnTimer);
            TimerManager.RegisterTimer(wallRollTimer);
            TimerManager.RegisterTimer(wallJumpTimer);
            TimerManager.RegisterTimer(bunnyJumpTimer);
            TimerManager.RegisterTimer(ceilingJumpTimer);
            TimerManager.RegisterTimer(overedgeTimer);
            TimerManager.RegisterTimer(deathTimer);
            TimerManager.RegisterTimer(purgeInputTimer);
            TimerManager.RegisterTimer(disallowJumpTimer);
            TimerManager.RegisterTimer(blockedGroundTimer);
            TimerManager.RegisterTimer(blockedLeftWallTimer);
            TimerManager.RegisterTimer(blockedRightWallTimer);
            TimerManager.RegisterTimer(tipTimer);
            TimerManager.RegisterTimer(inputCoodownTimer);
            TimerManager.RegisterTimer(resetFrictionTimer);
            TimerManager.RegisterTimer(speedRailTimer);
            TimerManager.RegisterTimer(inWaterTimer);
            TimerManager.RegisterTimer(waterCoyoteTimer);
            TimerManager.RegisterTimer(wireCoyoteTime);
            TimerManager.RegisterTimer(wireJumpTimer);
            TimerManager.RegisterTimer(waterBlobJumpingOutTimer);
            TimerManager.RegisterTimer(flipGravityTimer);
        }

        private void RemapInput()
        {
            Options.RemapInput(input);
        }

        void Start()
        {
            startPosition = transform.position;

            colliderMaterial = new PhysicsMaterial2D();
            boxCollider.sharedMaterial = colliderMaterial;
            circleCollider.sharedMaterial = colliderMaterial;
            Rigidbody.useFullKinematicContacts = true;

            BindInput();

            BindEvents();

            SetInputEnabled(InputState.Disabled);
            SetKinematic(true);
        }


        private void BindInput()
        {
            input.Player.Jump.performed += JumpBinding;
            
            input.Player.Menu.performed += PressMenu;
            input.Player.MapScreen.performed += PressMap;
            input.Player.Reset.performed += PressReset;
        }
        private void OnDestroy()
        {
            Options.OnInputRemap -= RemapInput;

            // cleanup timers
            TimerManager.UnRegisterTimer(inputTimer);
            TimerManager.UnRegisterTimer(jumpBufferTimer);
            TimerManager.UnRegisterTimer(groundTimer);
            TimerManager.UnRegisterTimer(noInputTime);
            TimerManager.UnRegisterTimer(airTimer);
            TimerManager.UnRegisterTimer(jumpTimer);
            TimerManager.UnRegisterTimer(coyoteTimer);
            TimerManager.UnRegisterTimer(superJumpTimer);
            TimerManager.UnRegisterTimer(superjumpAllowedTimer);
            TimerManager.UnRegisterTimer(sharpTurnTimer);
            TimerManager.UnRegisterTimer(wallRollTimer);
            TimerManager.UnRegisterTimer(wallJumpTimer);
            TimerManager.UnRegisterTimer(bunnyJumpTimer);
            TimerManager.UnRegisterTimer(ceilingJumpTimer);
            TimerManager.UnRegisterTimer(overedgeTimer);
            TimerManager.UnRegisterTimer(deathTimer);
            TimerManager.UnRegisterTimer(purgeInputTimer);
            TimerManager.UnRegisterTimer(disallowJumpTimer);
            TimerManager.UnRegisterTimer(blockedGroundTimer);
            TimerManager.UnRegisterTimer(blockedLeftWallTimer);
            TimerManager.UnRegisterTimer(blockedRightWallTimer);
            TimerManager.UnRegisterTimer(tipTimer);
            TimerManager.UnRegisterTimer(inputCoodownTimer);
            TimerManager.UnRegisterTimer(resetFrictionTimer);
            TimerManager.UnRegisterTimer(speedRailTimer);
            TimerManager.UnRegisterTimer(inWaterTimer);
            TimerManager.UnRegisterTimer(waterCoyoteTimer);
            TimerManager.UnRegisterTimer(wireCoyoteTime);
            TimerManager.UnRegisterTimer(wireJumpTimer);
            TimerManager.UnRegisterTimer(waterBlobJumpingOutTimer);
            TimerManager.UnRegisterTimer(flipGravityTimer);

            input.Player.Jump.performed -= JumpBinding;

            input.Player.Reset.performed -= PressReset;
            input.Player.Menu.performed -= PressMenu;
            input.Player.MapScreen.performed -= PressMap;
        }

        private void BindEvents()
        {
            wireCoyoteTime.OnExpired.AddListener(CleanupWire);

            QUIController.BindControllerEvents(MenuOpened, MenuClosed);
        }



        private void JumpBinding(InputAction.CallbackContext ctx)
        {
            if (!ctx.action.enabled || this == null || ConsoleManager.instance.isVisible || inputCoodownTimer.isRunning)
                return;

            jump = ((UnityEngine.InputSystem.Controls.ButtonControl)ctx.control).wasPressedThisFrame;

            // jump input buffering
            if (jump)
                jumpBufferTimer.Start(0.1f);

            jumpHold = ((UnityEngine.InputSystem.Controls.ButtonControl)ctx.control).isPressed;
        }
        private void PressMenu(InputAction.CallbackContext ctx)
        {
            if (!ctx.action.enabled || this == null || ConsoleManager.instance.isVisible || inputCoodownTimer.isRunning)
                return;

            RumbleManager.instance.ClearTimedRumbles();

            if (inputState == InputState.Cutscene || forcedCutscene)
                QUIController.OpenCutscene();
            else
                QUIController.OpenMenu();
        }
        private void PressMap(InputAction.CallbackContext ctx)
        {
            if (!ctx.action.enabled || this == null || ConsoleManager.instance.isVisible || inputCoodownTimer.isRunning)
                return;

            if (inputState == InputState.Cutscene || forcedCutscene)
                return;

            QUIController.OpenMap();
        }
        private void PressReset(InputAction.CallbackContext ctx)
        {
            if (!ctx.action.enabled || this == null || ConsoleManager.instance.isVisible || QUIController.IsOpen || inputCoodownTimer.isRunning)
                return;

            TrySuicide();
        }
        private void ResetInputValues()
        {
            rawInputH = 0;
            rawInputV = 0;
            inputH = 0;
            inputV = 0;
            jump = false;
            jumpHold = false;
        }
        // TODO disable player input actions during menu / enable menu input actions for menu
        private void MenuOpened()
        {
            input.Player.MovementHorizontal.Disable();
            input.Player.MovementVertical.Disable();

            input.Player.Jump.Disable();

            RumbleManager.instance.globalMultiplier = 0;
        }
        private void MenuClosed()
        {
            inputCoodownTimer.Start(0.01f);

            input.Player.MovementHorizontal.Enable();
            input.Player.MovementVertical.Enable();

            input.Player.Jump.Enable();

            RumbleManager.instance.globalMultiplier = 1;
        }

        public void SetSpawnPosition(Vector2 newPosition)
        {
            startPosition = newPosition;
            EventPlayerCheckpoint.Raise();
        }
        public void SetSpawn(Transform transform)
        {
            startPosition = transform.position;
            EventPlayerCheckpoint.Raise();
        }


        public void ResetTransform()
        {
            // reset transforms
            transform.position = startPosition;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            Rigidbody.MovePosition(startPosition);
            Rigidbody.MoveRotation(0);
            Rigidbody.velocity = Vector3.zero;
            Rigidbody.angularVelocity = 0;
            contactFriction = 10;

            gameObject.layer = LayerMask.NameToLayer("Player");
        }
        
        public void Reset()
        {
            if (silentSpawn)
                return;

            // reset input
            inputH = 0;
            inputV = 0;
            jump = false;
            jumpHold = false;

            isGrounded = false;
            frameGrounded = false;
            hasSpawned = true;
            inputTimer.Clear();
            groundTimer.Clear();
            hasNoInput = true;
            noInputTime.Clear();
            airTimer.Clear();
            jumpTimer.Clear();
            coyoteTimer.Clear();
            sharpTurnTimer.Clear();
            requestedFlipDir = 0;
            queuedFlipDir = 0;
            wallRollTimer.Clear();
            wallJumpTimer.Clear();
            hasGroundJumped = false;
            normalJumpCount = 0;
            hasWallJumped = false;
            walljumpDir = 0;
            hasCeilingJumped = false;
            ceilingJumpTimer.Clear();
            outsideForce = false;
            allowJump = true;
            disallowJump = false;
            disallowJumpTimer.Clear();
            useJump = false;
            allowOveredge = false;
            overedgeTimer.Clear();
            deathTimer.Clear();
            tipTimer.Clear();
            isDead = false;
            wallRollSide = 0;
            invertedGravity = false;
            jumpImmediate = false;
            outsideForce = false;
            resetFrictionTimer.Clear();
            colliderSphereify = 0;
            speedRailTimer.Clear();

            leftWallNormal = Vector2.zero;
            rightWallNormal = Vector2.zero;
            lastLeftWallNormal = Vector2.zero;
            lastRightWallNormal = Vector2.zero;

            debugLastPosition = transform.position;
            
            SetInputEnabled(InputState.Enabled);
            SetKinematic(false);
            SetCollisionEnabled(true);

            ResetTransform();

            // TODO
            if (Debug.isDebugBuild && Input.GetKey(KeyCode.F5))
            {
                inputPlayback = true;
            }
            else
            {
                inputId = 0;
                inputSequence.Clear();
            }

            if (godMode)
            {
                SetupGodMode();
            }

            EventPlayerSpawn.Raise();
            onSpawn.Invoke();
        }



        int counter;


        private void Update()
        {
            // show debug
            if (Input.GetKeyDown(KeyCode.F3) && Debug.isDebugBuild)
                debugShow = !debugShow;
            if ((Input.GetKeyDown(KeyCode.F2) || Input.GetKeyDown("joystick button 2")) && Debug.isDebugBuild)
                breakDebugger = true;
        }

        // actions
        public void Jump()
        {
            jump = true;
            jumpHold = true;
        }
        public void JumpImmediate()
        {
            jumpImmediate = true;
        }
        public void ResetFriction(float time = 0.01f)
        {
            resetFrictionTimer.Start(time);
        }
        public void AllowJumpEverywhere()
        {
            allowJumpEverywhere = true;
        }


        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (Physics2D.Distance(collision.collider, collision.otherCollider).distance > 0.15)
            {
                print("Swithable Element kill");
                // Is the player stuck inside a SwitchableElement?
                //if (collision.collider.GetComponent<SwitchableElement>())
                //    Kill();
            }
        }


        private RaycastHit2D[] sphereHits = new RaycastHit2D[8];
        private RaycastHit2D[] boxHits = new RaycastHit2D[8];
        private Collider2D[] sphereHitColliders = new Collider2D[8];
        private Collider2D[] boxHitColliders = new Collider2D[8];
        /// <summary>
        /// Check environment collisions
        /// </summary>
        private void TraceEnvironment()
        {
            float newContactFriction = 0;
            int frictionSamples = 0;
            int sphereHitCount = 0;
            int boxHitCount = 0;

            // trace start offset necessary for the box cast to aligne with the actual bottom of our cube
            float boxCastOffset = Mathf.Lerp(1, 1.414f, Mathf.Sin((transform.eulerAngles.z % 90) * 2 * Mathf.Deg2Rad));

            // check if the player is near geometry
            if (true)
            {
                const float nearOffset = 0.1f;
                sphereHitCount = Physics2D.OverlapCircleNonAlloc(transform.position, circleCollider.radius + nearOffset, sphereHitColliders, collisionMask);
                boxHitCount = Physics2D.OverlapBoxNonAlloc(transform.position, boxCollider.size + Vector2.one * nearOffset, transform.eulerAngles.z, boxHitColliders, collisionMask);

                nearGeometry = sphereHitCount + boxHitCount > 0;
            }
            if (true)
            {
                const float nearOffset = 0.25f;
                sphereHitCount = Physics2D.OverlapCircleNonAlloc(transform.position, circleCollider.radius + nearOffset, sphereHitColliders, collisionMask);
                boxHitCount = Physics2D.OverlapBoxNonAlloc(transform.position, boxCollider.size + Vector2.one * nearOffset, transform.eulerAngles.z, boxHitColliders, collisionMask);

                almostNearGeometry = sphereHitCount + boxHitCount > 0;
            }


            // trace ground
            if (surfaceNormal != Vector2.zero)
                lastSurfaceNormal = surfaceNormal;
            float testDistance = jumpTestDistance;
            // TODO readd sloped jumps
            if (!hasJumped && lastSurfaceNormal.y > 0.7)
                testDistance *= MathHelper.Remap(0.7f, 1, 5, 1, lastSurfaceNormal.y);
            sphereHitCount = Physics2D.CircleCastNonAlloc(transform.position, circleCollider.radius - physicsCastOffset, gravity.normalized, sphereHits, physicsCastOffset + testDistance, collisionMask);
            boxHitCount = Physics2D.BoxCastNonAlloc(transform.position, boxCollider.size - Vector2.one * 2 * physicsCastOffset, transform.eulerAngles.z, gravity.normalized, boxHits, physicsCastOffset * boxCastOffset + testDistance, collisionMask);
            wiskersRight = Physics2D.Raycast((Vector2)transform.position + Vector2.right * 0.4f, gravity.normalized, 1.5f, collisionMask);
            wiskersLeft = Physics2D.Raycast((Vector2)transform.position + Vector2.left * 0.4f, gravity.normalized, 1.5f, collisionMask);
            // draw wiskers
            Debug.DrawLine(transform.position + Vector3.right * 0.4f, transform.position + Vector3.right * 0.4f + (Vector3)gravity.normalized * 1.5f, wiskersRight.collider ? Color.white : Color.red);
            Debug.DrawLine(transform.position + Vector3.left * 0.4f, transform.position + Vector3.left * 0.4f + (Vector3)gravity.normalized * 1.5f, wiskersLeft.collider ? Color.white : Color.red);
            frameGrounded = sphereHitCount + boxHitCount > 0;
            frictionSamples += sphereHitCount + boxHitCount;
            // calculate ground normal
            if (sphereHitCount + boxHitCount > 0)
            {
                for (int i = 0; i < sphereHitCount; i++)
                {
                    RaycastHit2D rh = sphereHits[i];
                    surfaceNormal += rh.normal;
                    newContactFriction += rh.collider.sharedMaterial ? rh.collider.sharedMaterial.friction : 10;
                    if (!groundCollider)
                        groundCollider = rh.collider;

                }
                for (int i = 0; i < boxHitCount; i++)
                {
                    RaycastHit2D rh = boxHits[i];
                    surfaceNormal += rh.normal;
                    newContactFriction += rh.collider.sharedMaterial ? rh.collider.sharedMaterial.friction : 10;
                    if (!groundCollider)
                        groundCollider = rh.collider;
                }
                if (surfaceNormal.sqrMagnitude > 0)
                    surfaceNormal.Normalize();
            }
            else
            {
                surfaceNormal = Vector2.zero;
                groundCollider = null;
            }


            // trace left wall
            lastLeftWallNormal = leftWallNormal;
            leftWallNormal = Vector2.zero;
            lastFrameLeftWallNormal = frameLeftWallNormal;
            frameLeftWallNormal = Vector2.zero;
            sphereHitCount = Physics2D.CircleCastNonAlloc(transform.position, circleCollider.radius - physicsCastOffset, Vector2.left, sphereHits, physicsCastOffset + walljumpTestDistance, collisionMask);
            boxHitCount = Physics2D.BoxCastNonAlloc(transform.position, boxCollider.size - Vector2.one * 2 * physicsCastOffset, transform.eulerAngles.z, Vector2.left, boxHits, physicsCastOffset * boxCastOffset + walljumpTestDistance, collisionMask);
            if (!groundCollider)
                frictionSamples += sphereHitCount + boxHitCount;
            // calculate left wall normal
            if (sphereHitCount + boxHitCount > 0)
            {
                for (int i = 0; i < sphereHitCount; i++)
                {
                    RaycastHit2D rh = sphereHits[i];
                    frameLeftWallNormal += rh.normal;
                    if (!groundCollider)
                        newContactFriction += rh.collider.sharedMaterial ? rh.collider.sharedMaterial.friction : 10;
                    if (!leftCollider)
                        leftCollider = rh.collider;
                }
                for (int i = 0; i < boxHitCount; i++)
                {
                    RaycastHit2D rh = boxHits[i];
                    frameLeftWallNormal += rh.normal;
                    if (!groundCollider)
                        newContactFriction += rh.collider.sharedMaterial ? rh.collider.sharedMaterial.friction : 10;
                    if (!leftCollider)
                        leftCollider = rh.collider;
                }
                frameLeftWallNormal /= sphereHitCount + boxHitCount;
                if (frameLeftWallNormal.sqrMagnitude > 0)
                    frameLeftWallNormal.Normalize();
                if (!blockedLeftWallTimer.isRunning)
                {
                    // revert wall detection if the wall angle is not right
                    if (Vector3.Dot(frameLeftWallNormal, Vector2.right) > 0.8f)
                        leftWallNormal = frameLeftWallNormal;
                }
            }
            else
            {
                leftCollider = null;
            }


            // trace right wall
            lastRightWallNormal = rightWallNormal;
            rightWallNormal = Vector2.zero;
            lastFrameRightWallNormal = frameRightWallNormal;
            frameRightWallNormal = Vector2.zero;
            sphereHitCount = Physics2D.CircleCastNonAlloc(transform.position, circleCollider.radius - physicsCastOffset, Vector2.right, sphereHits, physicsCastOffset + walljumpTestDistance, collisionMask);
            boxHitCount = Physics2D.BoxCastNonAlloc(transform.position, boxCollider.size - Vector2.one * 2 * physicsCastOffset, transform.eulerAngles.z, Vector2.right, boxHits, physicsCastOffset * boxCastOffset + walljumpTestDistance, collisionMask);
            if (!groundCollider)
                frictionSamples += sphereHitCount + boxHitCount;
            // calculate right wall normal
            if (sphereHitCount + boxHitCount > 0)
            {
                for (int i = 0; i < sphereHitCount; i++)
                {
                    RaycastHit2D rh = sphereHits[i];
                    frameRightWallNormal += rh.normal;
                    if (!groundCollider)
                        newContactFriction += rh.collider.sharedMaterial ? rh.collider.sharedMaterial.friction : 10;
                    if (!rightCollider)
                        rightCollider = rh.collider;
                }
                for (int i = 0; i < boxHitCount; i++)
                {
                    RaycastHit2D rh = boxHits[i];
                    frameRightWallNormal += rh.normal;
                    if (!groundCollider)
                        newContactFriction += rh.collider.sharedMaterial ? rh.collider.sharedMaterial.friction : 10;
                    if (!rightCollider)
                        rightCollider = rh.collider;
                }
                if (frameRightWallNormal.sqrMagnitude > 0)
                    frameRightWallNormal.Normalize();
                if (!blockedRightWallTimer.isRunning)
                {
                    // revert wall detection if the wall angle is not right
                    if (Vector3.Dot(frameRightWallNormal, Vector2.left) > 0.8f)
                        rightWallNormal = frameRightWallNormal;
                }
            }
            else
            {
                rightCollider = null;
            }


            // trace ceiling
            if (true)
            {
                sphereHitCount = Physics2D.CircleCastNonAlloc(transform.position, circleCollider.radius - physicsCastOffset, -gravity.normalized, sphereHits, physicsCastOffset + ceilingTestDistance, collisionMask);
                boxHitCount = Physics2D.BoxCastNonAlloc(transform.position, boxCollider.size - Vector2.one * 2 * physicsCastOffset, transform.eulerAngles.z, -gravity.normalized, boxHits, physicsCastOffset * boxCastOffset + ceilingTestDistance, collisionMask);
                // calculate right wall normal
                if (sphereHitCount + boxHitCount > 0)
                {
                    for (int i = 0; i < sphereHitCount; i++)
                    {
                        RaycastHit2D rh = sphereHits[i];
                        ceilingNormal += rh.normal;
                        if (!ceilingCollider)
                            ceilingCollider = rh.collider;
                    }
                    for (int i = 0; i < boxHitCount; i++)
                    {
                        RaycastHit2D rh = boxHits[i];
                        ceilingNormal += rh.normal;
                        if (!ceilingCollider)
                            ceilingCollider = rh.collider;
                    }
                    if (ceilingNormal.sqrMagnitude > 0)
                        ceilingNormal.Normalize();
                }
                else
                {
                    ceilingNormal = Vector2.zero;
                    ceilingCollider = null;
                }
            }

            // calculate correct normalized friction
            if (frictionSamples > 0)
                contactFriction = newContactFriction / frictionSamples;
        }

        public void SetScale(float scale)
        {
            transform.localScale = Vector3.one * scale;
        }

        private RaycastHit2D[] overedgeTraceHits = new RaycastHit2D[4];
        private void FixedUpdate()
        {
            // debug break
            if (breakDebugger)
            {
                System.Diagnostics.Debugger.Break();
                breakDebugger = false;
            }

            if (isDead)
                return;


            inputH = 0;
            inputV = 0;
            if (!ConsoleManager.instance.isVisible)
            {
                inputH = input.Player.MovementHorizontal.ReadValue<float>();
                inputV = input.Player.MovementVertical.ReadValue<float>();
                if (input.Player.enabled && input.Player.MovementHorizontal.enabled)
                {
                    // TODO this is the dirtiest hack
                    // We convert a InputSystem key to a Unity Input KeyCode. This may cause errors when the two key strings are not named in the same way
                    const string sMoveH = "MovementHorizontal";
                    const string sMoveV = "MovementVertical";
                    const string sBoard = "<Keyboard>";
                    const string sPos = "positive";
                    const string sNeg = "negative";
                    foreach (var bind in input.Player.MovementHorizontal.bindings)
                    {
                        if (inputH == 0 && bind.action == sMoveH || inputV == 0 && bind.action == sMoveV)
                        {
                            if (bind.isPartOfComposite && bind.effectivePath.Contains(sBoard))
                            {
                                string key = bind.effectivePath;
                                key = key.Remove(0, key.IndexOf('/') + 1);                  // Remove "<Keyboard>/"
                                key = key[0].ToString().ToUpper() + key.Remove(0, 1);   // Make first char upper case
                                key = key.Replace("arrow", "Arrow");                    // Fix arrow key KeyCodes
                                if (System.Enum.TryParse(key, out KeyCode keycode))
                                {
                                    int offset = 0;
                                    if (Input.GetKey(keycode))
                                    {
                                        if (bind.name == sPos) offset++;
                                        if (bind.name == sNeg) offset--;
                                    }
                                    if (offset != 0)
                                    {
                                        if (bind.action == sMoveH) inputH += offset;
                                        if (bind.action == sMoveV) inputV += offset;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            //
            if (godMode)
            {
                UpdateGodMode();
                return;
            }

            // record input
            if (!inputPlayback)
            {
                PlayerInput pi = new PlayerInput();

                if (inputSequence.Count > 1)
                {
                    pi = inputSequence[inputSequence.Count - 2];
                    pi.position = Rigidbody.position;
                    pi.velocity = Rigidbody.velocity;
                    pi.angularVelocity = Rigidbody.angularVelocity;
                    inputSequence.RemoveAt(inputSequence.Count - 2);
                    inputSequence.Insert(inputSequence.Count - 1, pi);
                }

                pi = new PlayerInput
                {
                    inputH = inputH,
                    inputV = inputV,
                    jump = jump,
                    jumpHold = jumpHold,

                    position = Rigidbody.position,
                    velocity = Rigidbody.velocity,
                    angularVelocity = Rigidbody.angularVelocity
                };

                inputSequence.Add(pi);
            }
            else
            {
                if (inputId < inputSequence.Count)
                {
                    PlayerInput pi = inputSequence[inputId];
                    inputH = pi.inputH;
                    inputV = pi.inputV;
                    jump = pi.jump;
                    jumpHold = pi.jumpHold;
                    inputId++;
                }
                else
                {
                    inputH = 0;
                    inputV = 0;
                    jump = false;
                    jumpHold = false;
                }
            }


            // preprocess input
            jump |= jumpBufferTimer.isRunning;

            
            // Use the current jump, as some scripts may already process it
            if (useJump)
            {
                jumpBufferTimer.Clear();
                jump = false;
            }


            // Purge input
            if (purgeInputTimer.isRunning)
            {
                inputV = 0;
                inputH = 0;
            }

            // Invert vertical input if gravity is inverted
            if (invertedGravity)
            {
                inputV *= -1;
            }


            // trace environment
            TraceEnvironment();

            // HACK limit frame velocity if we just jumped to avoid getting
            // higher trajectories in compination with 90° tilts
            //if (hasJustJumped)
            //{
            //    rigidbody.velocity = new Vector2(rigidbody.velocity.x, 5.62f);

            //    hasJustJumped = false;
            //}

            // check frame data
            frameVelocity = Rigidbody.velocity;


            // air timer
            if (isNotSupported)
                airTimer.Start();
            else
                airTimer.Clear();

            // immediate grounded if we roll over the edge
            frameGrounded |= overedgeTimer.isRunning;

            // coyote time
            if (frameGrounded)
            {
                // TODO figure out exactly why we have this isSpawned variable...
                hasSpawned = false;
                coyoteTimer.Clear();
                if (frameGrounded && !isGrounded) groundTimer.Start();
                if (!frameGrounded && isGrounded) groundTimer.Clear();
                isGrounded = frameGrounded;
            }
            else
            {
                if (!coyoteTimer.isRunning && !hasSpawned)
                    coyoteTimer.Start();
            }
            if (coyoteTimer.time >= coyoteTime)
            {
                if (frameGrounded && !isGrounded) groundTimer.Start();
                if (!frameGrounded && isGrounded) groundTimer.Clear();
                isGrounded = frameGrounded;
            }


            
            // stick the player to the ground if he rolls from horizontal movement onto a slope
            if (!frameGrounded && isGrounded && !hasJumped && inputH != 0 && groundTimer.time > 0.25f &&
                (wiskersRight.collider != null && inputH > 0 || wiskersLeft.collider != null && inputH < 0))
            {
                Rigidbody.AddForce(gravity.normalized * 1.0f, ForceMode2D.Impulse);
            }


            // stop horizontal movement if the player is sliding down a narrow tube, to prevent him from wedging himself up
            if (isLeftWallSupported && isRightWallSupported && Mathf.Abs(Rigidbody.rotation % 90) < 10)
            {
                inputH = 0;
            }
            // TODO implement actual wedged state that includes
            // stopping the player if he is sliding down a tube
            // rotating the visuals to indicate the wedge
            // don't interfere with physics as they will push the player out of colliders anyway
            


            // debug surface normal and player input
            Debug.DrawRay(transform.position, surfaceNormal, Color.red);

            // deadzone
            if (Mathf.Abs(inputH) >= 0.4)
            {
                inputH = Mathf.Sign(inputH);

                if (!inputTimer.isRunning)
                    inputTimer.Start();

                //input = new Vector2(input.x, 0);
                //input.Normalize();

                // just started moving
                if (hasNoInput)
                {
                    if (isGrounded && colliderSphereify < 0.1 && !hasSpawned &&                                                                     // limit air flipping
                                                                                                                                                    //&& ((Mathf.Sign(input.x) == Mathf.Sign(frameVelocity.x) && Mathf.Abs(frameVelocity.x) >= 1) || Mathf.Abs(frameVelocity.x) < 1)      // limit immediate flipping
                        (isRightWallSupported && inputH < 0 || isLeftWallSupported && inputH > 0 || !isWallSupported                                // limit flipping into a wall
                        || isCeilingGrounded))
                    {
                        // set requested move direction
                        if (requestedFlipDir == 0)
                            requestedFlipDir = (int)Mathf.Sign(inputH);
                        else if (tipTimer.time > 0.15f)
                            queuedFlipDir = (int)Mathf.Sign(inputH);

                    }

                    if (!isGrounded && !isCeilingGrounded)
                    {
                        // initial rotation
                        float torqueMult = 1;
                        if (!isGrounded && insideWater && hasCeilingContact)
                            torqueMult *= -1;
                        Rigidbody.AddTorque(Mathf.Sign(-inputH) * torqueMult * impulseTorque, ForceMode2D.Impulse);
                    }

                }

                // sharp turn
                if (frameGrounded && groundTimer.time > 0.4 && Mathf.Abs(Rigidbody.velocity.x) > 4 && Mathf.Abs(Rigidbody.velocity.x) < 15 && Mathf.Sign(Rigidbody.velocity.x) != Mathf.Sign(inputH)
                    && (wiskersRight.collider && inputH < 0 || wiskersLeft.collider && inputH > 0)
                    && !resetFrictionTimer.isRunning)
                {
                    if (!sharpTurnTimer.isRunning)
                    {
                        sharpTurnTimer.Start(0.05f);
                    }
                }
                if (sharpTurnTimer.isRunning && inputH == 0)
                    sharpTurnTimer.Clear();
                if (sharpTurnTimer.hasStopped)
                {
                    // TODO if sharp turns are causing problems, this might be the source. Try uncommenting this line
                    float springAmount = MathHelper.Remap(maxMoveSpeed, 11, 0.5f, 0.25f, Mathf.Abs(Rigidbody.velocity.x));      // MathHelper.Remap(maxMoveSpeed, 11, 0.5f, -0.5f, Mathf.Abs(Rigidbody.velocity.x));
                    springAmount = Mathf.Lerp(1, springAmount, frictionCoefficient);
                    Rigidbody.velocity = new Vector2(Rigidbody.velocity.x * springAmount, Rigidbody.velocity.y);
                    Rigidbody.angularVelocity = Rigidbody.angularVelocity * springAmount;
                    //inputTime.OverrideCurrentTime(MathHelper.Remap(maxMoveSpeed * 0.5f, 11, 0, 0.3f, Mathf.Abs(rigidbody.velocity.x)));

                    sharpTurnTimer.Clear();
                }

                // limit initial movement
                //inputH = inputH * Mathf.Clamp01(inputTimer.time / 2);

                // input detected
                hasNoInput = false;
                noInputTime.Clear();
            }
            else
            {
                // No input
                if (noInputTime.time > 0.05f)        // Mathf.Abs(rigidbody.velocity.x) < 0.5f || 
                    inputTimer.Clear();
                inputH = 0;
                //wallRollTimer = 0;

                if (!noInputTime.isRunning)
                    noInputTime.Start();
                hasNoInput = true;
            }

            // state updates
            if (frameGrounded && (jumpTimer.time > 0.2 || !jumpTimer.isRunning))  //  && rigidbody.velocity.y <= 0.01f
            {
                hasGroundJumped = false;
                normalJumpCount = 0;
                hasWallJumped = false;
                walljumpDir = 0;
                hasCeilingJumped = false;
                jumpTimer.Clear();
            }
            if (hasCeilingContact && (ceilingJumpTimer.time > 0.2 || !ceilingJumpTimer.isRunning))
            {
                hasCeilingJumped = false;
                ceilingJumpTimer.Clear();
            }
            if (inputH == Mathf.Sign(-Rigidbody.velocity.x) || groundTimer.time > 0.02 && !hasJumped)
            {
                bunnyJumpTimer.Clear();
            }




            // External simulations
            SimulateWater();




            // wallroll timer
            if (frameGrounded)
            {
                canWallroll = false;
            }
            if (abilityWallRoll && (isNotSupported || isGrounded && isWallSupported) && wallRollSide != inputH)
            {
                canWallroll = true;
            }
            if (canWallroll)
            {
                if (!isWallSupported && wallRollTimer.time > 0.1 && ((blockedLeftWallTimer.isRunning || blockedRightWallTimer.isRunning)))
                {
                    canWallroll = false;
                }
                else
                {
                    if (isWallSupported && !(isLeftWallSupported && isRightWallSupported))
                    {
                        // start wall roll
                        if (wallRollSide < 0 && isRightWallSupported || wallRollSide > 0 && isLeftWallSupported)
                            wallRollTimer.Clear();
                        wallRollTimer.Start();
                        wallRollSide = isRightWallSupported ? 1 : -1;
                    }
                }
            }
            if (wallRollTimer.isRunning || wallRollTimer.isPaused)
            {
                if (isWallSupported && !(isLeftWallSupported && isRightWallSupported))
                {
                    wallRollTimer.Start();
                    wallRollSide = isRightWallSupported ? 1 : -1;
                }
                if (inputH != wallRollSide)
                {
                    wallRollTimer.Stop();
                    wallRollTimer.Clear();
                    canWallroll = false;
                }
            }
            if (airTimer.time > 0.25f)
            {
                wallRollTimer.Clear();
                wallRollSide = 0;
            }
            if (isGrounded)
            {
                wallRollTimer.Clear();
                wallJumpTimer.Clear();
                wireJumpTimer.Clear();
                ceilingJumpTimer.Clear();
                wallRollSide = 0;
            }



            // Reset contact friction to 10 after certain time to gain control after long ice jumps
            if (airTimer.time > 2)
            {
                contactFriction = Mathf.MoveTowards(contactFriction, 10, 10 / 3f * Time.fixedDeltaTime);
            }

            // physics material updates
            float newFriction = contactFriction;
            if (isGrounded && Rigidbody.velocity.sqrMagnitude < 1 && isWallSupported && inputH != 0)
            {
                // no friction if we are not moving
                newFriction = 0.1f;
            }
            if (contactFriction < 1 && Mathf.Abs(frameVelocity.x) > maxMoveSpeed && inputH == Mathf.Sign(frameVelocity.x))
            {
                // no friction if we are moving on ice
                newFriction = 0.01f;
            }
            if (isWallSupported && Rigidbody.velocity.y > 0 && inputH == 0
                || isWallSupported && hasCeilingContact && inputH != 0)
            {
                // stationary jump
                newFriction = 0;
                //rigidbody.velocity = new Vector2(0, rigidbody.velocity.y);
            }
            if (!isWallSupported && !isGrounded && !frameGrounded)
            {
                newFriction = 0;
                if (airTimer.time < 0.15f && Rigidbody.velocity.magnitude < 0.5f)
                {
                    float stableAngle = Mathf.Round(Rigidbody.rotation / 90f) * 90 - Rigidbody.rotation;
                    Rigidbody.AddTorque(stableAngle * 10, ForceMode2D.Force);
                }
            }
            if (Mathf.Abs(Rigidbody.velocity.x) < 0.1 && inputH != 0
                && ((isRightWallSupported && inputH < 0) || (isLeftWallSupported && inputH > 0)))
            {
                // stuck somewhere
                newFriction = 0;
                Rigidbody.velocity = new Vector2(inputH * 0.5f, Rigidbody.velocity.y);
                if (isGrounded) Rigidbody.velocity -= gravity.normalized * 0.1f;
                if (hasCeilingContact) Rigidbody.velocity += gravity.normalized * 0.1f;
            }
            if (resetFrictionTimer.isRunning)
            {
                newFriction = 10;
            }
            if (wallRollTimer.time >= wallRollTime * Accessibility.WallRollMult + 0.05f || (wallRollTimer.time == 0 && isWallSupported && !isGrounded && inputH != 0))
            {
                // Slippery wall after WallRoll
                newFriction = 0;
            }
            if (hasCeilingContact && !isGrounded && !isWallSupported && !outsideForce && !limitAirMovement)
            {
                // ceiling glide
                newFriction = 0;
            }
            if (hasCeilingContact && insideWater)
            {
                // Water ceiling
                newFriction = 0.01f;
            }
            // Slip and push towards walls
            if (inputH == 0 && !isGrounded && Mathf.Abs(Rigidbody.velocity.x) < 5)
            {
                if (isWallSupported)
                {
                    newFriction = 0;
                    if (isRightWallSupported)
                        Rigidbody.AddForce(Vector2.right * 0.1f, ForceMode2D.Impulse);
                    if (isLeftWallSupported)
                        Rigidbody.AddForce(Vector2.left * 0.1f, ForceMode2D.Impulse);
                }
                else
                {
                    // Reset any minuscule movement, so we cleanly continue falling after sliding down walls
                    if (wasWallSupported && Mathf.Abs(Rigidbody.velocity.x) < 0.25)
                    {
                        Rigidbody.MovePosition(Rigidbody.position + new Vector2((wasRightWallSupported ? -1 : 0) + (wasLeftWallSupported ? +1 : 0), 0) * 0.01f);
                        Rigidbody.velocity = new Vector2(0, Rigidbody.velocity.y);
                        Rigidbody.angularVelocity = 0;
                    }
                }
            }
            //if (inputH == 0 && (wasLeftWallSupported && !isLeftWallSupported || wasRightWallSupported && !isRightWallSupported) && Rigidbody.velocity.y * gravity.y < 0 && Rigidbody.velocity)
            if (colliderMaterial.friction != newFriction && !waterBlob)
            {
                colliderMaterial.friction = newFriction;
                circleCollider.enabled = false;
                circleCollider.enabled = true;
                boxCollider.enabled = false;
                boxCollider.enabled = true;
            }


            // Consistant Ice slide boost when jumping onto an ice surface
            if (frameGrounded && newFriction < 1 && groundTimer.time < 0.05f && inputH == 0 && Mathf.Abs(surfaceNormal.x) < 0.1f)
            {
                Vector2 targetVelocity = new Vector2(Mathf.Max(Mathf.Abs(lastVelocity.x), Mathf.Abs(Rigidbody.velocity.x)) * Mathf.Sign(Rigidbody.velocity.x), Rigidbody.velocity.y);
                if (Mathf.Abs(targetVelocity.x) > 4)
                {
                    Rigidbody.velocity += new Vector2(1, 0) * Mathf.Abs(Rigidbody.velocity.y) * 0.1f;
                }
            }





            // handle torque
            float torque = -inputH;
            if (slipperyMovement) torque *= MathHelper.Remap(0.025f, 0.05f, 0, 1, inputTimer.time);   // Ice movement
            else torque *= MathHelper.Remap(0.1f, 0.15f, 0, 1, inputTimer.time);                    // Normal movement
            if (isGrounded || isLeftWallSupported || isRightWallSupported)
                torque *= groundTorque;
            else
                torque *= airTorque;
            if (isCeilingGrounded)
                torque *= -1;
            if (invertedGravity)
                torque *= -1;
            Rigidbody.AddTorque(torque, ForceMode2D.Force);
            // Small rotation force when *jumping* out of a WallSqueeze
            if (wasLeftWallSupported && wasFrameLeftWallSupported && wasFrameRightWallSupported && !isFrameLeftWallSupported && !isFrameRightWallSupported)
            {
                if (jumpTimer.isRunning && Rigidbody.velocity.y * Rigidbody.gravityScale > 0)
                    Rigidbody.AddTorque(impulseAirTorque, ForceMode2D.Impulse);
                else
                    Rigidbody.angularVelocity = 0;
            }


            // jump
            bool externalJumpCondition = false;
            externalJumpCondition |= insideWater && inWaterTimer.time > 0.05f && (waterOnEdge == 0 || inputH == 0 || waterOnEdge == -inputH);
            externalJumpCondition |= currentWire && currentWire.CanJumpOut;
            bool wallSqueeze = isFrameLeftWallSupported && isFrameRightWallSupported;
            bool airjump = false;
            bool specialAirJump = false;
            bool specialAirWallJump = false;
            if (jump && !isGrounded && (normalJumpCount + (hasGroundJumped ? 0 : 1) < Accessibility.JumpCount || AirJumps > 0) && !isLeftWallSupported && !isRightWallSupported && !isCeilingGrounded)
            {
                // Consume extra jumps
                if (AirJumps > 0)
                {
                    specialAirJump = true;
                    AirJumps--;
                }
                airjump = true;

                if (Rigidbody.velocity.x * Mathf.Sign(inputH) < -2f)
                {
                    specialAirJump = false;
                    airjump = false;
                    specialAirWallJump = true;
                }
            }
            if ((jumpImmediate || airjump) || (isGrounded || externalJumpCondition || wallSqueeze) && jump && allowJump && !disallowJump && !disallowJumpTimer.isRunning && (!hasJumped || wallSqueeze || (hasJumped && wireCoyoteTime.isRunning)) && (jumpTimer.time > 0.2 || !jumpTimer.isRunning))
            {
                bool wireJumpKilled = false;
                if (currentWire)
                {
                    wireJumpKilled = !currentWire.JumpOut();
                }

                float newHorizontalVelocity = Rigidbody.velocity.x;
                // TODO figure out why we were doing this...
                if (inputH != 0 && Mathf.Sign(inputH) == Mathf.Sign(Rigidbody.velocity.x))
                {
                    //newHorizontalVelocity = Mathf.Min(Mathf.Abs(Rigidbody.velocity.x) * 1.5f, maxMoveSpeed);
                    //newHorizontalVelocity = Mathf.Max(newHorizontalVelocity, Mathf.Abs(Rigidbody.velocity.x));
                    //newHorizontalVelocity = newHorizontalVelocity * Mathf.Sign(Rigidbody.velocity.x);
                }
                // remove horizontal speed a bit when jumping off of vertically moving platforms
                if (!externalJumpCondition && inputH != 0 && Mathf.Sign(inputH) == Mathf.Sign(Rigidbody.velocity.x) && groundTimer.time > 0.25)
                {
                    newHorizontalVelocity *= MathHelper.Remap(maxMoveSpeed * 1.5f, maxMoveSpeed * 2.0f, 1.0f, 0.8f, Mathf.Abs(Rigidbody.velocity.x));
                }
                // remove vertical velocity based on surface
                if (tipTimer.isRunning)
                {
                    Rigidbody.velocity = new Vector2(newHorizontalVelocity, 0);
                }
                if (insideWater)
                {
                    Rigidbody.velocity = new Vector2(newHorizontalVelocity, Rigidbody.velocity.y * 0.3f);
                    onWaterJump.Invoke();
                }
                else if (currentWire)
                    Rigidbody.velocity = new Vector2(newHorizontalVelocity, Rigidbody.velocity.y * 0.25f);
                else
                    Rigidbody.velocity = new Vector2(newHorizontalVelocity, Rigidbody.velocity.y * Mathf.Abs(surfaceNormal.y));
                if (specialAirJump)
                    Rigidbody.velocity = new Vector2(newHorizontalVelocity, 0);
                float strength = 2 * (jumpHeight - 1) / Mathf.Sqrt((2 * (jumpHeight - 1)) / gravity.magnitude);
                // Limit y velocity
                if (Vector2.Dot(Rigidbody.velocity, -gravity) < 0)
                    Rigidbody.velocity = Vector3.ProjectOnPlane(Rigidbody.velocity, -gravity.normalized);
                // additional jump force when jumping up a slope
                if (!sharpTurnTimer.isRunning && surfaceNormal != Vector2.zero && surfaceNormal.y * (invertedGravity ? -1 : 1) < 0.95 && inputH != 0 && Mathf.Sign(-surfaceNormal.x) == Mathf.Sign(inputH))
                {
                    strength += MathHelper.Remap(1, 0.707f, 0.5f, 1.5f, surfaceNormal.y * (invertedGravity ? -1 : 1));
                    Rigidbody.velocity = new Vector2(Rigidbody.velocity.x * MathHelper.Remap(0.8f, 0.707f, 1, 0.5f, surfaceNormal.y * (invertedGravity ? -1 : 1)), 0);
                }
                // additional force when in water
                if (insideWater)
                    strength += MathHelper.Remap(1.75f, 1.25f, 0, 2, Rigidbody.velocity.y);
                Rigidbody.AddForce(-gravity.normalized * strength, ForceMode2D.Impulse);

                if (bunnyJumpTimer.isRunning)
                {
                    Rigidbody.AddForce(Vector2.right * inputH * 1, ForceMode2D.Impulse);
                }

                // Handle interactables
                groundCollider?.SendMessage("Contact", SendMessageOptions.DontRequireReceiver);

                if (!airjump)        // Dont add ground jump if we are doing a airjump
                    hasGroundJumped = true;
                if (!specialAirJump)
                    normalJumpCount++;
                jumpImmediate = false;
                jumpTimer.Clear();
                jumpTimer.Start();
                overedgeTimer.Clear();
                allowOveredge = false;
                // TODO we might need this...
                //wireCoyoteTime.Clear();
                blockedGroundTimer.Start(0.25f);

                jumpBufferTimer.Stop();

                // Save jumps
                save.SessionJumps++;
                if (specialAirJump)
                    save.SessionAirJumps++;

                if (!wireJumpKilled)
                {
                    EventPlayerJump.Raise();
                    onJump.Invoke();
                }
            }
            if (jumpTimer.isRunning && hasCeilingContact)
            {
                jumpTimer.Clear();
            }
            if (jumpTimer.isRunning && (jumpTimer.time < jumpMinTime || jumpTimer.time < jumpMaxTime && jumpHold))     //hasGroundJumped &&
            {
                float mult = 1;
                if (insideWater)
                    mult *= 0.5f;
                Rigidbody.AddForce(-gravity.normalized * jumpExtraStrength * mult * gravity.magnitude, ForceMode2D.Force);
            }



            // walljump & edge boost
            bool edgeBoost = isGrounded && frameVelocity.y < 0.5f && hasJumped;
            if (!isGrounded && jump && allowJump && !disallowJump && !disallowJumpTimer.isRunning && (!jumpTimer.isRunning || jumpTimer.time > 0.01) && !(isFrameRightWallSupported && isFrameLeftWallSupported))
            {
                // check for walls and determine jump direction
                walljumpDir = 0;
                bool jumpValid = false;
                if (Vector3.Dot(leftWallNormal, Vector3.right) > 0.5f && !insideWater                                           // wall jump
                    || (waterOnEdge > 0 && inputH >= 0 && (!insideWater || (insideWater && inputH > 0))))                       // water side jump
                {
                    walljumpDir += 1;
                    jumpValid = true;
                    blockedLeftWallTimer.Start(0.25f);
                }
                else if (Vector3.Dot(rightWallNormal, Vector3.left) > 0.5f && !insideWater                                      // wall jump
                    || (waterOnEdge < 0 && inputH <= 0) && (!insideWater || (insideWater && inputH < 0)))                       // water side jump
                {
                    walljumpDir -= 1;
                    jumpValid = true;
                    blockedRightWallTimer.Start(0.25f);
                }
                if (specialAirWallJump)
                {
                    walljumpDir = (int)Mathf.Sign(inputH);
                    jumpValid = true;
                }

                // jump is valid, apply forces
                if (jumpValid)
                {
                    // TODO figure out how to enable edge boost again

                    Vector3 jumpTrajectory = new Vector3(walljumpDir, -Mathf.Sign(gravity.y) * (flipGravityTimer.isRunning ? -1 : 1), 0).normalized;
                    // down wall jumps
                    if (abilityDownwardWallJump && inputV < -0.995f)
                    {
                        print("Walljump down");
                        jumpTrajectory = new Vector3(jumpTrajectory.x, -jumpTrajectory.y * 0.25f, 0);
                    }

                    Rigidbody.velocity = Vector2.zero;
                    Rigidbody.AddForce(jumpTrajectory * walljumpStrength, ForceMode2D.Impulse);

                    // Handle interactables
                    (walljumpDir > 0 ? leftCollider : rightCollider)?.SendMessage("Contact", SendMessageOptions.DontRequireReceiver);

                    Rigidbody.angularVelocity = 0;
                    Rigidbody.AddTorque(-walljumpDir * impulseTorque * 0.25f, ForceMode2D.Impulse);
                    hasWallJumped = true;
                    hasGroundJumped = false;
                    normalJumpCount = 0;
                    wallJumpTimer.Clear();
                    wallJumpTimer.Start();
                    bunnyJumpTimer.Start();
                    wallRollTimer.Clear();
                    overedgeTimer.Clear();
                    allowOveredge = false;
                    wireJumpTimer.Clear();

                    jumpBufferTimer.Stop();

                    save.SessionWallJumps++;
                    EventPlayerJump.Raise();
                    onJump.Invoke();
                }
            }
            if (wallJumpTimer.isRunning && (wallJumpTimer.time < walljumpMinTime || wallJumpTimer.time < walljumpMaxTime) && !jumpHold)
            {
                // Limit wall jump when not holding jump
                Rigidbody.AddForce(gravity * walljumpShortGravityBoost, ForceMode2D.Force);
                Rigidbody.velocity = Vector2.Lerp(Rigidbody.velocity, Vector2.zero, walljumpShortDrag * Time.fixedDeltaTime);
            }

            // Ceiling jump
            if (jumpImmediate || (hasCeilingContact && allowJumpEverywhere) && jump && allowJump && !disallowJump && !disallowJumpTimer.isRunning && (ceilingJumpTimer.time > 0.2 || !ceilingJumpTimer.isRunning))
            {
                if (Vector2.Dot(Rigidbody.velocity, gravity) < 0)
                    Rigidbody.velocity = Vector3.ProjectOnPlane(Rigidbody.velocity, gravity.normalized);

                Rigidbody.AddForce(gravity.normalized * ceilingjumpStrength, ForceMode2D.Impulse);

                hasCeilingJumped = true;
                ceilingJumpTimer.Clear();
                ceilingJumpTimer.Start();

                hasWallJumped = false;
                wallJumpTimer.Clear();

                allowOveredge = false;

                save.SessionCeilingjumps++;
                EventPlayerJump.Raise();
                onJump.Invoke();
            }


            //Weather Force
            Vector2 windVelocity = Vector3.zero;
            if (WeatherManager.Instance.WeatherEffectsMovement(new Vector2(Rigidbody.position.x, Rigidbody.position.y)))
            {
                windVelocity = WeatherManager.Instance.GetWeatherPushVector(Rigidbody.position);

                // Limit the horizontal wind component when runnin up a wall
                if (inputH > 0 && isRightWallSupported || inputH < 0 && isLeftWallSupported)
                    windVelocity.x *= MathHelper.Remap(25, 50, 0, 1, Mathf.Abs(windVelocity.x));

                Rigidbody.AddForce(windVelocity, ForceMode2D.Force);
            }


            // handle in air movement
            if (!isGrounded && !limitAirMovement
                // if movingLeft and (noWall and slowerThanMaxSpeed or wallOnRight and shouldRollOnWall)
                && (inputH > 0 && (!isRightWallSupported && Rigidbody.velocity.x < +maxMoveSpeed || isRightWallSupported && canWallroll && wallRollTimer.time < wallRollTime * Accessibility.WallRollMult)     // && hasJumped
                || (inputH < 0 && (!isLeftWallSupported  && Rigidbody.velocity.x > -maxMoveSpeed || isLeftWallSupported  && canWallroll && wallRollTimer.time < wallRollTime * Accessibility.WallRollMult))))
            {
                float mult = 1;
                // add strong push whenever we are falling onto a wall to help push us over the edge
                if ((inputH > 0 && isRightWallSupported || inputH < 0 && isLeftWallSupported))
                {
                    if (frictionCoefficient >= 1)
                    {
                        mult = Mathf.Lerp(10, 2, Mathf.Abs(Rigidbody.velocity.x));
                        // this helps players roll on walls if the friction is very high
                        //Rigidbody.AddForce(-gravity * (1 - frictionCoefficient), ForceMode2D.Force);
                        // limit gravity
                        if (Rigidbody.velocity.y * (invertedGravity ? -1 : 1) < 0)
                            Rigidbody.velocity = Vector2.Lerp(Rigidbody.velocity, new Vector2(Rigidbody.velocity.x, 0), Time.fixedDeltaTime * 4);
                    }
                    else
                    {
                        Rigidbody.AddForce(-gravity * (hasWallJumped ? 0.85f : 0.3f), ForceMode2D.Force);
                    }
                }
                // limit horizontal input if we are jumping on the spot (helps with rotating the player more precisely)
                if (hasGroundJumped && Mathf.Abs(Rigidbody.velocity.x) < 0.5)
                {
                    mult *= MathHelper.Remap(0.06f, 0.2f, 0, 1, inputTimer.time);
                }
                // limit horizontal input if we are wall jumping (more consistent jumping arcs)
                if (Mathf.Max(wallJumpTimer.time, wireJumpTimer.isRunning ? wireJumpTimer.time : 0) > 0)
                {
                    mult *= MathHelper.Remap(0.1f, 0.2f, 0, 1, Mathf.Max(wallJumpTimer.time, wireJumpTimer.isRunning ? wireJumpTimer.time : 0));
                }
                // stop horizontal movement when jumping at sloped ceilings (prevents running up the ceiling)
                if (hasCeilingContact && Mathf.Sign(-ceilingNormal.x) == Mathf.Sign(inputH) && !outsideForce)
                {
                    mult *= 0;
                }
                // allow movement if a force pushes us to the ceiling to initiate a ceiling run
                if (outsideForce && hasCeilingContact)
                {
                    mult *= MathHelper.Remap(0.1f, 0.15f, 0, 1, inputTimer.time);
                }
                // improve handling when rolling over an edge
                if (!hasJumped && frictionCoefficient >= 1)
                {
                    mult *= MathHelper.Remap(0.2f, 0.4f, 1.6f, 1, airTimer.time);
                }
                Rigidbody.AddForce(new Vector2(inputH, 0) * airSpeed * mult * frictionCoefficient, ForceMode2D.Force);
			}

			// wallroll drag
            if (Rigidbody.velocity.y * (invertedGravity ? -1 : 1) < 0 && (isRightWallSupported && inputH > 0 || isLeftWallSupported && inputH < 0))
			{
				Rigidbody.velocity = Vector2.Lerp(Rigidbody.velocity, new Vector2(Rigidbody.velocity.x, invertedGravity ? Mathf.Min(Rigidbody.velocity.y, 4) : Mathf.Max(Rigidbody.velocity.y, -4)), Time.fixedDeltaTime * 4);
			}


			// drag
			if (isGrounded && inputH == 0)
			{
				if (surfaceNormal.y < 0.8)
					Rigidbody.angularDrag = MathHelper.Remap(0.8f, 0.72f, 10, 0, surfaceNormal.y);
				else
					Rigidbody.angularDrag = MathHelper.Remap(1, 0.8f, 0, 10, surfaceNormal.y);
			}
			else
			{
				Rigidbody.angularDrag = 0.2f;
			}
			
			

            // fast fall
            if (abilityFastFall && inputV < -0.85 && Rigidbody.velocity.magnitude < 30 && isNotSupported && !hasCeilingContact && !waterBlobJumpingOutTimer.isRunning)
            {
                float gravityBoost = Mathf.Lerp(2, 0.5f, Mathf.Pow(Mathf.InverseLerp(0, 30, Rigidbody.velocity.magnitude), 0.25f));
                if (insideWater)
                    gravityBoost *= 0.25f;
                if (wallJumpTimer.isRunning)
                    gravityBoost *= MathHelper.Remap(0.1f, 0.2f, 0, 1, wallJumpTimer.time);
                //gravityBoost *= MathHelper.Remap(0, 0.2f, 0, 1, airTimer.time);
                Rigidbody.AddForce(gravity * gravityBoost, ForceMode2D.Force);
            }

            // slow fall
            if (inputV > 0.85 && Vector2.Dot(Rigidbody.velocity, gravity) > 0 && isNotSupported && !hasCeilingContact &&
                (!wallJumpTimer.isRunning || wallJumpTimer.time > 0.7f) && !waterBlobJumpingOutTimer.isRunning)
            {
                Rigidbody.AddForce(-gravity * 0.2f, ForceMode2D.Force);
            }

            // Wiggle to gain more height
            if (isNotSupported && Mathf.Sign(Rigidbody.velocity.x) != Mathf.Sign(lastVelocity.x) && Mathf.Sign(Rigidbody.velocity.x) == inputH && Mathf.Sign(lastVelocity.x) != inputH &&
                hasGroundJumped && Vector2.Dot(Rigidbody.velocity, gravity) < 0)
            {
                Rigidbody.AddForce(-gravity * 0.025f, ForceMode2D.Impulse);
            }



            // single 90 degree tip
            float tipTime = 0.25f;
            if (!tipTimer.isRunning && !tipTimer.hasStopped && jumpTimer.time < 0.1 && requestedFlipDir != 0 && frameGrounded
                && !slipperyMovement)      // && !jump && !hasJumped && !hasWallJumped
            {
                tipTimer.Start(0.3f);

                // Allow superjump
                if (Mathf.Abs(Rigidbody.angularVelocity) < 100 && groundTimer.time > 0.25 && !allowJumpEverywhere)
                {
                    superjumpAllowedTimer.Start(0.3f);
                }

                //rigidbody.centerOfMass = Quaternion.Inverse(GetNextStableRotation()) * new Vector2(tiltOffset.x * -requestedFlipDir, tiltOffset.y) * frictionCoefficient;
                //cornerPivot = rigidbody.centerOfMass;
                //rigidbody.AddTorque(90 / tipTime * 0.25f * Time.fixedDeltaTime * -requestedFlipDir, ForceMode2D.Impulse);
                //rigidbody.AddForce(new Vector2(-requestedFlipDir, 0) * MathHelper.Remap(1, 0, 0, 1, frictionCoefficient), ForceMode2D.Impulse);

                float ceilingFlip = isCeilingGrounded ? -1 : 1;

                Vector2 pos = Quaternion.Inverse(GetNextStableRotation()) * new Vector2(0.5f * requestedFlipDir, 0.5f) * frictionCoefficient;
                Vector2 forceDir = Quaternion.LookRotation(Vector3.forward, surfaceNormal * (invertedGravity ? -1 : 1) * ceilingFlip) * new Vector2(requestedFlipDir, (invertedGravity ? -0.25f : 0.25f) * ceilingFlip).normalized;
                float forceStrength = 5;
                forceStrength *= MathHelper.Remap(-0.707f, 0.707f, 0.5f, 1.75f, -surfaceNormal.x * requestedFlipDir);
                Rigidbody.AddForceAtPosition(forceDir * forceStrength * frictionCoefficient, transform.TransformPoint(pos), ForceMode2D.Impulse);
                float torqueAmount = 90 / tipTime * 0.15f;
                torqueAmount *= MathHelper.Remap(-0.707f, 0.707f, 0.5f, 1.75f, -surfaceNormal.x * requestedFlipDir);
                Rigidbody.AddTorque(torqueAmount * Time.fixedDeltaTime * -requestedFlipDir * frictionCoefficient * (invertedGravity ? -1 : 1) * ceilingFlip, ForceMode2D.Impulse);
                Rigidbody.angularVelocity = Rigidbody.angularVelocity * 0.5f * frictionCoefficient * (invertedGravity ? -1 : 1) * ceilingFlip;
                Rigidbody.AddForce(Vector2.right * inputH * 2.025f, ForceMode2D.Impulse);

                Rigidbody.velocity = new Vector2(Rigidbody.velocity.x * MathHelper.Remap(0, 10, 1, 0.7f, Mathf.Abs(Rigidbody.velocity.x)), Rigidbody.velocity.y);
            }
            //if (tipTimer.isRunning)
            //{
            //    rigidbody.AddForce(-gravity * 0.8f, ForceMode2D.Force);
            //    if (!hasJumped)
            //    {
            //        if (tipTimer.time > 0.1f && isLeftWallSupported)
            //            rigidbody.MovePosition((Vector2)transform.position + new Vector2(tiltWall * Time.fixedDeltaTime / tipTime, 0));
            //        if (tipTimer.time > 0.1f && isRightWallSupported)
            //            rigidbody.MovePosition((Vector2)transform.position - new Vector2(tiltWall * Time.fixedDeltaTime / tipTime, 0));
            //    }
            //}
            if (!isGrounded)
            {
                requestedFlipDir = 0;
                queuedFlipDir = 0;
                tipTimer.Stop();
            }
            if (tipTimer.hasStopped)
            {
                //if (inputTimer.time < 0.15f && Mathf.Abs(Mathf.Abs(transform.position.x % 1) - 0.5f) < 0.2f)
                //{
                //    transform.position = (new Vector2(Mathf.Floor(transform.position.x) + 0.5f - requestedFlipDir * 0.1f, transform.position.y));
                //    print("position corrected");
                //}
                Rigidbody.angularVelocity = Rigidbody.angularVelocity * 0.5f;
                //rigidbody.centerOfMass = Vector2.zero;

                if (queuedFlipDir != 0)
                {
                    requestedFlipDir = queuedFlipDir;
                    queuedFlipDir = 0;
                    tipTimer.Clear();
                }
                else
                {
                    requestedFlipDir = 0;
                    tipTimer.Clear();
                }
            }

            // Initiate super jump
            if (superjumpAllowedTimer.isRunning && !superJumpTimer.isRunning && jumpTimer.isRunning && tipTimer.isRunning && jumpTimer.time < 0.04 && tipTimer.time < 0.04)
            {
                superJumpTimer.Start(jumpMaxTime * 0.5f);
            }
            if (superJumpTimer.isRunning)
            {
                Rigidbody.AddForce(-gravity.normalized * superJumpStrength * superJumpTimer.progress, ForceMode2D.Force);
            }
            if (!jumpHold)
            {
                superJumpTimer.Clear();
            }



            // Set allowOveredge when we are in the air
            if (((airTimer.time > 0.02 && !isGrounded) || (airTimer.time == 0 && isGrounded)) && frictionCoefficient >= 1)    // 
            {
                allowOveredge = true;
            }
            if (groundTimer.time > 0.04 || insideWater || speedRailTimer.isRunning)
            {
                allowOveredge = false;
            }

            // detect if we roll over or under a ledge
            if ((anyJumpTime > 0.5 || jumpHold || !hasJumped || hasLeftSurfaceAfterJump) && ((wasLeftWallSupported && !isLeftWallSupported && inputH < 0) || (wasRightWallSupported && !isRightWallSupported && inputH > 0))        //hasLeftSurfaceAfterJump && 
                && frictionCoefficient > 0.25f && inputTimer.time > 0.08 && (allowOveredge || (jumpHold && anyJumpTime > 0.1)) && nearGeometry)
            {
                bool killColliderOverEdge = false;
                if (!Accessibility.Invincible && Rigidbody.velocity.y * Mathf.Sign(Rigidbody.gravityScale) > 4)
                {
                    // Test if there is something that can kill the player over the edge, and abort the overedge roll if necessary
                    for (int i = 0; i < overedgeTraceHits.Length; i++)
                        overedgeTraceHits[i] = new RaycastHit2D();

                    bool queriesHitTriggers = Physics2D.queriesHitTriggers;
                    bool queriesStartInColliders = Physics2D.queriesHitTriggers;
                    Physics2D.queriesHitTriggers = true;
                    Physics2D.queriesStartInColliders = true;
                    Physics2D.CircleCastNonAlloc(transform.position + Vector3.up * 0.333f * Rigidbody.gravityScale, 0.1f, Vector2.right * inputH, overedgeTraceHits, 1.15f, spikeDetectionMask);
                    Physics2D.queriesHitTriggers = queriesHitTriggers;
                    Physics2D.queriesStartInColliders = queriesStartInColliders;
                    foreach (var hit in overedgeTraceHits)
                    {
                        // TODO this might be inperformant...
                        if (hit.transform?.GetComponent<Kill>())
                        {
                            killColliderOverEdge = true;
                            // Push player up a bit (this helps them rolling up to jump over spikes)
                            Rigidbody.velocity = new Vector2(0, Mathf.Sign(Rigidbody.velocity.y) * Mathf.Max(Mathf.Abs(Rigidbody.velocity.y) + Mathf.Abs(Rigidbody.velocity.x) * 0.25f, 11.5f));
                            break;
                        }
                    }
                }


                if (!killColliderOverEdge)
                {
                    // enable timer for overedge boost
                    if (Rigidbody.velocity.y * (invertedGravity ? -1 : 1) > 0)       // && (jumpTimer.time == 0 || jumpTimer.time > 0.3f)
                    {
                        Rigidbody.velocity = new Vector2(inputH * 5, Rigidbody.velocity.y * frictionCoefficient);
                        overedgeTimer.Clear();
                        overedgeTimer.Start();
                    }
                    // stop the affects of the wall roll boost if we are falling off a wall
                    if (Rigidbody.velocity.y * (invertedGravity ? -1 : 1) < 0)
                    {
                        Rigidbody.velocity = new Vector2(0, Rigidbody.velocity.y);
                    }
                }
            }
            // correct upward momentum when we roll over a ledge
            if (overedgeTimer.isRunning)
			{
                Vector2 overedgeBoost = Vector2.right * inputH * 50 + gravity.normalized * 10;
				overedgeBoost *= MathHelper.Remap(0, 0.2f, 1, 0, overedgeTimer.time);
				Rigidbody.AddForce(overedgeBoost, ForceMode2D.Force);
                //Rigidbody.velocity = new Vector2(Rigidbody.velocity.x, Mathf.Min(Mathf.Abs(gravity.y), 10) * Mathf.Sign(gravity.y) * 0.5f);
                Rigidbody.velocity = new Vector2(Rigidbody.velocity.x, Mathf.Min(Mathf.Abs(Rigidbody.velocity.y), 5) * Mathf.Sign(Rigidbody.velocity.y) * 0.5f);
				if (overedgeTimer.HasElapsed(0.2f)) // || hasJumped
                {
                    Rigidbody.AddForce(Vector2.right * inputH + gravity.normalized * 1, ForceMode2D.Impulse);
                    overedgeTimer.Clear();
                }
			}


            // lerp to proper radius for movement
            colliderSphereify = MathHelper.Remap(0.1f, 0.175f, 0, 1, inputTimer.time);
            // this fixes player hovering when wedging himself between wall and ground, while being unable to rotate
            if (isGrounded && ((inputH > 0 && isRightWallSupported) || (inputH < 0 && isLeftWallSupported)) && Mathf.Abs(Rigidbody.angularVelocity) < 50)
                colliderSphereify *= MathHelper.Remap(3, 4, 0, 1, lastVelocity.magnitude);
            if (!isWallSupported)
                colliderSphereify *= MathHelper.Remap(270, 360, 0, 1, Mathf.Abs(Rigidbody.angularVelocity));
            //circleCollider.radius = MathHelper.Damp(circleCollider.radius, Mathf.Lerp(0.445f, 0.707f, sphereify), 1, Time.fixedDeltaTime * 30);
            //boxCollider.size = MathHelper.Damp(boxCollider.size, Vector2.Lerp(Vector2.one * 0.95f, Vector2.one * 0.9f, sphereify), 1, Time.fixedDeltaTime * 30);
            circleCollider.radius = Mathf.Lerp(0.445f, 0.707f, colliderSphereify);
            boxCollider.size = Vector2.Lerp(Vector2.one * 0.95f, Vector2.one * 0.89f, colliderSphereify);

            // air drag
            //if (!grounded)
            //    rigidbody.angularVelocity = Mathf.Lerp(rigidbody.angularVelocity, Mathf.Min(Mathf.Abs(rigidbody.angularVelocity), 250f) * Mathf.Sign(rigidbody.angularVelocity), Time.fixedDeltaTime * 8);




            // limit velocity
            float calculatedMaxSpeed = MathHelper.Remap(0, 1, 25, 75, Vector2.Dot(Rigidbody.velocity.normalized, Vector2.down * (invertedGravity ? -1 : 1)));
            if (Rigidbody.velocity.magnitude > calculatedMaxSpeed)
            {
                Rigidbody.velocity = Rigidbody.velocity.normalized * MathHelper.Damp(Rigidbody.velocity.magnitude, calculatedMaxSpeed, 0.99f, Time.fixedDeltaTime * 20);
            }

            // Horizontal drag
            Rigidbody.velocity = new Vector2(Rigidbody.velocity.x * MathHelper.Remap(2, 4, 0.9975f, 1, Mathf.Abs(Rigidbody.velocity.x)), Rigidbody.velocity.y);

            // link rotation and speed together
            //float maxSpeed = 650 / 360f * 4;
            //if (isGrounded && rigidbody.velocity.magnitude > maxSpeed)
            //{
            //    rigidbody.transform.position = ((Vector2)transform.position - rigidbody.velocity.normalized * (rigidbody.velocity.magnitude - maxSpeed) * Time.fixedDeltaTime);
            //}

            // limit angular velocity
            //Rigidbody.angularVelocity = Mathf.Lerp(Rigidbody.angularVelocity, Mathf.Min(Mathf.Abs(Rigidbody.angularVelocity), 330f) * Mathf.Sign(Rigidbody.angularVelocity), Time.fixedDeltaTime * 14);



            // Apply stats
            float frameTravelDistance = Rigidbody.velocity.magnitude * Time.fixedDeltaTime;
            save.TravelDistance += frameTravelDistance;
            if (inputH != 0 && isGrounded)
                save.TravelDistanceGround += frameTravelDistance;
            if (insideWater)
                save.TravelDistanceWater += frameTravelDistance;

            // set next frame input
            //if (inputPlayback && inputId < inputSequence.Count)
            //{
            //    Rigidbody.position = inputSequence[inputId].position;
            //    Rigidbody.velocity = inputSequence[inputId].velocity;
            //    Rigidbody.angularVelocity = inputSequence[inputId].angularVelocity;
            //}

            // reset states
            if (frameGrounded || isWallSupported || hasCeilingContact)
            {
                outsideForce = false;
            }
            limitAirMovement = false;

            // reset jump request
            // TODO this is a qwerk, input will not be reset by the input system.
            jump = false;
            allowJump = true;
            disallowJump = false;
            useJump = false;
            allowJumpEverywhere = false;

            // Revert vertical input if gravity is inverted
            if (invertedGravity)
            {
                inputV *= -1;
            }


            lastVelocity = frameVelocity;
            Debug.DrawLine(debugLastPosition, transform.position, Color.red, 1);
            debugLastPosition = transform.position;
        }


        #region Debug

        private Vector2 debugScreenPos;
        private int debugLineIndex;
        private static bool debugShow;
        private bool breakDebugger;
        private void OnGUI()
        {
            Cursor.visible |= debugShow;
            if (!debugShow)
                return;

            DrawPlayerStats();
            DrawAbilityCustomizer();
        }
        private void DrawAbilityCustomizer()
        {
            GUILayout.BeginArea(new Rect(50, 200, 200, 1000));
            GUILayout.Label("Player abilities:");
            abilityWallRoll = GUILayout.Toggle(abilityWallRoll, "Wall roll");
            abilityFastFall = GUILayout.Toggle(abilityFastFall, "Fast fall");
            abilityDownwardWallJump = GUILayout.Toggle(abilityDownwardWallJump, "Downward wall jump");
            GUILayout.EndArea();
        }
        private void DrawPlayerStats()
        {
            debugScreenPos = Camera.main.WorldToScreenPoint(transform.position);
            debugScreenPos = new Vector2(debugScreenPos.x, Screen.height - debugScreenPos.y);
            debugLineIndex = 0;

            DrawDebugLabel("waterOnEdge", waterOnEdge);
            DrawDebugLabel("outsideForce", outsideForce);
            DrawDebugLabel("tipTimer", tipTimer.time);
            DrawDebugLabel("contactFriction", contactFriction);
            DrawDebugLabel("rightWall", isRightWallSupported);
            DrawDebugLabel("leftwall", isLeftWallSupported);
            DrawDebugLabel("wallRollTimer", wallRollTimer.time);
            DrawDebugLabel("canWallroll", canWallroll);
            DrawDebugLabel("hasJumped", hasJumped);
            DrawDebugLabel("grounded", isGrounded);
            DrawDebugLabel("frameGrounded", frameGrounded);
            DrawDebugLabel("airTimer", airTimer.time);
            DrawDebugLabel("groundTimer", groundTimer.time);
            DrawDebugLabel("inputTime", inputTimer.time);
            DrawDebugLabel("jumpTimer", jumpTimer.time);
            DrawDebugLabel("angularVelocity", Rigidbody.angularVelocity);
            DrawDebugLabel("velocity.y", Rigidbody.velocity.y);
            DrawDebugLabel("velocity.x", Rigidbody.velocity.x);
            DrawDebugLabel("jumpBufferTimer", jumpBufferTimer.time);
        }
        private void DrawDebugLabel(string name, string value, Color valueColor)
        {
            GUIStyle styleLeft = new GUIStyle(GUI.skin.label);
            GUIStyle styleRight = new GUIStyle(GUI.skin.label);
            styleLeft.alignment = TextAnchor.LowerRight;
            styleRight.alignment = TextAnchor.LowerLeft;
            styleRight.normal.textColor = valueColor;
            styleLeft.fontSize = 10;
            styleRight.fontSize = 10;
            GUI.Label(new Rect(debugScreenPos.x - 500 - 5, debugScreenPos.y - 50 - debugLineIndex * 10, 500, 15), name, styleLeft);
            GUI.Label(new Rect(debugScreenPos.x + 5, debugScreenPos.y - 50 - debugLineIndex * 10, 500, 15), value, styleRight);
            debugLineIndex++;
        }
        private void DrawDebugLabel(string name, string value)
        {
            DrawDebugLabel(name, value, Color.white);
        }
        private void DrawDebugLabel(string name, bool value)
        {
            DrawDebugLabel(name, value.ToString(), value ? Color.green : Color.red);
        }
        private void DrawDebugLabel(string name, int value)
        {
            DrawDebugLabel(name, value.ToString());
        }
        private void DrawDebugLabel(string name, float value)
        {
            DrawDebugLabel(name, value.ToString("N2"));
        }

        #endregion


        private void UpdateGodMode()
        {
            float speed = 15f;
            if (Input.GetKey(KeyCode.LeftShift))
                speed *= 2;
            if (Input.GetKey(KeyCode.LeftControl))
                speed /= 2;
            transform.position += new Vector3(inputH, inputV, 0) * speed * Time.fixedDeltaTime;
        }

        #region WaterBlob

        private WaterBlob waterBlob;
        private Timer waterBlobJumpingOutTimer = new Timer(UpdateCycle.FixedUpdate);
        public void SetWaterBlob(WaterBlob blob)
        {
            waterBlob = blob;
            if (waterBlob == null)
                waterBlobJumpingOutTimer.Start(0.5f);
        }

        #endregion

        #region Waterblock

        public bool insideWater
        { get; private set; }
        private Timer inWaterTimer = new Timer(UpdateCycle.FixedUpdate);
        private Timer waterCoyoteTimer = new Timer(UpdateCycle.FixedUpdate);
        private int waterOnEdge;
        private Vector2 waterForce;
        private Vector2 waterForceCenterOffset;

        private Vector2[] waterCheckPositions = new Vector2[24];
        private Vector2[] waterExtraCheckPositions = new Vector2[8];
        private Collider2D[] waterOverlapResult = new Collider2D[1];

        /// <summary> Simulate fluid blocks. </summary>
        private void SimulateWater()
        {
            int waterMask = LayerMask.GetMask("Water");
            bool queriesHitTriggers = Physics2D.queriesHitTriggers;
            Physics2D.queriesHitTriggers = true;
            bool aroundWater = false;

            // Check player corners for water
            waterCheckPositions[00] = transform.position + transform.right * 0.5f + transform.up * 0.5f;
            waterCheckPositions[01] = transform.position + transform.right * 0.25f + transform.up * 0.5f;
            waterCheckPositions[02] = transform.position + transform.up * 0.5f;
            waterCheckPositions[03] = transform.position - transform.right * 0.25f + transform.up * 0.5f;
            waterCheckPositions[04] = transform.position - transform.right * 0.5f + transform.up * 0.5f;

            waterCheckPositions[05] = transform.position + transform.right * 0.5f + transform.up * 0.25f;
            waterCheckPositions[06] = transform.position + transform.right * 0.25f + transform.up * 0.25f;
            waterCheckPositions[07] = transform.position + transform.up * 0.25f;
            waterCheckPositions[08] = transform.position - transform.right * 0.25f + transform.up * 0.25f;
            waterCheckPositions[09] = transform.position - transform.right * 0.5f + transform.up * 0.25f;

            waterCheckPositions[10] = transform.position + transform.right * 0.5f;
            waterCheckPositions[11] = transform.position + transform.right * 0.25f;
            waterCheckPositions[12] = transform.position - transform.right * 0.25f;
            waterCheckPositions[13] = transform.position - transform.right * 0.5f;

            waterCheckPositions[14] = transform.position + transform.right * 0.5f - transform.up * 0.25f;
            waterCheckPositions[15] = transform.position + transform.right * 0.25f - transform.up * 0.25f; 
            waterCheckPositions[16] = transform.position - transform.up * 0.25f;
            waterCheckPositions[17] = transform.position - transform.right * 0.25f - transform.up * 0.25f; 
            waterCheckPositions[18] = transform.position - transform.right * 0.5f - transform.up * 0.25f; 

            waterCheckPositions[19] = transform.position + transform.right * 0.5f - transform.up * 0.5f;
            waterCheckPositions[20] = transform.position + transform.right * 0.25f - transform.up * 0.5f;
            waterCheckPositions[21] = transform.position - transform.up * 0.5f;
            waterCheckPositions[22] = transform.position - transform.right * 0.25f - transform.up * 0.5f;
            waterCheckPositions[23] = transform.position - transform.right * 0.5f - transform.up * 0.5f;


            // Check extra positions around player for water
            waterExtraCheckPositions[0] = transform.position + transform.right * 0.6f + transform.up * 0.6f;
            waterExtraCheckPositions[1] = transform.position - transform.right * 0.6f + transform.up * 0.6f;
            waterExtraCheckPositions[2] = transform.position + transform.right * 0.6f - transform.up * 0.6f;
            waterExtraCheckPositions[3] = transform.position - transform.right * 0.6f - transform.up * 0.6f;
            waterExtraCheckPositions[4] = transform.position + transform.up * 0.6f;
            waterExtraCheckPositions[5] = transform.position - transform.up * 0.6f;
            waterExtraCheckPositions[6] = transform.position + transform.right * 0.6f;
            waterExtraCheckPositions[7] = transform.position - transform.right * 0.6f;


            Vector2 centerPoint = Vector2.zero;
            float waterPercentage = 0;
            bool centerUnderwater = Physics2D.OverlapPointNonAlloc(transform.position, waterOverlapResult, waterMask) > 0;
            if (centerUnderwater)
            {
                // Fully inside water
                waterPercentage = 1;
                centerPoint = transform.position;
            }
            else
            {
                // Not fully inside water
                waterPercentage = 0;
                centerPoint = Vector2.zero;
                foreach (var p in waterCheckPositions)
                {
                    if (Physics2D.OverlapPointNonAlloc(p, waterOverlapResult, waterMask) > 0)
                    {
                        waterPercentage++;
                        centerPoint += p;
                    }
                }
                centerPoint = centerPoint / waterPercentage;
                waterPercentage /= (float)waterCheckPositions.Length;
                waterPercentage = Mathf.Pow(waterPercentage, 0.5f);
                centerPoint = Vector2.Lerp(centerPoint, transform.position, waterPercentage);

                // Check extra positions
                if (waterPercentage > 0)
                {
                    foreach (var p in waterExtraCheckPositions)
                    {
                        if (Physics2D.OverlapPointNonAlloc(p, waterOverlapResult, waterMask) > 0)
                        {
                            aroundWater = true;
                            break;
                        }
                    }
                }
            }


            // Check water sides
            RaycastHit2D rightWater = new RaycastHit2D();
            RaycastHit2D leftWater = new RaycastHit2D();
            RaycastHit2D upWater = new RaycastHit2D();
            RaycastHit2D downWater = new RaycastHit2D();
            if (Physics2D.OverlapPointNonAlloc(transform.position - Vector3.right * 0.5f, waterOverlapResult, waterMask) == 0)
                rightWater  = Physics2D.BoxCast(transform.position - Vector3.right  * 0.5f, new Vector2(0.1f, 1), 0, Vector3.right, 1.25f, waterMask);
            if (Physics2D.OverlapPointNonAlloc(transform.position - Vector3.left * 0.5f, waterOverlapResult, waterMask) == 0)
                leftWater   = Physics2D.BoxCast(transform.position - Vector3.left   * 0.5f, new Vector2(0.1f, 1), 0, Vector3.left, 1.25f, waterMask);
            if (Physics2D.OverlapPointNonAlloc(transform.position - Vector3.up * 0.5f * gravitySign, waterOverlapResult, waterMask) == 0)
                upWater     = Physics2D.BoxCast(transform.position - Vector3.up     * 0.5f * gravitySign, new Vector2(1, 0.1f), 0, Vector3.up * gravitySign, 1, waterMask);
            if (Physics2D.OverlapPointNonAlloc(transform.position - Vector3.down * 0.707f * gravitySign, waterOverlapResult, waterMask) == 0)
                downWater   = Physics2D.BoxCast(transform.position - Vector3.down   * 0.707f * gravitySign, new Vector2(1, 0.1f), 0, Vector3.down * gravitySign, 1.414f, waterMask);


            // Enable water wall jumps
            waterOnEdge = 0;
            if (!upWater.collider)
            {
                if (rightWater.collider && rightWater.distance != 0 && Mathf.Abs(rightWater.normal.y) < 0.707) waterOnEdge--;
                if (leftWater.collider && leftWater.distance != 0 && Mathf.Abs(leftWater.normal.y) < 0.707) waterOnEdge++;
            }                  
            
            // Inside water
            if (waterPercentage > 0.2)
            {
                // Calculate actual updraft
                float buoyoncyMult = 0;
                if (!downWater.collider)
                    buoyoncyMult = Mathf.Abs(inputH);
                buoyoncyMult += MathHelper.Remap(0, 0.75f, 0.75f, 0, jumpTimer.time);
                float density = Mathf.Lerp(70, 90, buoyoncyMult);
                density *= Mathf.Lerp(MathHelper.Remap(-10, 0, 0.85f, 1, Rigidbody.velocity.y), 1, buoyoncyMult);

                // Smooth player COG for the water
                if (true || !insideWater) waterForceCenterOffset = centerPoint - (Vector2)transform.position;
                else waterForceCenterOffset = MathHelper.Damp(waterForceCenterOffset, centerPoint - (Vector2)transform.position, 0.9999f, Time.fixedDeltaTime * 20);

                // Apply a bunch of special chases
                Vector2 targetWaterForce = (new Vector2(-waterForceCenterOffset.x, 0) * 20 - gravity).normalized * density * 1;
                if (downWater.collider && downWater.distance > 0)
                {
                    targetWaterForce = MathHelper.Remap(0.25f, 0.666f, targetWaterForce, -gravity * 0.8f, downWater.distance + Mathf.Abs(inputH) * 0.15f);
                }

                // Smooth and apply force
                if (true || !insideWater) waterForce = targetWaterForce;
                else waterForce = MathHelper.Damp(waterForce, targetWaterForce, 0.9999f, Time.fixedDeltaTime * 50);
                Rigidbody.AddForceAtPosition(waterForce, (Vector2)transform.position + waterForceCenterOffset * 0.5f, ForceMode2D.Force);

                // Apply velocity drag
                float dragMult = 1.0f - 2 * Time.fixedDeltaTime;
                if (dragMult < 0.0f) dragMult = 0.0f;
                Rigidbody.velocity = dragMult * Rigidbody.velocity;
                OutsideForce();

                // Apply rotation drag
                float angularDragMult = 1.0f - 2 * Time.fixedDeltaTime;
                if (angularDragMult < 0.0f) angularDragMult = 0.0f;
                Rigidbody.angularVelocity = angularDragMult * Rigidbody.angularVelocity;

                aroundWater = true;
            }
            else
            {
                insideWater = waterCoyoteTimer.isRunning;
                if (!insideWater)
                    inWaterTimer.Clear();
                waterForce = Vector2.zero;
            }

            // Water init
            if (aroundWater)
            {
                inWaterTimer.Start();
                insideWater = inWaterTimer.isRunning;
                waterCoyoteTimer.Clear();
                waterCoyoteTimer.Start(0.05f);
                ResetJump();
            }

            // Revert values
            Physics2D.queriesHitTriggers = queriesHitTriggers;
        }

        #endregion

        public void TrySuicide()
        {
            if (!resetable)
                return;

            save.SessionSuicides++;
            Kill(true);
        }

        public void Kill()
        {
            Kill(false, false);
        }
        public void Kill(bool forceKill)
        {
            Kill(forceKill, false);
        }
        public void Kill(bool forceKill, bool silent)
        {
            // Early out for player invincibility
            if (Accessibility.Invincible && !forceKill)
                return;

            if (isDead || !resetable || godMode)
                return;

            // Call custom kill logic instead of killing the player here
            if (currentWire)
            {
                ElectricWire cachedWire = currentWire;
                currentWire = null;
                cachedWire.KillPlayer();
                return;
            }

            isDead = true;
            save.SessionDeaths++;

#if !DISABLESTEAMWORKS
            if (SteamManager.Initialized)
            {
                Steamworks.SteamUserStats.SetAchievement("Death_1");
                Steamworks.SteamUserStats.StoreStats();
                Steamworks.SteamAPI.RunCallbacks();
            }
#endif

            SetInputEnabled(InputState.Disabled);
            SetKinematic(true);
            SetCollisionEnabled(false);

            LevelController.instance.PlayerDeath();

            if (!silent)
            {
                DeathRumble();

                EventPlayerDeath.Raise();
                onDeath.Invoke();
            }

            StopMovement();
            colliderSphereify = 0;
        }

        public void Crush()
        {
            if (!isDead)
                save.SessionDeathsCrush++;
            Kill(true);
        }

        [System.Serializable]
        public enum InputState
        {
            Disabled = 0,
            Enabled = 1,
            Cutscene = 2,
            MenuOnly = 3,
        }
        public void SetInputEnabled(int stateId)
        {
            SetInputEnabled((InputState)stateId);
        }
        public void SetInputEnabled(InputState state)
        {
            if (input == null)
                return;

            if (inputState != state)
                ResetInputValues();

            inputState = state;

            if (state == InputState.Enabled)
            {
                input.Player.Enable();
                input.Player.Jump.Enable();
                input.Player.Reset.Enable();
                input.Player.MovementHorizontal.Enable();
                input.Player.MovementVertical.Enable();
                input.Player.Menu.Enable();
                input.Player.MenuBack.Enable();
                input.Player.MapScreen.Enable();
            }
            else if (state == InputState.Disabled)
            {
                input.Player.Disable();
                input.Player.Jump.Disable();
                input.Player.Reset.Disable();
                input.Player.MovementHorizontal.Disable();
                input.Player.MovementVertical.Disable();
                input.Player.Menu.Disable();
                input.Player.MenuBack.Disable();
                input.Player.MapScreen.Disable();
            }
            else if (state == InputState.Cutscene || state == InputState.MenuOnly)
            {
                input.Player.Enable();
                input.Player.Jump.Disable();
                input.Player.Reset.Disable();
                input.Player.MovementHorizontal.Disable();
                input.Player.MovementVertical.Disable();
                input.Player.Menu.Enable();
                input.Player.MenuBack.Enable();
                input.Player.MapScreen.Disable();
            }
        }
        public void SetKinematic(bool kinematic)
        {
            if (!Rigidbody)
                return;

            Rigidbody.collisionDetectionMode = kinematic ? CollisionDetectionMode2D.Discrete : CollisionDetectionMode2D.Continuous;
            Rigidbody.isKinematic = kinematic;
        }
        public void StopMovement()
        {
            Rigidbody.velocity = Vector3.zero;
            Rigidbody.angularVelocity = 0;
        }
        public void SetCollisionEnabled(bool enabled)
        {
            collisionEnabled = enabled;

            boxCollider.enabled = collisionEnabled;
            circleCollider.enabled = collisionEnabled;

            Rigidbody.simulated = collisionEnabled;

            EventPlayerCollisionSet.Raise(enabled);
        }
        public void OnSpeedRail()
        {
            speedRailTimer.Start(0.05f);
        }

        public void StartExitThroughGoal(Finish goal)
        {
            if (goal == null)
                return;

            LevelExit = goal;
            OnExitingLevel.Invoke();
        }

        public Quaternion GetNextStableRotation()
        {
            return Quaternion.Euler(0, 0, Mathf.Round(transform.eulerAngles.z / 90) * 90);
        }
        public Quaternion GetNextStableRotation(float offset)
        {
            return Quaternion.Euler(0, 0, (Mathf.Round(transform.eulerAngles.z / 90) + offset) * 90);
        }

        public void LimitAirMovement()
        {
            limitAirMovement = true;
        }
        public void OutsideForce()
        {
            outsideForce = true;
        }
        public void AllowJump(bool allow = true)
        {
            allowJump = allow;
        }
        public void DisallowJump(bool disallow = true)
        {
            disallowJump = disallow;
        }
        public void DisallowJump(float time)
        {
            disallowJumpTimer.Start(time);
        }
        public void UseJump()
        {
            useJump = true;
        }
        public void AllowOveredge(bool allow = true)
        {
            allowOveredge = allow;
        }
        public void PurgeInput(float time)
        {
            if (!purgeInputTimer.isRunning || purgeInputTimer.expirationTime - purgeInputTimer.time < time)
                purgeInputTimer.Start(time);
        }
        public void ResetJump()
        {
            hasGroundJumped = false;
            hasWallJumped = false;
            //jumpTimer.Clear();
            //wallJumpTimer.Clear();
        }


        #region Rumble

        public void DeathRumble()
        {
            RumbleManager.instance.AddRumble(new StaticRumble(0.3f, 1));
        }


        #endregion


        #region ElectricWire

        private ElectricWire currentWire;
        private Timer wireCoyoteTime = new Timer(UpdateCycle.FixedUpdate);
        private Timer wireJumpTimer = new Timer(UpdateCycle.FixedUpdate);

        public void RideWire(ElectricWire wire)
        {
            if (wire != null)
            {
                currentWire = wire;
                wireJumpTimer.Clear();
            }
            else
            {
                wireCoyoteTime.Start(0.125f);
            }

            ResetJump();
        }

        private void CleanupWire()
        {
            currentWire = null;
        }

        public void WireJump(Vector2 dir)
        {
            // This causes the player to not gain control over his trajectory until a few frames
            // this makes for better feeling wire jumps
            hasGroundJumped = false;
            wallJumpTimer.Clear();
            hasWallJumped = true;
            jumpTimer.Clear();
            wireJumpTimer.Clear();
            wireJumpTimer.Start(1);
        }

        #endregion
    }
}