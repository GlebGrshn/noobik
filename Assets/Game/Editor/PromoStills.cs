using System;
using System.IO;
using System.Reflection;
using Nubik;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>
/// High-resolution stills of the game's locations for store pages: the real scene at Ultra, 3840 × 2160 with 8× MSAA,
/// plus a 1920 × 1080 copy averaged down from it. Staged like PromoRecorder (HUD and tool hidden, a worked shaft in
/// disposable progress); the editor save and the pipeline asset are restored afterwards. Never part of a player build.
/// </summary>
[InitializeOnLoad]
public static class PromoStills
{
    private const string ActiveKey = "Nubik.Stills.Active", FolderKey = "Nubik.Stills.Folder", ShotsKey = "Nubik.Stills.Shots";
    private const string SaveKey = "nubik.progress.v1", SettingsKey = "nubik.settings.v1";
    private const int Width = 3840, Height = 2160, Settle = 45;

    private sealed class Shot
    {
        public string name; public Vector3 at, look; public float fov;
        public Shot(string name, Vector3 at, Vector3 look, float fov) { this.name = name; this.at = at; this.look = look; this.fov = fov; }
    }

    private static readonly float Door = -120.5f + 1.6f;
    private static readonly Shot[] Shots =
    {
        new Shot("01_yard_overview", new Vector3(-1.5f, 7.2f, -13.8f), new Vector3(-.5f, 1.2f, 14), 60),
        new Shot("02_yard_house", new Vector3(-10.8f, 2.2f, 7.2f), new Vector3(-3.4f, 2.5f, 18.2f), 56),
        new Shot("03_yard_pond", new Vector3(-9.4f, 1.25f, -5.6f), new Vector3(-12.3f, .05f, -1.3f), 50),
        new Shot("04_yard_dog_garden", new Vector3(9.6f, 1.45f, -6), new Vector3(13.2f, .45f, 2.4f), 54),
        new Shot("05_shaft_to_the_sky", new Vector3(.9f, -9, -1.6f), new Vector3(-.1f, 2, 1.5f), 70),
        new Shot("06_house_buyer", new Vector3(-2.8f, 1.75f, 20.3f), new Vector3(-7.6f, 1, 22.9f), 62),
        new Shot("07_house_workshop", new Vector3(-3.8f, 1.75f, 20.3f), new Vector3(1.8f, 1, 23.1f), 62),
        new Shot("08_roots_camp_18m", Site(18), SiteLook(18), 70),
        new Shot("09_old_mine_48m", Site(48), SiteLook(48), 70),
        new Shot("10_crystal_grotto_88m", Site(88), SiteLook(88), 70),
        new Shot("11_lava_103m", Depths.Lava[0].Center + new Vector3(1.6f, 1.8f, -1.2f), Depths.Lava[0].Center + new Vector3(-.6f, -.5f, .5f), 72),
        new Shot("12_meteor_114m", new Vector3(2.6f, -113.8f, -4.6f), Depths.Meteor, 70),
        new Shot("13_door_of_seals_120m", new Vector3(.7f, Door + .3f, -2), new Vector3(0, Door + .35f, 1.9f), 64),
        new Shot("14_cthulhu", BossEncounter.Origin + new Vector3(.6f, 1.3f, -6.8f), BossEncounter.Origin + new Vector3(0, 4.3f, 6), 60),
    };

    // Mine camps: from the worked shaft, centred on the camp's frame so its posts border the view.
    private static Vector3 Site(int depth) => new Vector3(.1f, -depth + 1.8f, -1.6f);
    private static Vector3 SiteLook(int depth) => new Vector3(.2f, -depth + .6f, 2.4f);

    private static MineGame game;
    private static Camera camera;
    private static RenderTexture msaa, resolved, half;
    private static Texture2D full, small;
    private static int shot, settled, lastFrame = -1;
    private static bool finishing;
    private static string folder, selected;
    private static float renderScale, shadowDistance; private static int msaaCount; private static bool hdr;

    [Serializable] private sealed class Saved { public bool mainExists, backupExists, settingsExist; public string main, backup, settings; }

    static PromoStills()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(ActiveKey, false)) return;
            Restore();
            SessionState.SetBool(ActiveKey, false);
            Time.captureFramerate = 0;
            int code = SessionState.GetInt("Nubik.Stills.Exit", 1);
            Debug.Log(code == 0 ? "NUBIK_STILLS_OK: " + SessionState.GetString(FolderKey, "") : "NUBIK_STILLS_FAILED");
            if (Application.isBatchMode) EditorApplication.Exit(code);
        };
    }

    [MenuItem("Nubik/Capture 4K stills")]
    public static void CaptureAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start the capture in edit mode.");
        string output = null, selection = "";
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "-stillsOutput") output = args[i + 1];
            if (args[i] == "-stillsShots") selection = args[i + 1];
        }
        ProjectSetup.Prepare();
        folder = Path.GetFullPath(output ?? "Recordings/stills-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        if (!folder.StartsWith(Path.GetFullPath("Recordings") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Stills must go inside Recordings.");
        Directory.CreateDirectory(Path.Combine(folder, "3840x2160"));
        Directory.CreateDirectory(Path.Combine(folder, "1920x1080"));
        var saved = new Saved
        {
            mainExists = PlayerPrefs.HasKey(SaveKey), backupExists = PlayerPrefs.HasKey(SaveKey + ".backup"), settingsExist = PlayerPrefs.HasKey(SettingsKey),
            main = PlayerPrefs.GetString(SaveKey), backup = PlayerPrefs.GetString(SaveKey + ".backup"), settings = PlayerPrefs.GetString(SettingsKey)
        };
        File.WriteAllText(Path.Combine(folder, "editor-save-backup.json"), JsonUtility.ToJson(saved));
        SessionState.SetString(FolderKey, folder); SessionState.SetString(ShotsKey, selection);
        SessionState.SetInt("Nubik.Stills.Exit", 1); SessionState.SetBool(ActiveKey, true);
        PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.DeleteKey(SaveKey + ".backup"); PlayerPrefs.Save();
        // Russian world signs and Ultra graphics for the whole capture (restored with the rest of the settings).
        GameSettings.Set(s => { s.language = "ru"; s.quality = GameSettings.Ultra; });
        EditorApplication.isPlaying = true;
    }

    private static void Restore()
    {
        string path = Path.Combine(SessionState.GetString(FolderKey, ""), "editor-save-backup.json");
        var saved = JsonUtility.FromJson<Saved>(File.ReadAllText(path));
        Put(SaveKey, saved.mainExists, saved.main);
        Put(SaveKey + ".backup", saved.backupExists, saved.backup);
        Put(SettingsKey, saved.settingsExist, saved.settings);
        PlayerPrefs.Save();
        GameSettings.Set(s => JsonUtility.FromJsonOverwrite(saved.settingsExist ? saved.settings : "{}", s), false);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(path), "save-restored.txt"), "Editor save and settings restored after capture.");
    }

    private static void Put(string key, bool exists, string value)
    {
        if (exists) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key);
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying || finishing) return;
        try
        {
            if (game == null)
            {
                game = Object.FindAnyObjectByType<MineGame>();
                if (game == null || game.Progress == null) { game = null; return; }
                Setup();
            }
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            Pose(Shots[shot]);
            // Let lights, fog, site visibility and the yard's animation settle before the shot.
            if (++settled < Settle) return;
            Capture(Shots[shot]);
            settled = 0;
            do { shot++; } while (shot < Shots.Length && !Selected(shot));
            if (shot == Shots.Length) Finish(0);
            else Prepare(Shots[shot]);
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }

    private static void Setup()
    {
        folder = SessionState.GetString(FolderKey, "");
        selected = SessionState.GetString(ShotsKey, "");
        shot = 0; while (shot < Shots.Length && !Selected(shot)) shot++;
        if (shot == Shots.Length) throw new ArgumentException("No matching still names.");
        Time.captureFramerate = 30; Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
        AudioListener.volume = 0;
        game.enabled = false;
        game.GetComponent<MineHud>().enabled = false;
        foreach (var canvas in Object.FindObjectsByType<Canvas>()) if (canvas.renderMode != RenderMode.WorldSpace) canvas.enabled = false;
        Field<Transform>("tool").gameObject.SetActive(false);
        camera = Camera.main; camera.aspect = Width / (float)Height; camera.allowMSAA = true;

        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        renderScale = pipeline.renderScale; msaaCount = pipeline.msaaSampleCount; shadowDistance = pipeline.shadowDistance; hdr = pipeline.supportsHDR;
        pipeline.renderScale = 1; pipeline.msaaSampleCount = 8; pipeline.shadowDistance = 60; pipeline.supportsHDR = false;

        msaa = new RenderTexture(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24) { msaaSamples = 8, sRGB = true });
        resolved = new RenderTexture(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 0) { sRGB = true });
        half = new RenderTexture(new RenderTextureDescriptor(Width / 2, Height / 2, RenderTextureFormat.ARGB32, 0) { sRGB = true });
        resolved.filterMode = FilterMode.Bilinear;
        full = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        small = new Texture2D(Width / 2, Height / 2, TextureFormat.RGB24, false);

        var terrain = Field<VoxelTerrain>("terrain");
        // The same worked shaft as the promo videos, in disposable progress; generation and rock stay unchanged.
        for (float y = 0; y > -119; y -= .9f)
            terrain.Dig(new Vector3(.5f * Mathf.Sin(y * .07f), y, -1.2f), 2.3f, 999);
        terrain.Dig(new Vector3(2.8f, -114.4f, -3.5f), 2.3f, 999);
        // Open the chamber in front of the sealed door so its frame and lintel show.
        terrain.Dig(new Vector3(0, Door + .6f, .5f), 2.4f, 999);
        // A low dome over the first lava pool, so the molten surface and the cracked walls share the frame.
        terrain.Dig(Depths.Lava[0].Center + new Vector3(0, 2.1f, 0), 2f, 999);
        Call("RebuildDirty");
        // Ore the staged shaft left hanging in open air would look like a bug in a still: only ore in the walls shows.
        foreach (var item in Field<LootField>("loot").Items)
            item.Exposed = !item.Taken && !item.Meteor && terrain.IsExposed(item.Position, item.Size + .35f) && !Floating(terrain, item);
        Prepare(Shots[shot]);
        File.WriteAllText(Path.Combine(folder, "capture.txt"),
            "Unity scene at Ultra, 3840x2160 with 8x MSAA; 1920x1080 is the 4K frame averaged 2x2. HUD and tool hidden, staged camera, disposable excavation.\n");
    }

    private static bool Floating(VoxelTerrain terrain, LootItem item)
    {
        // Keys on their pedestals, chests, collectibles and secrets are placed in the open on purpose.
        if (item.Kind != LootKind.Find) return false;
        float reach = item.Size * .5f + .3f;
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                    if ((x != 0 || y != 0 || z != 0) && !terrain.IsAir(item.Position + new Vector3(x, y, z).normalized * reach)) return false;
        return true;
    }

    /// <summary>Scene changes a shot needs before it settles.</summary>
    private static void Prepare(Shot next)
    {
        var boss = Field<BossEncounter>("boss");
        if (next.name.Contains("cthulhu") && !boss.Inside)
        {
            game.Progress.keys = 31;
            boss.Enter(false);
            boss.Arm();
            // Let the awakening play out so the monster stands in its pose.
            for (int i = 0; i < 150; i++) boss.Tick(1f / 30, BossEncounter.Origin + new Vector3(0, 0, -5), false);
        }
        else if (!next.name.Contains("cthulhu") && boss.Inside) boss.Exit();
    }

    private static void Pose(Shot s)
    {
        game.DebugPlace(s.at - Vector3.up * 1.55f, 0, 0);
        camera.transform.position = s.at;
        camera.transform.rotation = Quaternion.LookRotation(s.look - s.at);
        camera.fieldOfView = s.fov;
        Call("UpdateAmbience");
        Field<MineSites>("sites").UpdateVisibility(-s.at.y);
        Field<Yard>("yard").Animate(1f / 30);
        Call("UpdateEmbers", 1f / 30);
        Call("UpdateVisibility");
        var boss = Field<BossEncounter>("boss");
        if (boss.Inside) boss.Tick(1f / 30, BossEncounter.Origin + new Vector3(0, 0, -5), false);
        Field<Transform>("tool").gameObject.SetActive(false);
    }

    private static void Capture(Shot s)
    {
        var previous = RenderTexture.active;
        camera.targetTexture = msaa; camera.Render(); camera.targetTexture = null;
        Graphics.Blit(msaa, resolved);
        // Exactly half size: each bilinear sample averages a 2 × 2 block, a clean 4× supersample for 1080p.
        Graphics.Blit(resolved, half);
        RenderTexture.active = resolved; full.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false); full.Apply(false);
        RenderTexture.active = half; small.ReadPixels(new Rect(0, 0, Width / 2, Height / 2), 0, 0, false); small.Apply(false);
        RenderTexture.active = previous;
        File.WriteAllBytes(Path.Combine(folder, "3840x2160", s.name + ".png"), full.EncodeToPNG());
        File.WriteAllBytes(Path.Combine(folder, "1920x1080", s.name + ".png"), small.EncodeToPNG());
        File.WriteAllBytes(Path.Combine(folder, "1920x1080", s.name + ".jpg"), small.EncodeToJPG(95));
        Debug.Log("NUBIK_STILL: " + s.name);
    }

    private static void Finish(int code)
    {
        finishing = true;
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline != null && msaa != null)
        {
            pipeline.renderScale = renderScale; pipeline.msaaSampleCount = msaaCount; pipeline.shadowDistance = shadowDistance; pipeline.supportsHDR = hdr;
        }
        foreach (var texture in new Object[] { msaa, resolved, half, full, small }) if (texture != null) Object.DestroyImmediate(texture);
        msaa = resolved = half = null; full = small = null;
        SessionState.SetInt("Nubik.Stills.Exit", code);
        EditorApplication.isPlaying = false;
    }

    private static T Field<T>(string name) => (T)typeof(MineGame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
    private static bool Selected(int index) => string.IsNullOrEmpty(selected) || Array.IndexOf(selected.Split(','), Shots[index].name) >= 0;
    private static void Call(string name, params object[] args) => typeof(MineGame).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, args);
}
