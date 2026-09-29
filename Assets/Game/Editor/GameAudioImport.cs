using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Applies to replacement clips too. WebGL transcodes compressed audio to AAC.</summary>
public sealed class GameAudioImport : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Game/Resources/Audio/")) return;
        Configure((AudioImporter)assetImporter);
    }

    public static void Configure(AudioImporter importer)
    {
        bool ambient = Path.GetFileName(importer.assetPath).StartsWith("ambient_");
        importer.forceToMono = !ambient;
        importer.loadInBackground = false;
        var settings = importer.defaultSampleSettings;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = ambient ? .55f : .7f;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
        settings.sampleRateOverride = ambient ? 32000u : 22050u;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
        importer.SetOverrideSampleSettings("WebGL", settings);
    }

    [MenuItem("Nubik/Configure audio compression")]
    public static void ReimportAll()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Game/Resources/Audio" }))
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            Configure(importer);
            importer.SaveAndReimport();
        }
    }
}
