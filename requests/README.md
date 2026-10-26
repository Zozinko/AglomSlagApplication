# Запросы GraphQL

Здесь находятся готовые запросы для входа, чтения, сохранения и расчётов. API содержит 5 запросов чтения (`query`) и 14 операций изменения или расчёта (`mutation`).

Если приложение ещё не запущено, выполните [четыре шага настройки](../docs/configuration.md). Все команды ниже выполняются из корня репозитория. По умолчанию используется Docker: `http://localhost:5000/graphql`. Для API из IDE добавьте `-Endpoint http://localhost:5093/graphql`.

Справочные материалы: [поля и единицы](contracts.md), [схема API](schema.graphql), [вход и права](../docs/authentication.md), [ручная проверка](../docs/manual-auth-check.md).

## Подключение

Все бизнес-операции, включая Hello и расчёты, требуют `Authorization: Bearer <accessToken>`. Исключение — Login с email и паролем либо токеном обновления. Вход в соседнем поле документа не разрешает другие методы. Проверяйте `errors` даже при HTTP 200. Если ответ потерян из-за сетевой ошибки, результат сохранения нужно проверить отдельным запросом.

Исполняемый документ находится в `operations/<Operation>.graphql`, JSON с переменными — в `examples/<Operation>.variables.json`. Для Login/ChangePassword примеры содержат только заполнители, настоящие пароли не сохранять в Git. Даты UTC ISO8601, дробные числа с точкой, названия полей чувствительны к регистру. ID имеют тип `Int`. Значение 0 в примере — заполнитель, отрицательные ID демонстрационных данных — настоящие записи. В изменяющих операциях использовать собственные ID.

`Invoke-GraphQL.ps1` принимает `-Variables` (объект в памяти) или `-VariablesPath`, `-AccessToken`, `-Headers`, `-Endpoint`; отправляет одну операцию и прекращает выполнение при GraphQL errors. Совместим с PowerShell 5.1+. HTTP работает также в Production. В браузере доступен редактор GraphQL; схема (`SDL` и introspection) открыта без входа. GET поддерживается, но бизнес-поля всегда требуют JWT.

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

Пустой состав подходит для сохранения, расчёт требует полного входа. Чтобы рассчитать сохранённый вариант, преобразовать его поля по [таблице соответствий](contracts.md#преобразование-сохранённого-варианта-в-расчёт); сервер автоматически этого не делает. При повторном запуске Compose аккаунты, пароли и сохранённые данные остаются.

## Сессии

| Операция | Аргументы / результат | Правило |
|---|---|---|
| [Login](operations/Login.graphql) | `input: LoginInput!` → `LoginPayload!` | либо email+password, либо только refreshToken; accessToken/accessExpiresAt/refreshToken/refreshExpiresAt/user |
| [Logout](operations/Logout.graphql) | нет → Boolean! | защищённая операция, отзывает цепочку обновлений текущего токена доступа по sub/sid |
| [ChangePassword](operations/ChangePassword.graphql) | currentPassword, newPassword → Boolean! | защищена, проверяет прежний пароль, отзывает все токены обновления пользователя |

Пример обновления — [Login.refresh.variables.json](examples/Login.refresh.variables.json); это переменные операции **Login**. При успехе замените оба токена в памяти. Повторное использование прежнего токена обновления отзывает всю цепочку замен. При потере ответа войдите с паролем. После Logout, блокировки или смены пароля токен доступа остаётся действующим до конца своего срока (по умолчанию 300 секунд и допуск часов 5 секунд). Неизвестный email, неверный пароль или токен обновления дают `INVALID_CREDENTIALS`; превышение числа попыток — `LOGIN_RATE_LIMITED`. Подробнее — [вход и права](../docs/authentication.md).

## Чтение данных

| Операция | Аргументы → результат | Поведение |
|---|---|---|
| [Hello](operations/Hello.graphql) | нет → String! | доступность защищённого GraphQL, без проверки текущей доступности БД |
| [BlastFurnaces](operations/BlastFurnaces.graphql) | нет → [BlastFurnace!]! | общие печи; вложенные варианты только разрешённых владельцев |
| [ComponentGuides](operations/ComponentGuides.graphql) | нет → [ComponentGuide!]! | собственный справочник; администратор видит всех; вложенные варианты фильтруются |
| [CalcVariants](operations/CalcVariants.graphql) | нет → [CalcVariant!]! | свои варианты; администратор — всех владельцев |
| [CalcVariantById](operations/CalcVariantById.graphql) | variantId:Int! → CalcVariant | отсутствующий/чужой ID обычному пользователю возвращает null |

Пагинации, сортировки и пользовательских фильтров пока нет (API-01). Объекты ответа отделены от объектов БД, глубина вложенных связей ограничена. Чужие parent/parentId/children скрываются. В собственном снимке состава `componentGuide` может быть null при чужом справочнике, сам снимок сохраняется.

## Расчёты

| Операция | Вход → результат | Поведение |
|---|---|---|
| [CalculateAglom](operations/CalculateAglom.graphql) | CalculateAglomInput! → AglomCalculationResult! | расчёт через DLL; id — эхо input.userId, не ID записи и не право доступа; createPreset пока игнорируется |
| [CalculateSlagMode](operations/CalculateSlagMode.graphql) | InputDataModelInput! → OutputSlagMode! | полные входные данные расчёта, без чтения/записи вариантов |

Сохранение набора настроек агломерата пока не реализовано. Вариант шлакового расчёта можно сохранить через AddCalcVariant, загрузить и преобразовать на клиенте по [таблице соответствий](contracts.md#преобразование-сохранённого-варианта-в-расчёт). Проверки интеграции DLL не являются научными эталонами.

## Создание, изменение и удаление печей

Только администратор; обычный пользователь получает `FORBIDDEN`.

| Операция | Аргументы | Поведение |
|---|---|---|
| [AddBlastFurnace](operations/AddBlastFurnace.graphql) | bfName | создаёт печь, возвращает BlastFurnace! |
| [UpdateBlastFurnace](operations/UpdateBlastFurnace.graphql) | bfId, bfName | новое имя / null при отсутствии |
| [DeleteBlastFurnace](operations/DeleteBlastFurnace.graphql) | bfId | Boolean!; используемая печь → REFERENCE_CONFLICT |

## Работа со справочником

| Операция | Аргументы | Поведение |
|---|---|---|
| [AddComponentGuide](operations/AddComponentGuide.graphql) | input:ComponentGuideInput! | владелец — текущий пользователь; input.userId игнорируется |
| [UpdateComponentGuide](operations/UpdateComponentGuide.graphql) | componentId, input | обновляет доступную запись без смены владельца; чужая/отсутствующая → null |
| [DeleteComponentGuide](operations/DeleteComponentGuide.graphql) | componentId | чужая/отсутствующая → false; используемая → REFERENCE_CONFLICT |

Администратор может менять чужой справочник, сохраняя владельца. Правка справочника не изменяет снимки состава сохранённых вариантов.

## Работа с вариантами

| Операция | Аргументы | Поведение |
|---|---|---|
| [AddCalcVariant](operations/AddCalcVariant.graphql) | input:CalcVariantInput!, components:[ComponentVariantInput!]! | сервер назначает текущего владельца; input.userId игнорируется |
| [UpdateCalcVariant](operations/UpdateCalcVariant.graphql) | variantId, input, components | полностью заменяет поля доступного обычного варианта, владелец/время создания сохраняются; чужой/отсутствующий → null |
| [DeleteCalcVariant](operations/DeleteCalcVariant.graphql) | variantId | чужой/отсутствующий → false, состав удаляется каскадно, родитель с детьми → REFERENCE_CONFLICT |

Ссылки на componentId должны быть доступны пользователю, иначе `FORBIDDEN`; отсутствующая печь → `REFERENCE_NOT_FOUND`. Компоненты уникальны внутри варианта. `components:[]` допустим для хранения/очистки, но непригоден для расчёта. Изменение возвращает актуальную печь, включая смену bfId. Время `saveTime` сохраняется в UTC и стабильно после update/restart; старые и демонстрационные записи имеют null.

`isImport=true` в обычном Add/Update запрещён (`VALIDATION_FAILED`). Сохранённый импорт обеим ролям запрещено обновлять (`IMPORTED_DATA_READ_ONLY`) или удалять (`IMPORTED_DATA_DELETE_FORBIDDEN`); снятие флага и замена состава также запрещены. Защита при прямой работе с объектами БД (IMP-01), создание дочернего варианта (VAR-01) и импорт файла (IMP-03) ещё не реализованы; `parentId` нельзя задать нынешними операциями.

## Обновление SDL

SDL доступна по `http://localhost:5000/graphql?sdl`, IDE — по `/graphql`; introspection и GET доступны также в Production. Для бизнес-полей нужен JWT. Для обновления и проверки снимка публичного SDL:

```powershell
.\tests\Run-ApiTests.ps1 -UpdateSchema
```

Тестовый скрипт загружает `/graphql?sdl` временного API, сохраняет схему с `-UpdateSchema` и сравнивает её со снимком в тестах. Для обычной работы это не требуется. Сервер не читает `requests/schema.graphql` как источник схемы. Необязательная операторская команда `--export-schema` выводит внутреннюю схему; директивы проверки доступа не входят в публичный SDL, поэтому эти экспорты могут отличаться.
