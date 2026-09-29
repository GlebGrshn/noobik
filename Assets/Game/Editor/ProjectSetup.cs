using System.IO;
using Nubik;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ProjectSetup
{
    [MenuItem("Nubik/Prepare prototype")]
    public static void Prepare()
    {
        GameAudioImport.ReimportAll();
        Directory.CreateDirectory("Assets/Game/Scenes");
        Directory.CreateDirectory("Assets/Game/Config");
        Directory.CreateDirectory("Assets/Game/Prefabs");
        var config = AssetDatabase.LoadAssetAtPath<MineConfig>("Assets/Game/Config/MineBalance.asset");
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<MineConfig>();
            AssetDatabase.CreateAsset(config, "Assets/Game/Config/MineBalance.asset");
        }
        string scenePath = "Assets/Game/Scenes/Mine.unity";
        EditorUtility.SetDirty(config);
        var shader = Shader.Find("Nubik/Lit");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Config/Prototype.mat");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, "Assets/Game/Config/Prototype.mat");
        }
        if (material.shader != shader) { material.shader = shader; EditorUtility.SetDirty(material); }
        var terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Config/Terrain.mat");
        if (terrainMaterial == null)
        {
            terrainMaterial = new Material(shader) { name = "Terrain" };
            terrainMaterial.SetFloat("_VertexColor", 1);
            terrainMaterial.SetFloat("_Noise", 0.08f);
            AssetDatabase.CreateAsset(terrainMaterial, "Assets/Game/Config/Terrain.mat");
        }
        AssignRockDetail(material, terrainMaterial);
        AssignSurfaces(material);
        var cube = PrimitivePrefab("Cube", PrimitiveType.Cube, material);
        var skyMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Config/Sky.mat");
        if (skyMaterial == null)
        {
            skyMaterial = new Material(Shader.Find("Nubik/Sky"));
            AssetDatabase.CreateAsset(skyMaterial, "Assets/Game/Config/Sky.mat");
        }
        var sphere = PrimitivePrefab("Sphere", PrimitiveType.Sphere, material);
        if (!File.Exists(scenePath))
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var game = new GameObject("NubikGame").AddComponent<MineGame>();
            game.config = config;
            game.prototypeMaterial = material;
            game.terrainMaterial = terrainMaterial;
            game.skyMaterial = skyMaterial;
            game.cubePrefab = cube;
            game.spherePrefab = sphere;
            EditorSceneManager.SaveScene(game.gameObject.scene, scenePath);
        }
        else
        {
            var scene = EditorSceneManager.OpenScene(scenePath);
            var game = Object.FindAnyObjectByType<MineGame>();
            if (game != null)
            {
                if (game.prototypeMaterial == null) game.prototypeMaterial = material;
                if (game.terrainMaterial == null) game.terrainMaterial = terrainMaterial;
                if (game.skyMaterial == null) game.skyMaterial = skyMaterial;
                if (game.cubePrefab == null) game.cubePrefab = cube;
                if (game.spherePrefab == null) game.spherePrefab = sphere;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        EditorSettings.serializationMode = SerializationMode.ForceText;
        PlayerSettings.companyName = "GlebGrshn";
        PlayerSettings.productName = "Nubik Miner";
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.runInBackground = true;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.WebGL.template = "PROJECT:Nubik";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.memorySize = 256;
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        settings.FindProperty("activeInputHandler").intValue = 0;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        AssetDatabase.SaveAssets();
        Debug.Log("NUBIK_SETUP_OK: prototype scene and WebGL settings prepared.");
    }

    /// <summary>Rock detail made by Tools/make_textures.py: linear data (normals, height, cracks), no compression.</summary>
    private static void AssignRockDetail(Material props, Material terrain)
    {
        const string path = "Assets/Game/Textures/RockDetail.png";
        if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
            (importer.sRGBTexture || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.anisoLevel != 4))
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }
        var detail = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        foreach (var material in new[] { props, terrain })
        {
            if (material.GetTexture("_Detail") == detail) continue;
            material.SetTexture("_Detail", detail);
            EditorUtility.SetDirty(material);
        }
        if (terrain.GetFloat("_DetailStrength") < 0.01f)
        {
            terrain.SetFloat("_DetailStrength", 1);
            terrain.SetFloat("_DetailScale", 0.55f);
            EditorUtility.SetDirty(terrain);
        }
    }

    /// <summary>
    /// Surface set made by Tools/make_surfaces.py: a 4 x 4 grid of 256 px tiles imported as a 16-layer
    /// Texture2DArray of linear data (normal xy, albedo, mask). Every prop material copies it from Prototype.mat.
    /// </summary>
    private static void AssignSurfaces(Material props)
    {
        const string path = "Assets/Game/Textures/Surfaces.png";
        if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
            (importer.textureShape != TextureImporterShape.Texture2DArray || importer.sRGBTexture || importer.textureCompression != TextureImporterCompression.Uncompressed))
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureShape = TextureImporterShape.Texture2DArray;
            settings.flipbookRows = 4;
            settings.flipbookColumns = 4;
            settings.sRGBTexture = false;
            settings.alphaIsTransparency = false;
            settings.mipmapEnabled = true;
            settings.wrapMode = TextureWrapMode.Repeat;
            settings.filterMode = FilterMode.Trilinear;
            settings.aniso = 4;
            importer.SetTextureSettings(settings);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
        var surfaces = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
        if (surfaces == null) { Debug.LogWarning("Surface set missing: run py -3 Tools/make_surfaces.py"); return; }
        if (props.GetTexture("_Surfaces") == surfaces) return;
        props.SetTexture("_Surfaces", surfaces);
        EditorUtility.SetDirty(props);
    }

    private static GameObject PrimitivePrefab(string name, PrimitiveType type, Material material)
    {
        string path = "Assets/Game/Prefabs/" + name + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null) return prefab;
        var instance = GameObject.CreatePrimitive(type);
        instance.GetComponent<Renderer>().sharedMaterial = material;
        prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return prefab;
    }

    [MenuItem("Nubik/Build WebGL")]
    public static void BuildWeb() => Build("Builds/WebGL", BuildOptions.None);

    /// <summary>Development build with readable stack traces, next to the release one.</summary>
    [MenuItem("Nubik/Build WebGL (development)")]
    public static void BuildWebDevelopment() => Build("Builds/WebGL-dev", BuildOptions.Development);

    private static void Build(string path, BuildOptions options)
    {
        Prepare();
        bool development = (options & BuildOptions.Development) != 0;
        var exceptions = PlayerSettings.WebGL.exceptionSupport;
        // Full managed stack traces only in the development build; the release keeps the smaller setting.
        if (development) PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace;
        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Game/Scenes/Mine.unity" },
                locationPathName = path,
                target = BuildTarget.WebGL,
                options = options
            });
        }
        finally { PlayerSettings.WebGL.exceptionSupport = exceptions; }
        if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("WebGL build failed: " + report.summary.result);
        Debug.Log("NUBIK_BUILD_OK: " + report.summary.totalSize + " bytes");
    }
}
