# Запросы к API и подключение

Автоматические HTTP-тесты всех описанных операций: [tests/README.md](../tests/README.md). Запуск из корня: `./tests/Run-ApiTests.ps1`; стенд использует отдельную временную PostgreSQL и не изменяет данные основного Compose.

Эта папка находится рядом с `Soft` и описывает все **16 корневых операций** API: 5 query и 11 mutation. Фактический контракт со всеми полями ответов, input-типами и nullability: [schema.graphql](schema.graphql). Архитектура и инструкции для изменения кода: [../arcitecture.md](../arcitecture.md). Итоги живой проверки: [verification.md](verification.md).

## Запуск и адреса

Из корня репозитория:

```powershell
docker compose -f Soft/Docker-compose.yml build --no-cache
docker compose -f Soft/Docker-compose.yml up -d
docker compose -f Soft/Docker-compose.yml ps -a
```

Ожидается: PostgreSQL healthy, migrator Exited (0), API Up. Для диагностики: `docker compose -f Soft/Docker-compose.yml logs --tail 100 migrator graphql-api`.

| Клиент | Endpoint |
|---|---|
| Браузер, Rider, PowerShell, Postman на хосте | `http://localhost:5000/graphql` |
| Контейнер в той же Compose-сети | `http://graphql-api:8080/graphql` |
| API запущен из IDE, профиль `http` | `http://localhost:5093/graphql` |
| Профиль IDE `https` | `https://localhost:7165/graphql` (нужен доверенный dev certificate) |

Открыть `/graphql` в браузере для GraphQL IDE, если её отдаёт текущая конфигурация Hot Chocolate. Надёжный способ вызова — POST JSON. Swagger/OpenAPI и REST-маршрутов в проекте нет. В проверенном Production-окружении introspection-запросы `__schema`/`__type` возвращают `HC0046`; автоматический schema discovery клиента может не сработать. При этом SDL скачивается через `GET http://localhost:5000/graphql?sdl`.

API сейчас **не требует токен, cookie или логин**: механизмы authentication/authorization не подключены. Тестовая запись из `AuthContext.cs` не является рабочим входом. `userId` в payload — обычное клиентское число, проверки владельца нет. CORS не настроен: для браузерного фронтенда с другого origin нужно изменить серверную конфигурацию.

## Подключение к PostgreSQL / Navicat

| Поле | Локальный Compose |
|---|---|
| Тип | PostgreSQL 16 |
| Host / Port | `localhost` / `5432` |
| User / Password | `postgres` / `postgres` (локальные значения из Compose) |
| Databases | `SlagMode` и `Auth` — две разные БД |
| Schema | `public` |
| Внутри Docker-сети | host `postgres`, port `5432` |

SQL с точными именами: `SELECT * FROM "BlastFurnaces";`, `SELECT * FROM "CalcVariants";`, `SELECT * FROM "ComponentsGuide";`, `SELECT * FROM "ComponentVariants";`. В базе `Auth`: `SELECT "UserId", "UserName", "UserEmail" FROM "Users";`. История EF — `"__EFMigrationsHistory"` в каждой базе.

В исследованной копии `.env` отсутствует. Текущий Compose содержит строки подключения напрямую и не использует `env_file` или `${...}`. Для других доступов заменить/переопределить `ConnectionStrings__SlagModeContext`, `ConnectionStrings__AuthContext` у API и `CONNECTION_SLAG`, `CONNECTION_AUTH` у migrator. При запуске в Development используются строки из `Soft/AglomSlagServer/appsettings.Development.json`.

## Формат запроса и обработка ответа

```http
POST http://localhost:5000/graphql
Content-Type: application/json; charset=utf-8

{"operationName":"Hello","query":"query Hello { hello }","variables":{}}
```

Успешный ответ: `{"data":{"hello":"Aglom GraphQL API"}}`. Всегда проверять `errors`, даже при HTTP 200: ошибка resolver/БД/расчёта может прийти в GraphQL envelope вместе с `data` или `data:null`. Ошибки синтаксиса и валидации могут иметь другой HTTP-статус. `errors[].path` указывает поле, `extensions.code` — публичную причину. В Production исключения, SQL и stack trace скрыты; Development добавляет только безопасный тип исключения и SQLSTATE. Справочник кодов и ограничения DTO приведены в [contracts.md](contracts.md).

Идентификаторы передаются как `Int`, даты — ISO 8601 UTC, например `2026-01-01T00:00:00Z`. Дробные числа в JSON пишутся с точкой. Названия полей чувствительны к регистру: `bfId`, `caO`, `siO2`, `feOinAgl`, `pmpp`; конкретные названия смотреть в SDL. Прежние input-поля обязательны (`!`); `slag.s` и `slagSContent` можно опускать благодаря явному default `0`. C# initializer сам по себе не означает GraphQL default. Пустая строка/пустой список не равны null; CRUD запрещает пустые имена, шлаковый расчёт — пустой список материалов.

## Исполняемые примеры

`operations/<Operation>.graphql` содержит один именованный документ на каждую операцию; `examples/<Operation>.variables.json` — полный объект variables. В Rider/Postman/GraphQL IDE вставить документ в Query, JSON в Variables и выбрать endpoint.

PowerShell из корня проекта:

```powershell
.\requests\Invoke-GraphQL.ps1 -Operation Hello | ConvertTo-Json -Depth 20
.\requests\Invoke-GraphQL.ps1 -Operation CalcVariantById | ConvertTo-Json -Depth 20
.\requests\Invoke-GraphQL.ps1 -Operation CalculateSlagMode | ConvertTo-Json -Depth 20
# Для IDE-профиля:
.\requests\Invoke-GraphQL.ps1 -Operation Hello -Endpoint http://localhost:5093/graphql
# Для собственного JSON:
.\requests\Invoke-GraphQL.ps1 -Operation UpdateBlastFurnace -VariablesPath C:\Temp\update-furnace.json
```

Скрипт PowerShell 5.1+ посылает **только выбранную** операцию, берёт пример variables по умолчанию и завершает выполнение с ошибкой при GraphQL `errors`. Он возвращает объект ответа, поэтому его можно использовать для получения созданного ID. Если локальная execution policy запрещает `.ps1`, можно отправить тот же JSON через клиент API; менять политику машины для чтения документации не требуется.

Примеры изменяющих операций используют ID **0 как заполнитель**. После создания печи/компонента/варианта подставить фактически возвращённые `bfId`, `componentId`, `variantId` в свой JSON. ID 0 не означает «создать», а отрицательные seed-ID (`-1` и т.д.) — реальные существующие записи. Не направлять delete/update на seed, если нужно сохранить демонстрационные данные. `AddCalcVariant` также требует заменить ID 0 внутри `input.bfId` и каждого `components[].componentId`.

Для прямого вызова без вспомогательного скрипта:

```powershell
$payload = @{
  operationName = 'CalcVariantById'
  query = Get-Content .\requests\operations\CalcVariantById.graphql -Raw -Encoding UTF8
  variables = @{ variantId = -1 }
} | ConvertTo-Json -Depth 30
Invoke-RestMethod -Uri http://localhost:5000/graphql -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($payload))
```

## Полный каталог query

Ссылки ведут на исполняемые документы; одноимённые variables находятся в `examples`.

| Операция / поле GraphQL | Аргументы → результат | Поведение |
|---|---|---|
| [Hello](operations/Hello.graphql) / `hello` | нет → `String!` | `Aglom GraphQL API`; без БД |
| [BlastFurnaces](operations/BlastFurnaces.graphql) / `blastFurnaces` | нет → `[BlastFurnace!]!` | все печи; варианты не загружаются |
| [ComponentGuides](operations/ComponentGuides.graphql) / `componentGuides` | нет → `[ComponentGuide!]!` | весь справочник без фильтра по пользователю |
| [CalcVariants](operations/CalcVariants.graphql) / `calcVariants` | нет → `[CalcVariant!]!` | все варианты с печью и компонентами |
| [CalcVariantById](operations/CalcVariantById.graphql) / `calcVariantById` | `variantId: Int!` → `CalcVariant` | один вариант с печью и компонентами; нет ID → `null` |

У списков нет пагинации, аргументов фильтрации/сортировки и гарантированного порядка. В запросах выбирать только нужные поля. Навигации публичных EF-сущностей присутствуют в SDL, но не все загружены; доступность поля в SDL не гарантирует полноту связей. Примеры намеренно выбирают загружаемые связи и скалярные значения.

## Полный каталог mutation

### Расчёты (БД не изменяют)

| Операция / поле GraphQL | Аргументы → результат | Поведение |
|---|---|---|
| [CalculateAglom](operations/CalculateAglom.graphql) / `calculateAglom` | `input: CalculateAglomInput!` → `AglomCalculationResult!` | расчёт агломерата через DLL; возвращает `id=userId`, сообщение и `components[].report*` |
| [CalculateSlagMode](operations/CalculateSlagMode.graphql) / `calculateSlagMode` | `input: InputDataModelInput!` → `OutputSlagMode!` | расчёт шлакового режима; состав/расход/кокс/чугун передаются полностью, обращения к БД нет |

`CalculateAglomInput.createPreset` обязателен, но игнорируется; текущий API не сохраняет агломерационные пресеты. Для шлака сохранить входные данные можно отдельной `addCalcVariant`, затем пересчитать на клиенте преобразованный input через `calculateSlagMode`. Сам `addCalcVariant` расчёта не запускает.

### Доменные печи

| Операция / поле GraphQL | Аргументы → результат | Поведение |
|---|---|---|
| [AddBlastFurnace](operations/AddBlastFurnace.graphql) / `addBlastFurnace` | `bfName: String!` → `BlastFurnace!` | создаёт печь, возвращает ID |
| [UpdateBlastFurnace](operations/UpdateBlastFurnace.graphql) / `updateBlastFurnace` | `bfId: Int!, bfName: String!` → `BlastFurnace` | заменяет имя; нет ID → `null` |
| [DeleteBlastFurnace](operations/DeleteBlastFurnace.graphql) / `deleteBlastFurnace` | `bfId: Int!` → `Boolean!` | удаляет свободную печь; нет ID → `false`; используемая печь → GraphQL error со списком вариантов |

### Справочник компонентов

| Операция / поле GraphQL | Аргументы → результат | Поведение |
|---|---|---|
| [AddComponentGuide](operations/AddComponentGuide.graphql) / `addComponentGuide` | `input: ComponentGuideInput!` → `ComponentGuide!` | создаёт запись справочника |
| [UpdateComponentGuide](operations/UpdateComponentGuide.graphql) / `updateComponentGuide` | `componentId: Int!, input: ComponentGuideInput!` → `ComponentGuide` | полностью заменяет поля; нет ID → `null` |
| [DeleteComponentGuide](operations/DeleteComponentGuide.graphql) / `deleteComponentGuide` | `componentId: Int!` → `Boolean!` | нет ID → `false`; используемый в вариантах компонент → GraphQL error |

Обновление справочника не меняет копии состава в уже сохранённых вариантах. `userId` не проверяется по базе Auth.

### Сохранённые варианты шлакового режима

| Операция / поле GraphQL | Аргументы → результат | Поведение |
|---|---|---|
| [AddCalcVariant](operations/AddCalcVariant.graphql) / `addCalcVariant` | `input: CalcVariantInput!, components: [ComponentVariantInput!]!` → `CalcVariant!` | проверяет печь, сохраняет вариант со всем составом |
| [UpdateCalcVariant](operations/UpdateCalcVariant.graphql) / `updateCalcVariant` | `variantId: Int!, input: CalcVariantInput!, components: [ComponentVariantInput!]!` → `CalcVariant` | нет ID → `null`; заменяет все поля и весь список компонентов |
| [DeleteCalcVariant](operations/DeleteCalcVariant.graphql) / `deleteCalcVariant` | `variantId: Int!` → `Boolean!` | нет ID → `false`; компоненты удаляются каскадно; внешние дочерние варианты могут блокировать удаление |

Для add/update нужен существующий `bfId` и существующий `componentId` каждого компонента. `componentId` уникален внутри варианта (PK — пара ID); повторение даёт ошибку. `components: []` допустим для хранения и очищает состав при update, но такой список непригоден для расчёта. Состав передаётся снимком, сервер не подставляет его из справочника. `ParentId` через текущие input не задаётся. `saveTime` сейчас не хранится в БД и меняется при чтении; не использовать его для аудита/проверки конкурентности.

Ответ `updateCalcVariant` возвращает актуальный `blastFurnace`, включая смену печи. Пример обновления выбирает эту связь; прежний дефект `HC0018` устранён и покрыт регрессией. Ожидаемые ошибки валидации и ограничений оставляют запись и состав неизменными. При неожиданном сетевом сбое после отправки mutation перед повтором всё равно следует проверить сохранённое состояние.

## Поля input и результаты расчёта

Подробный словарь, правила преобразования сохранённого варианта в расчётный input и единицы: [contracts.md](contracts.md). Полный формальный контракт всех выходных полей — в [schema.graphql](schema.graphql). Примеры расчётов предназначены для проверки интеграции, не являются согласованным металлургическим эталоном.

## Обновление снимка схемы

После изменения C# собрать и запустить текущий API, затем из корня репозитория:

```powershell
$schemaResponse = Invoke-WebRequest -Uri 'http://localhost:5000/graphql?sdl'
$schemaText = if ($schemaResponse.Content -is [byte[]]) {
  [Text.Encoding]::UTF8.GetString($schemaResponse.Content)
} else { [string]$schemaResponse.Content }
[IO.File]::WriteAllText((Join-Path (Get-Location) 'requests/schema.graphql'), $schemaText, [Text.UTF8Encoding]::new($false))
```

Затем обновить `.graphql`, JSON-примеры, `contracts.md` и каталог операций; выполнить примеры против этой же сборки. SDL — снимок автоматически сгенерированной схемы, сервер её из файла не читает.
