<p align="center">
    <img src="./logo.png" alt="Logo">
</p>

# LeoEcs Proto - Легковесный C# Entity Component System фреймворк
Производительность, нулевые или минимальные аллокации, минимизация использования памяти, отсутствие зависимостей от любого игрового движка - это основные цели данного фреймворка.

> **ВАЖНО!** Требует C#9 (или Unity >=2021.2).

> **ВАЖНО!** Не забывайте использовать `DEBUG`-версии билдов для разработки и `RELEASE`-версии билдов для релизов: все внутренние проверки/исключения будут работать только в `DEBUG`-версиях и удалены для увеличения производительности в `RELEASE`-версиях.

> **ВАЖНО!** LeoEcs Proto **не потокобезопасен** и никогда не будет таким! Если вам нужна многопоточность - вы должны реализовать ее самостоятельно и интегрировать синхронизацию в виде ecs-системы.

> **ВАЖНО!** Проверено на Unity 2021.3 (не зависит от нее) и содержит asmdef-описания для компиляции в виде отдельных сборок и уменьшения времени рекомпиляции основного проекта.


# Социальные ресурсы
Официальный блог: https://leopotam.ru


# Установка


## В виде unity модуля
Поддерживается установка в виде unity-модуля через git-ссылку в PackageManager или прямое редактирование `Packages/manifest.json`:
```
"ru.leopotam.ecsproto": "https://gitverse.ru/leopotam/ecsproto.git",
```


## В виде исходников
Код так же может быть склонирован или получен в виде архива со страницы релизов.


## Прочие источники
Официальная работоспособная версия размещена по адресу [https://gitverse.ru/leopotam/ecsproto](https://gitverse.ru/leopotam/ecsproto), все остальные версии (включая *nuget*, *npm* и прочие репозитории) являются неофициальными клонами или сторонним кодом с неизвестным содержимым.


# Основные типы


## Сущность
Сама по себе ничего не значит и является исключительно идентификатором для для набора компонентов. Реализована как `ProtoEntity`:
```c#
// Создаем новую сущность в мире. Создание возможно только через компонентный пул,
// полученный из мира, с одновременным навешиванием компонента этого пула на сущность.
ProtoPool<C1> C1Pool; // Инициализированный ранее пул.
ref C1 c1 = ref C1Pool.NewEntity (out ProtoEntity entity);

// Любая сущность может быть удалена, при этом сначала все ее компоненты
// будут автоматически удалены и только потом сущность будет считаться уничтоженной.
world.DelEntity (entity);

// Компоненты с любой активной сущности могут быть скопированы на другую существующую.
world.CopyEntity (srcEntity, dstEntity);

// Любая сущность может быть склонирована в новую сущность со всеми компонентами.
ProtoEntity clonedEntity = world.CloneEntity (srcEntity);
```

> **ВАЖНО!** На сущности может существовать только один экземпляр каждого типа компонента.

> **ВАЖНО!** Сущности не могут существовать без компонентов и будут автоматически уничтожаться при удалении последнего компонента на них.

> **ВАЖНО!** Тип `ProtoEntity` не является ссылочным, экземпляры этого типа нельзя сохранять за пределами текущего метода без обеспечения целостности. Если требуется сохранение, то следует сохранять пару `ProtoEntity`-сущность и ее поколение, полученное через вызов `ProtoWorld.EntityGen()`. В `EcsProto.QoL`-расширении есть готовая реализация в виде `ProtoPackedEntity` или `ProtoPackedEntityWithWorld`.

## Компонент
Является контейнером для данных пользователя и не должен содержать логику (допускается минимальная вспомогательная обвязка, но не куски основной логики):
```c#
struct Component1 {
    public int Id;
    public string Name;
}
struct Component2 {
    // Компоненты могут быть пустыми и использоваться как маркеры для фильтрации.
}
```
Компоненты могут быть добавлены, запрошены или удалены через компонентные пулы.


## Система
Является контейнером для основной логики для обработки отфильтрованных сущностей.
Существует в виде пользовательского класса, реализующего как минимум один из интерфейсов:
```c#
class UserSystem : IProtoInitSystem, IProtoRunSystem, IProtoDestroySystem {
    public void Init (IProtoSystems systems) {
        // Будет вызван один раз в момент работы IProtoSystems.Init().
    }

    public void Run () {
        // Будет вызван один раз в момент работы IProtoSystems.Run().
    }

    public void Destroy () {
        // Будет вызван один раз в момент работы IProtoSystems.Destroy().
    }
}
```


# Сервисы
Экземпляр любого пользовательского ссылочного типа (класса) может быть одновременно подключен ко всем системам:
```c#
class PathService {
    public string PrefabsPath;
}
interface ISettingsService { }
class SettingsService : ISettingsService {
    public Vector3 SpawnPoint;
}
// Инициализация в стартовом коде.
PathService pathService = new () { PrefabsPath = "Items/{0}" };
SettingsService settingsService = new () { SpawnPoint = new Vector3 (123, 0, 456) };
ProtoSystems systems = new (world);
systems
    .AddSystem (new System1 ())
    // Регистрация сервиса.
    .AddService (pathService)
    // Допускается переопределение типа для регистрации.
    .AddService (settingsService, typeof(ISettings))
    .Init ();
// Получение доступа в системе.
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


# Специальные типы


## Аспект
Является контейнером для пулов компонентов, существующих в мире.

> **ВАЖНО!** Пулы можно создавать только внутри инициализатора аспекта для мира.

> **ВАЖНО!** Аспекты могут быть частью других аспектов. В конструктор мира передается главный (корневой) аспект,
> являющийся композицией всех аспектов / пулов, из данных которых будет состоять мир.
> Инициализация вложенных аспектов должна выполняться путем вызова методов `Init()` и `PostInit()`.

```c#
class Aspect1 : IProtoAspect {
    public ProtoPool<Component1> C1Pool;

    public void Init (ProtoWorld world) {
        // Обязательная регистрация этого аспекта для дальнейшего доступа из систем.
        world.AddAspect (this);
        // Создание экземпляра пула с кешированием в поле аспекта.
        C1Pool = new ();
        // Обязательная регистрация этого пула в мире.
        world.AddPool (C1Pool);
    }
    public void PostInit () {
        // Дополнительный этап инициализации. Если есть вложенные аспекты,
        // созданные в процессе инициализации этого аспекта - у них должен быть
        // вызван метод PostInit(). Так же вложенные итераторы должны быть
        // инициализированы здесь вызовом метода Init().
    }
}
```
Аспекты могут выступать в качестве группировки уже существующих пулов:
```c#
class Aspect2 : IProtoAspect {
    public ProtoPool<Component1> C1Pool;

    public void Init (ProtoWorld world) {
        world.AddAspect (this);
        if (!world.HasPool (typeof (Component1)) {
            // Создаем новый пул если не существует.
            C1Pool = new ();
            world.AddPool (C1Pool);
        } else {
            // Получаем существующий пул.
            C1Pool = (ProtoPool<Component1>) world.Pool (typeof (Component1))
        }
    }
}
```


## Мир
Является контейнером для всех сущностей, данные каждого экземпляра уникальны и изолированы от других миров.

> **ВАЖНО!** Мир не может существовать хотя бы без одного аспекта.

> **ВАЖНО!** Необходимо вызывать `ProtoWorld.Destroy()` у экземпляра мира если он больше не нужен.

```c#
// Создаем мир.
ProtoWorld world = new (new Aspect1 ());
// Работаем с миром.
// ...
// Удаляем мир.
world.Destroy ();
```


## Пул
Является контейнером для компонентов, предоставляет апи для добавления / запроса / удаления компонентов на сущности:
```c#
ProtoWorld world = new (new Aspect1 ());
// Возможный, но нерекомендуемый способ доступа к существующему пулу мира.
ProtoPool<Component1> pool1 = (ProtoPool<Component1>) world.Pool (typeof (Component1);
ProtoPool<Component2> pool2 = (ProtoPool<Component2>) world.Pool (typeof (Component2);
// Правильный способ доступа к пулу.
Aspect1 proto1 = (Aspect1) world.Aspect (typeof (Aspect1));
pool = proto1.C1Pool;

// NewEntity() создает сущность и добавляет на нее компонент из пула.
ref Component1 c1 = ref pool1.NewEntity (out ProtoEntity entity);

// Add() добавляет компонент к сущности.
// Если компонент уже существует - будет брошено исключение в DEBUG-версии.
ref Component2 c2 = ref pool2.Add (entity);

// Has() проверяет наличие компонента на сущности и возвращает результат.
bool c1Exists = pool1.Has (entity);

// Get() возвращает существующий на сущности компонент.
// Если компонент не существовал - будет брошено исключение в DEBUG-версии.
ref Component1 c11 = ref pool1.Get (entity);

// Del() удаляет компонент с сущности. Если компонент не существовал - будет
// брошено исключение в DEBUG-версии. Если это был последний компонент, то
// сущность будет удалена автоматически.
pool1.Del (entity);

// Copy() выполняет копирование всех компонентов с одной сущности на другую.
// Если исходная или целевая сущность не существует - будет брошено исключение в DEBUG-версии.
pool1.Copy (srcEntity, dstEntity);
```

> **ВАЖНО!** После удаления компонент будет возвращен в пул для последующего переиспользования.
> Все поля компонента будут сброшены в значения по умолчанию автоматически.

> **ВАЖНО!** После вызова `pool.Add()` и `pool.Del()` все полученные ранее `ref`-ссылки на
компоненты из этого пула через вызовы `pool.Add()` и `pool.Get()` становятся потенциально невалидными,
для обращения к компонентам их требуется запрашивать снова через вызов `pool.Get()`.


## Итератор
Итератор является способом фильтрации сущностей по наличию или отсутствию на них указанных компонентов:
```c#
class System1 : IProtoInitSystem, IProtoRunSystem {
    Aspect1 _aspect;
    ProtoIt _it;
    public void Init (IProtoSystems systems) {
        // Получаем экземпляр мира по умолчанию.
        ProtoWorld world = systems.World ();
        // Получаем аспект мира (из примера выше) и кешируем его.
        _aspect = (Aspect1) world.Aspect (typeof (Aspect1));
        // Создаем итератор с явным указанием типов требуемых (include) компонентов.
        _it = new (new [] { typeof Component1 } );
        // Инициализируем его для указания, из какого мира берутся данные.
        _it.Init (world);

        // Создаем новую сущность и добавляем к ней компонент "Component1".
        _aspect.C1Pool.NewEntity (out _);
    }

    public void Run () {
        // Мы хотим получить все сущности с компонентом "Component1".
        for (ProtoEntity entity in _it) {
            // получаем доступ к компоненту на отфильтрованной сущности.
            ref Component1 c1 = ref _aspect.C1Pool.Get (entity);
        }
    }
}
```

Если требуется указать отсутствие определенных компонентов, то тип итератора меняется на `ProtoItExc`,
принимающий 2 параметра (include/exclude списки типов):
```c#
// Итератор по сущностям с компонентами `C1`,`C2`, но без `C3`.
ProtoItExc it = new (new [] { typeof (C1), typeof (C2) }, new [] { typeof (C3) });
```

> **ВАЖНО!** Итераторы должны создаваться один раз на старте и не предназначены для создания динамически в `Run()`-системах.

Итераторы могут быть частью аспекта, в этом случае они должны инициализироваться в методе `PostInit()`:

```c#
class Aspect2 : IProtoAspect {
    public ProtoWorld World;
    public ProtoPool<Component1> C1Pool;
    public ProtoIt C1It;

    public void Init (ProtoWorld world) {
        _world = world;
        world.AddAspect (this);
        if (!world.HasPool (typeof (Component1))) {
            // Создаем новый пул если не существует.
            C1Pool = new ();
            world.AddPool (C1Pool);
        } else {
            // Получаем существующий пул.
            C1Pool = (ProtoPool<Component1>) world.Pool (typeof (Component1))
        }
        // Создавать итератор можно внутри Init(), но без инициализации.
        C1It = new (new Type[] { typeof (Component1) });
    }
    public void PostInit () {
        // Инициализация итератора.
        C1It.Init (_world);
    }
}
```

Итераторы поддерживают перебор совместимых сущностей через `IProtoIt`, который они реализуют:

```c#
IProtoIt it = new ProtoIt (new [] { typeof (C1) }).Init (world);
foreach (ProtoEntity e in it) {
    // обработка.
}
```

> **ВАЖНО!** Этот вариант медленнее, чем перебор по конкретным типам итераторов и не рекомендован для большого количества сущностей.

### Количество сущностей в итераторе
Итераторы позволяют узнать количество сущностей, подходящих под их условия:

```c#
ProtoIt it = new (new [] { typeof (C1) });
it.Init (world);
int entitiesCount = it.LenSlow ();
```

> **ВАЖНО!** Не рекомендуется к использованию если сущностей может быть больше пары десятков - подсчет ведется полным перебором итератора.


### Наличие сущностей в итераторе
Если точное количество не требуется, а достаточно просто знать, что итератор не пустой, то можно воспользоваться следующим методом:

```c#
ProtoIt it = new (new [] { typeof (C1) });
it.Init (world);
bool isEmpty = it.IsEmptySlow ();
```

> **ВАЖНО!** Этот метод быстрее `IProtoIt.LenSlow()`, но в худшем случае все-равно выполняется полный перебор итератора.


### Получение первой сущности в итераторе
Если требуется получить только первую сущность из итератора с корректной обработкой ее отсутствия, то можно воспользоваться следующим методом:
```c#
(ProtoEntity entity, bool ok) = it.FirstSlow ();
if (ok) {
    // Проверяем успешность операции,
    // сущность валидна и ее можно использовать.
}
```

> **ВАЖНО!** В худшем случае все-равно выполняется полный перебор итератора.


## Группа систем
Является контейнером для систем, определяет порядок выполнения (на примере интеграции в Unity):
```c#
class Startup : MonoBehaviour {
    ProtoWorld _world;
    IProtoSystems _systems;

    void Start () {
        // Создаем окружение, подключаем системы.
        _world = new (new Aspect1 ());
        _systems = new ProtoSystems (_world);
        _systems
            .AddSystem (new System1 ())
            // Можно подключить дополнительные миры.
            // .AddWorld (new ProtoWorld (new Aspect2 ()), "events")
            .Init ();
    }

    void Update () {
        // Выполняем все подключенные системы.
        _systems?.Run ();
    }

    void OnDestroy () {
        // Уничтожаем подключенные системы.
        _systems?.Destroy ();
        _systems = null;
        // Очищаем окружение.
        _world?.Destroy ();
        _world = null;
    }
}
```

> **ВАЖНО!** Необходимо вызывать `IProtoSystems.Destroy()` у экземпляра группы систем если он больше не нужен.

Системы можно подключать в одном порядке, а выполнять - в другом, для этого можно указать "вес" системы:
```c#
systems
    .AddSystem (new System1 (), 3)
    .AddSystem (new System2 (), 2)
    .AddSystem (new System3 (), 1)
    .Init ();
```
Системы выполнятся в следующем порядке:
> System3 > System2 > System1

> **ВАЖНО!** "Веса" являются глобальными отсортированными по возрастанию блоками систем в пределах `IProtoSystems`, а не уникальными значениями для каждой системы, поэтому можно подключать несколько систем с одинаковым "весом" - в этом случае они будут выполняться в пределах одного "веса" в порядке подключения.

Если явно не указывать "вес" системы, система будет иметь вес 0:
```c#
systems
    .AddSystem (new System1 (), 3)
    .AddSystem (new System2 ())
    .AddSystem (new System3 (), 2)
    .AddSystem (new System4 (), 1)
    .Init ();
```
Системы выполнятся в следующем порядке:
> System2 > System4 > System3 > System1

> **ВАЖНО!** "Веса" могут иметь как положительные, так и отрицательные значения - это позволяет выполнять системы до систем с весом по умолчанию.


## Модуль
Используется для разделения пользовательского кода на модули:
```c#
class Module1 : IProtoModule {
    int _pointWeight;

    public Module1(int pointWeight) {
        // Если есть необходимость регистрации с определенным весом -
        // его значение можно передать через конструктор.
        _pointWeight = pointWeight;
    }

    public void Init (IProtoSystems systems) {
        // Регистрация систем и сервисов модуля.
        systems
            .AddSystem (new System1 (), _pointWeight)
            .AddService (new Service1 ());
    }

    // Метод должен вернуть список всех аспектов модуля
    // для возможности автоматизации регистрации, либо null.
    public IProtoAspect[] Aspects () {
        return new IProtoAspect[] { new Module1Aspect () };
    }

    // Метод должен вернуть список всех модулей, от которых
    // зависит текущий модуль и которые должны быть
    // зарегистрированы до него, либо null.
    public Type[] Dependencies () {
        return new IProtoModule[] { new Module2 () };
    }

    class System1 : IProtoInitSystem {
        public void Init (IProtoSystems systems) { }
    }

    class Service1 { }
}
// Подключение модуля.
systems
    .AddModule (new Module1 (1))
    .Init();
```

Аспект модуля так же может быть вынесен отдельно:
```c#
// Аспект модуля.
class Module1Aspect : IProtoAspect {
    public ProtoPool<Component1> C1Pool;

    public void Init (ProtoWorld world) {
        world.AddAspect (this);
        C1Pool = new ();
        world.AddPool (C1Pool);
    }

    public void PostInit() {}
}
// Главный аспект мира, включающий в себя аспекты всех модулей.
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


# Интеграция с движками


## Unity

Интегратор выполнен в виде модуля расширения и может быть установлен в дополнение к ядру.


## Кастомный движок

Каждая часть примера ниже должна быть корректно интегрирована в правильное место выполнения кода движком:
```c#
using Leopotam.EcsProto;

class EcsStartup {
    ProtoWorld _world;
    IProtoSystems _systems;

    // Инициализация окружения.
    void Init () {
        _world = new (new Aspect1 ());
        _systems = new ProtoSystems (_world);
        _systems
            // Дополнительные экземпляры миров
            // должны быть зарегистрированы здесь.
            // .AddWorld (new ProtoWorld (new Aspect2 ()), "events")

            // Модули должны быть зарегистрированы здесь.
            // .AddModule (new TestModule1 ())
            // .AddModule (new TestModule2 ())

            // Системы вне модулей могут
            // быть зарегистрированы здесь.
            // .AddSystem (new TestSystem1 ())
            // .AddSystem (new TestSystem2 ())

            // Сервисы могут быть добавлены в любом месте.
            // .AddService (new TestService1 ())

            .Init ();
    }

    // Метод должен быть вызван из
    // основного update-цикла движка.
    void UpdateLoop () {
        _systems.Run ();
    }

    // Очистка окружения.
    void Destroy () {
        _systems?.Destroy ();
        _systems = null;
        _world?.Destroy ();
        _world = null;
    }
}
```


# Расширения

* [Улучшение "качества жизни" (Quality of Life) разработчика](https://gitverse.ru/leopotam/ecsproto-qol)
* [Интеграция в редактор Unity](https://gitverse.ru/leopotam/ecsproto.unity)
* [Интеграция событий Unity Physics2D](https://gitverse.ru/leopotam/ecsproto.unity.physics2d)
* [Интеграция событий Unity Physics3D](https://gitverse.ru/leopotam/ecsproto.unity.physics3d)
* [Интеграция событий Unity uGui](https://gitverse.ru/leopotam/ecsproto.unity.ugui)
* [Интеграция многопоточной обработки](https://gitverse.ru/leopotam/ecsproto.threads)
* [Взаимоотношение родитель-ребенок для сущностей](https://gitverse.ru/leopotam/ecsproto.parenting)
* [Группировка систем в блоки с условным выполнением](https://gitverse.ru/leopotam/ecsproto.conditionalsystems)
* [UtilityAI](https://gitverse.ru/leopotam/ecsproto.ai.utility)
* [Unity-интеграция UtilityAI](https://gitverse.ru/leopotam/ecsproto.ai.utility.unity)


# Лицензия
Фреймворк выпускается под лицензией MIT-ZARYA, [подробности тут](./LICENSE.md).


# ЧаВо


### В чем отличие от LeoECS Lite?
Я предпочитаю называть их `Лайт` (ecslite) и `Прото` (ecsproto). Основные отличия `Прото` следующие:
* Кодовая база фреймворка уменьшилась (на соизмеримом функционале), ее стало проще поддерживать и расширять. Пулы и итераторы теперь могут быть реализованы пользователем.
* Появилась штатная поддержка модулей - пользовательской код теперь проще разделять и подключать в новые проекты.
* Появилась штатная система нелинейного подключения систем - можно явно указывать контрольные точки интеграции.
* Пулы теперь известны на старте мира и не могут быть добавлены в процессе намеренно или случайно.
* Отсутствие фильтров - при большом их количестве (от сотни) и тысячах сущностей, попадающих в них, `Прото` серьезно выигрывает по скорости (до х3 раз) при добавлении / удалении компонентов.
* Из-за отсутствия фильтров снизилась скорость линейной итерации по сущностям - от 10% с небольшим линейным замедлением в зависимости от количества компонентов в мире.
* Новая лицензия.


### Я хочу сохранить ссылку на сущность в компоненте. Как я могу это сделать?
Для этого следует реализовать сохранение Id+Gen сущности самостоятельно, либо воспользоваться реализацией из `EcsProto.QoL`-расширения.


### Я хочу одну систему вызвать в `MonoBehaviour.Update()`, а другую - в `MonoBehaviour.FixedUpdate()`. Как я могу это сделать?
Для разделения систем на основе разных методов из `MonoBehaviour` необходимо создать под каждый метод отдельную `IProtoSystems`-группу:
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


### Меня не устраивают значения по умолчанию для полей компонентов. Как я могу это настроить?
Компоненты поддерживают установку произвольных значений через реализацию обработчика `SetResetHandler()`:
```c#
struct MyComponent : IProtoHandlers<MyComponent> {
    public int Id;
    public object SomeExternalData;

    public void SetHandlers (IProtoPool<MyComponent> pool) => pool.SetResetHandler (OnReset);

    static void OnReset (ref MyComponent c) {
        // Не забываем, что актуальный компонент передается параметром!
        // Использование static-методов должно помочь с этой проблемой.
        c.Id = 2;
        c.SomeExternalData = null;
    }
}
```
Этот метод будет автоматически вызываться для всех новых компонентов, а так же для всех только что удаленных, до помещения их в пул.
> **ВАЖНО!** В случае применения обработчика `SetResetHandler()` все дополнительные очистки/проверки полей компонента отключаются, что может привести к утечкам памяти. Ответственность лежит на пользователе!


### Меня не устраивают значения для полей компонентов при их копировании через EcsWorld.CopyEntity() или Pool<>.Copy(). Как я могу это настроить?
Компоненты поддерживают установку произвольных значений при вызове `ProtoWorld.CopyEntity()` или `IPool.Copy()` через реализацию обработчика `SetCopyHandler()`:
```c#
struct MyComponent : IProtoHandlers<MyComponent> {
    public int Id;

    public void SetHandlers (IProtoPool<MyComponent> pool) => pool.SetCopyHandler (OnCopy);

    static void OnCopy (ref MyComponent src, ref MyComponent dst) {
        // Не забываем, что актуальные компоненты передаются параметрами!
        // Использование static-методов должно помочь с этой проблемой.
        dst.Id = src.Id * 123;
    }
}
```
> **ВАЖНО!** В случае применения обработчика `SetCopyHandler()` никакого копирования по умолчанию не происходит. Ответственность за корректность заполнения данных и за целостность исходных лежит на пользователе!


### Я хочу выполнять сериализацию/десериализацию компонентов без аллокаций. Как я могу это сделать?
Компоненты можно сериализовать своим обработчиком через вызов `IPool.Serialize()`/`IPool.Deserialize()`:
```c#
struct MyComponent : IProtoHandlers<MyComponent> {
    public int Id;

    public void SetHandlers (IProtoPool<MyComponent> pool) {
        pool.SetSerizalizeHandler (OnSerialize);
        pool.SetDeserizalizeHandler (OnDeserialize);
    }

    static void OnSerialize (ref MyComponent src, Stream writer) {
        // Не забываем, что актуальный компонент передается
        // параметром, this использовать нельзя! Использование
        // static-методов должно помочь с этой проблемой.
        Span<byte> b = stackalloc byte[sizeof (int)];
        BitConverter.TryWriteBytes (b, src.Id);
        writer.Write (b);
    }

    static void OnDeserialize (ref MyComponent src, Stream reader) {
        // Не забываем, что актуальный компонент передается
        // параметром, this использовать нельзя! Использование
        // static-методов должно помочь с этой проблемой.

        // Выполняем чтение в поля из reader.
    }
}
```
После этого можно вызывать сериализацию компонента на нужной сущности:
```c#
// IPool pool;
// ProtoEntity entity;
// Stream writer;
// Stream reader;
bool okWrite = pool.Serialize (entity, writer);
bool okRead = pool.Deserialize (entity, reader);
```
Результат операции будет `false`, если пул не реализует такой функционал, для пула не установлены обработчики `SetSerializeHandler()`/`SetDeserializeHandler()` или на указанной сущности нет компонента из указанного пула, иначе `true`.


### Я хочу добавить реактивности и обрабатывать события изменений в мире самостоятельно. Как я могу сделать это?
> **ВАЖНО!** Так делать не рекомендуется из-за падения производительности.

Для активации этого функционала следует добавить `LEOECSPROTO_WORLD_EVENTS` в список директив комплятора, а затем - добавить слушатель событий:
```c#
class TestEventListener : IProtoEventListener {
    public void OnEntityCreated (ProtoEntity entity) {
        // Сущность создана - метод будет вызван в момент вызова IProtoPool.NewEntity().
    }

    public void OnEntityChanged (ProtoEntity entity, ushort poolId, bool added) {
        // Сущность изменена - метод будет вызван в момент вызова pool.Add() / pool.Del().
    }

    public void OnEntityDestroyed (ProtoEntity entity) {
        // Сущность уничтожена - метод будет вызван в момент вызова world.DelEntity() или в момент удаления последнего компонента.
    }

    public void OnWorldResized (int capacity) {
        // Мир изменил размеры - метод будет вызван в случае изменения размеров кешей под сущности в момент вызова IProtoPool.NewEntity().
    }

    public void OnWorldDestroyed () {
        // Мир уничтожен - метод будет вызван в момент вызова world.Destroy().
    }
}
// Инициализация окружения.
ProtoWorld world = new (new Aspect1 ());
TestEventListener listener = new ();
world.AddEventListener (listener);
```


### Я хочу использовать модули в своем коде и у меня возникают проблемы с подключением аспектов из разных модулей - мир требует ручной сборки корневого аспекта. Как я могу это упростить?
Для этого следует воспользоваться `ProtoModules`-классом из `EcsProto.QoL`-расширения.


### Я хочу отключать системы целыми группами и включать их обратно когда потребуется. Как я могу это сделать?
Для этого следует воспользоваться `ConditionalSystem`-системой из `EcsProto.ConditionalSystems`-расширения.


### У меня будет не более пары десятков тысяч сущностей в мире и несколько тысяч разных компонентов. Как я могу оптимизировать потребление памяти?
Для активации режима экономии памяти на ограниченном количестве сущностей (до 65535 включительно) следует добавить `LEOECSPROTO_SMALL_WORLD` в список директив комплятора.


### Мне неудобно создавать новые сущности через пулы. Как я могу создавать пустую сущность и добавлять к ней нужные компоненты?
> **ВАЖНО!** Нет никакого механизма по отслеживанию пустых сущностей, это может привести к утечкам памяти.

Для активации режима создания пустых сущностей следует добавить `LEOECSPROTO_NEW_EMPTY_ENTITY` в список директив комплятора, после этого будет доступен метод `ProtoWorld.NewEntity()`.
