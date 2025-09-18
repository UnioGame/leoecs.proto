<p align="center">
    <img src="./logo.png" alt="Logo">
</p>

# LeoEcs Proto - Lightweight C# Entity Component System Framework
Performance, zero or minimal allocations, memory usage minimization, absence of dependencies on any game engine - these are the main goals of this framework.

> **IMPORTANT!** Requires C#9 (or Unity >=2021.2).

> **IMPORTANT!** Don't forget to use `DEBUG` builds for development and `RELEASE` builds for releases: all internal checks/exceptions will only work in `DEBUG` builds and are removed for increased performance in `RELEASE` builds.

> **IMPORTANT!** LeoEcs Proto is **not thread-safe** and never will be! If you need multithreading - you must implement it yourself and integrate synchronization as an ecs-system.

> **IMPORTANT!** Tested on Unity 2021.3 (does not depend on it) and contains asmdef descriptions for compilation as separate assemblies and reducing main project recompilation time.


# Social Resources
Official blog: https://leopotam.ru


# Installation


## As Unity Module
Installation as a unity module via git link in PackageManager or direct editing of `Packages/manifest.json` is supported:
```
"ru.leopotam.ecsproto": "https://gitverse.ru/leopotam/ecsproto.git",
```


## As Source Code
The code can also be cloned or obtained as an archive from the releases page.


## Other Sources
The official working version is hosted at [https://gitverse.ru/leopotam/ecsproto](https://gitverse.ru/leopotam/ecsproto), all other versions (including *nuget*, *npm* and other repositories) are unofficial clones or third-party code with unknown content.


# Core Types


## Entity
By itself means nothing and is exclusively an identifier for a set of components. Implemented as `ProtoEntity`:
```c#
// Create a new entity in the world. Creation is only possible through a component pool,
// obtained from the world, with simultaneous attachment of this pool's component to the entity.
ProtoPool<C1> C1Pool; // Previously initialized pool.
ref C1 c1 = ref C1Pool.NewEntity (out ProtoEntity entity);

// Any entity can be deleted, while first all its components
// will be automatically removed and only then the entity will be considered destroyed.
world.DelEntity (entity);

// Components from any active entity can be copied to another existing one.
world.CopyEntity (srcEntity, dstEntity);

// Any entity can be cloned into a new entity with all components.
ProtoEntity clonedEntity = world.CloneEntity (srcEntity);
```

> **IMPORTANT!** Only one instance of each component type can exist on an entity.

> **IMPORTANT!** Entities cannot exist without components and will be automatically destroyed when the last component is removed from them.

> **IMPORTANT!** The `ProtoEntity` type is not a reference type, instances of this type cannot be stored outside the current method without ensuring integrity. If storage is required, then you should store a pair of `ProtoEntity`-entity and its generation, obtained through calling `ProtoWorld.EntityGen()`. The `EcsProto.QoL` extension has a ready implementation in the form of `ProtoPackedEntity` or `ProtoPackedEntityWithWorld`.

## Component
Is a container for user data and should not contain logic (minimal auxiliary wrapper is allowed, but not pieces of main logic):
```c#
struct Component1 {
    public int Id;
    public string Name;
}
struct Component2 {
    // Components can be empty and used as markers for filtering.
}
```
Components can be added, queried, or removed through component pools.


## System
Is a container for main logic for processing filtered entities.
Exists as a user class implementing at least one of the interfaces:
```c#
class UserSystem : IProtoInitSystem, IProtoRunSystem, IProtoDestroySystem {
    public void Init (IProtoSystems systems) {
        // Will be called once during IProtoSystems.Init() execution.
    }

    public void Run () {
        // Will be called once during IProtoSystems.Run() execution.
    }

    public void Destroy () {
        // Will be called once during IProtoSystems.Destroy() execution.
    }
}
```


# Services
An instance of any user reference type (class) can be simultaneously connected to all systems:
```c#
class PathService {
    public string PrefabsPath;
}
interface ISettingsService { }
class SettingsService : ISettingsService {
    public Vector3 SpawnPoint;
}
// Initialization in startup code.
PathService pathService = new () { PrefabsPath = "Items/{0}" };
SettingsService settingsService = new () { SpawnPoint = new Vector3 (123, 0, 456) };
ProtoSystems systems = new (world);
systems
    .AddSystem (new System1 ())
    // Service registration.
    .AddService (pathService)
    // Type override for registration is allowed.
    .AddService (settingsService, typeof(ISettings))
    .Init ();
// Access in system.
class System1 : IProtoInitSystem {
    PathService _svcPath;
    ISettingsService _svcSettings;
    public void Init(IProtoSystems systems) {
        Dictionary<Type, object> svc = systems.Services();
        _svcPath = svc[typeof(PathService)] as PathService;
        _svcSettings = svc[typeof(ISettingsService)] as ISettingsService;
    }
}
```


# Special Types


## Aspect
Is a container for component pools existing in the world.

> **IMPORTANT!** Pools can only be created inside the aspect initializer for the world.

> **IMPORTANT!** Aspects can be part of other aspects. The main (root) aspect is passed to the world constructor,
> which is a composition of all aspects / pools, from whose data the world will consist.
> Initialization of nested aspects should be performed by calling `Init()` and `PostInit()` methods.

```c#
class Aspect1 : IProtoAspect {
    public ProtoPool<Component1> C1Pool;

    public void Init (ProtoWorld world) {
        // Mandatory registration of this aspect for further access from systems.
        world.AddAspect (this);
        // Creating a pool instance with caching in the aspect field.
        C1Pool = new ();
        // Mandatory registration of this pool in the world.
        world.AddPool (C1Pool);
    }
    public void PostInit () {
        // Additional initialization stage. If there are nested aspects,
        // created during the initialization of this aspect - they must have
        // the PostInit() method called. Also, nested iterators should be
        // initialized here by calling the Init() method.
    }
}
```
Aspects can act as a grouping of already existing pools:
```c#
class Aspect2 : IProtoAspect {
    public ProtoPool<Component1> C1Pool;

    public void Init (ProtoWorld world) {
        world.AddAspect (this);
        if (!world.HasPool (typeof (Component1)) {
            // Create a new pool if it doesn't exist.
            C1Pool = new ();
            world.AddPool (C1Pool);
        } else {
            // Get existing pool.
            C1Pool = (ProtoPool<Component1>) world.Pool (typeof (Component1))
        }
    }
}
```


## World
Is a container for all entities, data of each instance is unique and isolated from other worlds.

> **IMPORTANT!** A world cannot exist without at least one aspect.

> **IMPORTANT!** It is necessary to call `ProtoWorld.Destroy()` on the world instance if it is no longer needed.

```c#
// Create a world.
ProtoWorld world = new (new Aspect1 ());
// Work with the world.
// ...
// Destroy the world.
world.Destroy ();
```


## Pool
Is a container for components, provides API for adding / querying / removing components on entities:
```c#
ProtoWorld world = new (new Aspect1 ());
// Possible, but not recommended way to access an existing world pool.
ProtoPool<Component1> pool1 = (ProtoPool<Component1>) world.Pool (typeof (Component1);
ProtoPool<Component2> pool2 = (ProtoPool<Component2>) world.Pool (typeof (Component2);
// Correct way to access a pool.
Aspect1 proto1 = (Aspect1) world.Aspect (typeof (Aspect1));
pool = proto1.C1Pool;

// NewEntity() creates an entity and adds a component from the pool to it.
ref Component1 c1 = ref pool1.NewEntity (out ProtoEntity entity);

// Add() adds a component to an entity.
// If the component already exists - an exception will be thrown in DEBUG version.
ref Component2 c2 = ref pool2.Add (entity);

// Has() checks for the presence of a component on an entity and returns the result.
bool c1Exists = pool1.Has (entity);

// Get() returns an existing component on an entity.
// If the component didn't exist - an exception will be thrown in DEBUG version.
ref Component1 c11 = ref pool1.Get (entity);

// Del() removes a component from an entity. If the component didn't exist - an
// exception will be thrown in DEBUG version. If this was the last component, then
// the entity will be automatically removed.
pool1.Del (entity);

// Copy() performs copying of all components from one entity to another.
// If the source or target entity doesn't exist - an exception will be thrown in DEBUG version.
pool1.Copy (srcEntity, dstEntity);
```

> **IMPORTANT!** After removal, the component will be returned to the pool for subsequent reuse.
> All component fields will be reset to default values automatically.

> **IMPORTANT!** After calling `pool.Add()` and `pool.Del()`, all previously obtained `ref`-references to
components from this pool through calls to `pool.Add()` and `pool.Get()` become potentially invalid,
to access components they need to be requested again through calling `pool.Get()`.


## Iterator
Iterator is a way to filter entities by the presence or absence of specified components on them:
```c#
class System1 : IProtoInitSystem, IProtoRunSystem {
    Aspect1 _aspect;
    ProtoIt _it;
    public void Init (IProtoSystems systems) {
        // Get the default world instance.
        ProtoWorld world = systems.World ();
        // Get the world aspect (from the example above) and cache it.
        _aspect = (Aspect1) world.Aspect (typeof (Aspect1));
        // Create an iterator with explicit specification of required (include) component types.
        _it = new (new [] { typeof Component1 } );
        // Initialize it to specify which world the data comes from.
        _it.Init (world);

        // Create a new entity and add "Component1" component to it.
        _aspect.C1Pool.NewEntity (out _);
    }

    public void Run () {
        // We want to get all entities with "Component1" component.
        for (ProtoEntity entity in _it) {
            // get access to the component on the filtered entity.
            ref Component1 c1 = ref _aspect.C1Pool.Get (entity);
        }
    }
}
```

If you need to specify the absence of certain components, then the iterator type changes to `ProtoItExc`,
which takes 2 parameters (include/exclude type lists):
```c#
// Iterator for entities with components `C1`,`C2`, but without `C3`.
ProtoItExc it = new (new [] { typeof (C1), typeof (C2) }, new [] { typeof (C3) });
```

> **IMPORTANT!** Iterators should be created once at startup and are not intended for dynamic creation in `Run()` systems.

Iterators can be part of an aspect, in this case they should be initialized in the `PostInit()` method:

```c#
class Aspect2 : IProtoAspect {
    public ProtoWorld World;
    public ProtoPool<Component1> C1Pool;
    public ProtoIt C1It;

    public void Init (ProtoWorld world) {
        _world = world;
        world.AddAspect (this);
        if (!world.HasPool (typeof (Component1))) {
            // Create a new pool if it doesn't exist.
            C1Pool = new ();
            world.AddPool (C1Pool);
        } else {
            // Get existing pool.
            C1Pool = (ProtoPool<Component1>) world.Pool (typeof (Component1))
        }
        // Iterator can be created inside Init(), but without initialization.
        C1It = new (new Type[] { typeof (Component1) });
    }
    public void PostInit () {
        // Iterator initialization.
        C1It.Init (_world);
    }
}
```

Iterators support iteration over compatible entities through `IProtoIt`, which they implement:

```c#
IProtoIt it = new ProtoIt (new [] { typeof (C1) }).Init (world);
foreach (ProtoEntity e in it) {
    // processing.
}
```

> **IMPORTANT!** This option is slower than iteration over specific iterator types and is not recommended for a large number of entities.

### Number of entities in iterator
Iterators allow you to find out the number of entities that match their conditions:

```c#
ProtoIt it = new (new [] { typeof (C1) });
it.Init (world);
int entitiesCount = it.LenSlow ();
```

> **IMPORTANT!** Not recommended for use if there can be more than a couple of dozen entities - counting is done by full iteration of the iterator.


### Presence of entities in iterator
If the exact count is not required, and it's enough to just know that the iterator is not empty, then you can use the following method:

```c#
ProtoIt it = new (new [] { typeof (C1) });
it.Init (world);
bool isEmpty = it.IsEmptySlow ();
```

> **IMPORTANT!** This method is faster than `IProtoIt.LenSlow()`, but in the worst case still performs full iteration of the iterator.


### Getting the first entity in iterator
If you need to get only the first entity from the iterator with proper handling of its absence, then you can use the following method:
```c#
(ProtoEntity entity, bool ok) = it.FirstSlow ();
if (ok) {
    // Check the success of the operation,
    // entity is valid and can be used.
}
```

> **IMPORTANT!** In the worst case, full iteration of the iterator is still performed.


## System Group
Is a container for systems, defines execution order (using Unity integration as an example):
```c#
class Startup : MonoBehaviour {
    ProtoWorld _world;
    IProtoSystems _systems;

    void Start () {
        // Create environment, connect systems.
        _world = new (new Aspect1 ());
        _systems = new ProtoSystems (_world);
        _systems
            .AddSystem (new System1 ())
            // Additional worlds can be connected.
            // .AddWorld (new ProtoWorld (new Aspect2 ()), "events")
            .Init ();
    }

    void Update () {
        // Execute all connected systems.
        _systems?.Run ();
    }

    void OnDestroy () {
        // Destroy connected systems.
        _systems?.Destroy ();
        _systems = null;
        // Clean up environment.
        _world?.Destroy ();
        _world = null;
    }
}
```

> **IMPORTANT!** It is necessary to call `IProtoSystems.Destroy()` on the system group instance if it is no longer needed.

Systems can be connected in one order, but executed in another, for this you can specify the system "weight":
```c#
systems
    .AddSystem (new System1 (), 3)
    .AddSystem (new System2 (), 2)
    .AddSystem (new System3 (), 1)
    .Init ();
```
Systems will execute in the following order:
> System3 > System2 > System1

> **IMPORTANT!** "Weights" are globally sorted ascending blocks of systems within `IProtoSystems`, not unique values for each system, so you can connect multiple systems with the same "weight" - in this case they will execute within one "weight" in the order of connection.

If the system "weight" is not explicitly specified, the system will have weight 0:
```c#
systems
    .AddSystem (new System1 (), 3)
    .AddSystem (new System2 ())
    .AddSystem (new System3 (), 2)
    .AddSystem (new System4 (), 1)
    .Init ();
```
Systems will execute in the following order:
> System2 > System4 > System3 > System1

> **IMPORTANT!** "Weights" can have both positive and negative values - this allows systems to execute before systems with default weight.


## Module
Used to separate user code into modules:
```c#
class Module1 : IProtoModule {
    int _pointWeight;

    public Module1(int pointWeight) {
        // If there is a need for registration with a specific weight -
        // its value can be passed through the constructor.
        _pointWeight = pointWeight;
    }

    public void Init (IProtoSystems systems) {
        // Registration of module systems and services.
        systems
            .AddSystem (new System1 (), _pointWeight)
            .AddService (new Service1 ());
    }

    // Method should return a list of all module aspects
    // for automation of registration, or null.
    public IProtoAspect[] Aspects () {
        return new IProtoAspect[] { new Module1Aspect () };
    }

    // Method should return a list of all modules that
    // the current module depends on and which should be
    // registered before it, or null.
    public Type[] Dependencies () {
        return new IProtoModule[] { new Module2 () };
    }

    class System1 : IProtoInitSystem {
        public void Init (IProtoSystems systems) { }
    }

    class Service1 { }
}
// Module connection.
systems
    .AddModule (new Module1 (1))
    .Init();
```

Module aspect can also be separated:
```c#
// Module aspect.
class Module1Aspect : IProtoAspect {
    public ProtoPool<Component1> C1Pool;

    public void Init (ProtoWorld world) {
        world.AddAspect (this);
        C1Pool = new ();
        world.AddPool (C1Pool);
    }

    public void PostInit() {}
}
// Main world aspect, including aspects of all modules.
class MainAspect : IProtoAspect {
    public Module1Aspect Module1;

    public void Init (ProtoWorld world) {
        world.AddAspect (this);
        Module1 = new ();
        Module1.Init (world);
    }

    public void PostInit() {}
}
```


# Engine Integration


## Unity

The integrator is implemented as an extension module and can be installed in addition to the core.


## Custom Engine

Each part of the example below should be correctly integrated into the proper place in the engine's code execution:
```c#
using Leopotam.EcsProto;

class EcsStartup {
    ProtoWorld _world;
    IProtoSystems _systems;

    // Environment initialization.
    void Init () {
        _world = new (new Aspect1 ());
        _systems = new ProtoSystems (_world);
        _systems
            // Additional world instances
            // should be registered here.
            // .AddWorld (new ProtoWorld (new Aspect2 ()), "events")

            // Modules should be registered here.
            // .AddModule (new TestModule1 ())
            // .AddModule (new TestModule2 ())

            // Systems outside modules can
            // be registered here.
            // .AddSystem (new TestSystem1 ())
            // .AddSystem (new TestSystem2 ())

            // Services can be added anywhere.
            // .AddService (new TestService1 ())

            .Init ();
    }

    // Method should be called from
    // the main update loop of the engine.
    void UpdateLoop () {
        _systems.Run ();
    }

    // Environment cleanup.
    void Destroy () {
        _systems?.Destroy ();
        _systems = null;
        _world?.Destroy ();
        _world = null;
    }
}
```


# Extensions

* [Developer Quality of Life improvements](https://gitverse.ru/leopotam/ecsproto-qol)
* [Unity Editor integration](https://gitverse.ru/leopotam/ecsproto.unity)
* [Unity Physics2D events integration](https://gitverse.ru/leopotam/ecsproto.unity.physics2d)
* [Unity Physics3D events integration](https://gitverse.ru/leopotam/ecsproto.unity.physics3d)
* [Unity uGui events integration](https://gitverse.ru/leopotam/ecsproto.unity.ugui)
* [Multithreaded processing integration](https://gitverse.ru/leopotam/ecsproto.threads)
* [Parent-child relationships for entities](https://gitverse.ru/leopotam/ecsproto.parenting)
* [System grouping into blocks with conditional execution](https://gitverse.ru/leopotam/ecsproto.conditionalsystems)
* [UtilityAI](https://gitverse.ru/leopotam/ecsproto.ai.utility)
* [Unity UtilityAI integration](https://gitverse.ru/leopotam/ecsproto.ai.utility.unity)


# License
The framework is released under the MIT-ZARYA license, [details here](./LICENSE.md).


# FAQ


### What's the difference from LeoECS Lite?
I prefer to call them `Lite` (ecslite) and `Proto` (ecsproto). The main differences of `Proto` are:
* The framework codebase has been reduced (with comparable functionality), making it easier to maintain and extend. Pools and iterators can now be implemented by the user.
* Built-in module support appeared - user code is now easier to separate and connect to new projects.
* Built-in non-linear system connection system appeared - you can explicitly specify integration control points.
* Pools are now known at world startup and cannot be added during the process intentionally or accidentally.
* Absence of filters - with a large number of them (from hundreds) and thousands of entities falling into them, `Proto` seriously wins in speed (up to 3x) when adding / removing components.
* Due to the absence of filters, the speed of linear iteration over entities decreased - by 10% with a slight linear slowdown depending on the number of components in the world.
* New license.


### I want to save a reference to an entity in a component. How can I do this?
For this, you should implement saving Id+Gen of the entity yourself, or use the implementation from the `EcsProto.QoL` extension.


### I want to call one system in `MonoBehaviour.Update()`, and another in `MonoBehaviour.FixedUpdate()`. How can I do this?
To separate systems based on different methods from `MonoBehaviour`, you need to create a separate `IProtoSystems` group for each method:
```c#
IProtoSystems _update;
IProtoSystems _fixedUpdate;

void Start () {
    ProtoWorld world = new (new WorldAspect ());
    _update = new ProtoSystems (world);
    _update
        .AddSystem (new UpdateSystem ())
        .Init ();
    _fixedUpdate = new ProtoSystems (world);
    _fixedUpdate
        .AddSystem (new FixedUpdateSystem ())
        .Init ();
}

void Update () {
    _update.Run ();
}

void FixedUpdate () {
    _fixedUpdate.Run ();
}
```


### I'm not satisfied with the default values for component fields. How can I configure this?
Components support setting arbitrary values through implementing the `SetResetHandler()` handler:
```c#
struct MyComponent : IProtoHandlers<MyComponent> {
    public int Id;
    public object SomeExternalData;

    public void SetHandlers (IProtoPool<MyComponent> pool) => pool.SetResetHandler (OnReset);

    static void OnReset (ref MyComponent c) {
        // Don't forget that the actual component is passed as a parameter!
        // Using static methods should help with this problem.
        c.Id = 2;
        c.SomeExternalData = null;
    }
}
```
This method will be automatically called for all new components, as well as for all just removed ones, before placing them in the pool.
> **IMPORTANT!** When using the `SetResetHandler()` handler, all additional component field cleanup/checks are disabled, which can lead to memory leaks. The responsibility lies with the user!


### I'm not satisfied with the values for component fields when copying them through EcsWorld.CopyEntity() or Pool<>.Copy(). How can I configure this?
Components support setting arbitrary values when calling `ProtoWorld.CopyEntity()` or `IPool.Copy()` through implementing the `SetCopyHandler()` handler:
```c#
struct MyComponent : IProtoHandlers<MyComponent> {
    public int Id;

    public void SetHandlers (IProtoPool<MyComponent> pool) => pool.SetCopyHandler (OnCopy);

    static void OnCopy (ref MyComponent src, ref MyComponent dst) {
        // Don't forget that the actual components are passed as parameters!
        // Using static methods should help with this problem.
        dst.Id = src.Id * 123;
    }
}
```
> **IMPORTANT!** When using the `SetCopyHandler()` handler, no default copying occurs. The responsibility for correct data filling and source integrity lies with the user!


### I want to perform serialization/deserialization of components without allocations. How can I do this?
Components can be serialized with your own handler through calling `IPool.Serialize()`/`IPool.Deserialize()`:
```c#
struct MyComponent : IProtoHandlers<MyComponent> {
    public int Id;

    public void SetHandlers (IProtoPool<MyComponent> pool) {
        pool.SetSerizalizeHandler (OnSerialize);
        pool.SetDeserizalizeHandler (OnDeserialize);
    }

    static void OnSerialize (ref MyComponent src, Stream writer) {
        // Don't forget that the actual component is passed
        // as a parameter, this cannot be used! Using
        // static methods should help with this problem.
        Span<byte> b = stackalloc byte[sizeof (int)];
        BitConverter.TryWriteBytes (b, src.Id);
        writer.Write (b);
    }

    static void OnDeserialize (ref MyComponent src, Stream reader) {
        // Don't forget that the actual component is passed
        // as a parameter, this cannot be used! Using
        // static methods should help with this problem.

        // Perform reading into fields from reader.
    }
}
```
After this, you can call component serialization on the desired entity:
```c#
// IPool pool;
// ProtoEntity entity;
// Stream writer;
// Stream reader;
bool okWrite = pool.Serialize (entity, writer);
bool okRead = pool.Deserialize (entity, reader);
```
The operation result will be `false` if the pool doesn't implement such functionality, the pool doesn't have `SetSerializeHandler()`/`SetDeserializeHandler()` handlers set, or there's no component from the specified pool on the specified entity, otherwise `true`.


### I want to add reactivity and handle world change events myself. How can I do this?
> **IMPORTANT!** This is not recommended due to performance degradation.

To activate this functionality, you should add `LEOECSPROTO_WORLD_EVENTS` to the compiler directives list, and then add an event listener:
```c#
class TestEventListener : IProtoEventListener {
    public void OnEntityCreated (ProtoEntity entity) {
        // Entity created - method will be called at the moment of calling IProtoPool.NewEntity().
    }

    public void OnEntityChanged (ProtoEntity entity, ushort poolId, bool added) {
        // Entity changed - method will be called at the moment of calling pool.Add() / pool.Del().
    }

    public void OnEntityDestroyed (ProtoEntity entity) {
        // Entity destroyed - method will be called at the moment of calling world.DelEntity() or when removing the last component.
    }

    public void OnWorldResized (int capacity) {
        // World resized - method will be called when entity caches are resized at the moment of calling IProtoPool.NewEntity().
    }

    public void OnWorldDestroyed () {
        // World destroyed - method will be called at the moment of calling world.Destroy().
    }
}
// Environment initialization.
ProtoWorld world = new (new Aspect1 ());
TestEventListener listener = new ();
world.AddEventListener (listener);
```


### I want to use modules in my code and I have problems connecting aspects from different modules - the world requires manual assembly of the root aspect. How can I simplify this?
For this, you should use the `ProtoModules` class from the `EcsProto.QoL` extension.


### I want to disable systems in whole groups and enable them back when needed. How can I do this?
For this, you should use the `ConditionalSystem` system from the `EcsProto.ConditionalSystems` extension.


### I will have no more than a couple of tens of thousands of entities in the world and several thousand different components. How can I optimize memory consumption?
To activate memory saving mode for a limited number of entities (up to 65535 inclusive), you should add `LEOECSPROTO_SMALL_WORLD` to the compiler directives list.


### I find it inconvenient to create new entities through pools. How can I create an empty entity and add the necessary components to it?
> **IMPORTANT!** There is no mechanism for tracking empty entities, this can lead to memory leaks.

To activate the empty entity creation mode, you should add `LEOECSPROTO_NEW_EMPTY_ENTITY` to the compiler directives list, after which the `ProtoWorld.NewEntity()` method will be available.
