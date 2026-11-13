using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vhaldren.EditorTools
{
    /// <summary>
    /// Herramienta OPT-IN para FBX Mixamo. Nunca modifica KayKit, escenas,
    /// controladores existentes ni codigo de gameplay.
    /// Menu 1 solo inspecciona; menu 2 configura exclusivamente el staging.
    /// </summary>
    public static class MixamoStagingAudit
    {
        public const string Root = "Assets/ThirdParty/Personajes/Animaciones/Mixamo";
        public const string ReferencePath = Root + "/Reference/X Bot.fbx";

        [MenuItem("Vhaldren/Animaciones Mixamo/1 - Auditar staging (solo lectura)")]
        public static void Audit()
        {
            List<string> files = FindFiles();
            if (files.Count == 0)
            {
                Debug.LogWarning("[Vhaldren/Mixamo] No hay FBX en " + Root +
                    ". Importa primero UN X Bot en Reference y clips en subcarpetas.");
                return;
            }

            int validRig = 0, validAvatar = 0, noAnimation = 0, embeddedMeshes = 0;
            List<string> issues = new List<string>();
            HashSet<string> filenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Avatar reference = FindHumanAvatar(ReferencePath);

            if (reference == null)
                issues.Add("Falta Avatar Humanoid valido en " + ReferencePath +
                    ". NO se conectaran animaciones hasta configurarlo.");

            foreach (string path in files)
            {
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    issues.Add("No hay ModelImporter: " + path);
                    continue;
                }

                if (importer.animationType == ModelImporterAnimationType.Human)
                    validRig++;
                else
                    issues.Add("Rig NO Humanoid: " + path);

                if (importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther &&
                    importer.sourceAvatar != null)
                    validAvatar++;

                if (!importer.importAnimation && path != ReferencePath)
                {
                    noAnimation++;
                    issues.Add("Animacion desactivada: " + path);
                }

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null)
                {
                    int bones = 0;
                    foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
                        if (bone.name.StartsWith("mixamorig:", StringComparison.Ordinal))
                            bones++;

                    if (bones != 65)
                        issues.Add("Huesos Mixamo: " + bones + " en " + path +
                            " (en Drive se verificaron 65 por FBX)");
                    if (model.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                        embeddedMeshes++;
                }

                string name = Path.GetFileName(path);
                if (!filenames.Add(name))
                    issues.Add("Nombre repetido en carpetas distintas: " + name);
            }

            Debug.Log("[Vhaldren/Mixamo] AUDITORIA SIN CAMBIOS: " + files.Count +
                " FBX; Humanoid=" + validRig + "; Avatar copiado=" + validAvatar +
                "; animacion desactivada=" + noAnimation +
                "; archivos con malla=" + embeddedMeshes +
                "; avisos=" + issues.Count + "\n" + string.Join("\n", issues));
        }

        [MenuItem("Vhaldren/Animaciones Mixamo/2 - Preparar rigs del staging (confirmar)")]
        public static void Configure()
        {
            List<string> files = FindFiles();
            if (!files.Contains(ReferencePath))
            {
                Debug.LogError("[Vhaldren/Mixamo] Primero coloca UN X Bot.fbx (With Skin) en " +
                    ReferencePath + ". No se ha modificado ningun FBX.");
                return;
            }
            if (!EditorUtility.DisplayDialog("Vhaldren - Mixamo",
                "Se reimportaran solo los FBX bajo " + Root +
                "\nNo se modificaran KayKit, Animator Controllers ni escenas." +
                "\n¿Continuar?", "Preparar staging", "Cancelar"))
                return;

            ModelImporter referenceImporter =
                AssetImporter.GetAtPath(ReferencePath) as ModelImporter;
            if (referenceImporter == null)
            {
                Debug.LogError("[Vhaldren/Mixamo] No se puede leer el X Bot de referencia.");
                return;
            }

            // El Avatar de referencia debe existir y validar como humano antes de
            // reimportar clips. NO se usa un avatar Generic de KayKit.
            referenceImporter.animationType = ModelImporterAnimationType.Human;
            referenceImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            referenceImporter.materialImportMode = ModelImporterMaterialImportMode.None;
            referenceImporter.SaveAndReimport();

            Avatar avatar = FindHumanAvatar(ReferencePath);
            if (avatar == null)
            {
                Debug.LogError("[Vhaldren/Mixamo] Unity no pudo crear un Avatar Humanoid " +
                    "valido desde X Bot. Proceso detenido; revisa Rig > Configure.");
                return;
            }

            int updated = 0;
            try
            {
                for (int i = 0; i < files.Count; i++)
                {
                    string path = files[i];
                    if (path == ReferencePath)
                        continue;

                    EditorUtility.DisplayProgressBar("Vhaldren: preparando Mixamo",
                        path, (float)i / Mathf.Max(files.Count, 1));
                    ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer == null)
                        continue;

                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = avatar;
                    importer.importAnimation = true;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;

                    ModelImporterClipAnimation[] clips = importer.clipAnimations;
                    if (clips == null || clips.Length == 0)
                        clips = importer.defaultClipAnimations;
                    if (clips != null)
                    {
                        bool loop = ShouldLoop(Path.GetFileNameWithoutExtension(path));
                        foreach (ModelImporterClipAnimation clip in clips)
                        {
                            clip.loopTime = loop;
                            clip.loopPose = loop;
                            // Movimiento por PlayerMotor, no por el FBX. No se
                            // reescriben los deltas de esquivas ni ataques.
                            if (loop)
                                clip.lockRootPositionXZ = true;
                        }
                        importer.clipAnimations = clips;
                    }
                    importer.SaveAndReimport();
                    updated++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            Debug.Log("[Vhaldren/Mixamo] " + updated +
                " FBX preparados como Humanoid con Avatar X Bot. " +
                "SIN conectar controladores. Revisar visualmente locomocion, manos y muerte.");
            Audit();
        }

        public static bool ShouldLoop(string filename)
        {
            string name = filename.ToLowerInvariant();
            bool candidates = name.Contains("idle") || name.Contains("walk") ||
                name.Contains("run") || name.Contains("strafe");
            bool oneShot = name.Contains("attack") || name.Contains("jump") ||
                name.Contains("death") || name.Contains("impact") ||
                name.Contains("react") || name.Contains("dodge") ||
                name.Contains("slash") || name.Contains("kick") ||
                name.Contains("slide") || name.Contains("turn") ||
                name.Contains("casting") || name.Contains("cast spell") ||
                name.Contains("equip") || name.Contains("sheath") ||
                name.Contains("disarm") || name.Contains("power up");
            return candidates && !oneShot;
        }

        private static Avatar FindHumanAvatar(string path)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Avatar avatar && avatar.isValid && avatar.isHuman)
                    return avatar;
            return null;
        }

        private static List<string> FindFiles()
        {
            List<string> files = new List<string>();
            if (!AssetDatabase.IsValidFolder(Root))
                return files;

            foreach (string guid in AssetDatabase.FindAssets("", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    files.Add(path);
            }
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }
    }
}
