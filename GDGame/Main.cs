using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Security.AccessControl;
using GDEngine.Core;
using GDEngine.Core.Audio;
using GDEngine.Core.Collections;
using GDEngine.Core.Components;
using GDEngine.Core.Components.Controllers.Physics;
using GDEngine.Core.Debug;
using GDEngine.Core.Entities;
using GDEngine.Core.Events;
using GDEngine.Core.Factories;
using GDEngine.Core.Gameplay;
using GDEngine.Core.Impulses;
using GDEngine.Core.Input.Data;
using GDEngine.Core.Input.Devices;
using GDEngine.Core.Managers;
using GDEngine.Core.Orchestration;
using GDEngine.Core.Rendering;
using GDEngine.Core.Rendering.Base;
using GDEngine.Core.Rendering.UI;
using GDEngine.Core.Screen;
using GDEngine.Core.Serialization;
using GDEngine.Core.Services;
using GDEngine.Core.Systems;
using GDEngine.Core.Timing;
using GDEngine.Core.Utilities;
using GDGame.Demos.Components;
using GDGame.Demos.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SharpDX.Direct2D1.Effects;
using Color = Microsoft.Xna.Framework.Color;

namespace GDGame
{
    public class Main : Game
    {
        #region Core Fields (Common to all games)     
        private GraphicsDeviceManager _graphics;
        private ContentDictionary<Texture2D> _textureDictionary;
        private ContentDictionary<Model> _modelDictionary;
        private ContentDictionary<SpriteFont> _fontDictionary;
        private ContentDictionary<SoundEffect> _soundDictionary;
        private ContentDictionary<Effect> _effectsDictionary;
        private bool _disposed = false;
        private Material _matBasicUnlit, _matBasicLit, _matAlphaCutout, _matBasicUnlitGround;
        private PBRMaterial _matPBR;
        #endregion

        #region Game Fileds
        private AnimationCurve3D _animationPositionCurve, _animationRotationCurve;
        private AnimationCurve _animationCurve;
        private KeyboardState _newKBState, _oldKBState;
        private int _damageAmount;
        private int _insight = 0;
        private List<GameObject> insightItems = [];


        // Simple debug subscription for collision events
        private IDisposable _collisionSubscription;

        // LayerMask used to filter which collisions we care about in debug
        private LayerMask _collisionDebugMask = LayerMask.All;
        private SceneManager _sceneManager;
        private float _currentHealth = 100;
        private MenuManager _menuManager;
        private UIDebugInfo _debugRenderer;
        private MouseState _newMouseState;
        private MouseState _oldMouseState;

        private GameObject _dialogueGO;
        private UIText _textDialogue;
        private float _musicVolume = 0.01f;
        private string _currentMusic = "confused music";
        private float _sfxVolume = 0.7f;
        private bool _isExamining = false;
        private string _oldExamineName;
        private Vector3 _oldExaminePos;
        private Quaternion _oldExamineRot;
        private GameObject _uiReticleGO;

        #endregion

        #region Core Methods (Common to all games)     
        public Main()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            #region Core
            Window.Title = "My Amazing Game";
            InitializeGraphics(ScreenResolution.R_WXGA_16_10_1280x800);
            InitializeMouse();
            InitializeContext();

            var relativeFilePathAndName = "assets/data/asset_manifest.json";
            LoadAssetsFromJSON(relativeFilePathAndName);
            InitializeEffects();

            // Game component that exists outside scene to manage and swap scenes
            InitializeSceneManager();

            // Create the scene and register it
            InitializeScene();

            // Safe to use _sceneManager.ActiveScene from here on
            InitializeSystems();
            InitializeCameras();
            InitializeCameraManagers();

            int scale = 150;
            InitializeSkyParent();
            InitializeSkyBox(scale);
            InitializeCollidableGround(scale);
            //InitializePlayer();
            #endregion

            #region Demos

            #region Animation curves
            // Camera-demos
            InitializeAnimationCurves();
            #endregion

            #region Collidables
            // Demo event listeners on collision
            InitializeCollisionEventListener();

            // Collidable game object demos
            //DemoCollidablePrimitive(new Vector3(0, 20, 5.1f), Vector3.One * 6, new Vector3(15, 45, 45));
            //DemoCollidablePrimitive(new Vector3(0, 10, 5.2f), Vector3.One * 1, new Vector3(45, 0, 0));
            //DemoCollidablePrimitive(new Vector3(0, 5, 5.3f), Vector3.One * 1, new Vector3(0, 0, 45));
            //DemoCollidableModel(new Vector3(0, 50, 10), Vector3.Zero, new Vector3(2, 1.25f, 2));
            //DemoCollidableModel(new Vector3(0, 40, 11), Vector3.Zero, new Vector3(2, 1.25f, 2));
            //DemoCollidableModel(new Vector3(0, 25, 12), Vector3.Zero, new Vector3(2, 1.25f, 2));

            #endregion

            #region Insight items
            AddNote();
            AddClothes();
            AddPolaroids();
            
            #endregion

            #region Loading GameObjects from JSON
            DemoLoadFromJSON();
            #endregion

            #region Sequencing using Orchestration
            DemoOrchestrationSystem();
            #endregion

            #region PBR Lighting
            //DemoPBRGameObject(string objectName, string modelName, Vector3 position, Vector3 scale, Vector3 eulerRotationDegrees,
            //Texture2D albedoTexture, Texture2D normalTexture, Texture2D srmTexture,
            //Color albedoColor, float roughness, float metallic);
            #endregion

            #endregion

            // Mouse reticle
            InitializeUI();

            // Main menu
            InitializeMenuManager();
    
            // Set win/lose conditions
            SetWinConditions();

            // Set pause and show menu
            SetPauseShowMenu();

            // Set the active scene
            _sceneManager.SetActiveScene(AppData.LEVEL_1_NAME);

            LoadEmitters();

            //todelet
            NewDialogue();
            var events = EngineContext.Instance.Events;
            events.Publish(new PlayMusicEvent("confused music", _musicVolume, 8));
            base.Initialize();
        }

        private void LoadEmitters()
        {
            GameObject birdEmitter = new GameObject("Bird Emitter");
            var soundEmitter = birdEmitter.AddComponent<SoundEmitter>();
            soundEmitter.Sound = "birds";
            soundEmitter.Min = 170;
            soundEmitter.Max = 200;

            birdEmitter.Transform.TranslateTo(new Vector3(-300, 2, 5));

            _sceneManager.ActiveScene.Add(birdEmitter);

            GameObject carEmitter = new GameObject("Car Emitter");
            var carSoundEmitter = carEmitter.AddComponent<SoundEmitter>();
            carSoundEmitter.Sound = "cars honk";
            carSoundEmitter.Min = 20;
            carSoundEmitter.Max = 60;

            carEmitter.Transform.TranslateTo(new Vector3(300, 2, 10));
            _sceneManager.ActiveScene.Add(carEmitter);
        }

        private void SetPauseShowMenu()
        {
            // Give scenemanager the events reference so that it can publish the pause event
            _sceneManager.EventBus = EngineContext.Instance.Events;
            // Set paused and publish pause event
            _sceneManager.Paused = true;

            // Put all components that should be paused to sleep
            EngineContext.Instance.Events.Subscribe<GamePauseChangedEvent>(e =>
            {
                bool paused = e.IsPaused;

                _sceneManager.ActiveScene.GetSystem<PhysicsSystem>()?.SetPaused(paused);
                _sceneManager.ActiveScene.GetSystem<PhysicsDebugSystem>()?.SetPaused(paused);
                _sceneManager.ActiveScene.GetSystem<GameStateSystem>()?.SetPaused(paused);
            });
        }

        private void InitializeSceneManager()
        {
            _sceneManager = new SceneManager(this);
            Components.Add(_sceneManager);
        }

        private void InitializeCameraManagers()
        {
            //inside scene
            var go = new GameObject("Camera Manager");
            go.AddComponent<CameraEventListener>();
            _sceneManager.ActiveScene.Add(go);
        }

        private void InitializeMenuManager()
        {
            _menuManager = new MenuManager(this, _sceneManager);
            Components.Add(_menuManager);

            Texture2D btnTex = _textureDictionary.Get("button_rectangle_10");
            Texture2D trackTex = _textureDictionary.Get("Free Flat Hyphen Icon");
            Texture2D handleTex = _textureDictionary.Get("Free Flat Toggle Thumb Centre Icon");
            Texture2D controlsTx = _textureDictionary.Get("mona lisa");
            SpriteFont uiFont = _fontDictionary.Get("menufont");

            // Wire UIManager to the menu scene
            _menuManager.Initialize(_sceneManager.ActiveScene, 
                btnTex, trackTex, handleTex, uiFont,
                _textureDictionary.Get("mainmenu_monkey"),
                 _textureDictionary.Get("audiomenu_monkey"),
                  _textureDictionary.Get("controlsmenu_monkey"));

            // Subscribe to high-level events
            _menuManager.PlayRequested += () =>
            {
                _sceneManager.Paused = false;
                _menuManager.HideMenus();
            };

            _menuManager.ExitRequested += () =>
            {
                Exit();
            };

            _menuManager.MusicVolumeChanged += v =>
            {
                _musicVolume = v/100;

                EngineContext.Instance.Events.Publish(new StopMusicEvent(0));
                EngineContext.Instance.Events.Publish(new PlayMusicEvent(_currentMusic, _musicVolume, 5));
            };

            _menuManager.SfxVolumeChanged += v =>
            {
                _sfxVolume = v / 10;

                EngineContext.Instance.Events.Publish(new StopAllSfxEvent());
            };

   
        }

        private void InitializeCollidableGround(int scale = 500)
        {
            GameObject gameObject = null;
            MeshFilter meshFilter = null;
            MeshRenderer meshRenderer = null;

            gameObject = new GameObject("ground");
            //meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            //meshFilter = MeshFilterFactory.CreateQuadGridTexturedUnlit(_graphics.GraphicsDevice,
            //     1,
            //     1,
            //     1,
            //     1,
            //     20,
            //     20);

            gameObject.Transform.ScaleBy(new Vector3(scale, scale, 1));
            gameObject.Transform.RotateEulerBy(new Vector3(MathHelper.ToRadians(-90), 0, 0), true);
            gameObject.Transform.TranslateTo(new Vector3(0, -0.5f, 0));

            //gameObject.AddComponent(meshFilter);
            //meshRenderer = gameObject.AddComponent<MeshRenderer>();
            //meshRenderer.Material = _matBasicUnlitGround;
            //meshRenderer.Overrides.MainTexture = _textureDictionary.Get("ground_grass");

            // Add a box collider matching the ground size
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.Size = new Vector3(scale, scale, 0.025f);
            collider.Center = new Vector3(0, 0, -0.0125f);

            // Add rigidbody as Static (immovable)
            var rigidBody = gameObject.AddComponent<RigidBody>();
            rigidBody.BodyType = BodyType.Static;
            gameObject.IsStatic = true;

            gameObject.Layer = LayerMask.Ground;

            _sceneManager.ActiveScene.Add(gameObject);
        }

        private void InitializePlayer()
        {
            GameObject player = InitializeModel(new Vector3(0, 5, 10),
                new Vector3(0, 0, 0),
                2 * Vector3.One, "crate1", "monkey1", AppData.PLAYER_NAME);

            var simpleDriveController = new SimpleDriveController();
            player.AddComponent(simpleDriveController);

            // Listen for damage events on the player
            player.AddComponent<DamageEventListener>();

            // Adds an inventory to the player
            player.AddComponent<InventoryComponent>();
        }

        private void InitializePIPCamera(Vector3 position,
      Viewport viewport, int depth, int index = 0)
        {
            var pipCameraGO = new GameObject("PIP camera");
            pipCameraGO.Transform.TranslateTo(position);
            pipCameraGO.Transform.RotateEulerBy(new Vector3(0, MathHelper.ToRadians(-90), 0));

            //if (index == 0)
            //{
            //    pipCameraGO.AddComponent<KeyboardWASDController>();
            //    pipCameraGO.AddComponent<MouseYawPitchController>();
            //}

            var camera = pipCameraGO.AddComponent<Camera>();
            camera.StackRole = Camera.StackType.Overlay;
            camera.ClearFlags = Camera.ClearFlagsType.DepthOnly;
            camera.Depth = depth; //-100

            camera.Viewport = viewport; // new Viewport(0, 0, 400, 300);

            _sceneManager.ActiveScene.Add(pipCameraGO);
        }

        private void InitializeAnimationCurves()
        {
            //1D animation curve demo (e.g. scale, audio volume, lerp factor for color, etc)
            _animationCurve = new AnimationCurve(CurveLoopType.Cycle);
            _animationCurve.AddKey(0f, 10);
            _animationCurve.AddKey(2f, 11); //up
            _animationCurve.AddKey(0f, 12); //down
            _animationCurve.AddKey(8f, 13); //up further
            _animationCurve.AddKey(0f, 13.5f); //down

            //3D animation curve demo
            _animationPositionCurve = new AnimationCurve3D(CurveLoopType.Oscillate);
            _animationPositionCurve.AddKey(new Vector3(0, 4, 0), 0);
            _animationPositionCurve.AddKey(new Vector3(5, 8, 2), 1);
            _animationPositionCurve.AddKey(new Vector3(10, 12, 4), 2);
            _animationPositionCurve.AddKey(new Vector3(0, 4, 0), 3);

            // Absolute yaw/pitch/roll angles (radians) over time
            _animationRotationCurve = new AnimationCurve3D(CurveLoopType.Oscillate);
            _animationRotationCurve.AddKey(new Vector3(0, 0, 0), 0);              // yaw, pitch, roll
            _animationRotationCurve.AddKey(new Vector3(0, MathHelper.PiOver2, 0), 1);
            _animationRotationCurve.AddKey(new Vector3(0, MathHelper.Pi, 0), 2);
            _animationRotationCurve.AddKey(new Vector3(0, 0, 0), 3);
        }

        private void InitializeGraphics(Integer2 resolution)
        {
            // Enable per-monitor DPI awareness so the window/UI scales crisply on multi-monitor setups with different DPIs (avoids blurriness when moving between screens).
            System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);

            // Set preferred resolution
            ScreenResolution.SetResolution(_graphics, resolution);

            // Center on primary display (set to index of the preferred monitor)
            WindowUtility.CenterOnMonitor(this, 1);
        }

        private void InitializeMouse()
        {
            Mouse.SetPosition(_graphics.PreferredBackBufferWidth / 2, _graphics.PreferredBackBufferHeight / 2);

            // Set old state at start so its not null for comparison with new state in Update
            _oldKBState = Keyboard.GetState();
        }

        private void InitializeContext()
        {
            EngineContext.Initialize(GraphicsDevice, Content);
        }

        /// <summary>
        /// New asset loading from JSON using AssetEntry and ContentDictionary::LoadFromManifest
        /// </summary>
        /// <param name="relativeFilePathAndName"></param>
        /// <see cref="AssetEntry"/>
        /// <see cref="ContentDictionary{T}"/>
        private void LoadAssetsFromJSON(string relativeFilePathAndName)
        {
            // Make dictionaries to store assets
            _textureDictionary = new ContentDictionary<Texture2D>();
            _modelDictionary = new ContentDictionary<Model>();
            _fontDictionary = new ContentDictionary<SpriteFont>();
            _soundDictionary = new ContentDictionary<SoundEffect>();
            _effectsDictionary = new ContentDictionary<Effect>();
            //TODO - Add dictionary loading for other assets - song, other?

            var manifests = JSONSerializationUtility.LoadData<AssetManifest>(Content, relativeFilePathAndName); // single or array
            if (manifests.Count > 0)
            {
                foreach (var m in manifests)
                {
                    _modelDictionary.LoadFromManifest(m.Models, e => e.Name, e => e.ContentPath, overwrite: true);
                    _textureDictionary.LoadFromManifest(m.Textures, e => e.Name, e => e.ContentPath, overwrite: true);
                    _fontDictionary.LoadFromManifest(m.Fonts, e => e.Name, e => e.ContentPath, overwrite: true);
                    _soundDictionary.LoadFromManifest(m.Sounds, e => e.Name, e => e.ContentPath, overwrite: true);
                    _effectsDictionary.LoadFromManifest(m.Effects, e => e.Name, e => e.ContentPath, overwrite: true);
                    //TODO - Add dictionary loading for other assets - song, other?
                }
            }
        }

        private void InitializeEffects()
        {
            #region Unlit Textured BasicEffect 
            var unlitBasicEffect = new BasicEffect(_graphics.GraphicsDevice)
            {
                TextureEnabled = true,
                LightingEnabled = false,
                VertexColorEnabled = false
            };
   
            _matBasicUnlit = new Material(unlitBasicEffect);
            _matBasicUnlit.StateBlock = RenderStates.Opaque3D();      // depth on, cull CCW
            _matBasicUnlit.SamplerState = SamplerState.LinearClamp;   // helps avoid texture seams on sky

            //ground texture where UVs above [0,0]-[1,1]
            _matBasicUnlitGround = new Material(unlitBasicEffect.Clone());
            _matBasicUnlitGround.StateBlock = RenderStates.Opaque3D();      // depth on, cull CCW
            _matBasicUnlitGround.SamplerState = SamplerState.AnisotropicWrap;   // wrap texture based on UV values

            #endregion

            #region Lit Textured BasicEffect 
            var litBasicEffect = new BasicEffect(_graphics.GraphicsDevice)
            {
                TextureEnabled = true,
                LightingEnabled = true,
                PreferPerPixelLighting = true,
                VertexColorEnabled = false
            };
            litBasicEffect.EnableDefaultLighting();
            //litBasicEffect.AmbientLightColor = Color.Red.ToVector3();
            //litBasicEffect.EmissiveColor = Color.Green.ToVector3();
            //litBasicEffect.FogEnabled = true;
            //litBasicEffect.FogColor = Color.LightGray.ToVector3();
            //litBasicEffect.FogStart = 1;
            //litBasicEffect.FogEnd = 100;
            //litBasicEffect.SpecularPower = 8;  //int, power of 2, 1, 2, 4, 8
            //litBasicEffect.SpecularColor = Color.Yellow.ToVector3();
            _matBasicLit = new Material(litBasicEffect);  
            _matBasicLit.StateBlock = RenderStates.Opaque3D();

            #endregion

            #region Alpha-test for foliage/billboards
            var alphaFx = new AlphaTestEffect(GraphicsDevice)
            {
                VertexColorEnabled = false
            };
            _matAlphaCutout = new Material(alphaFx);

            // Depth test/write on; no blending (cutout happens in the effect). 
            // Make it two-sided so the quad is visible from both sides.
            _matAlphaCutout.StateBlock = RenderStates.Cutout3D()
                .WithRaster(new RasterizerState { CullMode = CullMode.None });

            // Clamp avoids edge bleeding from transparent borders.
            // (Use LinearWrap if the foliage textures tile.)
            _matAlphaCutout.SamplerState = SamplerState.LinearClamp;

            #endregion

            //#region Lit PBR Effect
            //// Load effect file
            //Effect pbrEffect = _effectsDictionary.Get("pbr_effect");

            //// Create a PBR material
            //_matPBR = new PBRMaterial(pbrEffect, ownsEffect: false);
            //#endregion
        }

        private void InitializeScene()
        {
            // Make a scene that will store all drawn objects and systems for that level
            var scene = new Scene(EngineContext.Instance, "The Room");

            // Add each new scene into the manager
            _sceneManager.AddScene(AppData.LEVEL_1_NAME, scene);

            // Set the active scene before anything that uses ActiveScene
            _sceneManager.SetActiveScene(AppData.LEVEL_1_NAME);
        }

        private void InitializeSystems()
        {
            InitializePhysicsSystem();
            InitializePhysicsDebugSystem(true);
            InitializeEventSystem();  //propagate events  
            InitializeInputSystem();  //input
            InitializeCameraAndRenderSystems(); //update cameras, draw renderable game objects, draw ui and menu
            InitializeAudioSystem();
            InitializeOrchestrationSystem(false); //show debugger
            InitializeImpulseSystem();    //camera shake, audio duck volumes etc
            InitializeUIEventSystem();
            InitializeGameStateSystem();   //manage and track game state
                                           //  InitializeNavMeshSystem();

            InitializeDebugInfo(true);
        }

        private void InitializeDebugInfo(bool showDebug)
        {
            if (showDebug)
            {
                GameObject debugGO = new GameObject("Perf Stats");
                _debugRenderer = debugGO.AddComponent<UIDebugInfo>();

                _debugRenderer.Font = _fontDictionary.Get("perf_stats_font");
                _debugRenderer.ScreenCorner = ScreenCorner.TopLeft;
                _debugRenderer.Margin = new Vector2(10f, 10f);

                var perfProvider = new PerformanceDebugInfoProvider
                {
                    Profile = DisplayProfile.Profiling,
                    ShowMemoryStats = true
                };

                //add memory related info
                _debugRenderer.Providers.Add(perfProvider);

                //add scene related info
                _debugRenderer.Providers.Add(_sceneManager);

                _sceneManager.ActiveScene.Add(debugGO);
            }
        }

        private void InitializeNavMeshSystem()
        {
            var scene = _sceneManager.ActiveScene;

            // Core navmesh system (implements INavigationService)
            var navMeshSystem = scene.AddSystem(new NavMeshSystem());

            // Debug overlay (F2 toggle)
            scene.Add(new NavMeshDebugSystem());
        }

        private void InitializeGameStateSystem()
        {
            // Add game state system
            _sceneManager.ActiveScene.AddSystem(new GameStateSystem());
        }

        private void InitializeUIEventSystem()
        {
            _sceneManager.ActiveScene.AddSystem(new UIEventSystem());
        }

        private void InitializeImpulseSystem()
        {
            _sceneManager.ActiveScene.Add(new ImpulseSystem(EngineContext.Instance.Impulses));
        }

        private void InitializeOrchestrationSystem(bool debugEnabled)
        {
            var orchestrationSystem = new OrchestrationSystem();
            orchestrationSystem.Configure(options =>
            {
                options.Time = Orchestrator.OrchestrationTime.Unscaled;
                options.LocalScale = 1;
                options.Paused = false;
            });
            _sceneManager.ActiveScene.Add(orchestrationSystem);

            // Debugger
            if (debugEnabled)
            {
                GameObject debugGO = new GameObject("Perf Stats");
                var _debugRenderer = debugGO.AddComponent<UIDebugInfo>();

                _debugRenderer.Font = _fontDictionary.Get("perf_stats_font");
                _debugRenderer.ScreenCorner = ScreenCorner.TopLeft;
                _debugRenderer.Margin = new Vector2(10f, 10f);

                // Register orchestration as a debug provider
                if (orchestrationSystem != null)
                    _debugRenderer.Providers.Add(orchestrationSystem);

                var perfProvider = new PerformanceDebugInfoProvider
                {
                    Profile = DisplayProfile.Profiling,
                    ShowMemoryStats = true
                };

                _debugRenderer.Providers.Add(perfProvider);

                _sceneManager.ActiveScene.Add(debugGO);
            }

        }

        private void InitializeAudioSystem()
        {
            _sceneManager.ActiveScene.Add(new AudioSystem(_soundDictionary));
        }

        private void InitializePhysicsDebugSystem(bool isEnabled)
        {
            if (isEnabled)
            {
                var physicsDebugRenderer = _sceneManager.ActiveScene.AddSystem(new PhysicsDebugSystem());

                // Toggle debug rendering on/off
                physicsDebugRenderer.Enabled = isEnabled; // or false to hide

                // Optional: Customize colors
                physicsDebugRenderer.StaticColor = Color.Green;      // Immovable objects
                physicsDebugRenderer.KinematicColor = Color.Blue;    // Animated objects
                physicsDebugRenderer.DynamicColor = Color.Yellow;    // Physics-driven objects
                physicsDebugRenderer.TriggerColor = Color.Red;       // Trigger volumes

            }

        }

        private void InitializePhysicsSystem()
        {
            // 1. add physics
            var physicsSystem = _sceneManager.ActiveScene.AddSystem(new PhysicsSystem());
            physicsSystem.Gravity = AppData.GRAVITY;
        }

        private void InitializeEventSystem()
        {
            _sceneManager.ActiveScene.Add(new EventSystem(EngineContext.Instance.Events));
        }

        private void InitializeCameraAndRenderSystems()
        {
            //manages camera
            var cameraSystem = new CameraSystem(_graphics.GraphicsDevice, -100);
            _sceneManager.ActiveScene.Add(cameraSystem);

            //3d
            var renderSystem = new RenderSystem(-100);
            _sceneManager.ActiveScene.Add(renderSystem);

            //2d
            var uiRenderSystem = new UIRenderSystem(-100);
            _sceneManager.ActiveScene.Add(uiRenderSystem); // draws in PostRender after RenderingSystem (order = -100)
        }

        private void InitializeInputSystem()
        {
            //set mouse, keyboard binding keys (e.g. WASD)
            var bindings = InputBindings.Default;
            // optional tuning
            bindings.MouseSensitivity = 0.12f;  // mouse look scale
            bindings.DebounceMs = 60;           // key/mouse debounce in ms
            bindings.EnableKeyRepeat = true;    // hold-to-repeat
            bindings.KeyRepeatMs = 300;         // repeat rate in ms

            // Create the input system 
            var inputSystem = new InputSystem();

            // Register all the devices, you don't have to, but its for the demo
            inputSystem.Add(new GDKeyboardInput(bindings));
            inputSystem.Add(new GDMouseInput(bindings));
            inputSystem.Add(new GDGamepadInput(PlayerIndex.One, "Gamepad P1"));

            _sceneManager.ActiveScene.Add(inputSystem);
        }

        private void InitializeCameras()
        {
            Scene scene = _sceneManager.ActiveScene;

            GameObject cameraGO = null;
            Camera camera = null;
           
            #region Static birds-eye camera
            cameraGO = new GameObject(AppData.CAMERA_NAME_STATIC_BIRDS_EYE);
            camera = cameraGO.AddComponent<Camera>();
            camera.FieldOfView = MathHelper.ToRadians(80);
            //ISRoT
            cameraGO.Transform.RotateEulerBy(new Vector3(MathHelper.ToRadians(-90), 0, 0));
            cameraGO.Transform.TranslateTo(Vector3.UnitY * 50);
            scene.Add(cameraGO);
            #endregion

            #region Third-person camera
            cameraGO = new GameObject(AppData.CAMERA_NAME_THIRD_PERSON);
            camera = cameraGO.AddComponent<Camera>();

            var thirdPersonController = new ThirdPersonController();
            thirdPersonController.TargetName = AppData.PLAYER_NAME;
            thirdPersonController.ShoulderOffset = 0;
            thirdPersonController.FollowDistance = 50;
            thirdPersonController.RotationDamping = 20;
            cameraGO.AddComponent(thirdPersonController);
            scene.Add(cameraGO);
            #endregion

            #region simple camera
            cameraGO = new GameObject("simple camera");
            camera = cameraGO.AddComponent<Camera>();
            cameraGO.Transform.TranslateTo(new Vector3(0f, 4f, 0));

            cameraGO.AddComponent<SimpleDriveController>();
            cameraGO.AddComponent<MouseYawPitchController>();
            scene.Add(cameraGO);
            #endregion

            #region First-person capsule + camera (parent/child)

            // PARENT: physics + movement (feet at y = 0 here)
            var parentGO = new GameObject(AppData.CAMERA_NAME_FIRST_PERSON_PARENT);
            parentGO.Layer = LayerMask.IgnoreRaycast;
            parentGO.Transform.TranslateTo(new Vector3(0f, 10f, 15f));

            // Capsule + rigidbody controller (kept upright internally)
            var rigidBody = parentGO.AddComponent<RigidBody>();
            var collider = parentGO.AddComponent<BoxCollider>();
            //collider.Height = 2f;
            //collider.Radius = 1f;
            collider.Size = new Vector3(1.5f, 1, 1.5f);
            collider.Center = Vector3.Zero;
            var fpsController = parentGO.AddComponent<PhysicsWASDController>();
            fpsController.MoveSpeed = 5f;
            fpsController.Obj = parentGO;
            
            //fpsController.Acceleration = 50.0f;
            //fpsController.GroundFriction = 2.0f;
            //fpsController.JumpImpulse = 0f;
            //fpsController.CapsuleRadius = 2f;
            //fpsController.CapsuleHeight = 4f;
            //fpsController.GroundCheckDistance = 5f;

            // camera that can pitch + yaw without affecting the collider
            cameraGO = new GameObject(AppData.CAMERA_NAME_FIRST_PERSON);
            cameraGO.Transform.SetParent(parentGO.Transform);

            // Local offset from feet → eye height
            cameraGO.Transform.TranslateTo(new Vector3(0, 4, 0));
            camera = cameraGO.AddComponent<Camera>();
            camera.FieldOfView = MathHelper.ToRadians(80.0f);
            var mouseLook = cameraGO.AddComponent<MouseYawPitchController>();
   
            // Add both objects to the scene so their components are updated
            scene.Add(parentGO);
            scene.Add(cameraGO);

            // Make this the active camera
            scene.ActiveCamera = camera;
            #endregion

            #region Curve camera
            cameraGO = new GameObject(AppData.CAMERA_NAME_INTRO_CURVE);
            cameraGO.Transform.RotateEulerBy(new Vector3(MathHelper.ToRadians(-90), 0, 0));
            camera = cameraGO.AddComponent<Camera>();
            camera.FieldOfView = MathHelper.ToRadians(80);

            var curveController = cameraGO.AddComponent<CurveController>();
            curveController.PositionCurve = BuildCameraPositionCurve(CurveLoopType.Oscillate);
            curveController.TargetCurve = BuildCameraTargetCurve(CurveLoopType.Constant);
            curveController.Duration = 10;
            scene.Add(cameraGO);
            #endregion

            //replace with new SetActiveCamera that searches by string
            //scene.SetActiveCamera(AppData.CAMERA_NAME_FIRST_PERSON);
            scene.SetActiveCamera("simple camera");
        }

        private AnimationCurve3D BuildCameraPositionCurve(CurveLoopType curveLoopType)
        {
            var curve = new AnimationCurve3D(curveLoopType);

            // start
            curve.AddKey(new Vector3(-20, 10, 40), 0);

            // moving inward, slight rise
            curve.AddKey(new Vector3(-10, 10, 30), 0.25f);

            // closest to origin (single “turn”)
            curve.AddKey(new Vector3(0, 10, 30), 0.5f);

            // heading back out
            curve.AddKey(new Vector3(10, 10, 40), 0.75f);

            // end
            curve.AddKey(new Vector3(20, 10, 40), 1);

            return curve;
        }

        private AnimationCurve3D BuildCameraTargetCurve(CurveLoopType curveLoopType)
        {
            var curve = new AnimationCurve3D(curveLoopType);

            // All points “in or around” origin, y ≈ 5 so we look slightly down from y=10–12.
            curve.AddKey(new Vector3(-5,0,0), 0);
            curve.AddKey(new Vector3(5,0,0), 1);

            return curve;
        }


        /// <summary>
        /// Add parent root at origin to rotate the sky
        /// </summary>
        private void InitializeSkyParent()
        {
            var _skyParent = new GameObject("SkyParent");
            var rot = _skyParent.AddComponent<RotationController>();

            // Turntable spin around local +Y
            rot._rotationAxisNormalized = Vector3.Up;

            // Dramatised fast drift at 2 deg/sec. 
            rot._rotationSpeedInRadiansPerSecond = MathHelper.ToRadians(2f);
            _sceneManager.ActiveScene.Add(_skyParent);
        }

        private void InitializeSkyBox(int scale = 500)
        {
            Scene scene = _sceneManager.ActiveScene;
            GameObject gameObject = null;
            MeshFilter meshFilter = null;
            MeshRenderer meshRenderer = null;

            // Find the sky parent object to attach sky to so sky rotates
            GameObject skyParent = scene.Find((GameObject go) => go.Name.Equals("SkyParent"));

            // back
            gameObject = new GameObject("back");
            gameObject.Transform.ScaleTo(new Vector3(scale, scale, 1));
            gameObject.Transform.TranslateTo(new Vector3(0, 0, -scale / 2));
            meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            gameObject.AddComponent(meshFilter);
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicUnlit;
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("skybox_back");
            scene.Add(gameObject);

            //set parent to allow rotation
            gameObject.Transform.SetParent(skyParent.Transform);

            // left
            gameObject = new GameObject("left");
            gameObject.Transform.ScaleTo(new Vector3(scale, scale, 1));
            gameObject.Transform.RotateEulerBy(new Vector3(0, MathHelper.ToRadians(90), 0), true);
            gameObject.Transform.TranslateTo(new Vector3(-scale / 2, 0, 0));
            meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            gameObject.AddComponent(meshFilter);
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicUnlit;
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("skybox_left");
            scene.Add(gameObject);

            //set parent to allow rotation
            gameObject.Transform.SetParent(skyParent.Transform);


            // right
            gameObject = new GameObject("right");
            gameObject.Transform.ScaleTo(new Vector3(scale, scale, 1));
            gameObject.Transform.RotateEulerBy(new Vector3(0, MathHelper.ToRadians(-90), 0), true);
            gameObject.Transform.TranslateTo(new Vector3(scale / 2, 0, 0));
            meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            gameObject.AddComponent(meshFilter);
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicUnlit;
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("skybox_right");
            scene.Add(gameObject);

            //set parent to allow rotation
            gameObject.Transform.SetParent(skyParent.Transform);

            // front
            gameObject = new GameObject("front");
            gameObject.Transform.ScaleTo(new Vector3(scale, scale, 1));
            gameObject.Transform.RotateEulerBy(new Vector3(0, MathHelper.ToRadians(180), 0), true);
            gameObject.Transform.TranslateTo(new Vector3(0, 0, scale / 2));
            meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            gameObject.AddComponent(meshFilter);
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicUnlit;
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("skybox_front");
            scene.Add(gameObject);

            //set parent to allow rotation
            gameObject.Transform.SetParent(skyParent.Transform);

            // sky (top)
            gameObject = new GameObject("sky");
            gameObject.Transform.ScaleTo(new Vector3(scale, scale, 1));
            gameObject.Transform.RotateEulerBy(new Vector3(MathHelper.ToRadians(90), 0, MathHelper.ToRadians(90)), true);
            gameObject.Transform.TranslateTo(new Vector3(0, scale / 2, 0));
            meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            gameObject.AddComponent(meshFilter);
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicUnlit;
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("skybox_sky");
            scene.Add(gameObject);

            //set parent to allow rotation
            gameObject.Transform.SetParent(skyParent.Transform);

        }

        private void InitializeUI()
        {
            InitializeUIReticleRenderer();
        }

        private void InitializeUIReticleRenderer()
        {
            _uiReticleGO = new GameObject("HUD");

            var reticleAtlas = _textureDictionary.Get("crosshair");
            var uiFont = _fontDictionary.Get("mouse_reticle_font");

            // Reticle (cursor): always on top
            var reticle = new UIReticle(reticleAtlas);
            reticle.Origin = reticleAtlas.GetCenter();
            reticle.SourceRectangle = null;
            reticle.Scale = new Vector2(0.1f, 0.1f);
            reticle.RotationSpeedDegPerSec = 0;
            reticle.LayerDepth = UILayer.Cursor;
            _uiReticleGO.AddComponent(reticle);

            var textRenderer = _uiReticleGO.AddComponent<UIText>();
            textRenderer.Font = uiFont;
            textRenderer.Offset = new Vector2(100, 30);  // Position text below reticle
            textRenderer.Color = Color.White;
            textRenderer.PositionProvider = () => _graphics.GraphicsDevice.Viewport.GetCenter();
            textRenderer.Anchor = TextAnchor.Center;

            var picker = _uiReticleGO.AddComponent<UIPickerInfo>();
            picker.HitMask = LayerMask.All;
            picker.MaxDistance = 5f;
            picker.HitTriggers = false;

            // Optional custom formatting
            picker.Formatter = hit =>
            {
                var go = hit.Body?.GameObject;
                if (go == null)
                    return string.Empty;
                _newMouseState = Mouse.GetState();
                if (go.Name.Contains("photo"))
                {
                    ClickedItem(go);
                    _oldMouseState = _newMouseState;
                    return "A polaroid picture\n--\nRight Click to Examine\nLeft Click to Take";
                }
                if (go.Name.Contains("sock") || go.Name.Equals("shirt") || go.Name.Equals("pants"))
                {
                    ClickedItem(go);
                    _oldMouseState = _newMouseState;
                    return "My clothes piece\n--\nRight Click to Examine\nLeft Click to Take";
                }
                if (go.Name.Equals("note")) 
                {
                    ClickedItem(go);
                    _oldMouseState = _newMouseState;
                    return "My drunk note\n--\nRight Click to Examine\nLeft Click to Take";
                }
                if (go.Name.Contains("examine"))
                {
                    ClickedItem(go);
                    _oldMouseState = _newMouseState;
                }

                _oldMouseState = _newMouseState;
                return "";
            };

            _sceneManager.ActiveScene.Add(_uiReticleGO);

            // Hide mouse since reticle will take its place
            IsMouseVisible = false;
        }

        /// <summary>
        /// Toggles the the visibility of reticle UI element.
        /// </summary>
        private void SetReticleoVisible(bool state)
        {
            if (_uiReticleGO == null) return;

            foreach (var ui in _uiReticleGO.GetComponents<UIRenderer>())
                ui.Enabled = state;
        }

        private void NewDialogue()
        {
            _dialogueGO = new GameObject("dialogue");
            _textDialogue = _dialogueGO.AddComponent<UIText>();
            _textDialogue.Font = _fontDictionary.Get("menufont");
            _textDialogue.FallbackColor = new Color(72, 59, 32);
            _textDialogue.PositionProvider = () => new Vector2(_graphics.PreferredBackBufferWidth/2 - 200, _graphics.PreferredBackBufferHeight-100);
            _textDialogue.TextProvider = () => "";

            _sceneManager.ActiveScene.Add(_dialogueGO);
        }

        private void ClickedItem(GameObject go)
        {
            var events = EngineContext.Instance.Events;
            if (_newMouseState.LeftButton == ButtonState.Pressed && _oldMouseState.LeftButton == ButtonState.Released)
            {
                if (!_isExamining)
                {
                    if (go.Name.Equals("note"))
                    {
                        for (int i = 1; i < 5; i++)
                            insightItems[i].Enabled = true;

                        events.Publish(new StopMusicEvent(1));
                        events.Publish(new PlayMusicEvent("calm music", _musicVolume, 1));
                        _currentMusic = "calm music";
                        _insight += 4;
                    }


                    if (go.Name.Contains("photo") || go.Name.Contains("sock") || go.Name.Equals("pants") || go.Name.Equals("shirt") || go.Name.Equals("note"))
                    {
                        events.Publish(new PlaySfxEvent("collect", _sfxVolume, false));
                        if (!go.Name.Contains("photo"))
                        {
                            _insight++;
                            insightItems[_insight - 1].Enabled = true;
                        }
                            
                        go.Enabled = false;
                        go.Name = "collected";
                        //cant destroy
                        //go.Destroy();
                    }
                }
            }

            if(_newMouseState.RightButton == ButtonState.Pressed && _oldMouseState.RightButton == ButtonState.Released)
            {
                
                var plr = _sceneManager.ActiveScene.Find(go => go.Name.Equals("simple camera"));
                var look = plr.GetComponent<MouseYawPitchController>();
                var move = plr.GetComponent<SimpleDriveController>();
                if (!_isExamining)
                {
                    _isExamining = true;
                    
                    look.Enabled = false;
                    move.Enabled = false;

                    if (go.Name.Equals("note"))
                    {
                        _textDialogue.TextProvider = () => "Wow what a weird note";
                    }
                    else if (go.Name.Equals("sock1"))
                    {
                        _textDialogue.TextProvider = () => "My precious sock";
                    }
                    else if (go.Name.Equals("sock2"))
                    {
                        _textDialogue.TextProvider = () => "My less precious sock";
                    }
                    else if (go.Name.Equals("shirt"))
                    {
                        _textDialogue.TextProvider = () => "My green shirt still looks wearable";
                    }
                    else if (go.Name.Equals("pants"))
                    {
                        _textDialogue.TextProvider = () => "My blue jeans with a new tear";
                    }
                    else if (go.Name.Equals("photo1"))
                    {
                        _textDialogue.TextProvider = () => "Wow a very cool picture";
                    }
                    else if (go.Name.Equals("photo2"))
                    {
                        _textDialogue.TextProvider = () => "Wow I look pretty rough here";
                    }
                    else if (go.Name.Equals("photo3"))
                    {
                        _textDialogue.TextProvider = () => "Who.. is that behind me";
                    }
                    else if (go.Name.Equals("photo4"))
                    {
                        _textDialogue.TextProvider = () => "Were they following me?";
                    }

                    if (go.Name.Contains("photo") || go.Name.Contains("sock") || go.Name.Equals("pants") || go.Name.Equals("shirt") || go.Name.Equals("note"))
                    {
                        events.Publish(new PlaySfxEvent("examine", _sfxVolume, false));

                        _oldExamineName = go.Name;
                        _oldExaminePos = go.Transform.Position;
                        _oldExamineRot = go.Transform.Rotation;

                        
                        if (go.Name.Equals("pants") || go.Name.Equals("shirt"))
                        {
                            go.Transform.ScaleBy(Vector3.One * .5f);
                        }

                        PlaceAndFaceItem(go, plr);
                        go.Name = "examine" + go.Name; 
                        SetReticleoVisible(false);
                    }
                }
                else
                {
                    var item = _sceneManager.ActiveScene.Find(g => g.Name.Contains("examine"));
                    look.Enabled = true;
                    move.Enabled = true;
                    item.Transform.RotateToWorld(_oldExamineRot);
                    item.Transform.TranslateTo(_oldExaminePos);
                    item.Name = _oldExamineName;
                    if (go.Name.Equals("pants") || go.Name.Equals("shirt"))
                        go.Transform.ScaleBy(Vector3.One * 2f);

                    _textDialogue.TextProvider = () => "";
                    SetReticleoVisible(true);
                    _isExamining = false;
                }

            }
        }

        void PlaceAndFaceItem(GameObject item, GameObject cam)
        {
            // Position
            Vector3 forward = Vector3.Transform( Vector3.Forward, cam.Transform.Rotation ); 
            Vector3 targetPos = cam.Transform.Position + forward * 2f; 
            item.Transform.TranslateTo(targetPos); 
            // Rotation (face player)
            Vector3 dirToCamera = cam.Transform.Position - targetPos; 
            dirToCamera.Normalize(); 
            Quaternion faceCamera = Quaternion.CreateFromRotationMatrix( Matrix.CreateWorld( Vector3.Zero, -dirToCamera, Vector3.Up ) ); 
            item.Transform.RotateToWorld(faceCamera);
        }

        /// <summary>
        /// Adds a single-part FBX model into the scene.
        /// </summary>
        private GameObject InitializeModel(Vector3 position,
            Vector3 eulerRotationDegrees, Vector3 scale,
            string textureName, string modelName, string objectName)
        {
            GameObject gameObject = null;

            gameObject = new GameObject(objectName);
            gameObject.Transform.TranslateTo(position);
            gameObject.Transform.RotateEulerBy(eulerRotationDegrees * MathHelper.Pi / 180f);
            gameObject.Transform.ScaleTo(scale);

          //  gameObject.Layer = LayerMask.NPC | LayerMask.Collectables;

            var model = _modelDictionary.Get(modelName);
            var texture = _textureDictionary.Get(textureName);
            var meshFilter = MeshFilterFactory.CreateFromModel(model, _graphics.GraphicsDevice, 0, 0);
            gameObject.AddComponent(meshFilter);

            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicLit;
            meshRenderer.Overrides.MainTexture = texture;

            _sceneManager.ActiveScene.Add(gameObject);

            return gameObject;
        }
        protected override void Update(GameTime gameTime)
        {
            //call time update
            #region Core
            Time.Update(gameTime);
            #endregion

            #region Demo
            DemoStuff();
            
            #endregion

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.CornflowerBlue);

            base.Draw(gameTime);
        }

        /// <summary>
        /// Override Dispose to clean up engine resources.
        /// MonoGame's Game class already implements IDisposable, so we override its Dispose method.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                base.Dispose(disposing);
                return;
            }

            if (disposing)
            {
                System.Diagnostics.Debug.WriteLine("Disposing Main...");

                // 1. Dispose Materials (which may own Effects)
                System.Diagnostics.Debug.WriteLine("Disposing Materials");
                _matBasicUnlit?.Dispose();
                _matBasicUnlit = null;

                _matBasicLit?.Dispose();
                _matBasicLit = null;

                _matAlphaCutout?.Dispose();
                _matAlphaCutout = null;

                // 2. Clear cached MeshFilters in factory registry
                System.Diagnostics.Debug.WriteLine("Clearing MeshFilter Registry");
                MeshFilterFactory.ClearRegistry();

                // 3. Dispose content dictionaries (now they implement IDisposable!)
                System.Diagnostics.Debug.WriteLine("Disposing Content Dictionaries");
                _textureDictionary?.Dispose();
                _textureDictionary = null;

                _modelDictionary?.Dispose();
                _modelDictionary = null;

                _fontDictionary?.Dispose();
                _fontDictionary = null;

                // 4. Dispose EngineContext (which owns SpriteBatch and Content)
                System.Diagnostics.Debug.WriteLine("Disposing EngineContext");
                EngineContext.Instance?.Dispose();

                // 5. Clear references to help GC
                System.Diagnostics.Debug.WriteLine("Clearing References");
                _animationCurve = null;
                _animationPositionCurve = null;
                _animationRotationCurve = null;

                // 6. Dispose of collision handlers
                if (_collisionSubscription != null)
                {
                    _collisionSubscription.Dispose();
                    _collisionSubscription = null;
                }

                System.Diagnostics.Debug.WriteLine("Main disposal complete");
            }

            _disposed = true;

            // Always call base.Dispose
            base.Dispose(disposing);
        }

        #endregion

        #region Demo - Game State
        private void SetWinConditions()
        {
            var gameStateSystem = _sceneManager.ActiveScene.GetSystem<GameStateSystem>();

            // Value providers (Strategy pattern via delegates)
            Func<float> healthProvider = () =>
            {
                //get the player and access the player's health/speed/other variable
                return _currentHealth;
            };

            // Delegate for time
            Func<float> timeProvider = () =>
            {
                return (float)Time.RealtimeSinceStartupSecs;
            };

            // Lose condition: health < 10 AND time > 60
            IGameCondition loseCondition =
                GameConditions.FromPredicate("all enemies visited", checkEnemiesVisited);

            IGameCondition winCondition =
            GameConditions.FromPredicate("reached gate", checkReachedGate);

            // Configure GameStateSystem (no win condition yet)
            gameStateSystem.ConfigureConditions(winCondition, loseCondition);
            gameStateSystem.StateChanged += HandleGameStateChange;
        }

        private bool checkReachedGate()
        {
            // we could pause the game on a win
            //Time.TimeScale = 0;
            return false;
        }

        private bool checkEnemiesVisited()
        {
            //get inventory and eval using boolean if all enemies visited;
            return false;
        }

        private void HandleGameStateChange(GameOutcomeState oldState, GameOutcomeState newState)
        {
            System.Diagnostics.Debug.WriteLine($"Old state was {oldState} and new state is {newState}");

            if (newState == GameOutcomeState.Lost)
            {
                System.Diagnostics.Debug.WriteLine("You lost!");
                //play sound
                //reset player
                //load next level
                //we decide what losing looks like here!
                //Exit();
            }
            else if (newState == GameOutcomeState.Won)
            {
                System.Diagnostics.Debug.WriteLine("You win!");
            }

        }
        #endregion
      
        private void DemoCollidableModel(Vector3 position, Vector3 eulerRotationDegrees, Vector3 scale)
        {
            var go = new GameObject("test");
            go.Transform.TranslateTo(position);
            go.Transform.RotateEulerBy(eulerRotationDegrees * MathHelper.Pi / 180f);
            go.Transform.ScaleTo(scale);

            go.Layer = LayerMask.Interactables;

            var model = _modelDictionary.Get("monkey1");
            var texture = _textureDictionary.Get("mona lisa");
            var meshFilter = MeshFilterFactory.CreateFromModel(model, _graphics.GraphicsDevice, 0, 0);
            go.AddComponent(meshFilter);

            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicLit;
            meshRenderer.Overrides.MainTexture = texture;
            _sceneManager.ActiveScene.Add(go);

            // Add box collider (1x1x1 cube)
            var collider = go.AddComponent<SphereCollider>();
            collider.Diameter = scale.Length();

            // Add rigidbody (Dynamic so it falls)
            var rigidBody = go.AddComponent<RigidBody>();
            rigidBody.BodyType = BodyType.Dynamic;
            rigidBody.Mass = 1.0f;
        }

        private void DemoStuff()
        {
            // Get new state
            _newKBState = Keyboard.GetState();
            DemoEventPublish();
            DemoCameraSwitch();
            DemoToggleFullscreen();
            DemoAudioSystem();
            DemoOrchestrationSystem();
            DemoImpulsePublish();
            //To allow object editing in scene
            //ObjectEditor("door");


            _currentHealth--;

            // Store old state (allows us to do was pressed type checks)
            _oldKBState = _newKBState;
        }

        private void ObjectEditor(String item)
        {
            GameObject go = _sceneManager.ActiveScene.Find(go => go.Name.Equals(item));

            bool isWPressed = _newKBState.IsKeyDown(Keys.W) && !_oldKBState.IsKeyDown(Keys.W);
            bool isSPressed = _newKBState.IsKeyDown(Keys.S) && !_oldKBState.IsKeyDown(Keys.S);
            bool isAPressed = _newKBState.IsKeyDown(Keys.A) && !_oldKBState.IsKeyDown(Keys.A);
            bool isDPressed = _newKBState.IsKeyDown(Keys.D) && !_oldKBState.IsKeyDown(Keys.D);
            bool isQPressed = _newKBState.IsKeyDown(Keys.Q) && !_oldKBState.IsKeyDown(Keys.Q);
            bool isEPressed = _newKBState.IsKeyDown(Keys.E) && !_oldKBState.IsKeyDown(Keys.E);
            bool isRPressed = _newKBState.IsKeyDown(Keys.R) && !_oldKBState.IsKeyDown(Keys.R);
            bool isFPressed = _newKBState.IsKeyDown(Keys.F) && !_oldKBState.IsKeyDown(Keys.F);
            bool isPPressed = _newKBState.IsKeyDown(Keys.P) && !_oldKBState.IsKeyDown(Keys.P);
            ////this is to rotate object and move the object 
            if (isWPressed || isSPressed)
            {
                go.Transform.TranslateBy(new Vector3((isWPressed == true ? 0.1f : -0.1f), 0, 0));
            }
            if (isAPressed || isDPressed)
            {
                go.Transform.TranslateBy(new Vector3(0, 0, (isAPressed == true ? 0.1f : -0.1f)));
            }
            if (isQPressed || isEPressed)
            {
                go.Transform.TranslateBy(new Vector3(0, (isQPressed == true ? 0.1f : -0.1f), 0));
            }

            if (isFPressed || isRPressed)
            {
                go.Transform.RotateEulerBy(new Vector3(0, 0, MathHelper.ToRadians(isFPressed == true ? 1 : -1f)));
            }

            if (isPPressed)
            {
                System.Diagnostics.Debug.WriteLine(go.Transform.LocalPosition.ToString());
                System.Diagnostics.Debug.WriteLine(go.Transform.LocalRotation.ToString());
            }
        }

        private void DemoImpulsePublish()
        {
            var impulses = EngineContext.Instance.Impulses;

            // a simple explosion reaction
            bool isZPressed = _newKBState.IsKeyDown(Keys.Z) && !_oldKBState.IsKeyDown(Keys.Z);
            if (isZPressed)
            {
                float duration = 0.35f;
                float amplitude = 0.6f;

                impulses.CreateContinuousSource(
                    (elapsed, totalDuration) =>
                    {
                        // Random 2D screen-space-ish direction
                        Vector3 dir = MathUtility.RandomShakeXY();

                        // Let Eased3DImpulse use its default easing (e.g. Ease.Linear)
                        return new Eased3DImpulse(
                            channel: "camera/impulse",
                            direction: dir,
                            amplitude: amplitude,
                            time: elapsed,
                            duration: totalDuration);
                    },
                    duration,
                    true);
            }

            // like a locked door try and fail
            bool isCPressed = _newKBState.IsKeyDown(Keys.X) && !_oldKBState.IsKeyDown(Keys.X);
            if (isCPressed)
            {
                float duration = 0.2f;
                float amplitude = 0.1f;

                impulses.CreateContinuousSource(
                    (elapsed, totalDuration) =>
                    {
                        float jitter = 0.05f;

                        // Small random left/right component
                        float z = (float)(Random.Shared.NextDouble() * 2.0 - 1.0) * jitter;

                        // Backward in world-space 
                        Vector3 dir = new Vector3(0, 0, z);

                        return new Eased3DImpulse(
                            channel: "camera/impulse",
                            direction: dir,
                            amplitude: amplitude,
                            time: elapsed,
                            duration: totalDuration,
                            ease: Ease.EaseOutQuad); // snappier than cubic, but still smooth
                    },
                    duration,
                    true);
            }
        }

        private void DemoOrchestrationSystem()
        {
            var orchestrator = _sceneManager.ActiveScene.GetSystem<OrchestrationSystem>().Orchestrator;

            //bool isPressed = _newKBState.IsKeyDown(Keys.O) && !_oldKBState.IsKeyDown(Keys.O);
            //if (isPressed)
            //{
            //    orchestrator.Build("my first sequence")
            //        .WaitSeconds(2)
            //        .Publish(new CameraEvent(AppData.CAMERA_NAME_FIRST_PERSON))
            //        .WaitSeconds(2)
            //        .Publish(new PlaySfxEvent("SFX_UI_Click_Designed_Pop_Generic_1", 1, false, null))
            //        .Register();

            //    orchestrator.Start("my first sequence", _sceneManager.ActiveScene, EngineContext.Instance);
            //}

            //bool isIPressed = _newKBState.IsKeyDown(Keys.I) && !_oldKBState.IsKeyDown(Keys.I);
            //if (isIPressed)
            //    orchestrator.Pause("my first sequence");

            //bool isPPressed = _newKBState.IsKeyDown(Keys.P) && !_oldKBState.IsKeyDown(Keys.P);
            //if (isPPressed)
            //    orchestrator.Resume("my first sequence");
        }

        private void DemoAudioSystem()
        {
            var events = EngineContext.Instance.Events;

            //TODO - Exercise
            bool isD3Pressed = _newKBState.IsKeyDown(Keys.D3) && !_oldKBState.IsKeyDown(Keys.D3);
            if (isD3Pressed)
            {
                //events.Publish(new PlaySfxEvent("SFX_UI_Click_Designed_Pop_Generic_1",1, false, null));
            }

            bool isD4Pressed = _newKBState.IsKeyDown(Keys.D4) && !_oldKBState.IsKeyDown(Keys.D4);
            if (isD4Pressed)
            {
                //events.Publish(new PlayMusicEvent("secret_door", 1, 8));
            }

            bool isD5Pressed = _newKBState.IsKeyDown(Keys.D5) && !_oldKBState.IsKeyDown(Keys.D5);
            if (isD5Pressed)
            {
                events.Publish(new StopMusicEvent(4));
            }

            bool isD6Pressed = _newKBState.IsKeyDown(Keys.D6) && !_oldKBState.IsKeyDown(Keys.D6);
            if (isD6Pressed)
            {
                events.Publish(new FadeChannelEvent(AudioMixer.AudioChannel.Master, 0.1f, 4));
            }

            bool isD7Pressed = _newKBState.IsKeyDown(Keys.D7) && !_oldKBState.IsKeyDown(Keys.D7);
            if (isD7Pressed)
            {
                //expensive and crude => move to Component::Start()
                var go = _sceneManager.ActiveScene.Find(go => go.Name.Equals(AppData.PLAYER_NAME));
                Transform emitterTransform = go.Transform;

                //events.Publish(new PlaySfxEvent("hand_gun1", 1, true, emitterTransform));
            }
        }

        private void DemoToggleFullscreen()
        {
            bool togglePressed = _newKBState.IsKeyDown(Keys.F5) && !_oldKBState.IsKeyDown(Keys.F5);
            if (togglePressed)
                _graphics.ToggleFullScreen();
        }

        private void DemoCameraSwitch()
        {
            var events = EngineContext.Instance.Events;

            bool isFirst = _newKBState.IsKeyDown(Keys.D1) && !_oldKBState.IsKeyDown(Keys.D1);
            if (isFirst)
            {
                events.Post(new CameraEvent(AppData.CAMERA_NAME_FIRST_PERSON));
                events.Publish(new PlaySfxEvent("SFX_UI_Click_Designed_Pop_Generic_1",1, false, null));
            }

            bool isThird = _newKBState.IsKeyDown(Keys.D2) && !_oldKBState.IsKeyDown(Keys.D2);
            if (isThird)
            {
                events.Post(new CameraEvent(AppData.CAMERA_NAME_THIRD_PERSON));
                events.Publish(new PlaySfxEvent("SFX_UI_Click_Designed_Pop_Mallet_Open_1", 1, false, null));
            }

            bool simple = _newKBState.IsKeyDown(Keys.D3) && !_oldKBState.IsKeyDown(Keys.D3);
            if (simple)
            {
                events.Post(new CameraEvent("simple camera"));
                events.Publish(new PlaySfxEvent("SFX_UI_Click_Designed_Pop_Mallet_Open_1",1, false, null));
            }
        }

        private void DemoEventPublish()
        {
            // F2: publish a test DamageEvent
            if (_newKBState.IsKeyDown(Keys.F6) && !_oldKBState.IsKeyDown(Keys.F6))
            {
                // Simple “debug” damage example
                var hitPos = new Vector3(0, 5, 0); //some fake position
                _damageAmount++;

                var damageEvent = new DamageEvent(_damageAmount, DamageEvent.DamageType.Strength,
                    "Plasma rifle", AppData.PLAYER_NAME, hitPos, false);

                EngineContext.Instance.Events.Post(damageEvent);
            }

            // Raise inventory event
            if (_newKBState.IsKeyDown(Keys.E) && !_oldKBState.IsKeyDown(Keys.E))
            {
                var inventoryEvent = new GDEngine.Core.Components.InventoryEvent();
                inventoryEvent.ItemType = ItemType.Weapon;
                inventoryEvent.Value = 10;
                EngineContext.Instance.Events.Publish(inventoryEvent);
            }

            //if (_newKBState.IsKeyDown(Keys.L) && !_oldKBState.IsKeyDown(Keys.L))
            //{
            //    var inventoryEvent = new GDEngine.Core.Components.InventoryEvent();
            //    inventoryEvent.ItemType = ItemType.Lore;
            //    inventoryEvent.Value = 0;
            //    EngineContext.Instance.Events.Publish(inventoryEvent);
            //}

            if (_newKBState.IsKeyDown(Keys.M) && !_oldKBState.IsKeyDown(Keys.M))
            {
                // EngineContext.Instance.Messages.Post(new PlayerDamageEvent(45, DamageType.Strength));
                //EngineContext.Instance.Messages.PublishImmediate(new PlayerDamageEvent(45, DamageType.Strength));
            }
        }

        private void DemoLoadFromJSON()
        {
            var relativeFilePathAndName = "assets/data/multi_model_spawn.json";
            //load multiple models
            foreach (var d in JSONSerializationUtility.LoadData<ModelSpawnData>(Content, relativeFilePathAndName))
                InitializeModel(d.Position, d.RotationDegrees, d.Scale, d.TextureName, d.ModelName, d.ObjectName);
        }

        private void DemoCollidablePrimitive(Vector3 position, Vector3 scale, Vector3 rotateDegrees)
        {
            GameObject gameObject = null;
            MeshFilter meshFilter = null;
            MeshRenderer meshRenderer = null;

            gameObject = new GameObject("test crate textured cube");
            gameObject.Transform.TranslateTo(position);
            gameObject.Transform.ScaleTo(scale * 0.5f);
            gameObject.Transform.RotateEulerBy(rotateDegrees * MathHelper.Pi / 180f);


            meshFilter = MeshFilterFactory.CreateCubeTexturedLit(_graphics.GraphicsDevice);
            gameObject.AddComponent(meshFilter);

            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicLit; //enable lighting for the crate
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("crate1");

            var collider = gameObject.AddComponent<BoxCollider>();
            collider.Size = scale;  // Collider is FULL size
            collider.Center = Vector3.Zero;

            var rb = gameObject.AddComponent<RigidBody>();
            rb.Mass = 1.0f;
            rb.BodyType = BodyType.Dynamic;

            _sceneManager.ActiveScene.Add(gameObject);
        }

        private void AddPolaroids()
        {
            List<Vector3> positions = [new Vector3(-16.3f, 1, 7.1f), new Vector3(-7f, 1.1f, 3.4f), new Vector3(-8.3f, 0.9f, 12.5f), new Vector3(-15.3f, 1.9f, 18.8f)];
            List<Vector3> rotations = [new Vector3(MathHelper.ToRadians(-90), 0, 0.707f), new Vector3(MathHelper.ToRadians(-90), 0, 0.705f), new Vector3(MathHelper.ToRadians(-90), MathHelper.ToRadians(-45), 0.6797f), new Vector3(MathHelper.ToRadians(-90), MathHelper.ToRadians(135), 0.7018f)];

            for (int i = 1; i <= positions.Count; i++)
            {
                var go = new GameObject("photo"+i);

                var mf = MeshFilterFactory.CreateQuadTexturedLit(GraphicsDevice);
                go.AddComponent(mf);

                var imageRenderer = go.AddComponent<MeshRenderer>();
                imageRenderer.Material = _matAlphaCutout;
                imageRenderer.Overrides.MainTexture = _textureDictionary.Get("photo"+i);

                imageRenderer.Overrides.SetInt("ReferenceAlpha", 128);
                imageRenderer.Overrides.Alpha = 1f;

                go.Transform.ScaleTo(new Vector3(1, 1, 0.5f));
                go.Transform.RotateEulerBy(rotations[i-1]);
                go.Transform.TranslateTo(positions[i-1]);

                var collider = go.AddComponent<BoxCollider>();
                collider.Size = new Vector3(1, 1, 0.5f);
                collider.Center = Vector3.Zero;

                var rigidBody = go.AddComponent<RigidBody>();
                rigidBody.BodyType = BodyType.Static;
                go.IsStatic = true;

                _sceneManager.ActiveScene.Add(go);
                go.Enabled = false;
                insightItems.Add(go);
            }
        }

        private void AddClothes()
        {
            List<String> names = ["sock1", "sock2", "shirt", "pants"];
            var groundLevel = -0.4f;
            List<Vector3> positions = [new Vector3(-18.1f, groundLevel, 9f), new Vector3(-8.8f, groundLevel, 3.2f), new Vector3(-4.3f, groundLevel, 6.5f), new Vector3(-11.3f, groundLevel, 14.8f)];
            List<Vector3> rotations = [new Vector3(MathHelper.ToRadians(-90), 0, 0.707f), new Vector3(MathHelper.ToRadians(-90), 0, 0.705f), new Vector3(MathHelper.ToRadians(-90), MathHelper.ToRadians(-45), 0.6797f), new Vector3(MathHelper.ToRadians(-90), MathHelper.ToRadians(135), 0.7018f)];

            for (int i = 1; i <= positions.Count; i++)
            {
                var go = new GameObject(names[i-1]);

                var mf = MeshFilterFactory.CreateQuadTexturedLit(GraphicsDevice);
                go.AddComponent(mf);

                var imageRenderer = go.AddComponent<MeshRenderer>();
                imageRenderer.Material = _matAlphaCutout;
                imageRenderer.Overrides.MainTexture = _textureDictionary.Get(names[i-1]);

                imageRenderer.Overrides.SetInt("ReferenceAlpha", 128);
                imageRenderer.Overrides.Alpha = 1f;

                var scale = (go.Name.Equals("shirt") || go.Name.Equals("pants") ? 3 : 1);

                go.Transform.ScaleTo(new Vector3(1* scale, 1 * scale, 0.5f));
                go.Transform.RotateEulerBy(rotations[i - 1]);
                go.Transform.TranslateTo(positions[i - 1]);

                var collider = go.AddComponent<BoxCollider>();
                collider.Size = new Vector3(1*scale, 1*scale, 0.5f);
                collider.Center = Vector3.Zero;

                var rigidBody = go.AddComponent<RigidBody>();
                rigidBody.BodyType = BodyType.Static;
                go.IsStatic = true;

                _sceneManager.ActiveScene.Add(go);
                go.Enabled = false;
                insightItems.Add(go);
            }

        }

        private void AddNote()
        {
            var go = new GameObject("note");

            var mf = MeshFilterFactory.CreateQuadTexturedLit(GraphicsDevice);
            go.AddComponent(mf);

            var imageRenderer = go.AddComponent<MeshRenderer>();
            imageRenderer.Material = _matAlphaCutout;
            imageRenderer.Overrides.MainTexture = _textureDictionary.Get("note");

            imageRenderer.Overrides.SetInt("ReferenceAlpha", 128);
            imageRenderer.Overrides.Alpha = 1f;

            var scale = (go.Name.Equals("shirt") || go.Name.Equals("pants") ? 3 : 1);

            go.Transform.ScaleTo(new Vector3(1 * scale, 1 * scale, 0.5f));
            go.Transform.RotateEulerBy(new Vector3(MathHelper.ToRadians(-90), MathHelper.ToRadians(-70), MathHelper.ToRadians(-30)));
            go.Transform.TranslateTo(new Vector3(-1.1f, 1.1f, 5.2f));

            var collider = go.AddComponent<BoxCollider>();
            collider.Size = new Vector3(1 * scale, 1 * scale, 0.5f);
            collider.Center = Vector3.Zero;

            var rigidBody = go.AddComponent<RigidBody>();
            rigidBody.BodyType = BodyType.Static;
            go.IsStatic = true;

            _sceneManager.ActiveScene.Add(go);
            //go.Enabled = false;
            insightItems.Add(go);
        }

        /// <summary>
        /// Subscribes a simple debug listener for physics collision events.
        /// </summary>
        private void InitializeCollisionEventListener()
        {
            var events = EngineContext.Instance.Events;

            // Lowest friction: just subscribe with default priority & no filter
            _collisionSubscription = events.Subscribe<CollisionEvent>(OnCollisionEvent);
        }

        /// <summary>
        /// Very simple collision debug handler.
        /// Adjust field names to match your CollisionEvent struct.
        /// </summary>
        private void OnCollisionEvent(CollisionEvent evt)
        {
            // Early-out if this collision does not involve any layer we care about.
            if (!evt.Matches(_collisionDebugMask))
                return;

            var bodyA = evt.BodyA;
            var bodyB = evt.BodyB;

            var nameA = bodyA?.GameObject?.Name ?? "<null>";
            var nameB = bodyB?.GameObject?.Name ?? "<null>";

            var layerA = evt.LayerA;
            var layerB = evt.LayerB;

            //System.Diagnostics.Debug.WriteLine(
            //    $"[Collision] {nameA} (Layer {layerA}) <-> {nameB} (Layer {layerB})");
        }

    }
}