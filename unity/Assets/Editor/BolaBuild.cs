using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BolaBuild
{
    const string Folder = "Assets/Generated/";
    public static void Build()
    {
        if (Application.unityVersion != "6000.0.75f1") throw new Exception("Wrong Unity version");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string path = Folder + "ThrowSource.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.importCameras = importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.SaveAndReimport();
        var description = importer.humanDescription;
        var mappings = new Dictionary<string,string> {
            {"Hips","Hips"},{"Spine","Spine"},{"Chest","Spine1"},{"UpperChest","Spine2"},
            {"Neck","Neck"},{"Head","Head"}
        };
        foreach (string side in new[]{"Left","Right"})
        {
            foreach(var pair in new[]{("Shoulder","Shoulder"),("UpperArm","Arm"),("LowerArm","ForeArm"),
                ("Hand","Hand"),("UpperLeg","UpLeg"),("LowerLeg","Leg"),("Foot","Foot"),("Toes","ToeBase")})
                mappings.Add(side+pair.Item1,side+pair.Item2);
            foreach(var finger in new[]{("Thumb","Thumb"),("Index","Index"),("Middle","Middle"),("Ring","Ring"),("Little","Pinky")})
                for(int i=0;i<3;i++) mappings.Add(side+" "+finger.Item1+" "+new[]{"Proximal","Intermediate","Distal"}[i],side+"Hand"+finger.Item2+(i+1));
        }
        description.human = mappings.Select(p=>new HumanBone {
            humanName=p.Key, boneName="mixamorig:"+p.Value, limit=new HumanLimit {useDefaultValues=true}
        }).ToArray();
        importer.humanDescription=description;
        var defaults=importer.defaultClipAnimations;
        var source=defaults.SingleOrDefault(c=>c.name.IndexOf("mixamo",StringComparison.OrdinalIgnoreCase)>=0);
        if(source==null) throw new Exception("Expected exactly one Mixamo action; refusing Take 001 fallback");
        source.name="ThrowSource";
        // Unity's imported clip is zero based; Blender's action uses frames 1..70.
        source.firstFrame=0; source.lastFrame=69;
        source.lockRootRotation=source.lockRootHeightY=source.lockRootPositionXZ=true;
        source.keepOriginalOrientation=source.keepOriginalPositionY=source.keepOriginalPositionXZ=true;
        importer.clipAnimations=new[]{source}; importer.SaveAndReimport();
        var assets=AssetDatabase.LoadAllAssetsAtPath(path);
        var avatar=assets.OfType<Avatar>().Single();
        if(!avatar.isValid || !avatar.isHuman) throw new Exception("Invalid humanoid avatar");
        var clip=assets.OfType<AnimationClip>().Single(c=>c.name=="ThrowSource");
        if(!clip.humanMotion || Math.Abs(clip.length-2.3f)>.001f) throw new Exception("Invalid humanoid throw duration");
        var clipPath=Folder+"ThrowSource.anim";
        AssetDatabase.DeleteAsset(clipPath);
        AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(clip),clipPath);
        var bundleAssets=new List<string> {clipPath};
        var actionImporter=(ModelImporter)AssetImporter.GetAtPath(Folder+"BolaActions.fbx");
        actionImporter.animationType=ModelImporterAnimationType.Human;
        actionImporter.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
        actionImporter.animationCompression=ModelImporterAnimationCompression.Off;
        actionImporter.SaveAndReimport();
        var actionDescription=actionImporter.humanDescription;
        actionDescription.human=description.human;
        actionImporter.humanDescription=actionDescription;
        var actions=actionImporter.defaultClipAnimations;
        string[] actionNames={"ChargeStart","ChargeLoop","ChargeCancel","ThrowRelease","ThrowRecover"};
        foreach(var action in actions)
        {
            action.name=actionNames.Single(n=>action.name.EndsWith(n,StringComparison.Ordinal));
            action.loopTime=action.name=="ChargeLoop";
            action.lockRootRotation=action.lockRootHeightY=action.lockRootPositionXZ=true;
            action.keepOriginalOrientation=action.keepOriginalPositionY=action.keepOriginalPositionXZ=true;
        }
        actionImporter.clipAnimations=actions; actionImporter.SaveAndReimport();
        var actionAssets=AssetDatabase.LoadAllAssetsAtPath(Folder+"BolaActions.fbx");
        var actionAvatar=actionAssets.OfType<Avatar>().Single();
        if(!actionAvatar.isValid || !actionAvatar.isHuman) throw new Exception("Adapted avatar failed validation");
        foreach(string name in actionNames)
        {
            var action=actionAssets.OfType<AnimationClip>().Single(c=>c.name==name);
            if(!action.humanMotion) throw new Exception("Adapted clip is not humanoid: "+name);
            string assetPath=Folder+name+".anim";
            AssetDatabase.DeleteAsset(assetPath); AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(action),assetPath);
            bundleAssets.Add(assetPath);
            Debug.Log("BOLA_ACTION "+name+" "+action.length);
        }

        var root=new GameObject("BolaVisual");
        var groups=new List<Renderer[]>();
        foreach(string lod in new[]{"Close","Distant"})
        {
            string modelPath=Folder+"Bola"+lod+".fbx";
            var modelImporter=(ModelImporter)AssetImporter.GetAtPath(modelPath);
            modelImporter.materialImportMode=ModelImporterMaterialImportMode.None;
            modelImporter.importAnimation=false; modelImporter.isReadable=true; modelImporter.SaveAndReimport();
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),root.transform);
            model.name=lod;
            var renderers=model.GetComponentsInChildren<MeshRenderer>();
            if(renderers.Length!=7) throw new Exception("Expected grip, three weights and three ropes");
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Debug.Log("BOLA_MODEL_BOUNDS "+lod+" "+bounds);
            if(bounds.size.y<.7f || bounds.size.y>1.1f) throw new Exception("Bola dimensions outside prototype metre scale");
            foreach(var renderer in renderers) renderer.sharedMaterials=new Material[renderer.sharedMaterials.Length];
            groups.Add(renderers);
        }
        root.AddComponent<LODGroup>().SetLODs(new[]{new LOD(.08f,groups[0]),new LOD(.015f,groups[1])});
        root.GetComponent<LODGroup>().RecalculateBounds();
        string prefabPath=Folder+"BolaVisual.prefab";
        PrefabUtility.SaveAsPrefabAsset(root,prefabPath); UnityEngine.Object.DestroyImmediate(root);
        bundleAssets.Add(prefabPath);
        AssetDatabase.SaveAssets();
        string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/bundle"));
        Directory.CreateDirectory(output);
        var manifest=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild {
            assetBundleName="bola.prototype.assets",assetNames=bundleAssets.ToArray()
        }},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
        if(!manifest) throw new Exception("Bundle build failed");
        var deps=AssetDatabase.GetDependencies(bundleAssets.ToArray(),true);
        if(deps.Any(p=>p.EndsWith("ThrowSource.fbx"))) throw new Exception("Reference robot unexpectedly included in bundle");
        File.WriteAllText(Path.Combine(output,"../unity-validation.json"),JsonUtility.ToJson(new Report {
            unity=Application.unityVersion,avatarValid=avatar.isValid,avatarHuman=avatar.isHuman,
            humanMappings=description.human.Length,clipSeconds=clip.length,dependencies=deps
        },true));
        Debug.Log("BOLA_ASSETS_OK");
    }
    [Serializable] sealed class Report
    {
        public string unity;
        public bool avatarValid,avatarHuman;
        public int humanMappings;
        public float clipSeconds;
        public string[] dependencies;
    }
}
