// ----------------------------------------------------------------------------
// Лицензия MIT-ZARYA
// (c) 2025 Leopotam <leopotam@yandex.ru>
// ----------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
#if ENABLE_IL2CPP
using Unity.IL2CPP.CompilerServices;
#endif

namespace Leopotam.EcsProto {
#if ENABLE_IL2CPP
    [Il2CppSetOption (Option.NullChecks, false)]
    [Il2CppSetOption (Option.ArrayBoundsChecks, false)]
#endif
    public sealed class Slice<T> {
        T[] _data;
        int _len;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public Slice (int cap = 16, bool filled = false) {
            _data = new T[cap];
            _len = filled ? cap : 0;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public T[] Data () => _data;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ref T Get (int idx) {
#if DEBUG
            if (idx < 0 || idx >= _len) { throw new Exception ("неправильный индекс"); }
#endif
            return ref _data[idx];
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public void Add (in T v) {
            if (_data.Length == _len) {
                Array.Resize (ref _data, _len << 1);
            }
            _data[_len++] = v;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public int Len () => _len;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public int Cap () => _data.Length;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ref T RemoveLast () {
#if DEBUG
            if (_len == 0) { throw new Exception ("нет элементов для удаления"); }
#endif
            _len--;
            return ref _data[_len];
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public void RemoveAt (int idx) {
            if (idx >= 0 && idx < _len) {
                _data[idx] = default;
                _len--;
                if (idx < _len) {
                    (_data[idx], _data[_len]) = (_data[_len], _data[idx]);
                }
            }
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public void Clear (bool setDefaults = true) {
            if (setDefaults) {
                for (var i = 0; i < _len; i++) {
                    _data[i] = default;
                }
            }
            _len = 0;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public SliceEnumerator GetEnumerator () => new (this);

        public ref struct SliceEnumerator {
            readonly Slice<T> _slice;
            int _idx;

            [MethodImpl (MethodImplOptions.AggressiveInlining)]
            public SliceEnumerator (Slice<T> slice) {
                _slice = slice;
                _idx = -1;
            }

            public T Current {
                [MethodImpl (MethodImplOptions.AggressiveInlining)]
                get => _slice.Get (_idx);
            }

            [MethodImpl (MethodImplOptions.AggressiveInlining)]
            public bool MoveNext () {
                _idx++;
                return _idx < _slice._len;
            }
        }
    }

#if ENABLE_IL2CPP
    [Il2CppSetOption (Option.NullChecks, false)]
    [Il2CppSetOption (Option.ArrayBoundsChecks, false)]
#endif
    public static class EntityMask {
        static readonly ushort[] _bitsLut = {
            0, 1, 17, 2, 18, 50, 3, 57, 47, 19, 22, 51, 29, 4, 33, 58,
            15, 48, 20, 27, 25, 23, 52, 41, 54, 30, 38, 5, 43, 34, 59, 8,
            63, 16, 49, 56, 46, 21, 28, 32, 14, 26, 24, 40, 53, 37, 42, 7,
            62, 55, 45, 31, 13, 39, 36, 6, 61, 44, 12, 35, 60, 11, 10, 9,
        };

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static void Set (Slice<ulong> data, ushort len, ProtoEntity idx, ushort index) {
            var div = (ushort) (index >> 6);
            var rem = (ushort) (index - (div << 6));
            data.Get (idx._id * len + div) |= 1UL << rem;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static void Unset (Slice<ulong> data, ushort len, ProtoEntity idx, ushort index) {
            var div = (ushort) (index >> 6);
            var rem = (ushort) (index - (div << 6));
            data.Get (idx._id * len + div) &= ~(1UL << rem);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static bool Has (Slice<ulong> data, ushort len, ProtoEntity idx, ushort index) {
            var div = (ushort) (index >> 6);
            var rem = (ushort) (index - (div << 6));
            return (data.Get (idx._id * len + div) & (1UL << rem)) != 0UL;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static bool IsEmpty (Slice<ulong> data, ushort len, ProtoEntity idx) {
            for (int i = idx._id * len, iMax = (idx._id + 1) * len; i < iMax; i++) {
                if (data.Get (i) != 0UL) {
                    return false;
                }
            }
            return true;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static ushort Len (Slice<ulong> data, ushort len, ProtoEntity idx) {
            ushort count = 0;
            var offset = idx._id * len;
            for (var i = 0; i < len; i++, offset++) {
                var v = data.Get (offset);
                v -= (v >> 1) & 0x5555555555555555;
                v = (v & 0x3333333333333333) + ((v >> 2) & 0x3333333333333333);
                count += (ushort) ((((v + (v >> 4)) & 0xF0F0F0F0F0F0F0F) * 0x101010101010101) >> 56);
            }
            return count;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static int GetMinIndex (Slice<ulong> data, ushort len, ProtoEntity idx) {
            var offset = idx._id * len;
            for (var i = 0; i < len; i++, offset++) {
                var v = data.Get (offset);
                if (v != 0UL) {
                    return (i << 6) + _bitsLut[((ulong) ((long) v & -(long) v) * 0x37E84A99DAE458F) >> 58];
                }
            }
            return -1;
        }
    }
#if DEBUG || UNITY_EDITOR
    public static class DebugHelpers {
        public static string CleanTypeName (Type type) {
            string name;
            if (!type.IsGenericType) {
                name = type.Name;
            } else {
                var constraints = "";
                foreach (var constraint in type.GetGenericArguments ()) {
                    if (constraints.Length > 0) { constraints += ", "; }
                    constraints += CleanTypeName (constraint);
                }
                var genericIndex = type.Name.LastIndexOf ("`", StringComparison.Ordinal);
                var typeName = genericIndex == -1
                    ? type.Name
                    : type.Name.Substring (0, genericIndex);
                name = $"{typeName}<{constraints}>";
            }
            return name;
        }
    }
#endif
}

#if ENABLE_IL2CPP
// Unity IL2CPP performance optimization attribute.
namespace Unity.IL2CPP.CompilerServices {
    enum Option {
        NullChecks = 1,
        ArrayBoundsChecks = 2
    }

    [AttributeUsage (AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    class Il2CppSetOptionAttribute : Attribute {
        public Option Option { get; private set; }
        public object Value { get; private set; }

        public Il2CppSetOptionAttribute (Option option, object value) { Option = option; Value = value; }
    }
}
#endif
