using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Vhaldren.EditorTools.Mundo
{
    /// <summary>
    /// Conecta el generador con componentes de juego por nombre (exploración, monturas,
    /// NPC). Si un componente aún no existe o su firma cambia, avisa y sigue: el mapa se
    /// genera igual.
    /// </summary>
    public static class MundoGancho
    {
        public static Type Tipo(string nombreCompleto)
        {
            Type t = Type.GetType(nombreCompleto + ", Vhaldren.Gameplay");
            if (t != null) return t;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = a.GetType(nombreCompleto);
                if (t != null) return t;
            }
            return null;
        }

        public static Component Añadir(GameObject go, string tipo)
        {
            Type t = Tipo(tipo);
            if (t == null)
            {
                Avisar("Falta el componente " + tipo);
                return null;
            }
            Component c = go.GetComponent(t);
            return c != null ? c : go.AddComponent(t);
        }

        /// <summary>Llama a un método público (por número de argumentos y tipos compatibles).</summary>
        public static bool Llamar(object objetivo, string metodo, params object[] args)
        {
            if (objetivo == null) return false;
            Type t = objetivo as Type ?? objetivo.GetType();
            object inst = objetivo is Type ? null : objetivo;
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (m.Name != metodo) continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length < args.Length) continue;
                object[] reales = new object[ps.Length];
                bool ok = true;
                for (int i = 0; i < ps.Length && ok; i++)
                {
                    if (i >= args.Length)
                    {
                        if (ps[i].HasDefaultValue) reales[i] = ps[i].DefaultValue;
                        else ok = false;
                        continue;
                    }
                    object a = args[i];
                    Type pt = ps[i].ParameterType;
                    if (a == null) { reales[i] = null; continue; }
                    if (pt.IsInstanceOfType(a)) { reales[i] = a; continue; }
                    try
                    {
                        if (pt == typeof(Vector2) && a is Vector3 v3) reales[i] = new Vector2(v3.x, v3.z);
                        else if (pt == typeof(Vector3) && a is Vector2 v2) reales[i] = new Vector3(v2.x, 0f, v2.y);
                        else if (pt == typeof(Color) && a is Vector3 c3) reales[i] = new Color(c3.x, c3.y, c3.z);
                        else if (pt.IsEnum && a is string es) reales[i] = Enum.Parse(pt, es, true);
                        else reales[i] = Convert.ChangeType(a, pt);
                    }
                    catch { ok = false; }
                }
                if (!ok) continue;
                try
                {
                    m.Invoke(inst, reales);
                    return true;
                }
                catch (Exception ex)
                {
                    Avisar(t.Name + "." + metodo + " falló: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                    return false;
                }
            }
            Avisar("No encontré " + t.Name + "." + metodo + " con " + args.Length + " argumentos");
            return false;
        }

        /// <summary>Asigna un campo serializado (privado o público) por nombre.</summary>
        public static void Campo(Component c, string campo, UnityEngine.Object valor)
        {
            if (c == null) return;
            SerializedObject so = new SerializedObject(c);
            SerializedProperty p = so.FindProperty(campo);
            if (p == null) { Avisar(c.GetType().Name + " no tiene el campo " + campo); return; }
            p.objectReferenceValue = valor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static readonly System.Collections.Generic.HashSet<string> avisados = new System.Collections.Generic.HashSet<string>();

        private static void Avisar(string texto)
        {
            if (avisados.Add(texto))
                Debug.LogWarning("[Vhaldren] Generador del mundo: " + texto);
        }
    }
}
