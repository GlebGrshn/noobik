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
            terrainMaterial.SetFloat("_Noise", 0.12f);
            AssetDatabase.CreateAsset(terrainMaterial, "Assets/Game/Config/Terrain.mat");
        }
        var cube = PrimitivePrefab("Cube", PrimitiveType.Cube, material);
        var sphere = PrimitivePrefab("Sphere", PrimitiveType.Sphere, material);
        if (!File.Exists(scenePath))
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var game = new GameObject("NubikGame").AddComponent<MineGame>();
            game.config = config;
            game.prototypeMaterial = material;
            game.terrainMaterial = terrainMaterial;
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
    public static void BuildWeb()
    {
        Prepare();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Game/Scenes/Mine.unity" },
            locationPathName = "Builds/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("WebGL build failed: " + report.summary.result);
        Debug.Log("NUBIK_BUILD_OK: " + report.summary.totalSize + " bytes");
    }
}
