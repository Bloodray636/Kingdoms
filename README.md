# KingdomsClone

RTS/город-строитель в стиле Kingdoms and Castles.

## Требования
- Unity 6 LTS **6000.0.XXfY** (точная версия — см. `ProjectSettings/ProjectVersion.txt`)
- Git LFS (`git lfs install` перед клоном)

## Запуск
1. Склонировать репо
2. Открыть папку проекта через Unity Hub (**Add project from disk**)
3. Открыть сцену `Assets/_Project/Scenes/Boot.unity`
4. Play

## Управление

| Клавиша | Действие |
|---|---|
| **WASD / стрелки** | Панорама камеры |
| **Shift + WASD** | Быстрая панорама |
| **Колесо мыши** | Зум |
| **Мышь к краю экрана** | Edge-pan |
| **`** (тильда/Ё) | Debug-меню |
| **B** | Открыть/закрыть панель зданий |
| **T** | Режим прокладки дорог |
| **X** | Режим сноса |
| **R** | Поворот здания (во время установки) |
| **Esc / ПКМ** | Отмена действия |

## Что готово

### ✅ Эпик 0. Фундамент
- ScriptableObject-архитектура (`GameDataSO`, `BuildingDataSO`, `ResourceDataSO`, `UnitDataSO`)
- Статический `EventBus`, `GameManager` с state machine (Boot/Menu/Playing/Paused)
- `TimeManager` (тики 4/сек, дни, сезоны, скорость 1x/2x/3x + debug x10)
- URP с day/night cycle и тенями
- RTS-камера с auto-bounds
- Debug-меню и пайплайн сборки

### ✅ Эпик 1. Мир и сетка
- Процедурная генерация карты (Perlin noise + flood fill)
- 5 типов тайлов: Grass / Forest / Rock / Water / Fertile
- Единый 3D-меш с vertex colors (нет дыр, нет z-fighting)
- 3D-декор: вода, деревья, камни
- Гарантия стартовой зоны (есть доступ к воде и лесу)
- Дороги (прокладка мышкой, вес для pathfinding, вырубка леса)

### ✅ Эпик 2. Строительство
- Призрак здания: зелёный/жёлтый/красный, поворот по R, отмена Esc
- CanPlace: проверка типа тайла, занятости, ресурсов
- Атомарное списание ресурсов, возврат 50% при сносе
- UI-меню категорий (Жильё / Производство / Счастье / Оборона / Склады)
- Тултипы с ценой, HP, размерами, эффектами
- Режим сноса с подсветкой
- Мультиклеточные здания (2×2, 3×3) с правильным footprint при повороте
- Время постройки (`BuildTimeSeconds`)

### ✅ Эпик 3. Жители (1–3.5)
- FSM: Idle / Move / Work / Eat / Sleep / Flee
- **A\*** — собственный, с бинарной кучей, весами (дороги дешевле), кэшем путей
- **Гранулярная инвалидация кэша** — при постройке сбрасываются только затронутые пути
- **Re-path на ходу** — если здание появилось на маршруте, житель перестраивает путь
- Рост населения: дом → свободные места → счастье ≥ 5 → спавн
- **JobManager** — назначение на вакансии по приоритету + дистанции
- Уникальные рабочие позиции (жители не стакаются)
- Separation force — мягкое расталкивание соседей

## Что в разработке

- Эпик 3.6+ — цикл «работа → склад → дом», реальное производство ресурсов
- Эпик 4 — экономика (налоги, цепочки производства, локальные склады)
- Эпик 5 — сезоны, зима, отопление, голод
- Эпик 6 — счастье и модификаторы
- Эпик 7 — стены, башни, цитадель
- Эпик 8 — враги (викинги, дракон, огр) и бои

## Архитектура (кратко)

- **ScriptableObject** — все игровые данные (ресурсы, здания, юниты) в `Assets/_Project/ScriptableObjects/`
- **EventBus** — глобальная шина событий (struct-события, `Subscribe/Unsubscribe` в `OnEnable/OnDisable`)
- **Менеджеры-синглтоны** — `GameManager`, `TimeManager`, `ResourceManager`, `GridSystem`, `BuildingManager`, `VillagerManager`, `JobManager`, `HousingManager`, `HappinessSystem`, `PopulationManager`, `PathfindingService`
- **Grid** — клетки 1×1 метра, `Cell[,]` массив, `WorldToCell` / `CellToWorld` / `GetSurfaceY`
- **Pathfinding** — A* на графе из клеток, вес = `Cell.MovementCost`

## Структура проекта
Assets/_Project/
├── Scripts/
│ ├── Core/ # GameManager, TimeManager, EventBus, ресурсы
│ ├── Data/ # ScriptableObject классы
│ ├── Grid/ # GridSystem, Cell, TileVisualizer, MapGenerator
│ ├── Gameplay/ # Buildings, Villagers, Jobs
│ ├── Pathfinding/ # A*, кэш, инвалидация
│ ├── CameraControl/ # RTS-камера
│ └── UI/ # BuildingUIManager, HoverHandler
├── ScriptableObjects/ # Инстансы данных (ресурсы, здания, юниты)
├── Resources/ # GameData.asset
├── Prefabs/ # Префабы зданий, декора, жителей
├── Scenes/ # Boot.unity
└── Art/ # Материалы, шейдеры

## Правила

- **Не пушить в `main` напрямую** — только через PR
- **Билд раз в неделю** — см. задачу 0.9 (`Build → Build Windows (Test Build)`)
- **Задачи** — в Notion-доске, не в GitHub Issues
- **Баги** — по шаблону из `G.0.4` (шаги → ожидание → факт → скрин → приоритет)

## Стек

- **Unity 6 LTS** + **URP** (Universal Render Pipeline)
- **New Input System** (`Keyboard.current`, `Mouse.current`)
- **uGUI** (для игрового UI) + **IMGUI** (для debug-меню)
- **Newtonsoft Json** (будет в Epic 12 для сохранений)
- **Git LFS** (арт-ассеты)

## Лицензия

Приватный проект, все права у авторов.