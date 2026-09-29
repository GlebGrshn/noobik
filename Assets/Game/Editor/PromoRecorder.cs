using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading.Tasks;
using Nubik;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

/// <summary>Offline, fixed-step 1080p recording of the actual scene. Never included in a player build.</summary>
[InitializeOnLoad]
public static class PromoRecorder
{
    private const string ActiveKey = "Nubik.Promo.Active", FolderKey = "Nubik.Promo.Folder", EncoderKey = "Nubik.Promo.Encoder";
    private const string SaveKey = "nubik.progress.v1";
    private const int Width = 1920, Height = 1080, Fps = 30;
    private static readonly string[] Names = { "01_yard", "02_descent", "03_roots", "04_slate", "05_crystals", "06_magma_meteor", "07_cthulhu", "08_victory" };
    private static readonly int[] Seconds = { 12, 16, 8, 8, 8, 16, 26, 10 };
    private static MineGame game;
    private static Camera camera;
    private static BossEncounter boss;
    private static RenderTexture target;
    private static Texture2D pixels;
    private static Process encoder;
    private static Task<string> encoderErrors;
    private static int clip, frame, lastFrame = -1;
    private static bool finishing, blasted;
    private static string folder;
    private static string selected;

    [Serializable] private sealed class SavedPreferences { public bool mainExists, backupExists; public string main, backup; }

    static PromoRecorder()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(ActiveKey, false)) return;
            RestorePreferences();
            SessionState.SetBool(ActiveKey, false);
            Time.captureFramerate = 0;
            int code = SessionState.GetInt("Nubik.Promo.Exit", 1);
            Debug.Log(code == 0 ? "NUBIK_PROMO_OK: " + SessionState.GetString(FolderKey, "") : "NUBIK_PROMO_FAILED");
            if (Application.isBatchMode) EditorApplication.Exit(code);
        };
    }

    [MenuItem("Nubik/Record 1080p promo")]
    public static void RecordAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start the recorder in edit mode.");
        string binary = Environment.GetEnvironmentVariable("NUBIK_FFMPEG");
        string output = null, selection = "";
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-promoEncoder") binary = args[i + 1];
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "-promoOutput") output = args[i + 1];
            if (args[i] == "-promoClips") selection = args[i + 1];
        }
        if (string.IsNullOrEmpty(binary) || !File.Exists(binary)) throw new FileNotFoundException("Pass -promoEncoder with the installed ffmpeg.exe path, or set NUBIK_FFMPEG.");
        ProjectSetup.Prepare();
        folder = Path.GetFullPath(output ?? "Recordings/promo-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        if (!folder.StartsWith(Path.GetFullPath("Recordings") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Promo output must be inside Recordings.");
        selected = selection;
        clip = frame = 0; lastFrame = -1; finishing = blasted = false; game = null;
        while (clip < Names.Length && !Selected(clip)) clip++;
        if (clip == Names.Length) throw new ArgumentException("No matching promo clip names.");
        Directory.CreateDirectory(folder);
        var saved = new SavedPreferences { mainExists = PlayerPrefs.HasKey(SaveKey), backupExists = PlayerPrefs.HasKey(SaveKey + ".backup"), main = PlayerPrefs.GetString(SaveKey), backup = PlayerPrefs.GetString(SaveKey + ".backup") };
        File.WriteAllText(Path.Combine(folder, "editor-save-backup.json"), JsonUtility.ToJson(saved));
        SessionState.SetString(FolderKey, folder); SessionState.SetString(EncoderKey, binary);
        SessionState.SetString("Nubik.Promo.Clips", selection);
        SessionState.SetInt("Nubik.Promo.Exit", 1); SessionState.SetBool(ActiveKey, true);
        PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.DeleteKey(SaveKey + ".backup"); PlayerPrefs.Save();
        EditorApplication.isPlaying = true;
    }

    private static void RestorePreferences()
    {
        string path = Path.Combine(SessionState.GetString(FolderKey, ""), "editor-save-backup.json");
        var saved = JsonUtility.FromJson<SavedPreferences>(File.ReadAllText(path));
        if (saved.mainExists) PlayerPrefs.SetString(SaveKey, saved.main); else PlayerPrefs.DeleteKey(SaveKey);
        if (saved.backupExists) PlayerPrefs.SetString(SaveKey + ".backup", saved.backup); else PlayerPrefs.DeleteKey(SaveKey + ".backup");
        PlayerPrefs.Save();
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(path), "save-restored.txt"), "Editor save restored after capture. Browser saves were not accessed.");
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
            if (encoder == null) BeginClip();
            float t = frame / (float)Fps;
            Pose(t, t / Seconds[clip]);
            Capture();
            if (++frame >= Seconds[clip] * Fps)
            {
                EndClip();
                do { clip++; } while (clip < Names.Length && !Selected(clip));
                if (clip == Names.Length) Finish(0);
            }
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }

    private static void Setup()
    {
        folder = SessionState.GetString(FolderKey, "");
        selected = SessionState.GetString("Nubik.Promo.Clips", "");
        clip = 0; while (clip < Names.Length && !Selected(clip)) clip++;
        Time.captureFramerate = Fps; Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
        AudioListener.volume = 0;
        game.enabled = false;
        game.GetComponent<MineHud>().enabled = false;
        foreach (var canvas in Object.FindObjectsByType<Canvas>()) if (canvas.renderMode != RenderMode.WorldSpace) canvas.enabled = false;
        Field<Transform>("tool").gameObject.SetActive(false);
        camera = Camera.main; camera.fieldOfView = 62; camera.aspect = Width / (float)Height;
        boss = Field<BossEncounter>("boss");
        target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        var terrain = Field<VoxelTerrain>("terrain");
        // A worked shaft in disposable progress; normal scene generation and rock stay unchanged.
        for (float y = 0; y > -119; y -= .9f)
            terrain.Dig(new Vector3(.5f * Mathf.Sin(y * .07f), y, -1.2f), 2.3f, 999);
        terrain.Dig(new Vector3(2.8f, -114.4f, -3.5f), 2.3f, 999);
        Call("RebuildDirty");
        foreach (var item in Field<LootField>("loot").Items)
            item.Exposed = !item.Taken && !item.Meteor && Field<VoxelTerrain>("terrain").IsExposed(item.Position, item.Size + .35f);
        File.WriteAllText(Path.Combine(folder, "capture.txt"), "1920x1080, 30 fps, H.264 yuv420p. Silent. Actual Unity scene with HUD/tool hidden. Staged camera, disposable excavation, choreographed boss shots.\n");
    }

    private static void BeginClip()
    {
        frame = 0;
        var path = Path.Combine(folder, Names[clip] + ".mp4");
        var start = new ProcessStartInfo(SessionState.GetString(EncoderKey, ""), "-hide_banner -loglevel warning -y -f rawvideo -pixel_format rgb24 -video_size 1920x1080 -framerate 30 -i pipe:0 -an -vf vflip -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p -movflags +faststart \"" + path + "\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true };
        encoder = Process.Start(start);
        encoderErrors = encoder.StandardError.ReadToEndAsync();
        if (clip == 6) { game.Progress.keys = 31; boss.Enter(false); }
        if (clip == 7)
        {
            if (!boss.Inside) boss.Enter(true);
            if (boss.Battle.Phase == BattlePhase.Waiting) boss.Arm();
            while (boss.Battle.Health > BossBattle.OpenHeadDamage) boss.Battle.Shoot(true);
        }
        Debug.Log("PROMO_RECORDING: " + Names[clip]);
    }

    private static void Pose(float t, float u)
    {
        float s = Mathf.SmoothStep(0, 1, u);
        Vector3 at, look;
        switch (clip)
        {
            case 0:
                float a = Mathf.Lerp(-.75f, .55f, s);
                at = new Vector3(Mathf.Sin(a) * 17, Mathf.Lerp(9, 5, s), 8 - Mathf.Cos(a) * 19);
                look = new Vector3(0, 1, 12); break;
            case 1:
                float y = Mathf.Lerp(2, -116, s);
                at = new Vector3(.45f * Mathf.Sin(y * .07f), y, -1.4f);
                look = at + new Vector3(.4f * Mathf.Sin(t*.4f), -4, 2); break;
            case 2: case 3: case 4:
                float depth = MineSites.All[clip - 2].Depth;
                at = new Vector3(Mathf.Lerp(-1.45f, 1.05f, s), -depth + 1.45f, .65f);
                look = new Vector3(0, -depth + 1, 2.8f); break;
            case 5:
                if (t < 6)
                {
                    var pool = Depths.Lava[0];
                    at = pool.Center + new Vector3(Mathf.Lerp(-.8f, .7f, t/6), 1.1f, -.8f);
                    look = pool.Center + new Vector3(0, -.6f, .4f);
                }
                else
                {
                    at = new Vector3(Mathf.Lerp(2.2f, 3.2f, (t-6)/10), -113.8f, -4.6f);
                    look = Depths.Meteor;
                    if (t > 12 && !blasted) { Call("Explode", Depths.Meteor + Vector3.back); blasted = true; }
                }
                break;
            default:
                at = BossEncounter.Origin + new Vector3(Mathf.Sin(t * .15f) * 3, clip == 7 ? 2.8f : 2.3f, -8.5f);
                look = BossEncounter.Origin + new Vector3(0, 3.7f, 6);
                if (clip == 6 && t > 2 && boss.Battle.Phase == BattlePhase.Waiting) boss.Arm();
                if (clip == 6 && t > 3 && frame % 15 == 0)
                {
                    // Shoot the real collider, keeping the finishing hit for its own take.
                    if (boss.Battle.Health > 45)
                    {
                        var aim = BossEncounter.Origin + new Vector3(0, 5, 6);
                        if (Physics.Raycast(at, (aim-at).normalized, out var hit, 35, ~0, QueryTriggerInteraction.Collide) && boss.IsBoss(hit.collider)) boss.Shoot(hit.collider);
                        boss.Trace(at + Vector3.right, aim);
                    }
                }
                if (clip == 7 && t > 1 && boss.Battle.Health > 0) boss.Battle.Shoot(true);
                boss.Tick(1f/Fps, BossEncounter.Origin + new Vector3(Mathf.Sin(t)*5, 0, -5));
                if (clip == 7 && t > 5)
                {
                    boss.Exit();
                    at = new Vector3(Mathf.Lerp(-8, -3, (t-5)/5), 3.5f, 10);
                    look = new Vector3(-3, 1.8f, 20);
                }
                break;
        }
        game.DebugPlace(at - Vector3.up * 1.55f, 0, 0);
        camera.transform.rotation = Quaternion.LookRotation(look - at);
        camera.fieldOfView = clip >= 2 && clip <= 5 ? 70 : 62;
        Call("UpdateAmbience");
        Field<MineSites>("sites").UpdateVisibility(-at.y);
        Field<Yard>("yard").Animate(1f/Fps);
        Call("UpdateEmbers", 1f/Fps);
        Call("UpdateCharges", 1f/Fps);
        Call("UpdateVisibility");
        Field<Transform>("tool").gameObject.SetActive(false);
    }

    private static void Capture()
    {
        var previous = RenderTexture.active;
        camera.targetTexture = target; camera.Render(); camera.targetTexture = null;
        RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false); pixels.Apply(false);
        RenderTexture.active = previous;
        byte[] data = pixels.GetRawTextureData<byte>().ToArray();
        encoder.StandardInput.BaseStream.Write(data, 0, data.Length);
        if (frame == Fps * 4) File.WriteAllBytes(Path.Combine(folder, Names[clip] + ".png"), pixels.EncodeToPNG());
    }

    private static void EndClip()
    {
        if (encoder == null) return;
        encoder.StandardInput.Close();
        if (!encoder.WaitForExit(30000)) { encoder.Kill(); throw new TimeoutException("ffmpeg did not finish the clip"); }
        string errors = encoderErrors.GetAwaiter().GetResult();
        int code = encoder.ExitCode; encoder.Dispose(); encoder = null;
        if (code != 0) throw new Exception("ffmpeg: " + errors);
        File.WriteAllText(Path.Combine(folder, Names[clip] + ".encode.log"), errors);
    }

    private static void Finish(int code)
    {
        finishing = true;
        try { EndClip(); } catch (Exception e) { Debug.LogException(e); code = 1; }
        if (target != null) { target.Release(); Object.DestroyImmediate(target); }
        if (pixels != null) Object.DestroyImmediate(pixels);
        SessionState.SetInt("Nubik.Promo.Exit", code);
        EditorApplication.isPlaying = false;
    }

    private static T Field<T>(string name) => (T)typeof(MineGame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
    private static bool Selected(int index) => string.IsNullOrEmpty(selected) || Array.IndexOf(selected.Split(','), Names[index]) >= 0;
    private static void Call(string name, params object[] args) => typeof(MineGame).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, args);
}
