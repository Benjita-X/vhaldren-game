using System;
using NUnit.Framework;
using UnityEditor;

namespace Vhaldren.Tests
{
    public sealed class KayKitControllerGuidTests
    {
        private const string ControllerPath = "Assets/ThirdParty/KayKitAdventurers/RogueHooded.controller";

        [Test]
        public void RegeneratingTheCharacterKeepsTheAnimatorControllerGuid()
        {
            // Las escenas guardan el Animator por GUID; si cambiara, perderian la animacion.
            Type setup = Type.GetType("Vhaldren.EditorTools.KayKitCharacterSetup, Assembly-CSharp-Editor");
            Assert.That(setup, Is.Not.Null, "No se encontro KayKitCharacterSetup.");
            setup.GetMethod("Prepare").Invoke(null, null);
            string before = AssetDatabase.AssetPathToGUID(ControllerPath);
            Assert.That(before, Is.Not.Empty);

            setup.GetMethod("Prepare").Invoke(null, null);

            Assert.That(AssetDatabase.AssetPathToGUID(ControllerPath), Is.EqualTo(before));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(ControllerPath), Is.Not.Null);
        }
    }
}
