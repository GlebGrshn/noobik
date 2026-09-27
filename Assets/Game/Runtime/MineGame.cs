using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nubik
{
    /// <summary>First-person digging in the backyard: player, shovel, terrain, finds, shop and saves.</summary>
    public sealed class MineGame : MonoBehaviour
    {
        public MineConfig config;
        public Material prototypeMaterial;
        public Material terrainMaterial;
        public GameObject cubePrefab;
        public GameObject spherePrefab;

        public GameProgress Progress { get; private set; }
        public int Depth => Mathf.Clamp(Mathf.FloorToInt(-body.transform.position.y + 0.1f), 0, config.depth);
        public ToolDef Tool => config.tools[Progress.tool];
        public string TargetText { get; private set; } = "";
        public string Hint { get; private set; } = "";
        public bool NearShop { get; private set; }
        public bool InReach { get; private set; }
        public bool TouchMode { get; private set; }
        /// <summary>Mouse play needs a click first; afterwards a lost pointer lock asks for another click.</summary>
        public bool NeedsClick => !TouchMode && !hud.PanelOpen && !(engaged && (WebInput.Locked || WebInput.LockUnavailable));
        public bool FreeMouse => !TouchMode && WebInput.LockUnavailable;
        public bool Active => !hud.PanelOpen && !YandexBridge.Paused && (TouchMode || !NeedsClick);

        private const float MouseDegreesPerPixel = 0.16f, EyeHeight = 1.55f;
        private const int IgnoreRaycastLayer = 2;
        private static readonly Color Sky = new Color(0.56f, 0.77f, 0.95f);
        private static readonly Color Haze = new Color(0.70f, 0.82f, 0.93f);
        private static readonly Color SurfaceAmbient = new Color(0.50f, 0.54f, 0.60f);
        private static readonly Color SunColor = new Color(1f, 0.95f, 0.85f);
        private static readonly Color Amber = new Color(1f, 0.78f, 0.25f);
        // Shovel held low on the right: pointing forward-up-left with the blade face towards the camera.
        private static readonly Vector3 ToolAim = new Vector3(-0.25f, 0.55f, 0.8f).normalized;
        private static readonly Quaternion ToolRotation = Quaternion.LookRotation(ToolAim, new Vector3(-0.2f, 0.4f, -0.9f));

        private VoxelTerrain terrain;
        private TerrainMesher mesher;
        private LootField loot;
        private Shapes shapes;
        private Yard yard;
        private MineHud hud;
        private YandexBridge platform;
        private GameAudio sound;
        private CharacterController body;
        private Transform head, terrainRoot, tool;
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
        private float yaw, pitch, verticalSpeed, nextHit, swing = 1, nextVisibility, nextAutosave, walkCycle, lastDig = -10;
        private bool saveDirty, endingShown, wasActive, engaged, wantLock;
        private int engagedFrame = -10;

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
            shapes = new Shapes(cubePrefab, spherePrefab, prototypeMaterial);
            terrain = new VoxelTerrain(config);
            foreach (var entry in Progress.terrain)
                if (!terrain.Decode(entry.chunk, entry.data)) Debug.LogWarning("Skipped damaged terrain chunk " + entry.chunk);
            terrain.ClearDirty();
            mesher = new TerrainMesher(terrain);
            loot = new LootField(terrain);
            loot.MarkTaken(Progress);
            foreach (var item in loot.Items) item.Exposed = !item.Taken && loot.IsExposed(item);
            yard = new Yard(shapes, config);
            BuildTerrain();
            BuildPlayer();
            hud = gameObject.AddComponent<MineHud>();
            hud.Setup(this);
            SpawnAtStart();
            UpdateAmbience();
            platform.Ready();
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
            AnimateTool();
        }

        private void SpawnAtStart()
        {
            if (Progress.hasResume && Standable(Progress.resume)) { Teleport(Progress.resume); yaw = Progress.resumeYaw; pitch = 15; }
            else if (Progress.expeditions == 0 && Progress.maxDepth == 0 && Standable(Yard.FirstSpawn)) { Teleport(Yard.FirstSpawn); yaw = 0; pitch = 36; }
            else { Teleport(Yard.SurfaceSpawn); yaw = Yard.SurfaceYaw; pitch = 10; }
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
            UpdateDebris();
            if (YandexBridge.Paused) { hud.ClearInput(); return; }
            if (!TouchMode && Input.touchCount > 0) { TouchMode = true; WebInput.WantLock(false); }
            bool want = !TouchMode && !hud.PanelOpen && !WebInput.LockUnavailable;
            if (want != wantLock) { wantLock = want; WebInput.WantLock(want); }
            bool active = Active;
            if (active != wasActive) { wasActive = active; platform.SetInMine(active); }
            if (active)
            {
                Look();
                Move(Time.deltaTime);
                if (Input.GetKeyDown(KeyCode.R)) RequestReturn();
                if (Input.GetKeyDown(KeyCode.E) && NearShop) OpenShop();
                if (Input.GetKeyDown(KeyCode.M)) ToggleSound();
            }
            else hud.ClearInput();
            UpdateTarget();
            // The click that starts play only engages the mouse.
            bool click = !TouchMode && Input.GetMouseButtonDown(0) && Time.frameCount > engagedFrame + 1;
            if (active && (DigHeld || click) && Time.time >= nextHit) Swing();
            AnimateTool();
            var feet = body.transform.position;
            NearShop = feet.y > -0.5f && Vector2.Distance(new Vector2(feet.x, feet.z), new Vector2(yard.ShopPoint.x, yard.ShopPoint.z)) < 2.8f;
            TrackDepth();
            if (Time.unscaledTime >= nextVisibility) UpdateVisibility();
            if (saveDirty && Time.unscaledTime >= nextAutosave) SaveNow();
            UpdateHint();
        }

        private bool DigHeld => TouchMode ? hud.Dig.Held : Input.GetMouseButton(0);

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
            if (body.isGrounded)
            {
                if (verticalSpeed < 0) verticalSpeed = -2;
                if (jump) verticalSpeed = config.jumpSpeed;
            }
            verticalSpeed = Mathf.Max(verticalSpeed - 20 * dt, -40);
            body.Move((horizontal + Vector3.up * verticalSpeed) * dt);
            walkCycle += input.magnitude * dt * 9;
            if (body.transform.position.y < config.FloorY - 3)
            {
                Teleport(Yard.SurfaceSpawn);
                hud.Notify("Выбрались на поверхность");
            }
        }

        private void UpdateTarget()
        {
            TargetText = "";
            InReach = false;
            if (!Physics.Raycast(view.transform.position, view.transform.forward, out var hit, config.reach + 5, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)) return;
            if (hit.distance > config.reach)
            {
                if (terrainColliders.Contains(hit.collider) || itemColliders.ContainsKey(hit.collider)) TargetText = "Подойди ближе";
                return;
            }
            InReach = true;
            if (itemColliders.TryGetValue(hit.collider, out var item)) { TargetText = ItemLabel(item); return; }
            if (!terrainColliders.Contains(hit.collider)) { TargetText = hit.point.y < 0.3f ? "Копать можно между колышками" : ""; return; }
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
            switch (item.Kind)
            {
                case LootKind.Collectible: return "Что-то особенное!";
                case LootKind.Chest: return "Сундук";
                default: return "Находка · " + item.Value + " монет";
            }
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
            nextHit = Time.time + config.hitInterval;
            swing = 0;
            lastDig = Time.time;
            var origin = view.transform.position;
            var direction = view.transform.forward;
            if (!Physics.Raycast(origin, direction, out var hit, config.reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)) return;
            if (itemColliders.TryGetValue(hit.collider, out var item))
            {
                sound.Play("dig_stone", 0.5f, 1.3f);
                Burst(hit.point, hit.normal, item.Color, 5);
                Collect(item);
                return;
            }
            if (!terrainColliders.Contains(hit.collider)) return;
            var center = hit.point + direction * Tool.radius * 0.4f;
            var result = terrain.Dig(center, Tool.radius, Tool.damage);
            var dust = hit.point.y > -0.2f && hit.normal.y > 0.5f ? new Color(0.34f, 0.60f, 0.22f) : result.Rock.color;
            Burst(hit.point, hit.normal, dust, result.Changed ? 7 : 3);
            if (!result.Changed) { sound.Play("dig_stone", 0.7f, 0.55f); return; }
            // Soft ground thuds, harder rock rings lower.
            int hardness = result.Rock.hardness;
            if (hardness <= 3) sound.Play("dig_dirt", 0.8f);
            else sound.Play("dig_stone", 0.75f, Mathf.Lerp(1.05f, 0.72f, Mathf.InverseLerp(6, 32, hardness)));
            RebuildDirty();
            int paid = Progress.AddDigValue(result.Value);
            if (paid > 0) { hud.Popup(hit.point, "+" + paid, Amber); sound.Play("coin", 0.35f, 1, 0.1f); }
            saveDirty = true;
            RevealLoot(center, Tool.radius);
        }

        private void RevealLoot(Vector3 center, float radius, bool collect = true)
        {
            loot.Near(center, radius + 1.2f, nearby);
            foreach (var item in nearby)
            {
                item.Exposed = loot.IsExposed(item);
                if (!item.Exposed) continue;
                if (collect && (item.Position - center).magnitude <= radius + item.Size * 0.5f) Collect(item);
                else if (item.View == null) ShowItem(item);
            }
        }

        private void Collect(LootItem item)
        {
            if (item.Taken) return;
            bool paid = Progress.Collect(item, config);
            item.Taken = true;
            if (item.View == null) ShowItem(item);
            var viewObj = item.View;
            itemColliders.Remove(viewObj.GetComponent<Collider>());
            Destroy(viewObj.GetComponent<Collider>());
            flights.Add(new Flight { obj = viewObj.transform, from = viewObj.transform.position });
            item.View = null;
            if (!paid) return;
            switch (item.Kind)
            {
                case LootKind.Collectible:
                    sound.Play("collect", 1, 1, 0);
                    var def = config.collection[item.Collectible];
                    hud.Notify("Коллекция: " + def.nameRu + "! +" + config.firstDiscoveryCoins + " монет");
                    hud.Popup(item.Position, def.nameRu, def.color);
                    break;
                case LootKind.Chest:
                    sound.Play("chest", 1, 1, 0);
                    hud.Notify("Сундук! +" + item.Value + " монет в рюкзак");
                    hud.Popup(item.Position, "+" + item.Value, item.Color);
                    break;
                default:
                    sound.Play("pickup", 0.9f);
                    hud.Popup(item.Position, "+" + item.Value + " в рюкзак", item.Color);
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
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = Vector3.one * item.Size * 2.2f;
            float s = item.Size;
            switch (item.Kind)
            {
                case LootKind.Chest:
                    shapes.Box("Chest", Vector3.zero, new Vector3(0.75f, 0.48f, 0.5f), new Color(0.52f, 0.32f, 0.18f), root);
                    shapes.Box("Lid", new Vector3(0, 0.27f, 0), new Vector3(0.78f, 0.1f, 0.53f), new Color(0.42f, 0.25f, 0.14f), root);
                    shapes.Box("Band", new Vector3(0, 0.05f, 0), new Vector3(0.8f, 0.08f, 0.54f), item.Color, root, false, 0.25f);
                    shapes.Box("Lock", new Vector3(0, 0.16f, -0.27f), new Vector3(0.12f, 0.14f, 0.05f), new Color(1f, 0.8f, 0.3f), root, false, 0.3f);
                    break;
                case LootKind.Collectible:
                    BuildCollectible(item, root);
                    var glow = new GameObject("Glint", typeof(Light)).GetComponent<Light>();
                    glow.transform.SetParent(root, false);
                    glow.type = LightType.Point; glow.range = 2.6f; glow.intensity = 1.3f; glow.color = item.Color;
                    break;
                default:
                    for (int i = 0; i < 3; i++)
                    {
                        uint k = h >> (i * 5);
                        var offset = new Vector3((k % 7 - 3) * 0.03f, (k / 7 % 5 - 2) * 0.03f, (k / 35 % 7 - 3) * 0.03f);
                        shapes.Box("Nugget", offset, Vector3.one * s * (1 - i * 0.22f), Quaternion.Euler(k % 90, k / 3 % 90, k / 11 % 90), item.Color, root, false, 0.35f);
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

        // ---------- Feedback ----------

        private void AnimateTool()
        {
            if (tool == null) return;
            swing = Mathf.MoveTowards(swing, 1, Time.deltaTime / (config.hitInterval * 0.8f));
            float thrust = Mathf.Sin(swing * Mathf.PI) * (swing < 1 ? 1 : 0);
            // Narrow portrait screens pull the shovel towards the centre so the blade stays visible.
            float side = Mathf.Lerp(0.07f, 0.2f, Mathf.InverseLerp(0.5f, 1.6f, view.aspect));
            var bob = new Vector3(Mathf.Sin(walkCycle) * 0.006f, Mathf.Abs(Mathf.Cos(walkCycle)) * 0.006f, 0);
            tool.localPosition = new Vector3(side, -0.2f, 0.34f) + bob + ToolAim * 0.09f * thrust;
            tool.localRotation = ToolRotation * Quaternion.Euler(-35 * thrust, 0, 0);
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
            float depth = Mathf.Max(0, -view.transform.position.y);
            float under = Mathf.Clamp01((depth - 0.3f) / 5f);
            int index = config.ZoneIndex(depth);
            var zone = config.zones[index];
            var previous = config.zones[Mathf.Max(0, index - 1)];
            float blend = index == 0 ? 1 : Mathf.Clamp01((depth - zone.startDepth) / 5f);
            Shader.SetGlobalColor("_NubikAmbient", Color.Lerp(SurfaceAmbient, Color.Lerp(previous.ambient, zone.ambient, blend), under));
            Shader.SetGlobalColor("_NubikFogColor", Color.Lerp(Haze, Color.Lerp(previous.fog, zone.fog, blend), under));
            Shader.SetGlobalVector("_NubikFog", new Vector4(Mathf.Lerp(45, 1.5f, under), Mathf.Lerp(170, Mathf.Lerp(previous.fogEnd, zone.fogEnd, blend), under)));
            sun.intensity = Mathf.Lerp(1.05f, Mathf.Lerp(previous.sunIntensity, zone.sunIntensity, blend), under);
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
                    hud.Notify("Новая зона: " + config.zones[newZone].nameRu + "!");
                    sound.Play("zone", 1, 1, 0);
                    SaveNow();
                }
            }
            if (!endingShown && body.transform.position.y < -(config.depth - 1.3f))
            {
                endingShown = true;
                Progress.finished = true;
                sound.Play("door", 1, 1, 0);
                SaveNow();
                ReleaseMouse();
                hud.ShowEnding();
            }
        }

        private void UpdateHint()
        {
            int next = Progress.tool + 1;
            if (Progress.coins == 0 && Progress.backpack == 0 && Progress.maxDepth == 0)
                Hint = TouchMode ? "Наведи прицел на землю и держи КОПАТЬ"
                    : FreeMouse ? "ЛКМ — копать, зажатая ПКМ — осмотреться" : "Наведи прицел на землю и держи ЛКМ";
            else if (Progress.backpack > 0 && Progress.expeditions == 0)
                Hint = "Находки продаются в лавке — " + (TouchMode ? "кнопка «Наверх»" : "клавиша R");
            else if (NearShop)
                Hint = TouchMode ? "Нажми «Лавка»" : "E — открыть лавку";
            else if (next < config.tools.Length && Progress.coins + Progress.backpack >= config.tools[next].price)
                Hint = "Хватает на лопату получше — загляни в лавку";
            else Hint = "";
        }

        /// <summary>The click that starts or resumes mouse play; the page takes the pointer lock in the same click.</summary>
        public void Engage()
        {
            engaged = true;
            engagedFrame = Time.frameCount;
#if UNITY_EDITOR || !UNITY_WEBGL
            if (!TouchMode) WebInput.LockNow();
#endif
        }

        private void ReleaseMouse()
        {
            wantLock = false;
            WebInput.WantLock(false);
        }

        public void RequestReturn()
        {
            if (hud.PanelOpen) return;
            if (DigHeld && Time.time - lastDig < 0.6f && Depth > 1)
            {
                ReleaseMouse();
                hud.ShowReturnConfirmation();
                return;
            }
            ReturnToSurface();
        }

        public void ReturnToSurface()
        {
            var feet = body.transform.position;
            if (Depth > 1)
            {
                Progress.hasDive = true;
                Progress.dive = feet;
                Progress.diveYaw = yaw;
                Progress.expeditions++;
            }
            Teleport(Yard.SurfaceSpawn);
            yaw = Yard.SurfaceYaw; pitch = 8;
            ApplyView();
            SaveNow();
            OpenShop();
        }

        public void OpenShop()
        {
            ReleaseMouse();
            hud.ClearInput();
            hud.ShowShop(true);
        }

        public void CloseShop() => hud.ShowShop(false);

        public void Descend()
        {
            if (!Progress.hasDive) return;
            CloseShop();
            if (Standable(Progress.dive)) Teleport(Progress.dive);
            yaw = Progress.diveYaw; pitch = 20;
            ApplyView();
        }

        public void Sell()
        {
            int amount = Progress.Sell();
            if (amount <= 0) return;
            SaveNow();
            sound.Play("sell", 1, 1, 0);
            hud.Notify("Продано на " + amount + " монет");
        }

        public void Upgrade()
        {
            if (!Progress.Upgrade(config)) return;
            SaveNow();
            sound.Play("buy", 1, 1, 0);
            BuildTool();
            hud.Notify("Новая лопата: " + Tool.nameRu + "!");
        }

        /// <summary>Localhost-only test helper (see the WebGL template): carves a shaft and drops the player to that depth.</summary>
        public void DebugDig(string value)
        {
            if (!int.TryParse(value, out int metres)) return;
            metres = Mathf.Clamp(metres, 1, config.depth);
            var shaft = new Vector3(0.8f, 0, -1.2f);
            for (float y = 0; y > -metres - 1.5f; y -= 0.5f)
            {
                terrain.Dig(new Vector3(shaft.x, y, shaft.z), 1.3f, 999);
                RevealLoot(new Vector3(shaft.x, y, shaft.z), 1.3f, false);
            }
            RebuildDirty();
            Teleport(new Vector3(shaft.x, -metres + 0.2f, shaft.z));
            saveDirty = true;
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

        public void SaveNow()
        {
            var feet = body.transform.position;
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
        private void OnDestroy() { shapes?.Dispose(); }
    }
}
