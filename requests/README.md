# Запросы GraphQL

Текущий контракт: 5 query и 14 mutation. Endpoint Docker — `http://localhost:5000/graphql`; Development IDE — `http://localhost:5093/graphql`. Инструкции: [настройка](../docs/configuration.md), [вход и права](../docs/authentication.md), [ручная приёмка четырёх задач](../docs/manual-auth-check.md), [словарь полей](contracts.md), [SDL](schema.graphql).

## Быстрый старт: четыре шага

1. Клонировать проект с GitHub и перейти в корень клона.
2. Скопировать `Soft/.env.example` в `Soft/.env`, заполнить DB-доступы, `JWT_SIGNING_KEY` и email/пароли обеих demo-ролей.
3. Выполнить `docker compose --env-file Soft/.env -f Soft/Docker-compose.yml up -d --build`: БД, миграции и demo-аккаунты создаются автоматически.
4. Выполнить Login ниже и передавать его access в запросах. В браузере доступна IDE `http://localhost:5000/graphql`.

## Подключение

Все бизнес-операции, включая Hello и расчёты, требуют `Authorization: Bearer <accessToken>`. Единственное исключение — Login с credentials или refresh. Ни Login в соседнем поле документа, ни aliases/fragments не открывают другие методы. Проверять `errors` даже при HTTP200; ошибка ответа сама по себе не доказывает отсутствие записи при неожиданном сетевом отказе.

Исполняемый документ находится в `operations/<Operation>.graphql`, JSON variables — в `examples/<Operation>.variables.json`. Для Login/ChangePassword примеры содержат только заполнители, рабочие credentials не сохранять в Git. Даты UTC ISO8601, дробные числа с точкой, названия полей чувствительны к регистру. ID имеют тип `Int`. Значение0 в примере — заполнитель, отрицательные seed-ID — настоящие записи. В изменяющих операциях использовать собственные ID.

`Invoke-GraphQL.ps1` принимает `-Variables` (объект в памяти) или `-VariablesPath`, `-AccessToken`, `-Headers`, `-Endpoint`; отправляет одну операцию и прекращает выполнение при GraphQL errors. Совместим с PowerShell5.1+. HTTP используется также в Production. IDE, GET, SDL и introspection доступны; бизнес-поля проверяют JWT при любом способе отправки.

## Две роли, сохранение и расчёт

Для обычного пользователя ввести `DEMO_USER_EMAIL/PASSWORD` из `Soft/.env`. Его ID — `-1`; администратор с `DEMO_ADMIN_EMAIL/PASSWORD` имеет ID `-2`. Входы сохраняются в отдельных переменных:

```powershell
$credential = Get-Credential -Message 'Обычный пользователь из Soft/.env'
$user = (.\requests\Invoke-GraphQL.ps1 -Operation Login -Variables @{
    input = @{ email = $credential.UserName; password = $credential.GetNetworkCredential().Password }
}).data.login
$credential = Get-Credential -Message 'Администратор из Soft/.env'
$admin = (.\requests\Invoke-GraphQL.ps1 -Operation Login -Variables @{
    input = @{ email = $credential.UserName; password = $credential.GetNetworkCredential().Password }
}).data.login

# Пользователь читает свои варианты и рассчитывает шлак по готовому JSON-примеру.
.\requests\Invoke-GraphQL.ps1 -Operation CalcVariants -AccessToken $user.accessToken
.\requests\Invoke-GraphQL.ps1 -Operation CalculateSlagMode -AccessToken $user.accessToken
# Администратор читает варианты всех владельцев.
.\requests\Invoke-GraphQL.ps1 -Operation CalcVariants -AccessToken $admin.accessToken

# Пример сохранения своего варианта без состава, чтения и удаления.
$inputData = Get-Content requests/examples/AddCalcVariant.variables.json -Raw | ConvertFrom-Json
$furnaces = (.\requests\Invoke-GraphQL.ps1 -Operation BlastFurnaces -AccessToken $user.accessToken).data.blastFurnaces
$inputData.input.bfId = $furnaces[0].bfId
$inputData.input.variantName = 'manual-example'
$inputData.components = @()
$saved = (.\requests\Invoke-GraphQL.ps1 -Operation AddCalcVariant -Variables $inputData -AccessToken $user.accessToken).data.addCalcVariant
.\requests\Invoke-GraphQL.ps1 -Operation CalcVariantById -Variables @{ variantId = $saved.variantId } -AccessToken $user.accessToken
.\requests\Invoke-GraphQL.ps1 -Operation DeleteCalcVariant -Variables @{ variantId = $saved.variantId } -AccessToken $user.accessToken

# Печи меняет администратор. Удаляем только что созданную свободную печь.
$furnace = (.\requests\Invoke-GraphQL.ps1 -Operation AddBlastFurnace -Variables @{ bfName = 'manual-example' } -AccessToken $admin.accessToken).data.addBlastFurnace
.\requests\Invoke-GraphQL.ps1 -Operation DeleteBlastFurnace -Variables @{ bfId = $furnace.bfId } -AccessToken $admin.accessToken

# Обновить пару токенов и завершить обе сессии.
$user = (.\requests\Invoke-GraphQL.ps1 -Operation Login -Variables @{
    input = @{ refreshToken = $user.refreshToken }
}).data.login
.\requests\Invoke-GraphQL.ps1 -Operation Logout -AccessToken $user.accessToken
.\requests\Invoke-GraphQL.ps1 -Operation Logout -AccessToken $admin.accessToken
```

Пустой состав подходит для сохранения, расчёт требует полного входа. Чтобы рассчитать сохранённый вариант, преобразовать его поля по [mapping](contracts.md); сервер автоматически этого не делает. При повторном запуске Compose аккаунты, пароли и сохранённые данные остаются.

## Сессии

| Операция | Аргументы / результат | Правило |
|---|---|---|
| [Login](operations/Login.graphql) | `input: LoginInput!` → `LoginPayload!` | либо email+password, либо только refreshToken; accessToken/accessExpiresAt/refreshToken/refreshExpiresAt/user |
| [Logout](operations/Logout.graphql) | нет → Boolean! | защищённая операция, отзывает семейство текущего access по sub/sid |
| [ChangePassword](operations/ChangePassword.graphql) | currentPassword, newPassword → Boolean! | защищена, проверяет прежний пароль, отзывает все refresh пользователя |

Refresh JSON-пример — [Login.refresh.variables.json](examples/Login.refresh.variables.json); передавать его как variables операции **Login**. На успехе заменить оба токена в памяти. Погашенный refresh нельзя повторять: replay отзывает всё семейство. При потерянном ответе войти паролем. JWT после Logout/блокировки/смены пароля действует до exp; новые права при входе/refresh берутся из Auth-БД. Для неизвестного email, неверного пароля/refresh один код `INVALID_CREDENTIALS`; ограничение попыток — `LOGIN_RATE_LIMITED`. Нет открытого Register и отдельного Refresh endpoint.

## Query

| Операция | Аргументы → результат | Поведение |
|---|---|---|
| [Hello](operations/Hello.graphql) | нет → String! | доступность защищённого GraphQL, без проверки текущей доступности БД |
| [BlastFurnaces](operations/BlastFurnaces.graphql) | нет → [BlastFurnace!]! | общие печи; вложенные варианты только разрешённых владельцев |
| [ComponentGuides](operations/ComponentGuides.graphql) | нет → [ComponentGuide!]! | собственный справочник; администратор видит всех; вложенные варианты фильтруются |
| [CalcVariants](operations/CalcVariants.graphql) | нет → [CalcVariant!]! | свои варианты; администратор — всех владельцев |
| [CalcVariantById](operations/CalcVariantById.graphql) | variantId:Int! → CalcVariant | отсутствующий/чужой ID обычному пользователю возвращает null |

Пагинации, сортировки и пользовательских фильтров пока нет (API-01). Публичные DTO отделены от EF, связи ограничены по глубине; схема не зависит от случайной EF-навигации. Чужие parent/parentId/children скрываются. В собственном снимке состава `componentGuide` может быть null при чужом справочнике, сам снимок сохраняется.

## Mutation расчётов

| Операция | Вход → результат | Поведение |
|---|---|---|
| [CalculateAglom](operations/CalculateAglom.graphql) | CalculateAglomInput! → AglomCalculationResult! | расчёт через DLL; id — эхо input.userId, не PK и не право доступа; createPreset пока игнорируется |
| [CalculateSlagMode](operations/CalculateSlagMode.graphql) | InputDataModelInput! → OutputSlagMode! | полный расчётный input, без чтения/записи вариантов |

CreatePreset агломерата не добавлен. Шлак можно сохранить через AddCalcVariant, загрузить и преобразовать к расчётному input на клиенте; [mapping](contracts.md). Проверки интеграции DLL не являются научными эталонами.

## CRUD печей

Только администратор; обычный пользователь получает `FORBIDDEN`.

| Операция | Аргументы | Поведение |
|---|---|---|
| [AddBlastFurnace](operations/AddBlastFurnace.graphql) | bfName | создаёт печь, возвращает BlastFurnace! |
| [UpdateBlastFurnace](operations/UpdateBlastFurnace.graphql) | bfId, bfName | новое имя / null при отсутствии |
| [DeleteBlastFurnace](operations/DeleteBlastFurnace.graphql) | bfId | Boolean!; используемая печь → REFERENCE_CONFLICT |

## CRUD справочника

| Операция | Аргументы | Поведение |
|---|---|---|
| [AddComponentGuide](operations/AddComponentGuide.graphql) | input:ComponentGuideInput! | владелец — текущий пользователь; input.userId игнорируется |
| [UpdateComponentGuide](operations/UpdateComponentGuide.graphql) | componentId, input | обновляет доступную запись без смены владельца; чужая/отсутствующая → null |
| [DeleteComponentGuide](operations/DeleteComponentGuide.graphql) | componentId | чужая/отсутствующая → false; используемая → REFERENCE_CONFLICT |

Администратор может менять чужой справочник, сохраняя владельца. Правка справочника не изменяет снимки состава сохранённых вариантов.

## CRUD вариантов

| Операция | Аргументы | Поведение |
|---|---|---|
| [AddCalcVariant](operations/AddCalcVariant.graphql) | input:CalcVariantInput!, components:[ComponentVariantInput!]! | сервер назначает текущего владельца; input.userId игнорируется |
| [UpdateCalcVariant](operations/UpdateCalcVariant.graphql) | variantId, input, components | полный update доступного обычного варианта, владелец/время создания сохраняются; чужой/отсутствующий → null |
| [DeleteCalcVariant](operations/DeleteCalcVariant.graphql) | variantId | чужой/отсутствующий → false, состав удаляется каскадно, родитель с детьми → REFERENCE_CONFLICT |

Ссылки на componentId должны быть доступны пользователю, иначе `FORBIDDEN`; отсутствующая печь → `REFERENCE_NOT_FOUND`. Компоненты уникальны внутри варианта. `components:[]` допустим для хранения/очистки, но непригоден для расчёта. Update возвращает актуальную печь, включая смену bfId. Время `saveTime` сохраняется в UTC и стабильно после update/restart; прежняя история/seed имеют null.

`isImport=true` в обычном Add/Update запрещён (`VALIDATION_FAILED`). Сохранённый импорт обеим ролям запрещено обновлять (`IMPORTED_DATA_READ_ONLY`) или удалять (`IMPORTED_DATA_DELETE_FORBIDDEN`); снятие флага и замена состава также запрещены. Полная защита ORM по IMP-01 ещё отдельная задача. Команда создания child по VAR-01 и multipart-импорт IMP-03 пока не реализованы; parentId нельзя задавать текущим CRUD.

## Обновление SDL

SDL доступна по `http://localhost:5000/graphql?sdl`, IDE — по `/graphql`; introspection и GET доступны также в Production. Для бизнес-полей нужен JWT. Для обновления и проверки снимка публичного SDL:

```powershell
.\tests\Run-ApiTests.ps1 -UpdateSchema
```

Runner загружает `/graphql?sdl` из собранного API на изолированном HTTP/PG-стенде, сохраняет его с `-UpdateSchema` и сравнивает публичный SDL со снимком в тестах. Сервер не читает `requests/schema.graphql` как источник схемы. Необязательная операторская команда `--export-schema` выводит внутреннюю схему; runtime auth-директивы опускаются в публичном SDL, поэтому эти экспорты могут отличаться.
