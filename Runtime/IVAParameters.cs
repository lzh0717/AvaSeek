using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace IVAAvatar
{
    /// <summary>
    /// Generic, by-name access to every tweakable field on an <see cref="IVARenderer"/>.
    ///
    /// This is what makes the avatar "parameterised": anything that can produce a
    /// (name, value) pair - a slider panel, a CSV replay, a network message, an
    /// optimizer - can drive the face without a single line of per-parameter code.
    ///
    /// Only public instance fields carrying a [Range] attribute are treated as
    /// parameters, so internal state and non-numeric fields are never touched.
    /// </summary>
    public static class IVAParameters
    {
        const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

        static FieldInfo[] _fields;
        static Dictionary<string, FieldInfo> _byName;

        /// <summary>Every parameter field, in declaration order.</summary>
        public static IReadOnlyList<FieldInfo> Fields => _fields ?? Build();

        /// <summary>Every parameter name, in declaration order.</summary>
        public static IReadOnlyList<string> Names
        {
            get
            {
                var f = Fields;
                var names = new string[f.Count];
                for (int i = 0; i < f.Count; i++) names[i] = f[i].Name;
                return names;
            }
        }

        static FieldInfo[] Build()
        {
            var all = typeof(IVARenderer).GetFields(PublicInstance);
            var keep = new List<FieldInfo>(all.Length);
            foreach (var f in all)
            {
                if (f.FieldType != typeof(float) && f.FieldType != typeof(int)) continue;
                if (System.Attribute.GetCustomAttribute(f, typeof(RangeAttribute)) == null) continue;
                keep.Add(f);
            }
            _fields = keep.ToArray();
            _byName = new Dictionary<string, FieldInfo>(_fields.Length);
            foreach (var f in _fields) _byName[f.Name] = f;
            return _fields;
        }

        /// <summary>The [Range] bounds of a parameter. Returns false for unknown names.</summary>
        public static bool TryGetRange(string name, out float min, out float max)
        {
            min = 0f; max = 1f;
            if (!TryGetField(name, out var f)) return false;
            var r = (RangeAttribute)System.Attribute.GetCustomAttribute(f, typeof(RangeAttribute));
            if (r == null) return false;
            min = r.min; max = r.max;
            return true;
        }

        /// <summary>Reads a parameter. Returns false for unknown names.</summary>
        public static bool TryGet(IVARenderer avatar, string name, out float value)
        {
            value = 0f;
            if (avatar == null || !TryGetField(name, out var f)) return false;
            value = f.FieldType == typeof(int) ? (int)f.GetValue(avatar) : (float)f.GetValue(avatar);
            return true;
        }

        /// <summary>
        /// Writes a parameter, clamped into its [Range]. Returns false for unknown names -
        /// check the result so a typo shows up instead of silently doing nothing.
        /// </summary>
        public static bool TrySet(IVARenderer avatar, string name, float value)
        {
            if (avatar == null || !TryGetField(name, out var f)) return false;
            var r = (RangeAttribute)System.Attribute.GetCustomAttribute(f, typeof(RangeAttribute));
            if (r != null) value = Mathf.Clamp(value, r.min, r.max);
            if (f.FieldType == typeof(int)) f.SetValue(avatar, Mathf.RoundToInt(value));
            else                            f.SetValue(avatar, value);
            return true;
        }

        static bool TryGetField(string name, out FieldInfo f)
        {
            if (_byName == null) Build();
            return _byName.TryGetValue(name, out f);
        }
    }
}
