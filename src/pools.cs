// ----------------------------------------------------------------------------
// Лицензия MIT-ZARYA
// (c) 2025 Leopotam <leopotam@yandex.ru>
// ----------------------------------------------------------------------------

using System;
using System.IO;
using System.Runtime.CompilerServices;
#if ENABLE_IL2CPP
using UnityEngine.Scripting;
using Unity.IL2CPP.CompilerServices;
#endif

namespace Leopotam.EcsProto {
    public interface IProtoPool {
        void Init (ushort id, ProtoWorld host, Func<ProtoEntity> entityCreator);
        Type ItemType ();
        ushort Id ();
        ProtoWorld World ();
        void NewEntityRaw (out ProtoEntity entity);
        bool Has (ProtoEntity entity);
        void Del (ProtoEntity entity);
        void AddRaw (ProtoEntity entity);
        object Raw (ProtoEntity entity);
        void SetRaw (ProtoEntity entity, object dataRaw);
        void AddBlocker (int amount);
        void Resize (int cap);
        int Len ();
        ProtoEntity[] Entities ();
        void Copy (ProtoEntity srcEntity, ProtoEntity dstEntity);
        bool Serialize (ProtoEntity entity, Stream writer);
        bool Deserialize (ProtoEntity entity, Stream reader);
    }

    public interface IProtoPool<T> where T : struct {
        void SetResetHandler (ProtoResetHandler<T> cb);
        void SetCopyHandler (ProtoCopyHandler<T> cb);
        void SetSerializeHandler (ProtoSerializeHandler<T> cb);
        void SetDeserializeHandler (ProtoSerializeHandler<T> cb);
    }

    public interface IProtoHandlers<T> where T : struct {
        void SetHandlers (IProtoPool<T> pool);
    }

    public delegate void ProtoResetHandler<T> (ref T c) where T : struct;
    public delegate void ProtoCopyHandler<T> (ref T src, ref T dst) where T : struct;
    public delegate void ProtoSerializeHandler<T> (ref T c, Stream s) where T : struct;

#if ENABLE_IL2CPP
    [Il2CppSetOption (Option.NullChecks, false)]
    [Il2CppSetOption (Option.ArrayBoundsChecks, false)]
#endif
    public class ProtoPool<T> : IProtoPool, IProtoPool<T> where T : struct {
        const int DefaultCapacity = 128;
        int _initCap;
        ushort _id;
        ProtoWorld _world;
        Func<ProtoEntity> _entityCreator;
        ProtoEntity[] _dense;
#if LEOECSPROTO_SMALL_WORLD
        ushort[] _sparse;
        ushort _len;
#else
        int[] _sparse;
        int _len;
#endif
        int _maxLen;
        T[] _data;
        Type _itemType;
        ProtoResetHandler<T> _resetHandler;
        ProtoCopyHandler<T> _copyHandler;
        ProtoSerializeHandler<T> _serializeHandler;
        ProtoSerializeHandler<T> _deserializeHandler;
#if DEBUG
        int _blockers;
#endif
#if ENABLE_IL2CPP
        [Preserve]
#endif
        public ProtoPool () : this (0) { }

        public ProtoPool (int capacity) {
            _initCap = capacity;
        }

        public void SetResetHandler (ProtoResetHandler<T> cb) => _resetHandler = cb;
        public void SetCopyHandler (ProtoCopyHandler<T> cb) => _copyHandler = cb;
        public void SetSerializeHandler (ProtoSerializeHandler<T> cb) => _serializeHandler = cb;
        public void SetDeserializeHandler (ProtoSerializeHandler<T> cb) => _deserializeHandler = cb;

        void IProtoPool.Init (ushort id, ProtoWorld world, Func<ProtoEntity> entityCreator) {
#if DEBUG
            if (_world != null) { throw new Exception ($"пул компонентов \"{DebugHelpers.CleanTypeName (_itemType)}\" уже привязан к миру"); }
            _blockers = 0;
#endif
            if (_initCap == 0) {
                _initCap = DefaultCapacity;
            }
            _dense = new ProtoEntity[_initCap];
            _data = new T[_initCap];
            _len = 0;
            _maxLen = 0;
            _itemType = typeof (T);
            if (default (T) is IProtoHandlers<T> protoHandlers) {
                protoHandlers.SetHandlers (this);
            }
            _id = id;
            _world = world;
            _entityCreator = entityCreator;
#if LEOECSPROTO_SMALL_WORLD
            _sparse = new ushort[_world.EntityGens ().Cap ()];
#else
            _sparse = new int[_world.EntityGens ().Cap ()];
#endif
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public Type ItemType () {
            if (_itemType == null) {
                _itemType = typeof (T);
            }
            return _itemType;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ushort Id () => _id;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ProtoWorld World () => _world;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ref T NewEntity (out ProtoEntity entity) {
            entity = _entityCreator ();
            return ref Add (entity);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ref T Get (ProtoEntity entity) {
#if DEBUG
            if (!Has (entity)) { throw new Exception ($"компонент \"{DebugHelpers.CleanTypeName (_itemType)}\" отсутствует на сущности \"{entity}\""); }
#endif
            return ref _data[_sparse[entity._id] - 1];
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public bool Has (ProtoEntity entity) {
#if DEBUG
            if (_world.EntityGens ().Get (entity._id) < 0) { throw new Exception ($"не могу получить доступ к удаленной сущности \"{entity}\""); }
#endif
            return _sparse[entity._id] > 0;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ref T Add (ProtoEntity entity) {
#if DEBUG
            if (Has (entity)) { throw new Exception ($"не могу добавить компонент \"{DebugHelpers.CleanTypeName (_itemType)}\", он уже присутствует на сущности \"{entity}\""); }
            if (_blockers > 1) { throw new Exception ($"нельзя изменить пул компонентов \"{DebugHelpers.CleanTypeName (_itemType)}\", он находится в режиме \"только чтение\" из-за множественного доступа"); }
#endif
            if (_dense.Length == _len) {
                ResizeData ();
            }
            var idx = _len;
            _len++;
            _dense[idx] = entity;
            _sparse[entity._id] = _len;
            ref var data = ref _data[idx];
            if (_resetHandler != null && _maxLen < _len) {
                _maxLen = _len;
                _resetHandler.Invoke (ref data);
            }
            _world.SetEntityMaskBit (entity, _id);
            return ref data;
        }

        void ResizeData () {
            Array.Resize (ref _dense, _len << 1);
            Array.Resize (ref _data, _len << 1);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public void Del (ProtoEntity entity) {
#if DEBUG
            if (!Has (entity)) { throw new Exception ($"не могу удалить компонент \"{DebugHelpers.CleanTypeName (_itemType)}\", он отсутствует на сущности \"{entity}\""); }
            if (_blockers > 1) { throw new Exception ($"нельзя изменить пул компонентов \"{DebugHelpers.CleanTypeName (_itemType)}\", он находится в режиме \"только чтение\" из-за множественного доступа"); }
#endif
            var idx = _sparse[entity._id] - 1;
            _sparse[entity._id] = 0;
            _len--;
            if (_resetHandler != null) {
                _resetHandler.Invoke (ref _data[idx]);
            } else {
                _data[idx] = default;
            }
            if (idx < _len) {
                _dense[idx] = _dense[_len];
#if LEOECSPROTO_SMALL_WORLD
                _sparse[_dense[idx]._id] = (ushort) (idx + 1);
#else
                _sparse[_dense[idx]._id] = idx + 1;
#endif
                (_data[idx], _data[_len]) = (_data[_len], _data[idx]);
            }
            _world.UnsetEntityMaskBit (entity, _id);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public void Copy (ProtoEntity srcEntity, ProtoEntity dstEntity) {
#if DEBUG
            if (_world.EntityGens ().Get (srcEntity._id) < 0) { throw new Exception ($"не могу получить доступ к удаленной исходной сущности \"{srcEntity}\""); }
            if (_world.EntityGens ().Get (dstEntity._id) < 0) { throw new Exception ($"не могу получить доступ к удаленной целевой сущности \"{dstEntity}\""); }
            if (_blockers > 1) { throw new Exception ($"нельзя изменить пул компонентов \"{DebugHelpers.CleanTypeName (_itemType)}\", он находится в режиме \"только чтение\" из-за множественного доступа"); }
#endif
            if (Has (srcEntity)) {
                ref var srcData = ref Get (srcEntity);
                if (!Has (dstEntity)) {
                    Add (dstEntity);
                }
                ref var dstData = ref Get (dstEntity);
                if (_copyHandler != null) {
                    _copyHandler.Invoke (ref srcData, ref dstData);
                } else {
                    dstData = srcData;
                }
            }
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public bool Serialize (ProtoEntity entity, Stream writer) {
#if DEBUG
            if (_world.EntityGens ().Get (entity._id) < 0) { throw new Exception ($"не могу получить доступ к удаленной сущности \"{entity}\""); }
            if (writer == null) { throw new Exception ("поток записи не инициализирован"); }
#endif
            if (_serializeHandler == null || !Has (entity)) {
                return false;
            }
            _serializeHandler.Invoke (ref Get (entity), writer);
            return true;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public bool Deserialize (ProtoEntity entity, Stream reader) {
#if DEBUG
            if (_world.EntityGens ().Get (entity._id) < 0) { throw new Exception ($"не могу получить доступ к удаленной сущности \"{entity}\""); }
            if (reader == null) { throw new Exception ("поток чтения не инициализирован"); }
#endif
            if (_deserializeHandler == null || !Has (entity)) {
                return false;
            }
            _deserializeHandler.Invoke (ref Get (entity), reader);
            return true;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public int Len () => _len;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ProtoEntity[] Entities () => _dense;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public T[] Data () => _data;

        void IProtoPool.NewEntityRaw (out ProtoEntity entity) => NewEntity (out entity);

        void IProtoPool.Resize (int cap) => Array.Resize (ref _sparse, cap);

        void IProtoPool.AddRaw (ProtoEntity entity) => Add (entity);

        object IProtoPool.Raw (ProtoEntity entity) => Get (entity);

        void IProtoPool.SetRaw (ProtoEntity entity, object dataRaw) {
#if DEBUG
            if (dataRaw != null && dataRaw.GetType () != _itemType) { throw new Exception ($"неправильный тип данных для использования в качестве компонента \"{DebugHelpers.CleanTypeName (_itemType)}\""); }
#endif
            Get (entity) = dataRaw != null ? (T) dataRaw : default;
        }

        void IProtoPool.AddBlocker (int amount) {
#if DEBUG
            _blockers += amount;
            if (_blockers < 0) { throw new Exception ($"ошибочный баланс пользователей пула компонентов \"{DebugHelpers.CleanTypeName (_itemType)}\" при попытке освобождения"); }
#endif
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ItEnumerator GetEnumerator () => new (this);

        public ref struct ItEnumerator {
            readonly ProtoPool<T> _pool;
            int _id;

            [MethodImpl (MethodImplOptions.AggressiveInlining)]
            public ItEnumerator (ProtoPool<T> pool) {
#if DEBUG
                ((IProtoPool) pool).AddBlocker (1);
#endif
                _pool = pool;
                _id = pool.Len ();
            }

            public ProtoEntity Current {
                [MethodImpl (MethodImplOptions.AggressiveInlining)]
                get => _pool._dense[_id];
            }

            [MethodImpl (MethodImplOptions.AggressiveInlining)]
            public bool MoveNext () {
                if (_id > 0) {
                    _id--;
                    return true;
                }
                return false;
            }
#if DEBUG
            public void Dispose () => ((IProtoPool) _pool).AddBlocker (-1);
#endif
        }
    }
}
