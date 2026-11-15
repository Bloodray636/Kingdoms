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


## Архитектура

- **ScriptableObject** — все игровые данные (ресурсы, здания, юниты) в `Assets/_Project/ScriptableObjects/`
- **EventBus** — глобальная шина событий (struct-события, `Subscribe/Unsubscribe` в `OnEnable/OnDisable`)
- **Менеджеры-синглтоны** — `GameManager`, `TimeManager`, `ResourceManager`, `GridSystem`, `BuildingManager`, `VillagerManager`, `JobManager`, `HousingManager`, `HappinessSystem`, `PopulationManager`, `PathfindingService`
- **Grid** — клетки 1×1 метра, `Cell[,]` массив, `WorldToCell` / `CellToWorld` / `GetSurfaceY`
- **Pathfinding** — A* на графе из клеток, вес = `Cell.MovementCost`

## Стек

- **Unity 6 LTS** + **URP** (Universal Render Pipeline)
- **New Input System** (`Keyboard.current`, `Mouse.current`)
- **uGUI** (для игрового UI) + **IMGUI** (для debug-меню)
- **Newtonsoft Json** (будет в Epic 12 для сохранений)
- **Git LFS** (арт-ассеты)

## Лицензия

Приватный проект, все права у авторов.