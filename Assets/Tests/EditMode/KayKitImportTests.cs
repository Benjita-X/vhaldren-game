using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Vhaldren.Tests
{
    public sealed class KayKitImportTests
    {
        private const string Folder = "Assets/ThirdParty/KayKitDungeon/Models/";

        [Test]
        public void OriginalModelsImportAsGameObjects()
        {
            string[] names =
            {
                "barrel_small.obj", "chest.obj", "chest_lid.obj",
                "pillar.obj", "torch_lit.obj"
            };

            foreach (string name in names)
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name),
                    Is.Not.Null, "Unity no pudo importar el modelo CC0: " + name);
        }

        [Test]
        public void SharedAtlasImportsAsTexture()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "dungeon_texture.png"),
                Is.Not.Null, "El atlas de KayKit debe importarse como Texture2D.");
        }
    }
}
