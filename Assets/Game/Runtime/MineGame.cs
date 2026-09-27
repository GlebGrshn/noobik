using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Nubik
{
    public sealed class MineGame : MonoBehaviour
    {
        public MineConfig config;
        public Material prototypeMaterial;
        public GameObject cubePrefab;
        public GameObject spherePrefab;
        public GameProgress Progress { get; private set; }
        public bool AtBase { get; private set; }
        public string Hint { get; private set; } = "Подойди к блоку и удерживай КОПАТЬ";
        public int Depth => Mathf.Clamp(Mathf.FloorToInt((hero.position.z + 1) / 2), 0, config.rows);
        public int TargetHealth => selected >= 0 ? health[selected] : 0;
        private readonly Dictionary<int, GameObject> blocks = new Dictionary<int, GameObject>();
        private readonly Dictionary<Collider, int> ids = new Dictionary<Collider, int>();
        private readonly Dictionary<int, int> health = new Dictionary<int, int>();
        private readonly List<Material> materials = new List<Material>();
        private Transform hero, pick;
        private Camera view;
        private MineHud hud;
        private YandexBridge platform;
        private Vector3 returnPosition;
        private GameObject marker;
        private int selected = -1;
        private float nextHit, swing;
        private bool mining;
        private Material dirt, earth, gold, teal, dark, skin;

        private void Start()
        {
            Application.targetFrameRate = 60;
            Input.simulateMouseWithTouches = false;
            Progress = ProgressStore.Load(config);
            BuildWorld();
            platform = gameObject.AddComponent<YandexBridge>();
            hud = gameObject.AddComponent<MineHud>();
            hud.Setup(this);
            platform.Ready();
        }

        private Material Mat(string name, Color color)
        {
            var result = new Material(prototypeMaterial) { name = name, color = color };
            result.SetFloat("_Smoothness", 0.12f);
            materials.Add(result);
            return result;
        }

        private GameObject Shape(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Transform parent = null, bool collision = false)
        {
            // Serialized prefab references keep native mesh/collider types in stripped WebGL builds.
            var obj = Instantiate(type == PrimitiveType.Sphere ? spherePrefab : cubePrefab);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Destroy(obj.GetComponent<Collider>());
            return obj;
        }

        private static float Floor(float z) => -Mathf.Max(0, z) * 0.16f;
        private Vector3 BlockPosition(int id)
        {
            float z = id / config.columns * 2f;
            return new Vector3((id % config.columns - config.columns / 2) * 2, Floor(z) + 0.78f, z);
        }

        private void BuildWorld()
        {
            dirt = Mat("Warm clay", new Color(0.57f, 0.32f, 0.19f));
            earth = Mat("Deep clay", new Color(0.40f, 0.23f, 0.17f));
            gold = Mat("Reward amber", new Color(1f, 0.69f, 0.18f));
            teal = Mat("Miner teal", new Color(0.08f, 0.61f, 0.59f));
            dark = Mat("Ink", new Color(0.09f, 0.16f, 0.20f));
            skin = Mat("Warm skin", new Color(0.91f, 0.66f, 0.40f));
            var floorMat = Mat("Sandstone", new Color(0.71f, 0.54f, 0.35f));
            Shape("Base platform", PrimitiveType.Cube, new Vector3(0, -0.4f, -4), new Vector3(13, 0.6f, 7), floorMat);
            for (int row = 0; row <= config.rows; row++)
            {
                float z = row * 2;
                var floor = Shape("Ramp floor " + row, PrimitiveType.Cube, new Vector3(0, Floor(z) - 0.35f, z), new Vector3(12, 0.5f, 2.05f), floorMat);
                floor.transform.rotation = Quaternion.Euler(9.09f, 0, 0);
                foreach (int side in new[] { -1, 1 })
                    Shape("Mine border", PrimitiveType.Cube, new Vector3(side * 5.8f, Floor(z) + 0.1f, z), new Vector3(0.6f, 0.8f, 1.95f), earth);
            }
            for (int id = 0; id < config.BlockCount; id++)
            {
                if (Progress.destroyed.Contains(id)) continue;
                var block = Shape("Dirt " + id, PrimitiveType.Cube, BlockPosition(id), new Vector3(1.85f, 1.55f, 1.85f), config.Hash(id) % 4 == 0 ? earth : dirt, collision: true);
                blocks[id] = block;
                ids[block.GetComponent<Collider>()] = id;
                health[id] = config.dirtHealth;
                if (config.Loot(id) > 0 || config.IsCollection(id))
                {
                    var gem = Shape(config.IsCollection(id) ? "Old helmet" : "Visible find", PrimitiveType.Cube, new Vector3(0, 0.55f, 0), new Vector3(0.27f, 0.24f, 0.27f), config.IsCollection(id) ? teal : gold, block.transform);
                    gem.transform.localRotation = Quaternion.Euler(0, 35, 15);
                }
            }
            // Original temporary miner, built from primitives; replace with Blender assets later.
            hero = new GameObject("Miner").transform;
            hero.position = new Vector3(0, 0, -2.4f);
            Shape("Coat", PrimitiveType.Cube, new Vector3(0, 0.75f, 0), new Vector3(0.72f, 0.8f, 0.48f), teal, hero);
            Shape("Head", PrimitiveType.Sphere, new Vector3(0, 1.43f, 0), Vector3.one * 0.62f, skin, hero);
            Shape("Helmet", PrimitiveType.Sphere, new Vector3(0, 1.66f, 0), new Vector3(0.76f, 0.33f, 0.72f), gold, hero);
            Shape("Headlamp", PrimitiveType.Cube, new Vector3(0, 1.62f, 0.37f), new Vector3(0.23f, 0.20f, 0.12f), floorMat, hero);
            for (int side = -1; side <= 1; side += 2)
            {
                Shape("Boot", PrimitiveType.Cube, new Vector3(side * 0.22f, 0.19f, 0.08f), new Vector3(0.29f, 0.36f, 0.49f), dark, hero);
                Shape("Glove", PrimitiveType.Sphere, new Vector3(side * 0.47f, 0.75f, 0.12f), Vector3.one * 0.27f, skin, hero);
            }
            pick = new GameObject("Pickaxe pivot").transform;
            pick.SetParent(hero, false);
            pick.localPosition = new Vector3(0.48f, 0.95f, 0.24f);
            Shape("Handle", PrimitiveType.Cube, Vector3.zero, new Vector3(0.10f, 0.85f, 0.10f), earth, pick);
            Shape("Pick head", PrimitiveType.Cube, new Vector3(0, 0.4f, 0), new Vector3(0.65f, 0.13f, 0.15f), gold, pick);
            marker = Shape("Selected block", PrimitiveType.Cube, Vector3.zero, new Vector3(1.94f, 0.06f, 1.94f), gold);
            marker.SetActive(false);
            Shape("End marker", PrimitiveType.Cube, new Vector3(0, Floor(config.rows * 2) + 1, config.rows * 2 + 1), new Vector3(3.4f, 2f, 0.3f), teal);
            var cameraObj = new GameObject("Game Camera", typeof(Camera), typeof(AudioListener));
            view = cameraObj.GetComponent<Camera>();
            cameraObj.tag = "MainCamera";
            view.orthographic = true;
            view.backgroundColor = new Color(0.075f, 0.13f, 0.17f);
            view.clearFlags = CameraClearFlags.SolidColor;
            view.farClipPlane = 160;
            view.transform.position = hero.position + new Vector3(6, 12, -9);
            view.transform.rotation = Quaternion.LookRotation(new Vector3(-6, -12, 11));
            var light = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(48, -28, 0);
            RenderSettings.ambientLight = new Color(0.65f, 0.70f, 0.75f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        }

        private void Update()
        {
            if (hero == null) return;
            view.orthographicSize = Mathf.Max(8, 6.6f / view.aspect);
            view.transform.position = Vector3.Lerp(view.transform.position, hero.position + new Vector3(6, 12, -9), 1 - Mathf.Exp(-6 * Time.unscaledDeltaTime));
            if (YandexBridge.Paused) { hud.ClearInput(); return; }
            if (AtBase || hud.ModalOpen) return;
            var movement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")) + hud.Stick.Value;
            movement = Vector2.ClampMagnitude(movement, 1);
            // Align stick/WASD with the camera's ground plane.
            var forward = view.transform.forward; forward.y = 0; forward.Normalize();
            var right = view.transform.right; right.y = 0; right.Normalize();
            Vector3 delta = (right * movement.x + forward * movement.y) * config.moveSpeed * Time.deltaTime;
            var proposed = hero.position;
            if (CanStand(proposed + new Vector3(delta.x, 0, 0))) proposed.x += delta.x;
            if (CanStand(proposed + new Vector3(0, 0, delta.z))) proposed.z += delta.z;
            proposed.y = Floor(proposed.z);
            hero.position = proposed;
            if (delta.sqrMagnitude > 0.001f) hero.rotation = Quaternion.Slerp(hero.rotation, Quaternion.LookRotation(delta), Time.deltaTime * 14);
            if (Depth > Progress.maxDepth) { Progress.maxDepth = Depth; Save(); }
            SelectTarget();
            bool mouseDig = Input.touchCount == 0 && Input.GetMouseButton(0) && !EventSystem.current.IsPointerOverGameObject();
            bool digging = mouseDig || hud.Dig.Held || Input.GetKey(KeyCode.Space);
            mining = digging && selected >= 0 && CanReach(selected);
            if (digging && Time.time >= nextHit) Hit();
            swing = Mathf.MoveTowards(swing, 0, Time.deltaTime * 5);
            pick.localRotation = Quaternion.Euler(-65 * Mathf.Sin(swing * Mathf.PI), 0, -18);
            if (Input.GetKeyDown(KeyCode.R)) RequestReturn();
            if (Depth >= config.rows) Hint = "30 м! Первая зона пройдена. На базе можно продать находки";
        }

        private bool CanStand(Vector3 position)
        {
            if (Mathf.Abs(position.x) > 4.9f || position.z < -6 || position.z > config.rows * 2) return false;
            foreach (var pair in blocks)
            {
                var p = BlockPosition(pair.Key);
                if (Mathf.Abs(position.x - p.x) < 1.22f && Mathf.Abs(position.z - p.z) < 1.22f) return false;
            }
            return true;
        }

        private void SelectTarget()
        {
            int target = -1;
            bool pointedAtWorld = false;
            if (Input.touchCount == 0 && !EventSystem.current.IsPointerOverGameObject())
            {
                if (Physics.Raycast(view.ScreenPointToRay(Input.mousePosition), out var hit, 150) && ids.TryGetValue(hit.collider, out int id))
                { target = id; pointedAtWorld = true; }
            }
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began || EventSystem.current.IsPointerOverGameObject(touch.fingerId)) continue;
                if (Physics.Raycast(view.ScreenPointToRay(touch.position), out var hit, 150) && ids.TryGetValue(hit.collider, out int id))
                { target = id; pointedAtWorld = true; }
            }
            if (Input.touchCount > 0 && !pointedAtWorld && selected >= 0 && blocks.ContainsKey(selected)) target = selected;
            if (target < 0)
            {
                float nearest = 2.7f;
                foreach (var pair in blocks)
                {
                    float distance = Distance(pair.Key);
                    if (distance < nearest) { nearest = distance; target = pair.Key; }
                }
            }
            selected = target;
            marker.SetActive(target >= 0);
            if (target >= 0)
            {
                marker.transform.position = BlockPosition(target) + Vector3.up * 0.83f;
                Hint = Distance(target) > 2.7f ? "Подойди ближе к выбранному блоку" : "Земля · " + health[target] + " / " + config.dirtHealth + "  •  удерживай КОПАТЬ";
            }
            else Hint = "Ищи золотые вкрапления — это находки для продажи";
        }

        private float Distance(int id)
        {
            var difference = BlockPosition(id) - hero.position; difference.y = 0;
            return difference.magnitude;
        }

        private bool CanReach(int id)
        {
            if (Distance(id) > 2.7f) return false;
            Vector3 origin = hero.position + Vector3.up * 0.8f;
            Vector3 direction = BlockPosition(id) - origin;
            return Physics.Raycast(origin, direction.normalized, out var hit, direction.magnitude + 0.1f) && ids.TryGetValue(hit.collider, out int hitId) && hitId == id;
        }

        private void Hit()
        {
            if (selected < 0 || !CanReach(selected)) return;
            nextHit = Time.time + config.hitInterval;
            swing = 1;
            var facing = BlockPosition(selected) - hero.position; facing.y = 0;
            hero.rotation = Quaternion.LookRotation(facing);
            health[selected] -= config.pickDamage[Progress.pickTier];
            if (health[selected] > 0)
            {
                blocks[selected].transform.localScale = new Vector3(1.78f, 1.47f, 1.78f);
                return;
            }
            int id = selected;
            if (!Progress.BreakBlock(id, config)) return;
            ids.Remove(blocks[id].GetComponent<Collider>());
            Destroy(blocks[id]); blocks.Remove(id); health.Remove(id);
            selected = -1;
            Save();
            hud.Notify(config.IsCollection(id) ? "Коллекция: старая каска! +30 монет" : "+" + config.Coins(id) + " монеты" + (config.Loot(id) > 0 ? " · находка в рюкзаке" : ""));
        }

        public void RequestReturn()
        {
            if (mining || hud.Dig.Held || Input.GetKey(KeyCode.Space)) { hud.ShowReturnConfirmation(); return; }
            ReturnToBase();
        }
        public void DigTap()
        {
            if (!AtBase && !YandexBridge.Paused && !hud.ModalOpen && Time.time >= nextHit) Hit();
        }
        public void SetMenuOpen(bool open) => platform.SetInMine(!open && !AtBase);
        public void ReturnToBase()
        {
            if (AtBase) return;
            returnPosition = hero.position;
            AtBase = true;
            Progress.expeditions++;
            hero.position = new Vector3(0, 0, -3);
            marker.SetActive(false);
            hud.ClearInput();
            Save(); platform.SetInMine(false); hud.ShowBase(true);
        }
        public void Descend()
        {
            if (!AtBase) return;
            AtBase = false; hero.position = returnPosition;
            hud.ShowBase(false); platform.SetInMine(true);
        }
        public void Sell() { if (!AtBase) return; int amount = Progress.Sell(); Save(); hud.Notify("Продано на " + amount + " монет"); }
        public void Upgrade() { if (AtBase && Progress.Upgrade(config)) { Save(); hud.Notify("Новая кирка: " + config.pickNames[Progress.pickTier]); } }
        private void Save() => ProgressStore.Save(Progress, config);
        private void OnApplicationPause(bool paused) { if (paused && Progress != null) Save(); }
        private void OnApplicationFocus(bool focused) { if (!focused && Progress != null) Save(); }
        private void OnApplicationQuit() { if (Progress != null) Save(); }
        private void OnDestroy() { foreach (var material in materials) Destroy(material); }
    }
}
