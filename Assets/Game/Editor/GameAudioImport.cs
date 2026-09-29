using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Applies to replacement clips too. WebGL transcodes compressed audio to AAC.</summary>
public sealed class GameAudioImport : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if (assetPath.StartsWith("Assets/Game/Resources/Audio/")) Configure((AudioImporter)assetImporter);
        else if (assetPath.StartsWith("Assets/Game/Resources/Music/")) ConfigureMusic((AudioImporter)assetImporter);
    }

    /// <summary>
    /// Effects and ambience loops are short: decompressed once, mono 22 kHz (browsers keep decoded audio as 32-bit floats,
    /// so seven 18-second stereo loops at 32 kHz alone would take about 32 MB).
    /// </summary>
    public static void Configure(AudioImporter importer)
    {
        bool ambient = Path.GetFileName(importer.assetPath).StartsWith("ambient_");
        importer.forceToMono = true;
        importer.loadInBackground = false;
        var settings = importer.defaultSampleSettings;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = ambient ? .5f : .7f;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
        settings.sampleRateOverride = 22050u;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
        importer.SetOverrideSampleSettings("WebGL", settings);
    }

    /// <summary>Long music tracks stay compressed in memory (Unity recommends it for music on WebGL).</summary>
    public static void ConfigureMusic(AudioImporter importer)
    {
        importer.forceToMono = false;
        importer.loadInBackground = false;
        var settings = importer.defaultSampleSettings;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = .45f;
        settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
        settings.sampleRateOverride = 44100u;
        settings.preloadAudioData = true;
        // The editor's FMOD cannot keep the WebGL (AAC) data compressed, so only the WebGL player does.
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        importer.defaultSampleSettings = settings;
        settings.loadType = AudioClipLoadType.CompressedInMemory;
        // MP3 keeps the editor able to play the WebGL clip compressed; the WebGL build encodes it for the browser.
        settings.compressionFormat = AudioCompressionFormat.MP3;
        importer.SetOverrideSampleSettings("WebGL", settings);
    }

    /// <summary>Reimports only clips whose settings differ from the rules above (called by Prepare and the build).</summary>
    public static void EnsureConfigured()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Game/Resources/Audio", "Assets/Game/Resources/Music" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            bool music = path.Contains("/Music/");
            var web = importer.GetOverrideSampleSettings("WebGL");
            var wanted = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            var current = importer.defaultSampleSettings;
            if (web.loadType == wanted && current.loadType == AudioClipLoadType.DecompressOnLoad && importer.forceToMono == !music &&
                (!music || web.compressionFormat == AudioCompressionFormat.MP3) &&
                current.sampleRateOverride == (music ? 44100u : 22050u)) continue;
            if (music) ConfigureMusic(importer); else Configure(importer);
            importer.SaveAndReimport();
        }
    }

    [MenuItem("Nubik/Configure audio compression")]
    public static void ReimportAll()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Game/Resources/Audio", "Assets/Game/Resources/Music" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            if (path.Contains("/Music/")) ConfigureMusic(importer); else Configure(importer);
            importer.SaveAndReimport();
        }
    }
}
