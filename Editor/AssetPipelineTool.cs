using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace assetpipelinetool{
    public class AssetPipelineTool : EditorWindow
    {
        private enum Tab { Prefabs, Materials, Textures }
        private Tab currentTab = Tab.Prefabs;
        private Vector2 scrollPos;
        private string statusLabel = "Ready.";

        private List<GameObject> modelsToProcess = new List<GameObject>();
        private List<Material> materialsToLink = new List<Material>();
        private List<Texture2D> texturesToProcess = new List<Texture2D>();

        private DefaultAsset materialSearchFolder; 
        private string textureBaseName = ""; 
        private bool autoStandardizeName = true;
        private bool resizeEnabled = false;
        private int targetResolution = 1024;
        private bool moveToOldAssets = false;

        private Shader pbrShader;
        private bool useEmptyRoot = true;
        private Texture2D headerLogo;
        private float logoAspectRatio;

        [MenuItem("Tools/AssetPipelineTool")]
        public static void ShowWindow() => GetWindow<AssetPipelineTool>("AssetPipelineTool");

        private void OnEnable()
        {
            headerLogo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Editor/Icons/AtlasToolLogo.png");
            if (headerLogo != null) logoAspectRatio = (float)headerLogo.width / headerLogo.height;
            if (pbrShader == null) pbrShader = Shader.Find("Universal Render Pipeline/Lit");
        }

        private void OnGUI()
        {
            DrawHeader();
            currentTab = (Tab)GUILayout.Toolbar((int)currentTab, new string[] { "PREFABS", "MATERIALS", "TEXTURES" }, GUILayout.Height(25));
        
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10); 
            EditorGUILayout.BeginVertical();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            switch (currentTab)
            {
                case Tab.Prefabs: DrawPrefabTab(); break;
                case Tab.Materials: DrawMaterialTab(); break;
                case Tab.Textures: DrawTextureTab(); break;
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
            GUILayout.Space(10); 
            EditorGUILayout.EndHorizontal();

            DrawFooter();
        }

        private void DrawPrefabTab()
        {
            DrawSectionHeader("PREFAB CREATOR", "Convert FBX models into standardized PBR Prefabs.");
            DrawPreview(modelsToProcess.Count > 0 ? modelsToProcess[0] : Selection.activeGameObject);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            pbrShader = (Shader)EditorGUILayout.ObjectField("Target Shader", pbrShader, typeof(Shader), false);
            useEmptyRoot = EditorGUILayout.Toggle("Add Empty Root", useEmptyRoot);
            EditorGUILayout.EndVertical();

            DrawList(modelsToProcess, "Drop FBX Models here...");
        
            if (DrawMainButton("PROCESS PREFABS"))
            {
                ProcessModels(modelsToProcess.Count > 0 ? new List<GameObject>(modelsToProcess) : GetSelected<GameObject>());
            }
        }

        private void DrawMaterialTab()
        {
            DrawSectionHeader("SMART LINKER", "Auto-link textures and fix Import Settings.");
            DrawPreview(materialsToLink.Count > 0 ? materialsToLink[0] : (Selection.activeObject as Material));

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            materialSearchFolder = (DefaultAsset)EditorGUILayout.ObjectField("Search Folder (Optional)", materialSearchFolder, typeof(DefaultAsset), false);
            EditorGUILayout.HelpBox("Automatically detects Normal Maps and fixes Texture Type to 'NormalMap'.", MessageType.Info);
            EditorGUILayout.EndVertical();

            DrawList(materialsToLink, "Drop Materials here...");

            if (DrawMainButton("LINK & FIX MATERIALS"))
            {
                ProcessMaterials(materialsToLink.Count > 0 ? new List<Material>(materialsToLink) : GetSelected<Material>());
            }
        }

        private void DrawTextureTab()
        {
            DrawSectionHeader("TEXTURE STUDIO", "Rename, Resize, and Standardize Texture types.");
            DrawPreview(texturesToProcess.Count > 0 ? texturesToProcess[0] : (Selection.activeObject as Texture2D));

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            textureBaseName = EditorGUILayout.TextField("New Base Name", textureBaseName);
            autoStandardizeName = EditorGUILayout.Toggle("Auto-Standardize", autoStandardizeName);
            resizeEnabled = EditorGUILayout.Toggle("Enable Resizing", resizeEnabled);
            if (resizeEnabled)
            {
                targetResolution = EditorGUILayout.IntPopup("Resolution", targetResolution, new string[] { "128", "256", "512", "1024", "2048", "4096" }, new int[] { 128, 256, 512, 1024, 2048, 4096 });
                moveToOldAssets = EditorGUILayout.Toggle("Backup Originals", moveToOldAssets);
            }
            EditorGUILayout.EndVertical();

            DrawList(texturesToProcess, "Drop Textures here...");

            if (DrawMainButton("PROCESS TEXTURES"))
            {
                ProcessTextures(texturesToProcess.Count > 0 ? new List<Texture2D>(texturesToProcess) : GetSelected<Texture2D>());
            }
        }
   
        private void FixTextureImporter(string path, string type)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool changed = false;
            if (type == "Normal" && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.convertToNormalmap = false;
                changed = true;
            }

            if (changed) importer.SaveAndReimport();
        }

        private void FixModelImporter(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null && importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.SaveAndReimport();
            }
        }
   
        private void ProcessTextures(List<Texture2D> list)
        {
            string backupDir = "Assets/Old_Assets/IMG";
            if (resizeEnabled && moveToOldAssets) CreateFolderSafe(backupDir);

            Material[] allProjectMaterials = GetAllMaterialsInProject();

            for (int i = 0; i < list.Count; i++)
            {
                Texture2D tex = list[i];
                if (!tex) continue;

                string oldPath = AssetDatabase.GetAssetPath(tex);
                string oldName = tex.name;
                string type = IdentifyTextureType(oldName.ToLower(), oldPath);
            
                FixTextureImporter(oldPath, type);

                string newName = BuildStandardName(tex, i, list.Count, type);
                string extension = resizeEnabled ? ".png" : Path.GetExtension(oldPath);
                string newPath = Path.Combine(Path.GetDirectoryName(oldPath), newName + extension).Replace("\\", "/");

                var materialRefs = GetAffectedMaterials(tex, allProjectMaterials);

                if (resizeEnabled)
                {
                    byte[] bytes = GetResizedPNGBytes(tex);
                    if (moveToOldAssets)
                    {
                        string bkpPath = Path.Combine(backupDir, Path.GetFileName(oldPath)).Replace("\\", "/");
                        AssetDatabase.MoveAsset(oldPath, bkpPath);
                    }
                    else if (oldPath != newPath) AssetDatabase.DeleteAsset(oldPath);

                    File.WriteAllBytes(Path.Combine(Application.dataPath, newPath.Replace("Assets/", "")), bytes);
                    AssetDatabase.ImportAsset(newPath, ImportAssetOptions.ForceUpdate);
                
                    Texture2D newTex = AssetDatabase.LoadAssetAtPath<Texture2D>(newPath);
                    if (newTex) RelinkMaterials(materialRefs, newTex);
                } 
                else if (oldName != newName) 
                {
                    AssetDatabase.RenameAsset(oldPath, newName);
                }
            }
            texturesToProcess.Clear();
            FinalizeAction("Textures processed.");
        }

        private void ProcessMaterials(List<Material> list)
        {
            string customPath = materialSearchFolder ? AssetDatabase.GetAssetPath(materialSearchFolder) : null;
            foreach (var m in list)
            {
                if (!m) continue;
                string searchPath = customPath ?? Path.GetDirectoryName(AssetDatabase.GetAssetPath(m));
                LinkAndStandardize(m, searchPath, m.name.Replace("Mat_", ""));
            }
            materialsToLink.Clear();
            FinalizeAction("Materials linked.");
        }

        private void ProcessModels(List<GameObject> list)
        {
            foreach (var model in list)
            {
                if (!model) continue;
                string path = AssetDatabase.GetAssetPath(model);
                FixModelImporter(path);

                string dir = Path.GetDirectoryName(path);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                foreach (var ren in instance.GetComponentsInChildren<Renderer>())
                {
                    Material[] mats = ren.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i]) mats[i] = GetOrCreateMat(mats[i].name.Replace("Mat_", ""), dir);
                    ren.sharedMaterials = mats;
                }
                SavePrefab(instance, dir, model.name);
                DestroyImmediate(instance);
            }
            modelsToProcess.Clear();
            FinalizeAction("Prefabs created.");
        }
    
        private string IdentifyTextureType(string name, string path)
        {
            if (Match(name, "_n", "_normal", "_norm", "_nm", "_bump")) return "Normal";
            if (Match(name, "_e", "_emiss", "_glow", "_light")) return "Emission";
            if (Match(name, "_albedo", "_bc", "_base", "_color", "_diff", "_d")) return "Albedo";
        
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType == TextureImporterType.NormalMap) return "Normal";

            return "Albedo";
        }

        private string BuildStandardName(Texture2D tex, int index, int total, string type)
        {
            bool hasBaseName = !string.IsNullOrEmpty(textureBaseName);
      
            if (autoStandardizeName)
            {
                string suffix = type switch { "Normal" => "_Normal", "Emission" => "_Emission", _ => "_Albedo" };
                string core = hasBaseName ? textureBaseName : tex.name;

                if (!hasBaseName)
                {
                    string low = core.ToLower();
                    string[] sufs = { "_n", "_normal", "_norm", "_nm", "_bump", "_albedo", "_bc", "_base", "_color", "_diff", "_d", "_e", "_emiss" };
                    foreach(var s in sufs) if(low.EndsWith(s)) { core = core.Substring(0, low.LastIndexOf(s)); break; }
                }
                else if (total > 1) 
                {
                    core += $"_{index:D2}";
                }

                return $"Tex_{core}{suffix}";
            }

            if (hasBaseName)
            {
                return textureBaseName + (total > 1 ? $"_{index:D2}" : "");
            }
            return tex.name;
        }

        private bool Match(string text, params string[] keys) { foreach (var k in keys) if (text.EndsWith(k) || text.Contains(k + "_")) return true; return false; }
    
        private Material GetOrCreateMat(string name, string folder)
        {
            string path = $"{folder}/Mat_{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(pbrShader); AssetDatabase.CreateAsset(m, path); }
            LinkAndStandardize(m, folder, name);
            return m;
        }

        private void SavePrefab(GameObject obj, string folder, string name)
        {
            string cleanName = name.Replace("Pfb_", "");
            GameObject root = obj;
            if (useEmptyRoot) { root = new GameObject("Pfb_" + cleanName); obj.transform.SetParent(root.transform); }
            else obj.name = "Pfb_" + cleanName;
            PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/Pfb_{cleanName}.prefab");
            if (useEmptyRoot) DestroyImmediate(root);
        }

        private struct MaterialReference { public Material mat; public string prop; }
        private List<MaterialReference> GetAffectedMaterials(Texture2D target, Material[] all)
        {
            var refs = new List<MaterialReference>();
            foreach (var m in all)
            {
                if (!m) continue;
                Shader s = m.shader;
                for (int i = 0; i < ShaderUtil.GetPropertyCount(s); i++)
                    if (ShaderUtil.GetPropertyType(s, i) == ShaderUtil.ShaderPropertyType.TexEnv && m.GetTexture(ShaderUtil.GetPropertyName(s, i)) == target)
                        refs.Add(new MaterialReference { mat = m, prop = ShaderUtil.GetPropertyName(s, i) });
            }
            return refs;
        }

        private void RelinkMaterials(List<MaterialReference> refs, Texture2D newT)
        {
            foreach (var r in refs) { Undo.RecordObject(r.mat, "Relink"); r.mat.SetTexture(r.prop, newT); EditorUtility.SetDirty(r.mat); }
        }

        private void LinkAndStandardize(Material mat, string folder, string baseName)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);

                if (fileName.Contains(baseName))
                {
                    string type = IdentifyTextureType(fileName.ToLower(), path);
                    FixTextureImporter(path, type);

                    string suffix = type switch { "Normal" => "_Normal", "Emission" => "_Emission", _ => "_Albedo" };
                    string slot = type switch { "Normal" => "_BumpMap", "Emission" => "_EmissionMap", _ => "_BaseMap" };

                    if (!string.IsNullOrEmpty(slot))
                    {
                        string standardName = $"Tex_{baseName}{suffix}";
                        if (fileName != standardName)
                        {
                            AssetDatabase.RenameAsset(path, standardName);
                            path = Path.Combine(Path.GetDirectoryName(path), standardName + Path.GetExtension(path)).Replace("\\", "/");
                        }
                        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        if (tex)
                        {
                            mat.SetTexture(slot, tex);
                            if (slot == "_BumpMap") mat.EnableKeyword("_NORMALMAP");
                        }
                    }
                }
            }
            EditorUtility.SetDirty(mat);
        }

        private byte[] GetResizedPNGBytes(Texture2D src)
        {
            RenderTexture rt = RenderTexture.GetTemporary(targetResolution, targetResolution);
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            Texture2D n = new Texture2D(targetResolution, targetResolution, TextureFormat.RGBA32, true);
            n.ReadPixels(new Rect(0, 0, targetResolution, targetResolution), 0, 0); n.Apply();
            RenderTexture.active = null; RenderTexture.ReleaseTemporary(rt);
            byte[] b = n.EncodeToPNG(); DestroyImmediate(n); return b;
        }

        private Material[] GetAllMaterialsInProject()
        {
            string[] guids = AssetDatabase.FindAssets("t:Material");
            List<Material> mats = new List<Material>();
            foreach (var g in guids) { var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)); if (m) mats.Add(m); }
            return mats.ToArray();
        }

        private void CreateFolderSafe(string path)
        {
            string[] parts = path.Split('/'); string current = parts[0];
            for (int i = 1; i < parts.Length; i++) {
                if (!AssetDatabase.IsValidFolder(current + "/" + parts[i])) AssetDatabase.CreateFolder(current, parts[i]);
                current += "/" + parts[i];
            }
        }
    
        private void DrawSectionHeader(string title, string sub) { GUILayout.Label(title, EditorStyles.boldLabel); GUILayout.Label(sub, EditorStyles.miniLabel); EditorGUILayout.Space(5); }
        private bool DrawMainButton(string label) { EditorGUILayout.Space(10); bool p = GUILayout.Button(label, GUILayout.Height(40)); EditorGUILayout.Space(10); return p; }
        private void DrawFooter() { EditorGUILayout.BeginVertical(EditorStyles.helpBox); GUILayout.Label($"Status: {statusLabel}", EditorStyles.centeredGreyMiniLabel); EditorGUILayout.EndVertical(); }
        private void DrawHeader() { if (headerLogo) GUILayout.Label(headerLogo, GUILayout.Width(position.width - 20), GUILayout.Height((position.width - 20) / logoAspectRatio)); }

        private void DrawPreview(Object obj)
        {
            Rect r = GUILayoutUtility.GetRect(0, 85, GUILayout.ExpandWidth(true)); GUI.Box(r, "", EditorStyles.helpBox);
            if (obj)
            {
                Texture2D p = AssetPreview.GetAssetPreview(obj);
                if (p) GUI.DrawTexture(new Rect(r.center.x - 35, r.y + 5, 70, 70), p, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(r.x, r.yMax - 18, r.width, 15), obj.name.Length > 25 ? obj.name.Substring(0, 22) + "..." : obj.name, EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawList<T>(List<T> list, string msg) where T : Object
        {
            EditorGUILayout.Space(5); Rect d = GUILayoutUtility.GetRect(0, 35, GUILayout.ExpandWidth(true)); GUI.Box(d, msg, (GUIStyle)"ProgressBarBack"); HandleDrag(d, list);
            for (int i = 0; i < list.Count; i++)
            {
                EditorGUILayout.BeginHorizontal(); list[i] = (T)EditorGUILayout.ObjectField(list[i], typeof(T), false);
                if (GUILayout.Button("×", GUILayout.Width(20))) { list.RemoveAt(i); GUIUtility.ExitGUI(); } EditorGUILayout.EndHorizontal();
            }
        }

        private void HandleDrag<T>(Rect a, List<T> l) where T : Object
        {
            Event e = Event.current;
            if ((e.type == EventType.DragUpdated || e.type == EventType.DragPerform) && a.Contains(e.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (Object o in DragAndDrop.objectReferences)
                    {
                        if (o is T t && !l.Contains(t)) l.Add(t);
                        if (currentTab == Tab.Materials && o is DefaultAsset && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(o))) materialSearchFolder = (DefaultAsset)o;
                    }
                }
                e.Use();
            }
        }

        private List<T> GetSelected<T>() where T : Object
        {
            var items = new List<T>();
            foreach (Object o in Selection.objects) if (o is T t) items.Add(t);
            return items;
        }

        private void FinalizeAction(string msg)
        {
            statusLabel = $"{System.DateTime.Now:HH:mm} - {msg}";
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); GUIUtility.ExitGUI();
        }
    }
}