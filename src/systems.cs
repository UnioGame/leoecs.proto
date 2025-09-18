// ----------------------------------------------------------------------------
// Лицензия MIT-ZARYA
// (c) 2025 Leopotam <leopotam@yandex.ru>
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
#if ENABLE_IL2CPP
using Unity.IL2CPP.CompilerServices;
#endif

namespace Leopotam.EcsProto {
    public interface IProtoSystem { }

    public interface IProtoInitSystem : IProtoSystem {
        void Init (IProtoSystems systems);
    }

    public interface IProtoRunSystem : IProtoSystem {
        void Run ();
    }

    public interface IProtoDestroySystem : IProtoSystem {
        void Destroy ();
    }

    public interface IProtoModule {
        void Init (IProtoSystems systems);
        IProtoAspect[] Aspects ();
        Type[] Dependencies ();

        // устарело, оставлено на время миграции.
        IProtoModule[] Modules () => null;
    }

    public interface IProtoSystems : IProtoNestedSystems {
        IProtoSystems AddSystem (IProtoSystem system, int weight = default);
        IProtoSystems AddService (object injectInstance, Type asType = default);
        IProtoSystems AddModule (IProtoModule module);
        IProtoSystems AddWorld (ProtoWorld world, string name);
        ProtoWorld World (string worldName = default);
        Dictionary<string, ProtoWorld> NamedWorlds ();
        Dictionary<Type, object> Services ();
        void Init ();
        void Run ();
        void Destroy ();
    }

    public interface IProtoNestedSystems {
        Slice<IProtoSystem> Systems ();
    }

#if ENABLE_IL2CPP
    [Il2CppSetOption (Option.NullChecks, false), Il2CppSetOption (Option.ArrayBoundsChecks, false)]
#endif
    public class ProtoSystems : IProtoSystems {
        protected const string DefaultPointName = "<default>";

        protected ProtoWorld _defaultWorld;
        protected Dictionary<string, ProtoWorld> _worldMap;
        protected Slice<IProtoSystem> _allSystems;
        protected Slice<IProtoRunSystem> _runSystems;
        protected Dictionary<int, Slice<IProtoSystem>> _deferredSystems;
        protected Slice<int> _deferredSystemsOrder;
        protected Dictionary<Type, object> _services;
        protected int _systemsCount;
        protected bool _inited;
#if DEBUG
        Dictionary<Type, IProtoModule> _modules;
#endif

        public ProtoSystems (ProtoWorld defaultWorld) {
#if DEBUG
            if (defaultWorld == null) { throw new Exception ("требуется мир по умолчанию"); }
            _modules = new (16);
#endif
            _defaultWorld = defaultWorld;
            _worldMap = new (4);
            _deferredSystems = new (32);
            _deferredSystemsOrder = new (32);
            _services = new (32);
        }

        public virtual IProtoSystems AddSystem (IProtoSystem system, int weight = default) {
#if DEBUG
            if (IsInited ()) { throw new Exception ($"не могу добавить систему \"{DebugHelpers.CleanTypeName (system.GetType ())}\", системы уже инициализированы"); }
#endif
            if (!_deferredSystems.TryGetValue (weight, out var list)) {
                _deferredSystemsOrder.Add (weight);
                list = new (8);
                _deferredSystems[weight] = list;
            }
            list.Add (system);
            _systemsCount++;
            return this;
        }

        public virtual IProtoSystems AddService (object injectInstance, Type asType = default) {
            var type = asType ?? injectInstance.GetType ();
#if DEBUG
            if (IsInited ()) { throw new Exception ($"не могу добавить сервис с типом \"{DebugHelpers.CleanTypeName (type)}\", системы уже инициализированы"); }
            if (injectInstance is IProtoSystem) { throw new Exception ($"не могу добавить сервис с типом \"{DebugHelpers.CleanTypeName (type)}\", система не должна использоваться как сервис"); }
            if (_services.ContainsKey (type)) { throw new Exception ($"не могу добавить сервис с типом \"{DebugHelpers.CleanTypeName (type)}\", такой тип уже существует"); }
#endif
            _services[type] = injectInstance;
            return this;
        }

        public virtual IProtoSystems AddModule (IProtoModule module) {
#if DEBUG
            if (module == null) { throw new Exception ("требуется модуль"); }
            var modType = module.GetType ();
            if (IsInited ()) { throw new Exception ($"не могу добавить модуль \"{DebugHelpers.CleanTypeName (modType)}\", системы уже инициализированы"); }
            if (_modules.ContainsKey (module.GetType ())) { throw new Exception ($"модуль \"{DebugHelpers.CleanTypeName (modType)}\" уже зарегистрирован"); }
            _modules[modType] = module;
            var deps = module.Dependencies ();
            if (deps != null) {
                foreach (var dep in deps) {
                    if (!_modules.ContainsKey (dep)) {
                        throw new Exception ($"модуль \"{DebugHelpers.CleanTypeName (modType)}\" требует наличие модуля \"{DebugHelpers.CleanTypeName (dep)}\"");
                    }
                }
            }
            var subMods = module.Modules ();
            if (subMods != null) {
                System.Text.StringBuilder list = new ();
                foreach (var subMod in subMods) {
                    if (list.Length > 0) { list.Append (", "); }
                    list.Append ($"\"{DebugHelpers.CleanTypeName (subMod.GetType ())}\"");
                }
                if (list.Length == 0) { list.Append ("\"пустой список\""); }
                throw new Exception ($"в модуле \"{DebugHelpers.CleanTypeName (module.GetType ())}\" обнаружены вложенные модули: {list}. Их следует зарегистрировать в общем списке модулей, а метод Modules() - удалить");
            }
#endif
            module.Init (this);
            return this;
        }

        public virtual IProtoSystems AddWorld (ProtoWorld world, string name) {
#if DEBUG
            if (string.IsNullOrEmpty (name)) { throw new Exception ("не могу добавить мир с пустым именем"); }
            if (IsInited ()) { throw new Exception ($"не могу добавить мир с именем \"{name}\", системы уже инициализированы"); }
            if (_worldMap.ContainsKey (name)) { throw new Exception ($"не могу добавить мир с именем \"{name}\", имя уже существует"); }
#endif
            _worldMap[name] = world;
            return this;
        }

        public ProtoWorld World (string worldName = default) {
            if (worldName == default) {
                return _defaultWorld;
            }
#if DEBUG
            if (!_worldMap.ContainsKey (worldName)) { throw new Exception ($"не могу найти мир с именем \"{worldName}\", его сперва надо зарегистрировать в системах"); }
#endif
            return _worldMap[worldName];
        }

        public Dictionary<string, ProtoWorld> NamedWorlds () => _worldMap;
        public Dictionary<Type, object> Services () => _services;
        public Slice<IProtoSystem> Systems () => _allSystems;

        public virtual void Init () {
#if DEBUG
            if (IsInited ()) { throw new Exception ("не могу инициализировать системы повторно"); }
#endif
            Array.Sort (_deferredSystemsOrder.Data (), 0, _deferredSystemsOrder.Len ());
            _allSystems = new (_systemsCount);
            _runSystems = new (_systemsCount);
            foreach (var weight in _deferredSystemsOrder) {
                foreach (var sys in _deferredSystems[weight]) {
                    _allSystems.Add (sys);
                    if (sys is IProtoRunSystem runSys) {
                        _runSystems.Add (runSys);
                    }
                }
            }
            for (int i = 0, iMax = _allSystems.Len (); i < iMax; i++) {
                if (_allSystems.Get (i) is IProtoInitSystem iSystem) {
                    iSystem.Init (this);
                }
            }
            _inited = true;
        }

        public virtual void Run () {
#if DEBUG
            if (!IsInited ()) { throw new Exception ("системы не инициализированы"); }
#endif
            for (int i = 0, iMax = _runSystems.Len (); i < iMax; i++) {
                _runSystems.Get (i).Run ();
            }
        }

        public virtual void Destroy () {
            if (IsInited ()) {
                for (int i = 0, iMax = _allSystems.Len (); i < iMax; i++) {
                    if (_allSystems.Get (i) is IProtoDestroySystem dSystem) {
                        dSystem.Destroy ();
                    }
                }
            }
        }

        public virtual bool IsInited () => _inited;
    }
}
