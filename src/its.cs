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
    public interface IProtoIt {
        IProtoIt Init (ProtoWorld world);
        ProtoWorld World ();
        bool Has (ProtoEntity entity);
        int LenSlow ();
        bool IsEmptySlow ();
        (ProtoEntity Entity, bool Ok) FirstSlow ();
        IProtoPool[] Includes ();
        ProtoItIEnumerator GetEnumerator ();
#if DEBUG
        void AddBlocker (int amount);
#endif
    }

    public struct MaskItem {
        public int Idx;
        public ulong Data;
    }

    public ref struct ProtoItIEnumerator {
        readonly IProtoIt _it;
        readonly ProtoEntity[] _entities;
        readonly ProtoWorld _world;
        readonly Slice<MaskItem> _incIndices;
        readonly Slice<MaskItem> _excIndices;
        int _id;
        ProtoEntity _currEntity;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ProtoItIEnumerator (IProtoIt it, Slice<MaskItem> incIndices, Slice<MaskItem> excIndices) {
#if DEBUG
            it.AddBlocker (1);
#endif
            _it = it;
            var incPools = it.Includes ();
            var minPool = incPools[0];
            var minVal = minPool.Len ();
            for (var i = 1; i < incPools.Length; i++) {
                var p = incPools[i];
                var v = p.Len ();
                if (v < minVal) {
                    minVal = v;
                    minPool = p;
                }
            }
            _entities = minPool.Entities ();
            _id = minVal;
            _world = it.World ();
            _incIndices = incIndices;
            _excIndices = excIndices;
            _currEntity = default;
        }

        public ProtoEntity Current {
            [MethodImpl (MethodImplOptions.AggressiveInlining)]
            get => _currEntity;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public bool MoveNext () {
            while (_id > 0) {
                _id--;
                _currEntity = _entities[_id];
                if (_excIndices == null) {
                    if (_world.EntityCompatibleWith (_currEntity, _incIndices)) {
                        return true;
                    }
                } else {
                    if (_world.EntityCompatibleWithAndWithout (_currEntity, _incIndices, _excIndices)) {
                        return true;
                    }
                }
            }
            return false;
        }
#if DEBUG
        public void Dispose () => _it.AddBlocker (-1);
#endif
    }

#if ENABLE_IL2CPP
    [Il2CppSetOption (Option.NullChecks, false)]
    [Il2CppSetOption (Option.ArrayBoundsChecks, false)]
#endif
    public sealed class ProtoIt : IProtoIt {
        ProtoWorld _world;
        Type[] _iTypes;
        IProtoPool[] _incPools;
        Slice<ulong> _incMask;
        Slice<MaskItem> _incMaskIndices;
#if DEBUG
        bool _inited;
#endif

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ProtoIt (Type[] iTypes) {
#if DEBUG
            if (iTypes == null || iTypes.Length < 1) { throw new Exception ("некорректный список include-пулов для инициализации итератора"); }
#endif
            _iTypes = iTypes;
        }

        public IProtoIt Init (ProtoWorld world) {
            _world = world;
            var maskLen = world.EntityMaskItemLen ();
            _incMask = new (maskLen, true);
            _incPools = new IProtoPool[_iTypes.Length];
            _incMaskIndices = new (_iTypes.Length);
            ProtoEntity maskE = default;
            for (var i = 0; i < _iTypes.Length; i++) {
                var pool = _world.Pool (_iTypes[i]);
                EntityMask.Set (_incMask, maskLen, maskE, pool.Id ());
                _incPools[i] = pool;
            }
            for (int i = 0, iMax = _incMask.Len (); i < iMax; i++) {
                var data = _incMask.Get (i);
                if (data != 0) { _incMaskIndices.Add (new () { Idx = i, Data = data }); }
            }
#if DEBUG
            _inited = true;
#endif
            return this;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ItEnumerator GetEnumerator () => new (this);

        ProtoItIEnumerator IProtoIt.GetEnumerator () => new (this, _incMaskIndices, null);

        public ref struct ItEnumerator {
            readonly ProtoIt _it;
            readonly ProtoEntity[] _entities;
            int _id;
            ProtoEntity _currEntity;

            [MethodImpl (MethodImplOptions.AggressiveInlining)]
            public ItEnumerator (ProtoIt it) {
#if DEBUG
                it.AddBlocker (1);
#endif
                var minPool = it._incPools[0];
                var minVal = minPool.Len ();
                for (var i = 1; i < it._incPools.Length; i++) {
                    var p = it._incPools[i];
                    var v = p.Len ();
                    if (v < minVal) {
                        minVal = v;
                        minPool = p;
                    }
                }
                _it = it;
                _entities = minPool.Entities ();
                _id = minVal;
                _currEntity = default;
            }

            public ProtoEntity Current {
                [MethodImpl (MethodImplOptions.AggressiveInlining)]
                get => _currEntity;
            }

            [MethodImpl (MethodImplOptions.AggressiveInlining)]
            public bool MoveNext () {
                while (_id > 0) {
                    _id--;
                    _currEntity = _entities[_id];
                    if (_it._world.EntityCompatibleWith (_currEntity, _it._incMaskIndices)) {
                        return true;
                    }
                }
                return false;
            }
#if DEBUG
            public void Dispose () => _it.AddBlocker (-1);
#endif
        }

#if DEBUG
        public void AddBlocker (int amount) {
            if (!_inited) { throw new Exception ("итератор не инициализирован"); }
            for (var i = 0; i < _incPools.Length; i++) {
                _incPools[i].AddBlocker (amount);
            }
        }
#endif

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ProtoWorld World () => _world;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public bool Has (ProtoEntity entity) => _world.EntityCompatibleWith (entity, _incMaskIndices);

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public Slice<MaskItem> IncMaskIndices () => _incMaskIndices;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public IProtoPool[] Includes () => _incPools;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public (IProtoPool, int) MinPool () {
            var minPool = _incPools[0];
            var minVal = minPool.Len ();
            for (var i = 1; i < _incPools.Length; i++) {
                var p = _incPools[i];
                var v = p.Len ();
                if (v < minVal) {
                    minVal = v;
                    minPool = p;
                }
            }
            return (minPool, minVal);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public int LenSlow () {
            var len = 0;
            foreach (var _ in this) { len++; }
            return len;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public bool IsEmptySlow () {
            foreach (var _ in this) { return false; }
            return true;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public (ProtoEntity Entity, bool Ok) FirstSlow () {
            foreach (var e in this) { return (e, true); }
            return (default, false);
        }
    }

#if ENABLE_IL2CPP
    [Il2CppSetOption (Option.NullChecks, false)]
    [Il2CppSetOption (Option.ArrayBoundsChecks, false)]
#endif
    public sealed class ProtoItExc : IProtoIt {
        ProtoWorld _world;
        Type[] _iTypes;
        IProtoPool[] _incPools;
        Slice<ulong> _incMask;
        Slice<MaskItem> _incMaskIndices;
        Type[] _eTypes;
        IProtoPool[] _excPools;
        Slice<ulong> _excMask;
        Slice<MaskItem> _excMaskIndices;
#if DEBUG
        bool _inited;
#endif

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ProtoItExc (Type[] iTypes, Type[] eTypes) {
#if DEBUG
            if (iTypes == null || iTypes.Length < 1) { throw new Exception ("некорректный список include-пулов для инициализации итератора"); }
            if (eTypes == null || eTypes.Length < 1) { throw new Exception ("некорректный список exclude-пулов для инициализации итератора"); }
#endif
            _iTypes = iTypes;
            _eTypes = eTypes;
        }

        public IProtoIt Init (ProtoWorld world) {
            _world = world;
            var maskLen = world.EntityMaskItemLen ();
            _incMask = new (maskLen, true);
            _incPools = new IProtoPool[_iTypes.Length];
            _incMaskIndices = new (_iTypes.Length);
            ProtoEntity maskE = default;
            for (var i = 0; i < _iTypes.Length; i++) {
                var pool = _world.Pool (_iTypes[i]);
                EntityMask.Set (_incMask, maskLen, maskE, pool.Id ());
                _incPools[i] = pool;
            }
            for (int i = 0, iMax = _incMask.Len (); i < iMax; i++) {
                var data = _incMask.Get (i);
                if (data != 0) { _incMaskIndices.Add (new () { Idx = i, Data = data }); }
            }
            _excMask = new (maskLen, true);
            _excPools = new IProtoPool[_eTypes.Length];
            _excMaskIndices = new (_eTypes.Length);
            for (var i = 0; i < _eTypes.Length; i++) {
                var pool = world.Pool (_eTypes[i]);
                EntityMask.Set (_excMask, maskLen, maskE, pool.Id ());
                _excPools[i] = pool;
            }
            for (int i = 0, iMax = _incMask.Len (); i < iMax; i++) {
                var data = _excMask.Get (i);
                if (data != 0) { _excMaskIndices.Add (new () { Idx = i, Data = data }); }
            }
#if DEBUG
            _inited = true;
#endif
            return this;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ItEnumerator GetEnumerator () => new (this);

        ProtoItIEnumerator IProtoIt.GetEnumerator () => new (this, _incMaskIndices, _excMaskIndices);

        public ref struct ItEnumerator {
            readonly ProtoItExc _it;
            readonly ProtoEntity[] _entities;
            int _id;
            ProtoEntity _currEntity;

            [MethodImpl (MethodImplOptions.AggressiveInlining)]
            public ItEnumerator (ProtoItExc it) {
#if DEBUG
                it.AddBlocker (1);
#endif
                var minPool = it._incPools[0];
                var minVal = minPool.Len ();
                for (var i = 1; i < it._incPools.Length; i++) {
                    var p = it._incPools[i];
                    var v = p.Len ();
                    if (v < minVal) {
                        minVal = v;
                        minPool = p;
                    }
                }
                _it = it;
                _entities = minPool.Entities ();
                _id = minVal;
                _currEntity = default;
            }

            public ProtoEntity Current {
                [MethodImpl (MethodImplOptions.AggressiveInlining)]
                get => _currEntity;
            }

            [MethodImpl (MethodImplOptions.AggressiveInlining)]
            public bool MoveNext () {
                while (_id > 0) {
                    _id--;
                    _currEntity = _entities[_id];
                    if (_it._world.EntityCompatibleWithAndWithout (_currEntity, _it._incMaskIndices, _it._excMaskIndices)) {
                        return true;
                    }
                }
                return false;
            }
#if DEBUG
            public void Dispose () => _it.AddBlocker (-1);
#endif
        }

#if DEBUG
        public void AddBlocker (int amount) {
            if (!_inited) { throw new Exception ("итератор не инициализирован"); }
            for (var i = 0; i < _incPools.Length; i++) {
                _incPools[i].AddBlocker (amount);
            }
            for (var i = 0; i < _excPools.Length; i++) {
                _excPools[i].AddBlocker (amount);
            }
        }
#endif

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ProtoWorld World () => _world;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public bool Has (ProtoEntity entity) => _world.EntityCompatibleWithAndWithout (entity, _incMaskIndices, _excMaskIndices);

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public Slice<MaskItem> IncMaskIndices () => _incMaskIndices;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public Slice<MaskItem> ExcMaskIndices () => _excMaskIndices;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public IProtoPool[] Includes () => _incPools;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public IProtoPool[] Excludes () => _excPools;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public (IProtoPool, int) MinPool () {
            var minPool = _incPools[0];
            var minVal = minPool.Len ();
            for (var i = 1; i < _incPools.Length; i++) {
                var p = _incPools[i];
                var v = p.Len ();
                if (v < minVal) {
                    minVal = v;
                    minPool = p;
                }
            }
            return (minPool, minVal);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public int LenSlow () {
            var len = 0;
            foreach (var _ in this) { len++; }
            return len;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public bool IsEmptySlow () {
            foreach (var _ in this) { return false; }
            return true;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public (ProtoEntity Entity, bool Ok) FirstSlow () {
            foreach (var e in this) { return (e, true); }
            return (default, false);
        }
    }
}
