using System.Collections.Generic;
using UnityEngine;

namespace Nubik
{
    /// <summary>
    /// Sound: 2D one-shots, a jetpack loop, positional loops (lava, the meteorite) and the ambience of the current place.
    /// Clips come from Resources/Audio by name, so a sound or an ambience can be replaced by dropping in a file with the
    /// same name (wav, ogg or mp3). Ambience names: <see cref="Ambiences"/>.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        /// <summary>Ambience clip per place: the yard, the house, each zone by index, the boss chamber.</summary>
        public const string Surface = "ambient_surface", House = "ambient_house", Boss = "ambient_boss";
        public static readonly string[] Zones = { "ambient_roots", "ambient_slate", "ambient_crystals", "ambient_magma" };
        public static IEnumerable<string> Ambiences { get { yield return Surface; yield return House; foreach (var zone in Zones) yield return zone; yield return Boss; } }

        private const float AmbientFade = 2.5f, AmbientVolume = .55f;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly List<AudioSource> sources = new List<AudioSource>();
        private readonly List<AudioSource> spots = new List<AudioSource>();
        private readonly AudioSource[] ambience = new AudioSource[2];
        private AudioSource loop;
        private int next, nextSpot, current;
        private string ambientName;

        private void Awake()
        {
            foreach (var clip in Resources.LoadAll<AudioClip>("Audio")) clips[clip.name] = clip;
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
        }

        public AudioClip Clip(string name) => clips.TryGetValue(name, out var clip) ? clip : null;

        public void Play(string name, float volume = 1, float pitch = 1, float jitter = 0.06f)
        {
            if (!clips.TryGetValue(name, out var clip)) return;
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
            source.volume = volume;
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
            loop.volume = volume;
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

        private void Update()
        {
            float step = Time.unscaledDeltaTime / AmbientFade * AmbientVolume;
            for (int i = 0; i < ambience.Length; i++)
            {
                var source = ambience[i];
                source.volume = Mathf.MoveTowards(source.volume, i == current && source.clip != null ? AmbientVolume : 0, step);
                if (i != current && source.volume <= 0 && source.isPlaying) source.Stop();
            }
        }

        private void OnDestroy()
        {
            foreach (var spot in spots) if (spot != null) Destroy(spot.gameObject);
        }

        public static void SetMuted(bool muted) => AudioListener.volume = muted ? 0 : 1;
    }
}
