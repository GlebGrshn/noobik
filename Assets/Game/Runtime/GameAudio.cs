using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nubik
{
    /// <summary>
    /// Sound in four groups with their own volume (<see cref="GameSettings"/>): music, effects (one-shots, the jetpack,
    /// positional loops such as lava), ambience of the current place, and the master level.
    /// Clips come from Resources/Audio and Resources/Music by name, so any of them can be replaced by a file with the same
    /// name (wav, ogg or mp3).
    ///
    /// Music follows depth: each track has a weight that changes smoothly with the depth, and every change of weight is
    /// eased over several seconds, so tracks blend into each other instead of switching. The tracks themselves are plain
    /// MP3 files in StreamingAssets/Music played by the page (Yandex.jslib) as streamed media through Web Audio gains:
    /// Unity would decode them whole in Chrome (hundreds of MB) and could not start them on an iPhone outside a tap.
    /// In the editor only the weights run.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        /// <summary>Ambience clip per place: the yard, the house, each zone by index, the boss chamber.</summary>
        public const string Surface = "ambient_surface", House = "ambient_house", Boss = "ambient_boss";
        public static readonly string[] Zones = { "ambient_roots", "ambient_slate", "ambient_crystals", "ambient_magma" };
        public static IEnumerable<string> Ambiences { get { yield return Surface; yield return House; foreach (var zone in Zones) yield return zone; yield return Boss; } }
        /// <summary>Music: the yard, house and roots; the roots and slate; the crystals and magma; the boss.</summary>
        public static readonly string[] Music = { "music_1_yard", "music_2_roots", "music_3_deep", "music_4_boss" };

        private const float AmbientFade = 3f, AmbientLevel = .5f, MusicLevel = .75f;
        /// <summary>Seconds for a track to rise from silence to full weight (or back), and for a loop to cross-fade.</summary>
        private const float MusicFade = 8f;

        private sealed class Emitted { public AudioSource source; public float volume; }

        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        /// <summary>name, name_2, name_3…: one of them is picked at random, so repeated hits never sound identical.</summary>
        private readonly Dictionary<string, List<AudioClip>> variants = new Dictionary<string, List<AudioClip>>();
        private readonly Dictionary<string, AudioSource> loops = new Dictionary<string, AudioSource>();
        private readonly Dictionary<string, float> loopVolumes = new Dictionary<string, float>();
        private readonly List<AudioSource> sources = new List<AudioSource>();
        private readonly List<AudioSource> spots = new List<AudioSource>();
        private readonly List<Emitted> emitters = new List<Emitted>();
        private readonly AudioSource[] ambience = new AudioSource[2];
        private readonly float[] musicWeight = new float[4], musicTarget = new float[4], musicSent = { -1, -1, -1, -1 };
        private bool musicPaused;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void NubikMusicInit(string urls);
        [DllImport("__Internal")] private static extern void NubikMusicLevel(int index, float level);
        [DllImport("__Internal")] private static extern void NubikMusicPause(int paused);
#endif
        private AudioSource loop;
        private float loopVolume = 1;
        private int next, nextSpot, current;
        private string ambientName;
        private bool muted;

        private void Awake()
        {
            foreach (var clip in Resources.LoadAll<AudioClip>("Audio")) clips[clip.name] = clip;
            foreach (var clip in clips.Values)
            {
                int cut = clip.name.LastIndexOf('_');
                bool numbered = cut > 0 && int.TryParse(clip.name.Substring(cut + 1), out _);
                string family = numbered ? clip.name.Substring(0, cut) : clip.name;
                if (!variants.TryGetValue(family, out var list)) variants[family] = list = new List<AudioClip>();
                list.Add(clip);
            }
            for (int i = 0; i < 6; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0;
                sources.Add(source);
            }
            for (int i = 0; i < ambience.Length; i++)
            {
                ambience[i] = gameObject.AddComponent<AudioSource>();
                ambience[i].playOnAwake = false;
                ambience[i].loop = true;
                ambience[i].spatialBlend = 0;
                ambience[i].volume = 0;
            }
            for (int i = 0; i < 4; i++)
            {
                var spot = new GameObject("Sound spot", typeof(AudioSource)).GetComponent<AudioSource>();
                Setup3D(spot, 18);
                spots.Add(spot);
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            var urls = new List<string>();
            foreach (var name in Music) urls.Add(Application.streamingAssetsPath + "/Music/" + name + ".mp3");
            NubikMusicInit(string.Join(",", urls));
#endif
            GameSettings.Changed += ApplyVolumes;
            ApplyVolumes();
        }

        private static float Effects => GameSettings.Current.effects;

        private void ApplyVolumes()
        {
            AudioListener.volume = muted ? 0 : GameSettings.Current.master;
            foreach (var source in sources) source.volume = Effects;
            foreach (var spot in spots) spot.volume = Effects;
            foreach (var emitted in emitters) if (emitted.source != null) emitted.source.volume = emitted.volume * Effects;
            if (loop != null) loop.volume = loopVolume * Effects;
            foreach (var pair in loops) pair.Value.volume = loopVolumes[pair.Key] * Effects;
        }

        public AudioClip Clip(string name) => clips.TryGetValue(name, out var clip) ? clip : null;

        public void Play(string name, float volume = 1, float pitch = 1, float jitter = 0.06f)
        {
            AudioClip clip;
            if (variants.TryGetValue(name, out var family)) clip = family[Random.Range(0, family.Count)];
            else if (!clips.TryGetValue(name, out clip)) return;
            var source = sources[next];
            next = (next + 1) % sources.Count;
            source.pitch = pitch * (1 + Random.Range(-jitter, jitter));
            source.PlayOneShot(clip, volume);
        }

        /// <summary>A one-shot heard from a place in the world: quieter and panned with distance.</summary>
        public void PlayAt(string name, Vector3 position, float volume = 1, float pitch = 1)
        {
            if (!clips.TryGetValue(name, out var clip)) return;
            var spot = spots[nextSpot];
            nextSpot = (nextSpot + 1) % spots.Count;
            spot.transform.position = position;
            spot.pitch = pitch * (1 + Random.Range(-.05f, .05f));
            spot.PlayOneShot(clip, volume);
        }

        /// <summary>A looping positional sound attached to an object, such as bubbling lava.</summary>
        public AudioSource Emitter(Transform parent, string name, float volume, float range)
        {
            var clip = Clip(name);
            var source = parent.gameObject.AddComponent<AudioSource>();
            Setup3D(source, range);
            source.loop = true;
            source.volume = volume * Effects;
            emitters.Add(new Emitted { source = source, volume = volume });
            if (clip == null) return source;
            source.clip = clip;
            // Start somewhere inside the loop so neighbouring emitters do not pulse together.
            source.time = Random.Range(0, clip.length * .9f);
            source.Play();
            return source;
        }

        private static void Setup3D(AudioSource source, float range)
        {
            source.playOnAwake = false;
            source.spatialBlend = 1;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = range;
            source.dopplerLevel = 0;
        }

        /// <summary>A named looping channel besides the jetpack, such as the drill's motor.</summary>
        public void Loop(string channel, string name, bool on, float volume)
        {
            if (!loops.TryGetValue(channel, out var source))
            {
                if (!on) return;
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0;
                loops[channel] = source;
            }
            loopVolumes[channel] = volume;
            source.volume = volume * Effects;
            if (!on) { if (source.isPlaying) source.Stop(); return; }
            if (!clips.TryGetValue(name, out var clip)) return;
            if (source.isPlaying && source.clip == clip) return;
            source.clip = clip;
            source.Play();
        }

        /// <summary>Starts or stops a looping sound, such as the jetpack hiss.</summary>
        public void Loop(string name, bool on, float volume = 1)
        {
            if (loop == null)
            {
                loop = gameObject.AddComponent<AudioSource>();
                loop.playOnAwake = false;
                loop.loop = true;
                loop.spatialBlend = 0;
            }
            if (!on) { if (loop.isPlaying) loop.Stop(); return; }
            if (!clips.TryGetValue(name, out var clip)) return;
            loopVolume = volume;
            loop.volume = volume * Effects;
            if (loop.isPlaying && loop.clip == clip) return;
            loop.clip = clip;
            loop.Play();
        }

        /// <summary>Cross-fades to the ambience of a place; a missing clip fades to silence.</summary>
        public void SetAmbience(string name)
        {
            if (name == ambientName) return;
            ambientName = name;
            current = 1 - current;
            var source = ambience[current];
            source.clip = Clip(name);
            if (source.clip == null) { source.Stop(); return; }
            source.time = Random.Range(0, source.clip.length * .9f);
            source.Play();
        }

        /// <summary>
        /// Sets the music for a depth: the first track until the roots, blending into the second through the roots,
        /// the second through the slate, the third from the crystals down; the boss has its own.
        /// </summary>
        public void SetMusicDepth(float depth, bool boss)
        {
            float first = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(6, 26, depth));
            float third = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(55, 75, depth));
            float second = Mathf.Max(0, 1 - first - third);
            SetTargets(boss ? 0 : first, boss ? 0 : second, boss ? 0 : third, boss ? 1 : 0);
        }

        private void SetTargets(float a, float b, float c, float d)
        {
            musicTarget[0] = a; musicTarget[1] = b; musicTarget[2] = c; musicTarget[3] = d;
        }

        /// <summary>Master mute (the M key) that keeps the slider value.</summary>
        public void Mute(bool value) { muted = value; ApplyVolumes(); }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float step = dt / AmbientFade * AmbientLevel;
            float ambientTarget = AmbientLevel * GameSettings.Current.ambience;
            for (int i = 0; i < ambience.Length; i++)
            {
                var source = ambience[i];
                source.volume = Mathf.MoveTowards(source.volume, i == current && source.clip != null ? ambientTarget : 0, step);
                if (i != current && source.volume <= 0 && source.isPlaying) source.Stop();
            }
            // Music waits while the whole game is paused (tab hidden, ad): the page pauses its players too.
            bool paused = AudioListener.pause;
            if (paused != musicPaused)
            {
                musicPaused = paused;
#if UNITY_WEBGL && !UNITY_EDITOR
                NubikMusicPause(paused ? 1 : 0);
#endif
            }
            if (paused) return;
            float master = muted ? 0 : GameSettings.Current.master;
            for (int i = 0; i < musicWeight.Length; i++)
            {
                musicWeight[i] = Mathf.MoveTowards(musicWeight[i], musicTarget[i], dt / MusicFade);
                // Equal-power gain keeps the sum of two blending tracks at the same loudness.
                float level = Mathf.Sqrt(musicWeight[i]) * MusicLevel * GameSettings.Current.music * master;
                if (Mathf.Abs(level - musicSent[i]) < .002f && !(level == 0 && musicSent[i] != 0)) continue;
                musicSent[i] = level;
#if UNITY_WEBGL && !UNITY_EDITOR
                NubikMusicLevel(i, level);
#endif
            }
        }

        /// <summary>Current weight of each music track (tests, debugging).</summary>
        public float MusicWeight(int track) => track < musicWeight.Length ? musicWeight[track] : 0;
        /// <summary>The track's file ships with the game (the page streams it).</summary>
        public bool MusicLoaded(int track) =>
            track < Music.Length && File.Exists(Path.Combine(Application.streamingAssetsPath, "Music", Music[track] + ".mp3"));

        private void OnDestroy()
        {
            GameSettings.Changed -= ApplyVolumes;
            foreach (var spot in spots) if (spot != null) Destroy(spot.gameObject);
        }

        /// <summary>Mutes all sound without a GameAudio at hand (tests); the instance keeps it in step on the next change.</summary>
        public static void SetMuted(bool muted) => AudioListener.volume = muted ? 0 : GameSettings.Current.master;
    }
}
