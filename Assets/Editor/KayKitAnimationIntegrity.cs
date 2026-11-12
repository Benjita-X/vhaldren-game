using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Vhaldren.EditorTools
{
    /// <summary>
    /// Repara el Controller KayKit DESDE EL EDITOR cuando contiene únicamente
    /// los parámetros de la versión antigua. Los scripts de habilidades
    /// nuevos dependen de estos triggers; si faltan, el golpe existe pero
    /// no se reproduce su animación.
    ///
    /// Solo se comprueba al abrir el Editor (y mediante menú). Nunca durante
    /// Play, builds, batchmode, pruebas automatizadas o en runtime.
    /// </summary>
    [InitializeOnLoad]
    public static class KayKitAnimationIntegrity
    {
        private static readonly string[] Required =
        {
            "MoveX", "MoveY", "Dodge", "DodgeDir", "Shoot",
            "Harvest", "HarvestStyle", "SwordAction", "SpellAction",
            "FrostAction", "ShieldAction", "WardAction", "ParryAction"
        };

        private static bool checkedThisEditorSession;

        static KayKitAnimationIntegrity()
        {
            EditorApplication.delayCall += CheckOnce;
        }

        [MenuItem("Vhaldren/Personaje/Validar y reparar animaciones del juego")]
        private static void ValidateFromMenu()
        {
            checkedThisEditorSession = false;
            CheckOnce();
        }

        private static void CheckOnce()
        {
            if (checkedThisEditorSession || Application.isBatchMode ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            // Evitar reparar un proyecto parcialmente importado. En ese caso
            // el usuario puede ejecutar el menú una vez importados los assets.
            if (!KayKitCharacterSetup.IsAvailable)
            {
                Debug.LogWarning("[Vhaldren] Falta RogueHooded.fbx. " +
                    "No se pueden preparar las animaciones de combate.");
                return;
            }

            checkedThisEditorSession = true;

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                KayKitCharacterSetup.ControllerPath);

            if (controller != null && HasAllParameters(controller))
                return;

            try
            {
                Debug.LogWarning(
                    "[Vhaldren] El controlador KayKit no incluía todas las " +
                    "animaciones del combate. Reparando automáticamente...");
                KayKitCharacterSetup.Prepare();

                controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    KayKitCharacterSetup.ControllerPath);
                if (controller == null || !HasAllParameters(controller))
                    throw new InvalidOperationException(
                        "El controlador regenerado aún no tiene los parámetros requeridos.");

                Debug.Log("[Vhaldren] Animator KayKit reparado: magia, espada, " +
                    "escudo, parry y recolección preparados.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Vhaldren] No fue posible reparar el Animator. " +
                    "Ejecuta Vhaldren > Personaje > Preparar personaje KayKit. " +
                    ex.Message);
            }
        }

        private static bool HasAllParameters(AnimatorController controller)
        {
            if (controller == null)
                return false;

            AnimatorControllerParameter[] current = controller.parameters;
            for (int i = 0; i < Required.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < current.Length; j++)
                {
                    if (current[j].name == Required[i])
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }
            return true;
        }
    }
}
