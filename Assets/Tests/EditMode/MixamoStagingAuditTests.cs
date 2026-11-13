using System;
using System.Reflection;
using NUnit.Framework;

namespace Vhaldren.Tests
{
    /// <summary>
    /// Prueba solo clasificacion de importacion; no reimporta FBX ni toca
    /// assets/escenas de otros agentes.
    /// </summary>
    public sealed class MixamoStagingAuditTests
    {
        private static MethodInfo LoopMethod()
        {
            Type type = Type.GetType("Vhaldren.EditorTools.MixamoStagingAudit, Assembly-CSharp-Editor");
            Assert.That(type, Is.Not.Null, "La herramienta Mixamo debe compilar en Editor.");
            MethodInfo method = type.GetMethod("ShouldLoop", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        [TestCase("sword and shield idle", true)]
        [TestCase("great sword walk", true)]
        [TestCase("standing run forward", true)]
        [TestCase("sword and shield block idle", true)]
        [TestCase("Standing melee run jump attack", false)]
        [TestCase("Great Sword Slide Attack", false)]
        [TestCase("Sword And Shield Death", false)]
        [TestCase("Standing Dodge Backward", false)]
        [TestCase("Sword And Shield Casting", false)]
        [TestCase("great sword 180 turn", false)]
        public void ClassifyLoopWithoutChangingAnyAsset(string name, bool expected)
        {
            bool actual = (bool)LoopMethod().Invoke(null, new object[] { name });
            Assert.That(actual, Is.EqualTo(expected), name);
        }
    }
}
