using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Nubik
{
    public enum Station { None, Counter, Workbench }

    /// <summary>
    /// First-person digging in the backyard: player, shovel, terrain, finds, health, jetpack and saves.
    /// There are no teleports: ore is carried out of the mine and sold in the house.
    /// </summary>
    public sealed partial class MineGame : MonoBehaviour
    {
        public MineConfig config;
        public Material prototypeMaterial;
        public Material terrainMaterial;
        public Material skyMaterial;
        public GameObject cubePrefab;
        public GameObject spherePrefab;

        public GameProgress Progress { get; private set; }
        public int Depth => Mathf.Clamp(Mathf.FloorToInt(-body.transform.position.y + 0.1f), 0, config.depth);
        public ToolDef Tool => config.tools[Progress.tool == config.tools.Length - 1 && !UsingDrill ? Progress.tool - 1 : Progress.tool];
        public string TargetText { get; private set; } = "";
        public string Hint { get; private set; } = "";
        public Station Station { get; private set; }
        public bool InReach { get; private set; }
        public bool TouchMode { get; private set; }
        /// <summary>The touch pause menu (on PC releasing the mouse opens the same card).</summary>
        public bool MenuOpen { get; private set; }
        /// <summary>Mouse play needs a click first; afterwards a lost pointer lock asks for another click.</summary>
        public bool NeedsClick => MenuOpen || !TouchMode && !hud.PanelOpen && !(engaged && (WebInput.Locked || WebInput.LockUnavailable));
        public bool FreeMouse => !TouchMode && WebInput.LockUnavailable;
        public bool Active => !hud.PanelOpen && !YandexBridge.Paused && !NeedsClick;
        public float Health => Progress.Health(config);
        public float MaxHealth => Progress.MaxHealth(config);
        public bool HasJetpack => true;
        public float Fuel { get => Progress.Petrol(config); private set => Progress.fuel = value; }
        public float FuelMax => Progress.FuelCapacity(config);
        public bool Thrusting { get; private set; }
        public bool Underground => body.transform.position.y < -1.5f;
        public bool ScanActive => Time.time < scanUntil;
        public float ScanWait => Mathf.Max(0, scanReadyAt - Time.time);
        public readonly List<LootItem> ScanHits = new List<LootItem>();

        private const float MouseDegreesPerPixel = 0.16f, EyeHeight = 1.55f;
        private const int IgnoreRaycastLayer = 2;
        private static readonly Color Sky = new Color(0.56f, 0.77f, 0.95f);
        private static readonly Color Haze = new Color(0.70f, 0.82f, 0.93f);
        private static readonly Color SurfaceAmbient = new Color(0.36f, 0.43f, 0.47f);
        private static readonly Color SunColor = new Color(1f, 0.95f, 0.85f);
        private static readonly Color Amber = new Color(1f, 0.78f, 0.25f);
        private static readonly Color Danger = new Color(1f, 0.38f, 0.32f);
        // Shovel held low on the right: pointing forward-up-left with the blade face towards the camera.
        private static readonly Vector3 ToolAim = new Vector3(-0.25f, 0.55f, 0.8f).normalized;
        private static readonly Quaternion ToolRotation = Quaternion.LookRotation(ToolAim, new Vector3(-0.2f, 0.4f, -0.9f));

        private VoxelTerrain terrain;
        private TerrainMesher mesher;
        private LootField loot;
        private Shapes shapes;
        private Yard yard;
        private MineSites sites;
        public int CurrentSite { get; private set; } = -1;
        public int NextSite
        {
            get { for (int i = 0; i < MineSites.All.Length; i++) if (!Progress.HasSite(i)) return i; return -1; }
        }
        public string ExpeditionGoal => InBoss ? (Progress.hasWeapon ? "Ктулху · " + Mathf.CeilToInt(boss.Battle.Health) : "Возьми гарпун у входа") : Expedition.Objective(Progress);
        private MineHud hud;
        private YandexBridge platform;
        private GameAudio sound;
        private CharacterController body;
        private Transform head, terrainRoot, tool, load;
        private Renderer[] loadParts;
        private float loadSince = -10;
        private Camera view;
        private Light sun, lamp;
        private Mesh scratch;
        private readonly Dictionary<int, ChunkView> chunks = new Dictionary<int, ChunkView>();
        private readonly Dictionary<Collider, LootItem> itemColliders = new Dictionary<Collider, LootItem>();
        private readonly HashSet<Collider> terrainColliders = new HashSet<Collider>();
        private readonly List<LootItem> nearby = new List<LootItem>();
        private readonly List<Flight> flights = new List<Flight>();
        private readonly List<Debris> debris = new List<Debris>();
        private readonly Stack<Debris> debrisPool = new Stack<Debris>();
        private float yaw, pitch, verticalSpeed, nextHit, swing = 1, nextVisibility, nextAutosave, walkCycle;
        private float jetDelay, scanUntil = -10, scanReadyAt, fullBagNoticeAt;
        private bool saveDirty, endingShown, wasActive, engaged, wantLock, wentDown;
        private int engagedFrame = -10;
        private string pendingDebugDig, pendingGoto;
        private bool restarting;

        private sealed class ChunkView { public GameObject obj; public Mesh mesh; public MeshCollider collider; }
        private sealed class Flight { public Transform obj; public Vector3 from; public float t; }
        private sealed class Debris { public Transform obj; public Renderer renderer; public Vector3 velocity; public float life, max, size; }

        private void Awake()
        {
            // The page sends visibility messages as soon as the instance starts.
            platform = gameObject.AddComponent<YandexBridge>();
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            Input.simulateMouseWithTouches = false;
            TouchMode = Application.isMobilePlatform;
            Progress = ProgressStore.Load(config);
            Progress.EnsureQuest(config);
            shapes = new Shapes(cubePrefab, spherePrefab, prototypeMaterial);
            terrain = new VoxelTerrain(config);
            foreach (var entry in Progress.terrain)
                if (!terrain.Decode(entry.chunk, entry.data)) Debug.LogWarning("Skipped damaged terrain chunk " + entry.chunk);
            MineSites.Carve(terrain);
            Expedition.Prepare(terrain);
            Secrets.Prepare(terrain);
            terrain.ClearDirty();
            mesher = new TerrainMesher(terrain);
            loot = new LootField(terrain);
            loot.MarkTaken(Progress);
            foreach (var item in loot.Items) item.Exposed = !item.Taken && loot.IsExposed(item);
            yard = new Yard(shapes, config);
            sites = new MineSites(shapes);
            Expedition.Decorate(shapes);
            Secrets.Decorate(shapes);
            boss = new BossEncounter(shapes);
            BuildTerrain();
            BuildPlayer();
            hud = gameObject.AddComponent<MineHud>();
            hud.Setup(this);
            SpawnAtStart();
            UpdateAmbience();
            platform.Ready();
            if (pendingDebugDig != null) DebugDig(pendingDebugDig);
            if (pendingGoto != null) DebugGoto(pendingGoto);
        }

        // ---------- World ----------

        private void BuildTerrain()
        {
            terrainRoot = new GameObject("Terrain").transform;
            scratch = new Mesh();
            for (int chunk = 0; chunk < config.ChunkCount; chunk++) RebuildChunk(chunk);
        }

        private void RebuildChunk(int chunk)
        {
            chunks.TryGetValue(chunk, out var chunkView);
            bool any = mesher.Build(chunk, chunkView != null ? chunkView.mesh : scratch);
            if (chunkView == null)
            {
                if (!any) return;
                chunkView = new ChunkView { mesh = scratch, obj = new GameObject("Chunk " + chunk) };
                scratch = new Mesh();
                chunkView.mesh.name = "Terrain " + chunk;
                chunkView.mesh.MarkDynamic();
                chunkView.obj.transform.SetParent(terrainRoot, false);
                chunkView.obj.AddComponent<MeshFilter>().sharedMesh = chunkView.mesh;
                chunkView.obj.AddComponent<MeshRenderer>().sharedMaterial = terrainMaterial;
                chunkView.collider = chunkView.obj.AddComponent<MeshCollider>();
                terrainColliders.Add(chunkView.collider);
                chunks[chunk] = chunkView;
            }
            chunkView.obj.SetActive(any);
            chunkView.collider.sharedMesh = null;
            if (any) chunkView.collider.sharedMesh = chunkView.mesh;
        }

        private void RebuildDirty()
        {
            foreach (int chunk in terrain.DirtyChunks) RebuildChunk(chunk);
            terrain.ClearDirty();
        }

        private void BuildPlayer()
        {
            // The camera sits inside the capsule; keep the player out of dig and aim rays.
            var player = new GameObject("Player") { layer = IgnoreRaycastLayer };
            body = player.AddComponent<CharacterController>();
            body.height = 1.7f; body.radius = 0.3f; body.center = new Vector3(0, 0.85f, 0);
            body.stepOffset = 0.45f; body.slopeLimit = 55; body.skinWidth = 0.03f; body.minMoveDistance = 0;
            head = new GameObject("Head").transform;
            head.SetParent(player.transform, false);
            head.localPosition = Vector3.up * EyeHeight;
            var cameraObj = new GameObject("Game Camera", typeof(Camera), typeof(AudioListener));
            cameraObj.tag = "MainCamera";
            cameraObj.transform.SetParent(head, false);
            view = cameraObj.GetComponent<Camera>();
            view.fieldOfView = 70; view.nearClipPlane = 0.03f; view.farClipPlane = 220;
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = Sky;
            if (skyMaterial != null) { RenderSettings.skybox = skyMaterial; view.clearFlags = CameraClearFlags.Skybox; }
            sound = cameraObj.AddComponent<GameAudio>();
            GameAudio.SetMuted(Progress.muted);

            lamp = new GameObject("Headlamp", typeof(Light)).GetComponent<Light>();
            lamp.transform.SetParent(view.transform, false);
            lamp.transform.localPosition = new Vector3(0, 0.12f, 0);
            lamp.type = LightType.Spot; lamp.spotAngle = 88; lamp.innerSpotAngle = 40; lamp.range = 16;
            lamp.color = new Color(1f, 0.93f, 0.80f); lamp.intensity = 0; lamp.shadows = LightShadows.None;

            sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(52, -35, 0);
            sun.shadows = LightShadows.Hard; sun.shadowStrength = 0.8f; sun.color = SunColor;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = SurfaceAmbient;
            BuildTool();
        }

        private void BuildTool()
        {
            if (tool != null) Destroy(tool.gameObject);
            tool = new GameObject("Shovel").transform;
            tool.SetParent(view.transform, false);
            modelDrill = UsingDrill; modelWeapon = InBoss && Progress.hasWeapon;
            if (BuildPoweredTool()) { AnimateTool(); return; }
            var wood = new Color(0.58f, 0.38f, 0.22f);
            // Modelled along +z (shaft behind the pivot, blade ahead), blade face along +y.
            void Part(string name, Vector3 at, Vector3 size, Color color, float yaw = 0)
            {
                var part = shapes.Make(name, cubePrefab, at, size, Quaternion.Euler(0, yaw, 0), shapes.Mat(color, 0.15f, false), tool, false);
                part.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            Part("Shaft", new Vector3(0, 0, -0.19f), new Vector3(0.018f, 0.018f, 0.36f), wood);
            Part("Collar", new Vector3(0, 0, -0.005f), new Vector3(0.026f, 0.026f, 0.04f), new Color(0.32f, 0.32f, 0.35f));
            Part("Blade", new Vector3(0, 0, 0.055f), new Vector3(0.1f, 0.007f, 0.09f), Tool.color);
            Part("Blade tip", new Vector3(0, 0, 0.1f), new Vector3(0.071f, 0.007f, 0.071f), Tool.color, 45);
            Part("Blade ridge", new Vector3(0, .006f, .05f), new Vector3(.012f,.008f,.09f), Tool.color * 1.2f);
            Part("Blade edge", new Vector3(0, .004f, .105f), new Vector3(.062f,.008f,.015f), new Color(.8f,.85f,.83f));
            // Folded tread for the boot, rivets through the socket and a ferrule on the shaft.
            Part("Blade step metal", new Vector3(0, .005f, .013f), new Vector3(.1f, .007f, .012f), Tool.color * .82f);
            foreach (float x in new[] { -.008f, .008f })
                Part("Collar rivet metal", new Vector3(x, .0135f, -.006f), new Vector3(.005f, .004f, .005f), new Color(.72f, .72f, .7f));
            Part("Shaft ferrule metal", new Vector3(0, 0, -.25f), new Vector3(.021f, .021f, .012f), new Color(.45f, .47f, .5f));
            Part("Grip", new Vector3(0,0,-.30f), new Vector3(.026f,.026f,.09f), new Color(.14f,.25f,.25f));
            Part("Glove", new Vector3(.025f,-.018f,-.20f), new Vector3(.063f,.052f,.075f), new Color(.18f,.42f,.37f));
            Part("Cuff", new Vector3(.025f,-.021f,-.248f), new Vector3(.067f,.054f,.023f), new Color(.89f,.65f,.31f));
            // A scoop of ground rides on the blade for a moment after each successful hit.
            load = new GameObject("Load").transform;
            load.SetParent(tool, false);
            load.localPosition = new Vector3(0, 0.012f, 0.06f);
            loadParts = new Renderer[3];
            for (int i = 0; i < loadParts.Length; i++)
            {
                var lump = shapes.Make("Lump", cubePrefab, new Vector3((i - 1) * 0.022f, i == 1 ? 0.012f : 0.004f, (i % 2) * 0.012f),
                    new Vector3(0.05f, 0.03f - i % 2 * 0.008f, 0.045f), Quaternion.Euler(10 * i, 30 * i, 8), shapes.Mat(Color.gray, 0, false), load, false);
                loadParts[i] = lump.GetComponent<Renderer>();
                loadParts[i].shadowCastingMode = ShadowCastingMode.Off;
            }
            load.gameObject.SetActive(false);
            AnimateTool();
        }

        private void SpawnAtStart()
        {
            if (Progress.hasResume && Standable(Progress.resume)) { Teleport(Progress.resume); yaw = Progress.resumeYaw; pitch = 15; }
            else if (Progress.expeditions == 0 && Progress.maxDepth == 0 && Standable(Yard.FirstSpawn)) { Teleport(Yard.FirstSpawn); yaw = 0; pitch = 36; }
            else { Teleport(Yard.HomeSpawn); yaw = Yard.HomeYaw; pitch = 5; }
            ApplyView();
        }

        private bool Standable(Vector3 feet) => feet.y > config.FloorY && terrain.IsAir(feet + Vector3.up * 0.5f) && terrain.IsAir(feet + Vector3.up * 1.4f);

        private void Teleport(Vector3 feet)
        {
            body.enabled = false;
            body.transform.position = feet;
            body.enabled = true;
            verticalSpeed = 0;
            Physics.SyncTransforms();
        }

        // ---------- Frame ----------

        private void Update()
        {
            if (body == null) return;
            UpdateAmbience();
            UpdateFlights();
            yard.Animate(Time.deltaTime);
            // Narrow portrait screens get a wider vertical view so the shovel and HUD leave room for the world.
            view.fieldOfView = Mathf.Lerp(88, 70, Mathf.InverseLerp(0.5f, 1.3f, view.aspect));
            UpdateDebris();
            if (YandexBridge.Paused) { hud.ClearInput(); sound.Loop("jet", false); return; }
            // Esc closes a window or pauses; E also closes the house it opened.
            bool keyUsed = false;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (hud.PanelOpen) hud.ClosePanel();
                else if (Active) OpenMenu();
                keyUsed = true;
            }
            else if (Input.GetKeyDown(KeyCode.E) && hud.PanelOpen) { hud.ClosePanel(); keyUsed = true; }
            if (!TouchMode && Input.touchCount > 0) { TouchMode = true; WebInput.WantLock(false); }
            bool want = !TouchMode && !hud.PanelOpen && !MenuOpen && !WebInput.LockUnavailable;
            if (want != wantLock) { wantLock = want; WebInput.WantLock(want); }
            bool active = Active;
            if (active != wasActive) { wasActive = active; platform.SetInMine(active); }
            if (active)
            {
                Look();
                Move(Time.deltaTime);
                if (!keyUsed && Input.GetKeyDown(KeyCode.E)) Interact();
                if (Input.GetKeyDown(KeyCode.F)) Scan();
                if (Input.GetKeyDown(KeyCode.Q)) UseMedkit();
                if (Input.GetKeyDown(KeyCode.G)) ThrowDynamite();
                if (Input.GetKeyDown(KeyCode.M)) ToggleSound();
            }
            else
            {
                hud.ClearInput();
                Thrusting = false;
            }
            sound.Loop("jet", Thrusting, 0.55f);
            UpdateTarget();
            // The click that starts play only engages the mouse.
            bool click = !TouchMode && Input.GetMouseButtonDown(0) && Time.frameCount > engagedFrame + 1;
            bool overUi = !TouchMode && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (Active && !overUi && (DigHeld || click) && Time.time >= nextHit) Swing();
            AnimateTool();
            UpdateStation();
            UpdateHealth(Time.deltaTime);
            TrackTrips();
            if (!InBoss) { TrackDepth(); TrackSites(); }
            UpdateCharges(Time.deltaTime);
            UpdateExpedition(Time.deltaTime);
            if (ScanActive) RefreshScan();
            if (Time.unscaledTime >= nextVisibility) UpdateVisibility();
            if (saveDirty && Time.unscaledTime >= nextAutosave) SaveNow();
            UpdateHint();
        }

        private bool DigHeld => TouchMode ? hud.Dig.Held : Input.GetMouseButton(0);
        private bool JumpHeld => Input.GetKey(KeyCode.Space) || hud.Jump.Held;

        private void Look()
        {
            var delta = hud.Look.Consume() * (110f / Mathf.Max(1, Screen.height));
            var mouse = WebInput.TakeMouseDelta();
            // Without pointer lock the free cursor turns the view only while the right button is held.
            if (!TouchMode && (WebInput.Locked || FreeMouse && Input.GetMouseButton(1))) delta += mouse * MouseDegreesPerPixel;
            yaw = Mathf.Repeat(yaw + delta.x, 360);
            pitch = Mathf.Clamp(pitch - delta.y, -85, 85);
            ApplyView();
        }

        private void ApplyView()
        {
            body.transform.rotation = Quaternion.Euler(0, yaw, 0);
            head.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        private void Move(float dt)
        {
            var input = Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")) + hud.Stick.Value, 1);
            var horizontal = Quaternion.Euler(0, yaw, 0) * new Vector3(input.x, 0, input.y) * config.moveSpeed;
            bool jump = Input.GetKeyDown(KeyCode.Space) | hud.ConsumeJump();
            bool grounded = body.isGrounded;
            if (grounded)
            {
                if (verticalSpeed < 0) verticalSpeed = -2;
                // A plain jump first; holding on lights the jetpack a moment later.
                if (jump) { verticalSpeed = config.jumpSpeed; jetDelay = 0.2f; }
            }
            jetDelay -= dt;
            Thrusting = !grounded && JumpHeld && HasJetpack && Fuel > 0 && jetDelay <= 0;
            if (Thrusting)
            {
                verticalSpeed = Mathf.Min(verticalSpeed + config.jetThrust * dt, config.jetMaxRise);
                Fuel = Mathf.Max(0, Fuel - Progress.JetConsumption(config) * dt);
                saveDirty = true;
            }

            verticalSpeed = Mathf.Max(verticalSpeed - config.gravity * dt, -40);
            float impact = -verticalSpeed;
            body.Move((horizontal + Vector3.up * verticalSpeed) * dt);
            if (!grounded && body.isGrounded && impact > config.safeFallSpeed) Land(impact);
            walkCycle += input.magnitude * dt * 9;
            if (body.transform.position.y < config.FloorY - 3) WakeAtHome("ТЫ ВЫПАЛ ИЗ МИРА", "Очнулся дома", false);
        }

        private void Land(float impact)
        {
            float damage = (impact - config.safeFallSpeed) * config.fallDamage;
            sound.Play("hurt", Mathf.Clamp01(0.5f + damage / 60f));
            hud.Hurt(damage / MaxHealth);
            hud.Popup(view.transform.position + view.transform.forward * 1.2f - Vector3.up * 0.3f, "-" + Mathf.CeilToInt(damage), Danger);
            saveDirty = true;
            if (Progress.Hurt(damage, config))
            {
                if (InBoss) LoseBattle();
                else WakeAtHome("ТЫ ПОТЕРЯЛ СОЗНАНИЕ", "Падение было слишком высоким", true);
            }
        }

        private void UpdateHealth(float dt)
        {
            var feet = body.transform.position;
            // Rest at home; slow recovery on the lawn; none underground.
            float regen = Yard.InsideHouse(feet) ? 60 : feet.y > -0.3f ? config.surfaceRegen : 0;
            if (regen > 0 && Health < MaxHealth) Progress.Heal(regen * dt, config);
        }

        private void UpdateTarget()
        {
            TargetText = "";
            InReach = false;
            if (InBoss)
            {
                if (Physics.Raycast(view.transform.position, view.transform.forward, out var aim, 35, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide) && boss.IsBoss(aim.collider))
                { InReach = Progress.hasWeapon; }
                return;
            }
            if (!Physics.Raycast(view.transform.position, view.transform.forward, out var hit, config.reach + 5, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)) return;
            if (hit.distance > config.reach)
            {
                if (itemColliders.ContainsKey(hit.collider)) TargetText = "Находка · подойди ближе";
                return;
            }
            InReach = true;
            if (itemColliders.TryGetValue(hit.collider, out var item)) { TargetText = ItemLabel(item); return; }
            if (!terrainColliders.Contains(hit.collider))
            {
                InReach = false;
                TargetText = Underground ? "Деревянный настил" : hit.point.y < 0.3f && !Yard.InsideHouse(hit.point) ? "Копать можно в рамке участка" : "";
                return;
            }
            float edge = config.width / 2f - 0.55f;
            if (hit.point.y < config.FloorY + 0.8f) TargetText = "Коренная порода — глубже не пройти";
            else if (Mathf.Abs(hit.point.x) > edge || Mathf.Abs(hit.point.z) > edge) TargetText = "Край участка";
            else
            {
                var rock = config.Rock(hit.point - hit.normal * 0.25f);
                int hits = config.HitsToClear(rock, Progress.tool);
                TargetText = rock.nameRu + " · " + hits + " " + Plural(hits, "удар", "удара", "ударов");
            }
        }

        private string ItemLabel(LootItem item)
        {
            if (item.Kind == LootKind.Key) return Expedition.Keys[item.Key].Name + " · забрать";
            if (item.Kind == LootKind.Collectible) return "Что-то особенное!";
            if (item.Kind == LootKind.Secret) return "Здесь что-то спрятано · ударь";
            var ore = config.ores[item.Ore];
            string space = item.Slots > 1 ? " · " + item.Slots + " " + Plural(item.Slots, "слот", "слота", "слотов") : "";
            return ore.nameRu + " · " + ore.value + " монет" + space + (Progress.FreeSlots(config) < item.Slots ? " · рюкзак полон" : "");
        }

        public static string Plural(int n, string one, string few, string many)
        {
            int mod10 = n % 10, mod100 = n % 100;
            if (mod10 == 1 && mod100 != 11) return one;
            return mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14) ? few : many;
        }

        // ---------- Digging ----------

        private void Swing()
        {
            if (InBoss) { FireHarpoon(); return; }
            nextHit = Time.time + Tool.interval;
            swing = 0;
            var origin = view.transform.position;
            var direction = view.transform.forward;
            if (!Physics.Raycast(origin, direction, out var hit, config.reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)) return;
            hud.HitFeedback();
            if (itemColliders.TryGetValue(hit.collider, out var item))
            {
                sound.Play("dig_stone", 0.5f, 1.3f);
                Burst(hit.point, hit.normal, item.Color, 5);
                Collect(item);
                return;
            }
            if (!terrainColliders.Contains(hit.collider)) return;
            var diggingTool = Tool;
            if (UsingDrill && !Progress.UseFuel(config.drillFuelPerHit, config)) return;
            var center = hit.point + direction * diggingTool.radius * 0.4f;
            var result = terrain.Dig(center, diggingTool.radius, diggingTool.damage);
            var dust = hit.point.y > -0.2f && hit.normal.y > 0.5f ? new Color(0.34f, 0.60f, 0.22f) : result.Rock.color;
            Burst(hit.point, hit.normal, dust, result.Changed ? 7 : 3);
            if (!result.Changed) { sound.Play("dig_stone", 0.7f, 0.55f); return; }
            // Soft ground thuds, harder rock rings lower.
            int hardness = result.Rock.hardness;
            if (hardness <= 3) sound.Play("dig_dirt", 0.8f);
            else sound.Play("dig_stone", 0.75f, Mathf.Lerp(1.05f, 0.72f, Mathf.InverseLerp(6, 32, hardness)));
            RebuildDirty();
            ShowLoad(dust);
            saveDirty = true;
            RevealLoot(center, Tool.radius);
        }

        private void RevealLoot(Vector3 center, float radius)
        {
            loot.Near(center, radius + 1.2f, nearby);
            foreach (var item in nearby)
            {
                item.Exposed = loot.IsExposed(item);
                if (!item.Exposed) continue;
                // Uncovering a find must leave something to see and aim at. A later direct hit collects it.
                if (item.View == null) ShowItem(item);
            }
        }

        private void Collect(LootItem item)
        {
            if (item.Taken) return;
            var result = Progress.Collect(item, config);
            if (result == Pickup.BagFull)
            {
                // The ore stays in the wall; remind without spamming.
                sound.Play("dig_stone", 0.6f, 0.6f);
                if (Time.time > fullBagNoticeAt)
                {
                    hud.Notify("Рюкзак полон — продай руду в доме");
                    fullBagNoticeAt = Time.time + 2.5f;
                }
                hud.BagFull();
                return;
            }
            item.Taken = true;
            if (item.View == null) ShowItem(item);
            var viewObj = item.View;
            itemColliders.Remove(viewObj.GetComponent<Collider>());
            Destroy(viewObj.GetComponent<Collider>());
            flights.Add(new Flight { obj = viewObj.transform, from = viewObj.transform.position });
            item.View = null;
            ScanHits.Remove(item);
            if (result != Pickup.Collected) return;
            switch (item.Kind)
            {
                case LootKind.Key:
                    sound.Play("collect", 1, 1, 0);
                    hud.Announce(UiGlyph.Kind.Key, item.Color, "КЛЮЧИ ПЕЧАТИ · " + Progress.KeyCount + " / 5", Expedition.Keys[item.Key].Name,
                        Progress.KeyCount == 5 ? "Все ключи найдены. Открой дверь на 120 м." : "Ключ сохранён навсегда. Следующая подсказка — в дневнике.");
                    break;
                case LootKind.Collectible:
                    sound.Play("collect", 1, 1, 0);
                    var def = config.collection[item.Collectible];
                    hud.Announce((UiGlyph.Kind)((int)UiGlyph.Kind.Helmet + item.Collectible), def.color,
                        "В КОЛЛЕКЦИЮ  ·  " + Progress.CollectionCount + " / " + config.collection.Length, def.nameRu,
                        "Предмет навсегда в альбоме. +" + config.firstDiscoveryCoins + " монет");
                    hud.Popup(item.Position, def.nameRu, def.color);
                    break;
                case LootKind.Secret:
                    sound.Play("chest", 1, 1.15f, 0);
                    var secret = Secrets.All[item.Secret];
                    hud.Announce(UiGlyph.Kind.Star, secret.Color, "СЕКРЕТ НАЙДЕН · " + Secrets.Count(Progress) + " / " + Secrets.All.Length, secret.Name,
                        secret.Note + " +" + secret.Reward + " монет");
                    hud.Popup(item.Position, "+" + secret.Reward, Amber);
                    break;
                case LootKind.Chest:
                    sound.Play("chest", 1, 1, 0);
                    hud.Notify(config.ores[item.Ore].nameRu + " в рюкзаке · " + item.Value + " монет при продаже");
                    hud.Popup(item.Position, config.ores[item.Ore].nameRu, item.Color);
                    break;
                default:
                    sound.Play("pickup", 0.9f);
                    hud.Popup(item.Position, "+" + config.ores[item.Ore].nameRu, item.Color);
                    break;
            }
            SaveNow();
        }

        private void ShowItem(LootItem item)
        {
            var root = new GameObject(item.Kind + " find").transform;
            root.position = item.Position;
            uint h = config.Hash(Mathf.RoundToInt(item.Position.x * 97), Mathf.RoundToInt(item.Position.y * 97), Mathf.RoundToInt(item.Position.z * 97), 5);
            root.rotation = Quaternion.Euler(h % 40, h % 360, h / 7 % 40);
            foreach (var site in MineSites.All)
                if (item.Special == site.CacheId) { root.rotation = Quaternion.identity; break; }
            if (item.Kind == LootKind.Secret) root.rotation = Quaternion.identity;
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = Vector3.one * item.Size * 2.2f;
            if (item.Kind == LootKind.Key) { collider.center = Vector3.up * .08f; collider.size = new Vector3(.6f, .85f, .4f); }
            if (item.Kind == LootKind.Secret) collider.size = Vector3.one * .9f;
            float s = item.Size;
            switch (item.Kind)
            {
                case LootKind.Key:
                    BuildSealKey(item, root);
                    break;
                case LootKind.Chest:
                    shapes.Box("Chest", Vector3.zero, new Vector3(0.75f, 0.48f, 0.5f), new Color(0.52f, 0.32f, 0.18f), root);
                    shapes.Box("Lid", new Vector3(0, 0.27f, 0), new Vector3(0.78f, 0.1f, 0.53f), new Color(0.42f, 0.25f, 0.14f), root);
                    shapes.Box("Band", new Vector3(0, 0.05f, 0), new Vector3(0.8f, 0.08f, 0.54f), item.Color, root, false, 0.25f);
                    shapes.Box("Lock", new Vector3(0, 0.16f, -0.27f), new Vector3(0.12f, 0.14f, 0.05f), new Color(1f, 0.8f, 0.3f), root, false, 0.3f);
                    break;
                case LootKind.Secret:
                    BuildSecret(item, root);
                    break;
                case LootKind.Collectible:
                    BuildCollectible(item, root);
                    var glow = new GameObject("Glint", typeof(Light)).GetComponent<Light>();
                    glow.transform.SetParent(root, false);
                    glow.type = LightType.Point; glow.range = 2.6f; glow.intensity = 1.3f; glow.color = item.Color;
                    break;
                default:
                    // Coal is dull; metals and gems glow a little so they read in the dark.
                    float shine = Mathf.Clamp01(config.ores[item.Ore].value / 60f) * 0.45f + 0.08f;
                    for (int i = 0; i < 3; i++)
                    {
                        uint k = h >> (i * 5);
                        // Cast before subtraction: unsigned underflow sent nuggets millions of metres away.
                        var offset = new Vector3(((int)(k % 7) - 3) * 0.03f, ((int)(k / 7 % 5) - 2) * 0.03f, ((int)(k / 35 % 7) - 3) * 0.03f);
                        shapes.Box("Nugget", offset, Vector3.one * s * (1 - i * 0.22f), Quaternion.Euler(k % 90, k / 3 % 90, k / 11 % 90), item.Color, root, false, shine);
                    }
                    break;
            }
            item.View = root.gameObject;
            itemColliders[collider] = item;
        }

        private void BuildCollectible(LootItem item, Transform root)
        {
            var c = item.Color;
            switch (item.Collectible)
            {
                case 0: // old helmet
                    shapes.Ball("Helmet", Vector3.zero, new Vector3(0.5f, 0.32f, 0.5f), c, root, false, 0.2f);
                    shapes.Box("Brim", new Vector3(0, -0.08f, 0.04f), new Vector3(0.58f, 0.04f, 0.6f), c * 0.8f, root);
                    shapes.Box("Lamp", new Vector3(0, 0.02f, 0.25f), new Vector3(0.12f, 0.1f, 0.06f), new Color(1f, 0.9f, 0.5f), root, false, 0.8f);
                    break;
                case 1: // fossil shell
                    shapes.Ball("Shell", Vector3.zero, new Vector3(0.46f, 0.18f, 0.4f), c, root, false, 0.2f);
                    for (int i = -1; i <= 1; i++)
                        shapes.Box("Rib", new Vector3(i * 0.1f, 0.07f, 0), new Vector3(0.03f, 0.05f, 0.36f), Quaternion.Euler(0, i * 20, 0), c * 0.75f, root);
                    break;
                case 2: // glowing crystal
                    for (int i = 0; i < 3; i++)
                        shapes.Box("Crystal", new Vector3((i - 1) * 0.08f, 0.05f, 0), new Vector3(0.1f, 0.42f - i * 0.08f, 0.1f), Quaternion.Euler(0, i * 30, (i - 1) * 22), c, root, false, 0.9f);
                    break;
                case 3: // mechanism fragment
                    shapes.Box("Gear", Vector3.zero, new Vector3(0.4f, 0.07f, 0.4f), c, root, false, 0.25f);
                    shapes.Box("Gear", Vector3.zero, new Vector3(0.4f, 0.07f, 0.4f), Quaternion.Euler(0, 45, 0), c, root, false, 0.25f);
                    shapes.Ball("Axle", Vector3.zero, new Vector3(0.14f, 0.12f, 0.14f), c * 0.6f, root);
                    break;
                default: // mysterious key
                    shapes.Box("Key shaft", new Vector3(0, 0, 0.08f), new Vector3(0.07f, 0.07f, 0.4f), c, root, false, 0.4f);
                    shapes.Ball("Key ring", new Vector3(0, 0, -0.18f), new Vector3(0.24f, 0.07f, 0.24f), c, root, false, 0.4f);
                    shapes.Box("Key bit", new Vector3(0.07f, 0, 0.24f), new Vector3(0.1f, 0.06f, 0.08f), c, root, false, 0.4f);
                    break;
            }
        }

        /// <summary>Secrets face the middle of the yard, where the player comes from; +z is their front.</summary>
        private void BuildSecret(LootItem item, Transform root)
        {
            var secret = Secrets.All[item.Secret];
            var c = item.Color;
            if (secret.Look != SecretLook.Letter) root.rotation = Quaternion.LookRotation(new Vector3(-item.Position.x, 0, -item.Position.z));
            void Glow(Color color, float range, float intensity)
            {
                var light = new GameObject("Secret glow", typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(root, false);
                light.transform.localPosition = new Vector3(0, .25f, .3f);
                light.type = LightType.Point; light.range = range; light.intensity = intensity; light.color = color;
            }
            switch (secret.Look)
            {
                case SecretLook.Gnome:
                    var coat = new Color(.2f, .38f, .78f);
                    shapes.Ball("Gnome coat", new Vector3(0, -.08f, 0), new Vector3(.3f, .34f, .28f), coat, root);
                    shapes.Box("Gnome belt", new Vector3(0, -.08f, 0), new Vector3(.29f, .04f, .27f), new Color(.2f, .14f, .1f), root);
                    foreach (float x in new[] { -.075f, .075f })
                        shapes.Ball("Gnome boot", new Vector3(x, -.23f, .04f), new Vector3(.1f, .06f, .14f), new Color(.18f, .12f, .09f), root);
                    shapes.Ball("Gnome face", new Vector3(0, .12f, .02f), new Vector3(.17f, .17f, .16f), new Color(1f, .78f, .64f), root);
                    shapes.Ball("Gnome nose", new Vector3(0, .12f, .1f), new Vector3(.055f, .05f, .05f), new Color(1f, .6f, .52f), root);
                    shapes.Ball("Gnome beard", new Vector3(0, .03f, .08f), new Vector3(.17f, .17f, .09f), new Color(.96f, .95f, .92f), root);
                    // A soft pointed cap: ever smaller layers leaning back.
                    for (int i = 0; i < 5; i++)
                        shapes.Ball("Gnome hat", new Vector3(0, .2f + i * .055f, -i * .012f), new Vector3(.2f - i * .037f, .1f, .2f - i * .037f), c, root);
                    break;
                case SecretLook.Letter:
                    // Sticks out of the mailbox slot.
                    shapes.Box("Letter paper", new Vector3(0, .13f, -.3f), new Vector3(.2f, .13f, .1f), Quaternion.Euler(0, 0, 8), c, root);
                    shapes.Ball("Letter wax seal", new Vector3(0, .13f, -.355f), new Vector3(.04f, .04f, .015f), new Color(.75f, .12f, .12f), root);
                    break;
                case SecretLook.Capsule:
                    shapes.Ball("Capsule metal", new Vector3(0, -.18f, 0), new Vector3(.3f, .3f, .62f), c, root);
                    foreach (float z in new[] { -.15f, .15f })
                        shapes.Box("Capsule band metal", new Vector3(0, -.18f, z), new Vector3(.31f, .31f, .04f), c * .7f, root);
                    shapes.Ball("Capsule cap", new Vector3(0, -.18f, .3f), new Vector3(.22f, .22f, .1f), new Color(.8f, .25f, .2f), root);
                    shapes.Box("Capsule label", new Vector3(0, -.03f, 0), new Vector3(.14f, .012f, .2f), new Color(.95f, .9f, .7f), root);
                    break;
                case SecretLook.Skull:
                    shapes.Ball("Fossil skull", new Vector3(0, -.1f, 0), new Vector3(.6f, .45f, .72f), c, root);
                    shapes.Box("Fossil jaw", new Vector3(0, -.28f, .22f), new Vector3(.4f, .1f, .38f), c * .9f, root);
                    foreach (float x in new[] { -.14f, .14f })
                    {
                        shapes.Ball("Skull socket", new Vector3(x, -.02f, .3f), new Vector3(.16f, .14f, .1f), new Color(.1f, .08f, .07f), root);
                        shapes.Box("Fossil horn", new Vector3(x * 1.9f, .15f, -.05f), new Vector3(.07f, .36f, .07f), Quaternion.Euler(-20, 0, x < 0 ? 28 : -28), c * .85f, root);
                    }
                    for (int i = 0; i < 5; i++)
                        shapes.Box("Fossil tooth", new Vector3(-.14f + i * .07f, -.2f, .38f), new Vector3(.035f, .08f, .035f), new Color(.98f, .95f, .86f), root);
                    break;
                case SecretLook.Statue:
                    shapes.Box("Statue plinth stone", new Vector3(0, -.25f, 0), new Vector3(.52f, .2f, .52f), new Color(.42f, .43f, .46f), root);
                    foreach (float x in new[] { -.075f, .075f })
                        shapes.Box("Golden foot metal", new Vector3(x, -.05f, 0), new Vector3(.12f, .2f, .15f), c * .8f, root, false, .06f);
                    shapes.Box("Golden body metal", new Vector3(0, .2f, 0), new Vector3(.3f, .3f, .17f), c, root, false, .08f);
                    foreach (float x in new[] { -.21f, .21f })
                        shapes.Box("Golden arm metal", new Vector3(x, .2f, 0), new Vector3(.11f, .3f, .13f), c * .88f, root, false, .06f);
                    shapes.Box("Golden head metal", new Vector3(0, .5f, 0), new Vector3(.3f, .3f, .3f), c, root, false, .1f);
                    shapes.Box("Golden hair metal", new Vector3(0, .66f, -.02f), new Vector3(.32f, .05f, .3f), c * .75f, root, false, .05f);
                    shapes.Box("Statue smile", new Vector3(0, .42f, .151f), new Vector3(.1f, .025f, .01f), new Color(.35f, .2f, .08f), root);
                    foreach (float x in new[] { -.07f, .07f })
                        shapes.Box("Statue eye", new Vector3(x, .52f, .151f), new Vector3(.05f, .06f, .01f), new Color(.2f, .12f, .05f), root);
                    Glow(c, 3.2f, 1.4f);
                    break;
                case SecretLook.Mushroom:
                    var stem = new Color(.9f, .95f, .88f);
                    shapes.Box("Mushroom stem", new Vector3(0, -.15f, 0), new Vector3(.11f, .4f, .11f), stem, root, false, .2f);
                    shapes.Ball("Mushroom cap", new Vector3(0, .07f, 0), new Vector3(.5f, .2f, .5f), c, root, false, .9f);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 1.26f;
                        shapes.Ball("Mushroom spot", new Vector3(Mathf.Cos(a) * .15f, .15f, Mathf.Sin(a) * .15f), Vector3.one * .06f, new Color(.95f, 1f, .95f), root, false, .5f);
                    }
                    foreach (var at in new[] { new Vector3(.27f, -.27f, .1f), new Vector3(-.22f, -.29f, .16f) })
                    {
                        shapes.Box("Mushroom stem", at, new Vector3(.04f, .12f, .04f), stem, root);
                        shapes.Ball("Mushroom cap", at + Vector3.up * .07f, new Vector3(.14f, .06f, .14f), c, root, false, .9f);
                    }
                    Glow(c, 3.5f, 1.6f);
                    break;
                default: // old safe
                    shapes.Box("Safe metal", new Vector3(0, -.05f, 0), new Vector3(.6f, .6f, .55f), c, root);
                    shapes.Box("Safe door metal", new Vector3(0, -.05f, .28f), new Vector3(.48f, .48f, .03f), c * 1.25f, root);
                    shapes.Ball("Safe dial", new Vector3(.06f, -.02f, .3f), new Vector3(.12f, .12f, .03f), new Color(.8f, .62f, .3f), root, false, .15f);
                    shapes.Box("Safe handle metal", new Vector3(-.13f, -.05f, .31f), new Vector3(.03f, .16f, .03f), new Color(.75f, .72f, .66f), root);
                    foreach (var at in new[] { new Vector3(-.2f, .15f, .29f), new Vector3(.18f, -.24f, .29f), new Vector3(.3f, .1f, .1f) })
                        shapes.Ball("Rust", at, new Vector3(.12f, .08f, .04f), new Color(.55f, .28f, .13f), root);
                    break;
            }
        }

        private void HideItem(LootItem item)
        {
            itemColliders.Remove(item.View.GetComponent<Collider>());
            Destroy(item.View);
            item.View = null;
        }

        private void UpdateVisibility()
        {
            nextVisibility = Time.unscaledTime + 0.4f;
            var eye = view.transform.position;
            foreach (var item in loot.Items)
            {
                if (item.Taken || !item.Exposed) continue;
                float distance = (item.Position - eye).sqrMagnitude;
                if (item.View == null && distance < 18 * 18) ShowItem(item);
                else if (item.View != null && distance > 24 * 24) HideItem(item);
            }
        }

        // ---------- Items ----------

        /// <summary>Scanner in the backpack: marks ore and treasures through the rock for a few seconds.</summary>
        public void Scan()
        {
            if (!Progress.scanner || !Active) return;
            if (Time.time < scanReadyAt) { hud.Notify("Сканер заряжается · " + Mathf.CeilToInt(ScanWait) + " с"); return; }
            scanUntil = Time.time + config.scanDuration;
            scanReadyAt = Time.time + config.scanCooldown;
            RefreshScan();
            sound.Play("zone", 0.5f, 1.8f, 0);
            hud.Notify(ScanHits.Count > 0 ? "Сканер: рядом " + ScanHits.Count + " " + Plural(ScanHits.Count, "находка", "находки", "находок") : "Сканер: поблизости пусто");
        }

        private void RefreshScan()
        {
            ScanHits.Clear();
            var eye = view.transform.position;
            float range = config.scanner.power;
            loot.Near(eye, range, nearby);
            nearby.Sort((a, b) =>
            {
                int priority = (a.Kind == LootKind.Key ? 0 : 1).CompareTo(b.Kind == LootKind.Key ? 0 : 1);
                return priority != 0 ? priority : (a.Position - eye).sqrMagnitude.CompareTo((b.Position - eye).sqrMagnitude);
            });
            for (int i = 0; i < nearby.Count && ScanHits.Count < 14; i++)
                if (!nearby[i].Taken) ScanHits.Add(nearby[i]);
        }

        public void UseMedkit()
        {
            if (!Active && !hud.PanelOpen) return;
            if (Progress.medkits <= 0) { hud.Notify("Аптечек нет — купи в мастерской"); return; }
            if (!Progress.UseMedkit(config)) { hud.Notify("Здоровье и так полное"); return; }
            sound.Play("pickup", 0.9f, 0.8f);
            hud.Popup(view.transform.position + view.transform.forward * 1.2f, "+" + Mathf.RoundToInt(config.medkit.power), new Color(0.45f, 0.95f, 0.6f));
            SaveNow();
        }

        // ---------- Feedback ----------

        private void ShowLoad(Color color)
        {
            var material = shapes.Mat(color * 0.9f, 0.05f, false);
            foreach (var part in loadParts) part.sharedMaterial = material;
            loadSince = Time.time;
        }

        private void AnimateTool()
        {
            if (tool == null) return;
            float carried = Time.time - loadSince;
            load.gameObject.SetActive(carried < 0.5f);
            load.localScale = Vector3.one * Mathf.Clamp01(carried / 0.08f);
            swing = Mathf.MoveTowards(swing, 1, Time.deltaTime / (Tool.interval * 0.8f));
            float thrust = Mathf.Sin(swing * Mathf.PI) * (swing < 1 ? 1 : 0);
            // Narrow portrait screens pull the shovel towards the centre so the blade stays visible.
            float side = Mathf.Lerp(0.07f, 0.2f, Mathf.InverseLerp(0.5f, 1.6f, view.aspect));
            var bob = new Vector3(Mathf.Sin(walkCycle) * 0.006f, Mathf.Abs(Mathf.Cos(walkCycle)) * 0.006f, 0);
            // The jetpack shakes the view model a little.
            if (Thrusting) bob += Random.insideUnitSphere * 0.003f;
            tool.localPosition = new Vector3(side, -0.2f, 0.34f) + bob + ToolAim * 0.09f * thrust;
            tool.localRotation = (modelDrill || modelWeapon ? Quaternion.Euler(-8, -8, 0) : ToolRotation) * Quaternion.Euler(-35 * thrust, 0, 0);
            if (drillRotor != null) drillRotor.localRotation = Quaternion.Euler(0, 0, Time.time * (DigHeld && Active ? 1600 : 140));
        }

        private void Burst(Vector3 at, Vector3 normal, Color color, int count)
        {
            var material = shapes.Mat(color);
            for (int i = 0; i < count; i++)
            {
                Debris piece;
                if (debrisPool.Count > 0) piece = debrisPool.Pop();
                else
                {
                    var obj = shapes.Box("Debris", Vector3.zero, Vector3.one, color);
                    piece = new Debris { obj = obj.transform, renderer = obj.GetComponent<Renderer>() };
                    piece.renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                piece.obj.gameObject.SetActive(true);
                piece.renderer.sharedMaterial = material;
                piece.obj.position = at + normal * 0.05f;
                piece.obj.rotation = Random.rotation;
                piece.size = Random.Range(0.04f, 0.09f);
                piece.velocity = normal * Random.Range(1.2f, 2.4f) + Random.insideUnitSphere * 1.3f + Vector3.up * 1.2f;
                piece.life = piece.max = Random.Range(0.45f, 0.75f);
                debris.Add(piece);
            }
        }

        private void UpdateDebris()
        {
            float dt = Time.deltaTime;
            for (int i = debris.Count - 1; i >= 0; i--)
            {
                var piece = debris[i];
                piece.life -= dt;
                if (piece.life <= 0)
                {
                    piece.obj.gameObject.SetActive(false);
                    debrisPool.Push(piece);
                    debris.RemoveAt(i);
                    continue;
                }
                piece.velocity.y -= 14 * dt;
                piece.obj.position += piece.velocity * dt;
                piece.obj.localScale = Vector3.one * piece.size * (piece.life / piece.max);
            }
        }

        private void UpdateFlights()
        {
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var flight = flights[i];
                flight.t += Time.unscaledDeltaTime / 0.4f;
                var target = view.transform.position + view.transform.forward * 0.3f - view.transform.up * 0.25f;
                flight.obj.position = Vector3.Lerp(flight.from, target, flight.t * flight.t);
                flight.obj.localScale = Vector3.one * Mathf.Max(0.1f, 1 - flight.t * 0.85f);
                if (flight.t < 1) continue;
                Destroy(flight.obj.gameObject);
                flights.RemoveAt(i);
            }
        }

        private void UpdateAmbience()
        {
            if (InBoss)
            {
                Shader.SetGlobalColor("_NubikAmbient", new Color(.3f, .42f, .43f));
                Shader.SetGlobalColor("_NubikFogColor", new Color(.035f, .075f, .09f));
                Shader.SetGlobalVector("_NubikFog", new Vector4(15, 48));
                sun.intensity = .1f; lamp.intensity = 1.8f; return;
            }
            float depth = Mathf.Max(0, -view.transform.position.y);
            float under = Mathf.Clamp01((depth - 0.3f) / 5f);
            int index = config.ZoneIndex(depth);
            var zone = config.zones[index];
            var previous = config.zones[Mathf.Max(0, index - 1)];
            float blend = index == 0 ? 1 : Mathf.Clamp01((depth - zone.startDepth) / 5f);
            Shader.SetGlobalColor("_NubikAmbient", Color.Lerp(SurfaceAmbient, Color.Lerp(previous.ambient, zone.ambient, blend), under));
            Shader.SetGlobalColor("_NubikFogColor", Color.Lerp(Haze, Color.Lerp(previous.fog, zone.fog, blend), under));
            Shader.SetGlobalVector("_NubikFog", new Vector4(Mathf.Lerp(45, 1.5f, under), Mathf.Lerp(170, Mathf.Lerp(previous.fogEnd, zone.fogEnd, blend), under)));
            sun.intensity = Mathf.Lerp(.9f, Mathf.Lerp(previous.sunIntensity, zone.sunIntensity, blend), under);
            sun.color = Color.Lerp(SunColor, Color.Lerp(previous.sun, zone.sun, blend), under);
            lamp.intensity = Mathf.Lerp(previous.lamp, zone.lamp, blend) * Mathf.Clamp01((depth - 0.8f) / 2.5f);
            if (depth > config.depth - 30)
            {
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 2.2f);
                foreach (var rune in yard.Runes) rune.sharedMaterial.SetColor("_Emission", new Color(0.3f, 1f, 0.85f) * pulse);
                yard.DoorLight.intensity = 1.2f + 0.6f * pulse;
            }
        }

        // ---------- Progress ----------

        private void UpdateStation()
        {
            var feet = body.transform.position;
            Station = Station.None;
            if (!Yard.InsideHouse(feet)) return;
            var flat = new Vector2(feet.x, feet.z);
            if (Vector2.Distance(flat, new Vector2(Yard.CounterPoint.x, Yard.CounterPoint.z)) < 2.4f) Station = Station.Counter;
            else if (Vector2.Distance(flat, new Vector2(Yard.WorkbenchPoint.x, Yard.WorkbenchPoint.z)) < 2.4f) Station = Station.Workbench;
        }

        /// <summary>A trip counts when the player comes back up from below three metres.</summary>
        private void TrackTrips()
        {
            var feet = body.transform.position;
            if (feet.y < -3) wentDown = true;
            else if (wentDown && feet.y > -0.2f && body.isGrounded)
            {
                wentDown = false;
                Progress.expeditions++;
                saveDirty = true;
            }
        }

        private void TrackDepth()
        {
            int depth = Depth;
            if (depth > Progress.maxDepth)
            {
                int oldZone = config.ZoneIndex(Progress.maxDepth), newZone = config.ZoneIndex(depth);
                Progress.maxDepth = depth;
                saveDirty = true;
                if (newZone > oldZone)
                {
                    var zone = config.zones[newZone];
                    hud.Announce(UiGlyph.Kind.Down, new Color(0.43f, 0.86f, 0.72f), "НОВАЯ ЗОНА  ·  " + zone.startDepth + " м", zone.nameRu, zone.noteRu);
                    sound.Play("zone", 1, 1, 0);
                    SaveNow();
                }
            }
            if (!endingShown && Depth >= config.depth - 2)
            {
                endingShown = true;
                hud.Announce(UiGlyph.Kind.Key, Amber, "ДВЕРЬ ПЯТИ ПЕЧАТЕЙ", "За дверью кто-то ждёт",
                    Progress.KeyCount == 5 ? "Все ключи собраны. Подойди к двери." : "Нужно пять ключей. Подсказки есть в дневнике.");
            }
        }

        private void TrackSites()
        {
            sites.UpdateVisibility(Depth);
            CurrentSite = -1;
            for (int i = 0; i < MineSites.All.Length; i++)
            {
                var site = MineSites.All[i];
                if (!site.Contains(body.transform.position)) continue;
                CurrentSite = i;
                if (!Active || !Progress.DiscoverSite(i)) break;
                hud.Announce(UiGlyph.Kind.Helmet, site.Accent, "МЕСТО ОТКРЫТО · " + site.Depth + " м", site.Name, site.Note);
                sound.Play("zone", 1, 1, 0);
                SaveNow();
                break;
            }
        }

        private void UpdateHint()
        {
            if (InBoss) { Hint = boss.Battle.Cue + (Battle.Phase == BattlePhase.Warning ? " · " + Battle.Remaining.ToString("0.0") + " с" : ""); return; }
            if (NearDoor) { Hint = Progress.finished ? "Печать снята · Ктулху побеждён" : Progress.KeyCount == 5 ? "E — открыть дверь пяти печатей" : "Дверь ждёт ключи: " + Progress.KeyCount + " / 5"; return; }
            if (Refuelling) { Hint = "База · заправка бензином " + Mathf.FloorToInt(Fuel) + " / " + FuelMax + " л"; return; }
            int free = Progress.FreeSlots(config);
            if (Progress.coins == 0 && Progress.OrePieces == 0 && Progress.maxDepth == 0)
                Hint = TouchMode ? "Наведи прицел на землю и держи КОПАТЬ"
                    : FreeMouse ? "ЛКМ — копать, зажатая ПКМ — осмотреться" : "Наведи прицел на землю и держи ЛКМ";
            else if (Station == Station.Counter)
                Hint = TouchMode ? "Нажми «Скупка»" : "E — скупка руды";
            else if (Station == Station.Workbench)
                Hint = TouchMode ? "Нажми «Мастерская»" : "E — мастерская";
            else if (Health < MaxHealth * 0.3f && Underground)
                Hint = Progress.medkits > 0 ? (TouchMode ? "Мало здоровья — нажми «Аптечка»" : "Мало здоровья — Q, аптечка") : "Мало здоровья — осторожнее с прыжками вниз";
            else if (free <= 0 && Progress.OrePieces > 0)
                Hint = "Рюкзак полон — отнеси руду в дом, к скупщику";
            else if (Progress.OrePieces > 0 && !Underground)
                Hint = "Продай руду в доме: вход за патио, скупщик слева";
            else if (Underground && Fuel < Mathf.Max(FuelMax * .25f, Depth / config.jetMaxRise * Progress.JetConsumption(config)))
                Hint = Fuel <= 0 ? "Бак пуст · поднимайся по ступенькам или вызови спасателей через паузу" : "Мало бензина для подъёма · береги запас и возвращайся на базу";
            else if (CurrentSite >= 0 && !Progress.HasSpecial(MineSites.All[CurrentSite].CacheId))
                Hint = "Тайник справа у дальней стены · нужно 2 места в рюкзаке";
            else if (NextSite >= 0 && Mathf.Abs(Depth - MineSites.All[NextSite].Depth) <= 3)
                Hint = "Здесь есть старый проход — ищи свет фонарей";
            else if (!HasJetpack && Depth > 6 && Depth < 10)
                Hint = "Оставляй ступеньки для возвращения";
            else if (Progress.CanDeliver && !Underground)
                Hint = "Заказ скупщика собран · отнеси его в дом";
            // Where the keys are is for the journal to tell, not the screen.
            else Hint = "";
        }

        /// <summary>The click that starts or resumes mouse play; the page takes the pointer lock in the same click.</summary>
        public void Engage()
        {
            engaged = true;
            MenuOpen = false;
            engagedFrame = Time.frameCount;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
#if UNITY_EDITOR || !UNITY_WEBGL
            if (!TouchMode) WebInput.LockNow();
#endif
        }

        public void OpenMenu()
        {
            MenuOpen = true;
            hud.ClearInput();
            ReleaseMouse();
        }

        private void ReleaseMouse()
        {
            wantLock = false;
            WebInput.WantLock(false);
        }

        public bool CanRescue => Underground;

        /// <summary>Rescuers haul the player home; the ore stays in the mine.</summary>
        public void CallRescue()
        {
            if (!CanRescue) return;
            MenuOpen = false;
            WakeAtHome("СПАСАТЕЛИ", "Тебя подняли и отвезли домой", true);
            Engage();
        }

        private void WakeAtHome(string kicker, string title, bool dropOre)
        {
            if (InBoss) { boss.Exit(); BuildTool(); }
            int lost = dropOre ? Progress.DropOre() : 0;
            Progress.health = MaxHealth;
            wentDown = false;
            Teleport(Yard.HomeSpawn);
            yaw = Yard.HomeYaw; pitch = 5;
            ApplyView();
            Fuel = FuelMax;
            Thrusting = false;
            hud.FadeIn(1.2f);
            sound.Play("faint", 1, 1, 0);
            hud.Announce(UiGlyph.Kind.Heart, Danger, kicker, title,
                lost > 0 ? "Руда из рюкзака осталась в шахте: " + lost + " шт." : "Рюкзак цел. Отдохни и возвращайся.");
            SaveNow();
        }

        public void OpenHouse()
        {
            ReleaseMouse();
            hud.ClearInput();
            hud.ShowHouse(Station == Station.Counter ? Progress.CanDeliver ? MineHud.OrderPage : MineHud.SellPage : MineHud.UpgradePage);
        }

        public void CloseHouse() => hud.ClosePanel();

        public void SellOre()
        {
            int amount = Progress.Sell(config);
            if (amount <= 0) return;
            SaveNow();
            sound.Play("sell", 1, 1, 0);
            hud.Notify("Руда продана: +" + amount + " монет");
        }

        public void DeliverOrder()
        {
            var ore = Progress.HasQuest ? config.ores[Progress.questOre] : null;
            int paid = Progress.DeliverQuest(config);
            if (paid <= 0) return;
            SaveNow();
            sound.Play("sell", 1, 1.1f, 0);
            hud.Notify("Заказ выполнен: " + ore.nameRu + " · +" + paid + " монет");
        }

        public void SkipOrder()
        {
            if (!Progress.HasQuest) return;
            Progress.SkipQuest(config);
            SaveNow();
            sound.Play("pickup", 0.6f, 0.8f, 0);
            hud.Notify("Скупщик выдал другой заказ");
        }

        /// <summary>Testing aid: the workshop shows a free upgrade button on every track. Turn off before release.</summary>
        public const bool TestUpgrades = true;

        public void Buy(Track track) => Upgrade(track, false);

        /// <summary>Test button: the next level without paying.</summary>
        public void BuyFree(Track track) { if (TestUpgrades) Upgrade(track, true); }

        private void Upgrade(Track track, bool free)
        {
            if (!Progress.Upgrade(track, config, free)) return;
            if (track == Track.Fuel) Fuel = FuelMax;
            SaveNow();
            sound.Play("buy", 1, 1, 0);
            if (track == Track.Tool) BuildTool();
            hud.Notify((free ? "Тест · " : "") + (track == Track.Tool ? "Новый инструмент: " + Tool.nameRu : track == Track.Backpack ? "Рюкзак стал вместительнее"
                : track == Track.Jetpack ? "Джетпак расходует меньше бензина" : track == Track.Fuel ? "Общий бензобак увеличен" : "Здоровье выросло"));
        }

        public void BuyScanner()
        {
            if (!Progress.BuyScanner(config)) return;
            SaveNow();
            sound.Play("buy", 1, 1, 0);
            hud.Notify(TouchMode ? "Сканер в рюкзаке: кнопка «Скан»" : "Сканер в рюкзаке: клавиша F");
        }

        public void BuyMedkit()
        {
            if (!Progress.BuyMedkit(config)) return;
            SaveNow();
            sound.Play("buy", 0.8f, 1.2f, 0);
        }

        /// <summary>Localhost-only test helper (see the WebGL template): carves a shaft and drops the player to that depth.</summary>
        public void DebugDig(string value)
        {
            // The page may call before the first frame on slow devices; run it once the world exists.
            if (terrain == null) { pendingDebugDig = value; return; }
            if (!int.TryParse(value, out int metres)) return;
            metres = Mathf.Clamp(metres, 1, config.depth);
            var shaft = new Vector3(0.8f, 0, -1.2f);
            for (float y = 0; y > -metres - 1.5f; y -= 0.5f)
            {
                terrain.Dig(new Vector3(shaft.x, y, shaft.z), 1.3f, 999);
                RevealLoot(new Vector3(shaft.x, y, shaft.z), 1.3f);
            }
            RebuildDirty();
            Teleport(new Vector3(shaft.x, -metres + 0.2f, shaft.z));
            saveDirty = true;
        }

        /// <summary>Localhost-only test helper: stands the player at a named spot (counter, workbench, yard).</summary>
        public void DebugGoto(string spot)
        {
            if (terrain == null) { pendingGoto = spot; return; }
            foreach (var site in MineSites.All)
                if (spot == "site" + site.Depth) { DebugPlace(site.Origin + new Vector3(0, .2f, .4f), 0, 8); return; }
            if (spot == "counter") DebugPlace(Yard.CounterPoint + Vector3.up * 0.15f, 0, 5);
            else if (spot == "workbench") DebugPlace(Yard.WorkbenchPoint + Vector3.up * 0.15f, 0, 5);
            else DebugPlace(Yard.SurfaceSpawn, Yard.SurfaceYaw, 8);
        }

        /// <summary>Test helper: places the player without touching progress.</summary>
        public void DebugPlace(Vector3 feet, float newYaw, float newPitch)
        {
            Teleport(feet);
            SetView(newYaw, newPitch);
        }

        public void SetView(float newYaw, float newPitch)
        {
            yaw = newYaw;
            pitch = newPitch;
            ApplyView();
        }

        public void ToggleSound()
        {
            Progress.muted = !Progress.muted;
            GameAudio.SetMuted(Progress.muted);
            SaveNow();
            hud.Notify(Progress.muted ? "Звук выключен" : "Звук включён");
        }

        /// <summary>
        /// Wipes the save and loads the world again from scratch. Sound settings survive: they are not progress.
        /// </summary>
        public void RestartGame()
        {
            restarting = true;
            if (InBoss) boss.Exit();
            ProgressStore.Reset(config, Progress.muted);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void SaveNow()
        {
            // A restart already wrote the fresh save; the old world must not overwrite it on the way out.
            if (restarting) return;
            var feet = InBoss ? DoorLanding : body.transform.position;
            Progress.hasResume = Standable(feet);
            Progress.resume = feet;
            Progress.resumeYaw = yaw;
            Progress.terrain.Clear();
            foreach (int chunk in terrain.EditedChunks()) Progress.terrain.Add(new ChunkSave { chunk = chunk, data = terrain.Encode(chunk) });
            ProgressStore.Save(Progress, config);
            saveDirty = false;
            nextAutosave = Time.unscaledTime + 6;
        }

        private void OnApplicationPause(bool paused) { if (paused && body != null) SaveNow(); }
        private void OnApplicationFocus(bool focused) { if (!focused && body != null) SaveNow(); }
        private void OnApplicationQuit() { if (body != null) SaveNow(); }
        private void OnDestroy() { boss?.Dispose(); sites?.Dispose(); yard?.Dispose(); shapes?.Dispose(); }
    }
}
