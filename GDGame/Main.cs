using System;
using System.Collections.Generic;
using GDEngine.Core;
using GDEngine.Core.Audio;
using GDEngine.Core.Collections;
using GDEngine.Core.Components;
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
using GDGame.Demos.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
        #endregion

        #region Game Fileds
        private AnimationCurve3D _animationPositionCurve, _animationRotationCurve;
        private AnimationCurve _animationCurve;
        private KeyboardState _newKBState, _oldKBState;

        // Simple debug subscription for collision events
        private IDisposable _collisionSubscription;

        // LayerMask used to filter which collisions we care about in debug
        private LayerMask _collisionDebugMask = LayerMask.All;
        private SceneManager _sceneManager;
        private MenuManager _menuManager;
        private UIDebugInfo _debugRenderer;
        private MouseState _newMouseState;
        private MouseState _oldMouseState;

        private int _insight = 0;
        private List<GameObject> insightItems = [];
        private GameObject _dialogueGO;
        private UIText _textDialogue;
        private float _musicVolume = 0.1f;
        private string _currentMusic = "confused music";
        private float _sfxVolume = 0.4f;
        private bool _isExamining = false;
        private string _oldExamineName;
        private Vector3 _oldExaminePos;
        private Quaternion _oldExamineRot;
        private GameObject _uiReticleGO;
        private bool _hasKey;
        private bool _hasHammer;
        private int _unlockValue;
        private GameObject _gameUIGO;
        private UIText _insightCounter;
        private UIText _objectiveText;

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

            #endregion

            #region Loading GameObjects from JSON
            DemoLoadFromJSON();
            #endregion

            #endregion

            // Mouse reticle
            InitializeUI();

            // Main menu
            InitializeMenuManager();

            // Set pause and show menu
            SetPauseShowMenu();

            // Set the active scene
            _sceneManager.SetActiveScene(AppData.LEVEL_1_NAME);

            #region Insight items
            AddNote();
            AddClothes();
            AddPolaroids();
            spawnModels();

            #endregion

            //preloading items
            LoadEmitters();
            CreateDialogue();
            CreateGameUI();
            GameOrchestrationSystem();

            _sceneManager.Paused = true;
            base.Initialize();
        }

        /// <summary>
        /// Loads spacial sound emitters and places them a bit further away.
        /// 
        /// Settings random intervals when the sound can play again.
        /// </summary>
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
                IsMouseVisible = paused;
                GameUIVisible(!paused);
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
                _textureDictionary.Get("backgroundimage"),
                 _textureDictionary.Get("backgroundimage"),
                  _textureDictionary.Get("backgroundimage"), 
                  _textureDictionary.Get("logo"), 
                  _textureDictionary.Get("controls"));

            // Subscribe to high-level events
            _menuManager.PlayRequested += () =>
            {
                _sceneManager.Paused = false;
                _menuManager.HideMenus();
                var orchestrator = _sceneManager.ActiveScene.GetSystem<OrchestrationSystem>()?.Orchestrator;
                orchestrator.Start("intro", _sceneManager.ActiveScene, EngineContext.Instance);
                IsMouseVisible = false;
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
                _sfxVolume = v / 100;

                EngineContext.Instance.Events.Publish(new StopAllSfxEvent());
            };

   
        }

        private void InitializeCollidableGround(int scale = 500)
        {
            GameObject gameObject = null;
            MeshFilter meshFilter = null;
            MeshRenderer meshRenderer = null;

            gameObject = new GameObject("ground");

            gameObject.Transform.ScaleBy(new Vector3(scale, scale, 1));
            gameObject.Transform.RotateEulerBy(new Vector3(MathHelper.ToRadians(-90), 0, 0), true);
            gameObject.Transform.TranslateTo(new Vector3(0, -0.5f, 0));

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
            InitializePhysicsDebugSystem(false);
            InitializeEventSystem();  //propagate events  
            InitializeInputSystem();  //input
            InitializeCameraAndRenderSystems(); //update cameras, draw renderable game objects, draw ui and menu
            InitializeAudioSystem();
            InitializeOrchestrationSystem(false); //show debugger
            InitializeImpulseSystem();    //camera shake, audio duck volumes etc
            InitializeUIEventSystem();
            InitializeGameStateSystem();   //manage and track game state
                                           //  InitializeNavMeshSystem();

            InitializeDebugInfo(false);
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

        //TODO
        private void InitializeCameras()
        {
            Scene scene = _sceneManager.ActiveScene;

            GameObject cameraGO = null;
            Camera camera = null;
           
            #region simple camera
            cameraGO = new GameObject(AppData.CAMERA_NAME_FIRST_PERSON);
            camera = cameraGO.AddComponent<Camera>();
            cameraGO.Transform.TranslateTo(new Vector3(-2f, 4f, 3));

            cameraGO.AddComponent<SimpleDriveController>();
            cameraGO.AddComponent<MouseYawPitchController>();
            scene.Add(cameraGO);
            #endregion

            #region Camera Cutscene
            cameraGO = new GameObject(AppData.CAMERA_NAME_CUTSCENE);
            cameraGO.Transform.RotateEulerBy(new Vector3(MathHelper.ToRadians(90), MathHelper.ToRadians(90), 0));
            cameraGO.Transform.TranslateTo(new Vector3(-1f, 2f, 2.2f));
            camera = cameraGO.AddComponent<Camera>();
            scene.Add(cameraGO);
            #endregion

            scene.SetActiveCamera(AppData.CAMERA_NAME_CUTSCENE);
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

            picker.Formatter = hit =>
            {
                var go = hit.Body?.GameObject;
                if (go == null)
                    return string.Empty;
                _newMouseState = Mouse.GetState();
                var str = HandleObjectHit(go);
                _oldMouseState = _newMouseState;
                return str;
            };

            _sceneManager.ActiveScene.Add(_uiReticleGO);

            // Hide mouse since reticle will take its place
            //IsMouseVisible = false;
        }

        /// <summary>
        /// Handles all the objects hit by the raycast.
        /// </summary>
        /// <reused>Based on interaction logic developed in GCA Group Project</reused>
        private string HandleObjectHit(GameObject go)
        {
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
            if (go.Name.Equals("lock"))
            {
                ClickedItem(go);
                _oldMouseState = _newMouseState;
                if (_hasKey)
                    return "Lock\n--\nLeft Click to Unlock";
                else
                    return "Lock\n--\nNeed a key to Unlock";
            }
            if (go.Name.Equals("plank"))
            {
                if (_hasHammer)
                {
                    ClickedItem(go);
                    _oldMouseState = _newMouseState;
                    return "Plank\n--\nLeft Click to Take Off";
                }
                else
                    return "Plank\n--\nNeed a hammer to Take Off";
            }

            if (go.Name.Equals("key"))
            {
                ClickedItem(go);
                _oldMouseState = _newMouseState;
                return "The Key\n--\nRight Click to Examine\nLeft Click to Take";
            }
            if (go.Name.Equals("hammer"))
            {
                ClickedItem(go);
                _oldMouseState = _newMouseState;
                return "The Hammer\n--\nRight Click to Examine\nLeft Click to Take";
            }

            return "";
        }

        /// <summary>
        /// Toggles the the visibility of reticle UI element.
        /// </summary>
        /// <reused>GCA Group Project</reused>
        private void SetReticleoVisible(bool state)
        {
            if (_uiReticleGO == null) return;

            foreach (var ui in _uiReticleGO.GetComponents<UIRenderer>())
                ui.Enabled = state;
        }

        /// <summary>
        /// Changes the dialogue text and visibility.
        /// </summary>
        private void DialogueVisible(string text)
        {
            _textDialogue.TextProvider = () => text;

            if (text.Length < 1)
                DialogueEnable(false);
            else
                DialogueEnable(true);
        }

        /// <summary>
        /// Makes the dialogue UI elements enabled or disabled.
        /// </summary>
        private void DialogueEnable(bool state)
        {
            foreach (var renderable in _dialogueGO.GetComponents<UIRenderer>())
            {
                renderable.Enabled = state;
            }
        }

        /// <summary>
        /// Creates the dialogue UI element.
        /// </summary>
        private void CreateDialogue()
        {
            int backBufferWidth = _graphics.PreferredBackBufferWidth;
            int backBufferHeight = _graphics.PreferredBackBufferHeight;
            Vector2 viewportSize = new Vector2(backBufferWidth, backBufferHeight);

            _dialogueGO = new GameObject("dialogue");
            var texture = _dialogueGO.AddComponent<UITexture>();
            texture.Texture = _textureDictionary.Get("dialogue");
            texture.Size = viewportSize;        // cover screen
            texture.Position = Vector2.Zero;

            _textDialogue = _dialogueGO.AddComponent<UIText>();
            _textDialogue.Font = _fontDictionary.Get("menufont");
            _textDialogue.FallbackColor = new Color(255, 255, 255);
            _textDialogue.PositionProvider = () => new Vector2(30, _graphics.PreferredBackBufferHeight-110);
            _textDialogue.TextProvider = () => "";

            _textDialogue.LayerDepth = UILayer.MenuBack;
            _sceneManager.ActiveScene.Add(_dialogueGO);
            DialogueVisible("");
        }

        /// <summary>
        /// Opens the door when the player has unlocked the lock and removed the planks.
        /// </summary>
        private void openDoor()
        {
            var orchestrator = _sceneManager.ActiveScene.GetSystem<OrchestrationSystem>()?.Orchestrator;
            orchestrator.Start("outro", _sceneManager.ActiveScene, EngineContext.Instance);
            GameUIVisible(false);
        }

        /// <summary>
        /// Detects if an item was clicked and handles the logic.
        /// 
        /// It checks for left clicks for collecting items and right clicks for examining items.
        /// </summary>
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
                        SetObjective("FIND MORE INSIGHTS");
                    }


                    if (go.Name.Contains("photo") || go.Name.Contains("sock") || go.Name.Equals("pants") || go.Name.Equals("shirt") || go.Name.Equals("note"))
                    {
                        events.Publish(new PlaySfxEvent("collect", _sfxVolume, false));
                        SetInsight();
                        _insight++;
                        if (_insight < 10)
                            insightItems[_insight - 1].Enabled = true;

                        if (_insight > 12)
                        {
                            SetObjective("FIND TOOLS TO UNLOCK DOOR");
                            var key = _sceneManager.ActiveScene.Find(g => g.Name.Equals("key"));
                            key.Enabled = true;
                            var hammer = _sceneManager.ActiveScene.Find(g => g.Name.Equals("hammer"));
                            hammer.Enabled = true;
                        }

                        go.Enabled = false;
                        go.Name = "collected";
                        //cant destroy
                        //go.Destroy();
                    }

                    if (go.Name.Equals("key"))
                    {
                        events.Publish(new PlaySfxEvent("collect", _sfxVolume, false));
                        _hasKey = true;
                        go.Destroy();
                    }
                    if (go.Name.Equals("hammer"))
                    {
                        events.Publish(new PlaySfxEvent("collect", _sfxVolume, false));
                        _hasHammer = true;
                        go.Destroy();
                    }


                    if (go.Name.Equals("plank") && _hasHammer)
                    {
                        _unlockValue += 1;
                        events.Publish(new PlaySfxEvent("plank", _sfxVolume, false));
                        go.Destroy();

                        if (_unlockValue >= 3)
                        {
                           openDoor();
                        }
                    }

                    if (go.Name.Equals("lock") && _hasKey)
                    {
                        _unlockValue += 1;
                        events.Publish(new PlaySfxEvent("keys", _sfxVolume, false));
                        go.Destroy();
                        if (_unlockValue >= 3)
                        {
                            openDoor();
                        }
                    }
                }
            }
            if (_newMouseState.RightButton == ButtonState.Pressed && _oldMouseState.RightButton == ButtonState.Released)
            {

                var plr = _sceneManager.ActiveScene.Find(go => go.Name.Equals(AppData.CAMERA_NAME_FIRST_PERSON));
                var look = plr.GetComponent<MouseYawPitchController>();
                var move = plr.GetComponent<SimpleDriveController>();
                if (!_isExamining) 
                {
                    if (!go.Name.Equals("plank") && !go.Name.Equals("lock"))
                    {
                        _isExamining = true;

                        look.Enabled = false;
                        move.Enabled = false;

                        if (go.Name.Equals("note"))
                        {
                            DialogueVisible("Why am I in a random room? Who was banging that door? \nThat is quite a weird note. No wonder my head hurts!");
                        }
                        else if (go.Name.Equals("sock1"))
                        {
                            DialogueVisible("My clothes are everywhere, at least I found my precious sock!");
                        }
                        else if (go.Name.Equals("sock2"))
                        {
                            DialogueVisible("Well here is my less precious sock.");
                        }
                        else if (go.Name.Equals("shirt"))
                        {
                            DialogueVisible("My green shirt and it doesn't look that dirty! I'll wear it.");
                        }
                        else if (go.Name.Equals("pants"))
                        {
                            DialogueVisible("My blue jeans! Seems like it gained another battle scar.");
                        }
                        else if (go.Name.Equals("photo1"))
                        {
                            DialogueVisible("That is the last thing I remember from last night.");
                        }
                        else if (go.Name.Equals("photo2"))
                        {
                            DialogueVisible("I look pretty rough here, what is that circle for?");
                        }
                        else if (go.Name.Equals("photo3"))
                        {
                            DialogueVisible("Why is there someone behind me in this picture?");
                        }
                        else if (go.Name.Equals("photo4"))
                        {
                            DialogueVisible("Were they following me? At least, I am in a random room.");
                        }
                        else if (go.Name.Equals("hammer"))
                        {
                            DialogueVisible("I can use this hammer to get rid of the planks.");
                        }
                        else if (go.Name.Equals("key"))
                        {
                            DialogueVisible("I can use this key for the lock. I'm quite skilled when I'm drunk?");
                        }

                        if (go.Name.Contains("photo") || go.Name.Contains("sock") || go.Name.Equals("pants") || go.Name.Equals("shirt") || go.Name.Equals("note") || go.Name.Equals("key") || go.Name.Equals("hammer"))
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
                }
                else
                {
                    if (!go.Name.Equals("plank") && !go.Name.Equals("lock"))
                    {
                        var item = _sceneManager.ActiveScene.Find(g => g.Name.Contains("examine"));
                        look.Enabled = true;
                        move.Enabled = true;
                        item.Transform.RotateToWorld(_oldExamineRot);
                        item.Transform.TranslateTo(_oldExaminePos);
                        item.Name = _oldExamineName;
                        if (go.Name.Equals("pants") || go.Name.Equals("shirt"))
                            go.Transform.ScaleBy(Vector3.One * 2f);

                        DialogueVisible("");
                        SetReticleoVisible(true);
                        _isExamining = false;
                    }
                }

            }
        }

        /// <summary>
        /// Places the item 2 units in front of the camera and rotates it to face the camera.
        /// </summary>
        void PlaceAndFaceItem(GameObject item, GameObject cam)
        {
            // Position
            Vector3 forward = Vector3.Transform(Vector3.Forward, cam.Transform.Rotation);
            Vector3 targetPos = cam.Transform.Position + forward * 2f;
            item.Transform.TranslateTo(targetPos);
            // Rotation (face player)
            Vector3 dirToCamera = cam.Transform.Position - targetPos;
            dirToCamera.Normalize();
            Quaternion faceCamera = Quaternion.CreateFromRotationMatrix(Matrix.CreateWorld(Vector3.Zero, -dirToCamera, Vector3.Up));
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

        /// <summary>
        /// Uses CollidableModel to spawn models in the scene with specific positions, rotations and scales.
        /// </summary>
        private void spawnModels()
        {
            List<String> list = ["hammer", "key", "lock", "plank", "plank"];
            List<Vector3> positions = [new Vector3(-13, 0.3f, 20), new Vector3(-15, 0.05f, 1.7f), new Vector3(-4.4f, 2f, 13.42f), new Vector3(-3.4f, 1f, 13.2f), new Vector3(-3.4f, 3.5f, 13.2f)];
            List<Vector3> rotations = [new Vector3(MathHelper.ToRadians(-90), 0,0), new Vector3(MathHelper.ToRadians(-90), 0, 0), new Vector3(MathHelper.ToRadians(-30), MathHelper.ToRadians(180), MathHelper.ToRadians(-90)), new Vector3(0, 0, MathHelper.ToRadians(-35)), new Vector3(0, 0, MathHelper.ToRadians(-25))];
            List<Vector3> scale = [new Vector3(0.07f, 0.07f, 0.8f), new Vector3(0.07f, 0.07f, 0.07f), new Vector3(0.2f, 0.2f, 0.2f), new Vector3(0.2f, 0.2f, 0.05f), new Vector3(0.2f, 0.2f, 0.05f)];

            for (int i = 0; i < 5; i++)
            {
                CollidableModel(list[i], positions[i], rotations[i], scale[i]);
            }
        }

        /// <summary>
        /// Make a collidable model with box collider and rigidbody
        /// </summary>
        private void CollidableModel(String name, Vector3 position, Vector3 rotation, Vector3 scale)
        {
            var go = new GameObject(name);
            go.Transform.RotateEulerBy(rotation);
            go.Transform.TranslateTo(position);
            go.Transform.ScaleTo(scale);

            var model = _modelDictionary.Get(name);
            var texture = _textureDictionary.Get("colourmap");
            var meshFilter = MeshFilterFactory.CreateFromModel(model, _graphics.GraphicsDevice, 0, 0);
            go.AddComponent(meshFilter);

            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicLit;
            meshRenderer.Overrides.MainTexture = texture;
            _sceneManager.ActiveScene.Add(go);

            // Add box collider (1x1x1 cube)
            var collider = go.AddComponent<BoxCollider>();
            collider.Size = scale*5f;
            if (name.Equals("hammer"))
                collider.Size = new Vector3(0.4f, 0.35f, 2.2f);
            if (name.Equals("plank"))
                collider.Size = new Vector3(2f, 1f, 0.25f);
            collider.Center = Vector3.Zero;

            // Add rigidbody (Dynamic so it falls)
            var rigidBody = go.AddComponent<RigidBody>();
            rigidBody.BodyType = BodyType.Static;
            rigidBody.Mass = 1.0f;
            
            if( name.Equals("key") || name.Equals("hammer"))
                go.Enabled = false;
        }

        private void DemoStuff()
        {
            // Get new state
            _newKBState = Keyboard.GetState();
            DemoToggleFullscreen();
            //To allow object editing in scene
            //ObjectEditor("door");

            _oldKBState = _newKBState;
        }


        /// <summary>
        /// Editor to move objects in the game space for testing purposes and placement.
        /// </summary>
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

        /// <summary>
        /// Creates an orchestration for the intro sequence // maybe add more orchestrations.
        /// </summary>
        private void GameOrchestrationSystem()
        {
            var orchestrator = _sceneManager.ActiveScene.GetSystem<OrchestrationSystem>().Orchestrator;
            var cam = _sceneManager.ActiveScene.Find(go => go.Name.Equals(AppData.CAMERA_NAME_CUTSCENE));
            //needed to make orchestration work
            int i;
            //wakeup, intro camera, main camera
            orchestrator.Build("intro")
                .Do((i) => { GameUIVisible(false); })
                .WaitSeconds(2)
                .Publish(new PlaySfxEvent("door knock", _sfxVolume, false, null))
                .WaitSeconds(1)
                .Do((i) => { DialogueVisible("Who's banging the door?"); })
                .Publish(new PlaySfxEvent("selftalk", _sfxVolume, false, null))
                .RotateEulerTo(cam.Transform, new Vector3(0, MathHelper.ToRadians(90), 0), 2f)
                .MoveTo(cam.Transform, new Vector3(-2, 3, 2.2f), 1f)
                //.WaitSeconds(1)
               
                .Publish(new PlaySfxEvent("headache", _sfxVolume, false, null))
                .Do((i) => { DialogueVisible("My head hurts so much what happened?"); })
                .RotateEulerTo(cam.Transform, new Vector3(0, MathHelper.ToRadians(180), 0), 1f)
                .MoveTo(cam.Transform, new Vector3(-2f, 4f, 3f), 2f)
                .RotateEulerTo(cam.Transform, new Vector3(MathHelper.ToRadians(-10), MathHelper.ToRadians(130), 0), 3f)
                .Do((i) => { DialogueVisible("This is not my house?"); })
                //.WaitSeconds(1)
                .RotateEulerTo(cam.Transform, new Vector3(MathHelper.ToRadians(-60), MathHelper.ToRadians(210), 0), 2.4f)
                .Do((i) => { DialogueVisible("My head!!!"); })
                .Publish(new PlaySfxEvent("headache", _sfxVolume, false, null))
                .WaitSeconds(2)
                .Publish(new PlayMusicEvent("confused music", _musicVolume, 8))
                .RotateEulerTo(cam.Transform, new Vector3(0, MathHelper.ToRadians(180), 0), 3f)
                .WaitSeconds(2)
                .Do((i) => { DialogueVisible("I need to get my stuff and leave this place."); })
                .Publish(new CameraEvent(AppData.CAMERA_NAME_FIRST_PERSON))
                .WaitSeconds(3)
                .Do((i) => { DialogueVisible(""); GameUIVisible(true); })
                .Register();

            var door = _sceneManager.ActiveScene.Find(g => g.Name.Equals("door"));

            orchestrator.Build("outro")
                .Publish(new CameraEvent(AppData.CAMERA_NAME_CUTSCENE))
                //.RotateEulerTo(cam.Transform, new Vector3(0, MathHelper.ToRadians(180), 0), 0.01f)
                //.MoveTo(cam.Transform, new Vector3(-2f, 4f, 3f), 0.01f)
                .MoveTo(cam.Transform, new Vector3(-3.4f, 4, 4f), 1f)
                .Do((i) => { DialogueVisible("I can finally leave this place."); })
                .MoveTo(door.Transform, new Vector3(-3.9f, 2.2f, 13.8f), .1f)
                .RotateEulerTo(door.Transform, new Vector3(0, MathHelper.ToRadians(10), 0), 2f)
                .MoveTo(cam.Transform, new Vector3(-3.4f, 4, 8f), 2f)
                .Do((i) => { DialogueVisible("I hope the person who owns this place doesn't mind"); })
                .WaitSeconds(2)
                .Do((i) => { DialogueVisible("I should really drink responsibility.."); })
                .MoveTo(cam.Transform, new Vector3(-3.5f, 4, 11f), 2f)
                .RotateEulerTo(cam.Transform, new Vector3(MathHelper.ToRadians(-30), MathHelper.ToRadians(170), 0), 1f)
                .Do((i) => { DialogueVisible("THE END. Please drink responsibility! You can \nend up in scarier/dangerous situations!"); })
                .WaitSeconds(8)
                .Do((i) => { DialogueVisible("Goodbye! Thanks for playing!"); })
                .WaitSeconds(3)
                .Do((i) => { Exit(); })
                .Register();

        }

        private void DemoToggleFullscreen()
        {
            bool togglePressed = _newKBState.IsKeyDown(Keys.F5) && !_oldKBState.IsKeyDown(Keys.F5);
            if (togglePressed)
                _graphics.ToggleFullScreen();
        }

        private void DemoLoadFromJSON()
        {
            var relativeFilePathAndName = "assets/data/multi_model_spawn.json";
            //load multiple models
            foreach (var d in JSONSerializationUtility.LoadData<ModelSpawnData>(Content, relativeFilePathAndName))
                InitializeModel(d.Position, d.RotationDegrees, d.Scale, d.TextureName, d.ModelName, d.ObjectName);
        }

        /// <summary>
        /// Sets the visibility of the game ui visibility
        /// </summary>
        private void GameUIVisible(bool state)
        {
            foreach (var renderable in _gameUIGO.GetComponents<UIRenderer>())
            {
                renderable.Enabled = state;
            }
        }

        private void SetObjective(String s)
        {
            _objectiveText.TextProvider = () => s;
        }

        private void SetInsight()
        {
            _insightCounter.TextProvider = () => (_insight-4)+"/9";
        } 

        /// <summary>
        /// Creates objective and insight counter
        /// </summary>
        private void CreateGameUI()
        {
            _gameUIGO = new GameObject("gameUI");
            var texture = _gameUIGO.AddComponent<UITexture>();
            texture.Texture = _textureDictionary.Get("game-ui");
            texture.Size = new Vector2(200, 100);
            texture.Position = new Vector2(5, 200);

            _insightCounter = _gameUIGO.AddComponent<UIText>();
            _insightCounter.Font = _fontDictionary.Get("menufont");
            _insightCounter.FallbackColor = new Color(255, 255, 255);
            _insightCounter.PositionProvider = () => new Vector2(100, 235);
            _insightCounter.TextProvider = () => "0/9";
            _insightCounter.LayerDepth = UILayer.MenuBack;

            _objectiveText = _gameUIGO.AddComponent<UIText>();
            _objectiveText.Font = _fontDictionary.Get("menufontsmall");
            _objectiveText.FallbackColor = new Color(255, 255, 255);
            _objectiveText.PositionProvider = () => new Vector2(15, 170);
            _objectiveText.TextProvider = () => "READ THE NOTE";
            _objectiveText.LayerDepth = UILayer.MenuBack;

            _sceneManager.ActiveScene.Add(_gameUIGO);
            GameUIVisible(false);
        }

        /// <summary>
        /// Adds polaroid photo objects to the scene with specific positions and rotations using the alpha cutout material
        /// </summary>
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

        /// <summary>
        /// adds clothes objects to the scene with specific positions and rotations using the alpha cutout material
        /// </summary>
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


        /// <summary>
        /// Adds a note object to the scene using the alpha cutout material.
        /// </summary>
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