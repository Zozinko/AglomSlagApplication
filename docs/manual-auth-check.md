# Ручная проверка входа и прав

Выполните [обычный запуск](configuration.md). Для проверки нужны демонстрационный пользователь и администратор из `Soft/.env`. Команды выполняются из корня проекта. На Docker адрес — `http://localhost:5000/graphql`; при запуске из IDE замените его на `http://localhost:5093/graphql`.

## 1. Войти и выполнить запрос

`Get-Credential` запрашивает email и пароль. Токены остаются в переменных; в выводе ниже показываются только сведения о пользователях.

```powershell
$endpoint = 'http://localhost:5000/graphql'
$credential = Get-Credential -Message 'Обычный пользователь из Soft/.env'
$user = (.\requests\Invoke-GraphQL.ps1 -Operation Login -Endpoint $endpoint -Variables @{
    input = @{ email = $credential.UserName; password = $credential.GetNetworkCredential().Password }
}).data.login
$credential = Get-Credential -Message 'Администратор из Soft/.env'
$admin = (.\requests\Invoke-GraphQL.ps1 -Operation Login -Endpoint $endpoint -Variables @{
    input = @{ email = $credential.UserName; password = $credential.GetNetworkCredential().Password }
}).data.login
$user.user
$admin.user
.\requests\Invoke-GraphQL.ps1 -Operation Hello -Endpoint $endpoint -AccessToken $user.accessToken
```

Ожидаются ID `-1` и `-2`, `admin=false` и `admin=true`. Запрос `Hello` выполняется. В данных пользователей нет пароля и хеша. Неправильный email или пароль возвращает `INVALID_CREDENTIALS`.

Повторите `Hello` без `-AccessToken`: ожидается `AUTH_NOT_AUTHENTICATED`. Та же проверка действует для расчётов и изменений данных. В браузере `/graphql` открывается без входа, `/graphql?sdl` возвращает схему; это не открывает бизнес-методы. Вход в одном поле документа не разрешает остальные поля.

## 2. Обновить токены и выйти

```powershell
$oldRefresh = $user.refreshToken
$user = (.\requests\Invoke-GraphQL.ps1 -Operation Login -Endpoint $endpoint -Variables @{
    input = @{ refreshToken = $oldRefresh }
}).data.login
.\requests\Invoke-GraphQL.ps1 -Operation Hello -Endpoint $endpoint -AccessToken $user.accessToken
.\requests\Invoke-GraphQL.ps1 -Operation Logout -Endpoint $endpoint -AccessToken $user.accessToken
```

Обновление возвращает новую пару токенов того же пользователя; `Hello` проходит. После `Logout` новый `$user.refreshToken` больше не принимается. Уже выданный токен доступа работает до истечения срока: по умолчанию 300 секунд и допуск часов 5 секунд.

Для отдельной проверки войдите заново, обновите токены и повторно отправьте прежний токен обновления. Ожидается отказ; последняя замена тоже перестаёт работать, поскольку отозвана вся цепочка. При потере ответа нужно войти с паролем. Второй независимый вход не затрагивается. После перезапуска API неиспользованный токен обновления остаётся рабочим.

## 3. Проверить владельцев

Снова получите токены по первому шагу. `CalcVariants` и `ComponentGuides` под пользователем возвращают только его записи, под администратором — записи всех владельцев. `BlastFurnaces` доступен обоим; менять печи может только администратор.

Создайте два временных варианта без состава:

```powershell
$base = Get-Content requests/examples/AddCalcVariant.variables.json -Raw | ConvertFrom-Json
$base.input.bfId = -1
$base.input.variantName = 'manual-user-variant'
$base.components = @()
$ordinary = (.\requests\Invoke-GraphQL.ps1 -Operation AddCalcVariant -Endpoint $endpoint -AccessToken $user.accessToken -Variables $base).data.addCalcVariant
$base.input.variantName = 'manual-admin-variant'
$administrator = (.\requests\Invoke-GraphQL.ps1 -Operation AddCalcVariant -Endpoint $endpoint -AccessToken $admin.accessToken -Variables $base).data.addCalcVariant
.\requests\Invoke-GraphQL.ps1 -Operation CalcVariantById -Endpoint $endpoint -AccessToken $user.accessToken -Variables @{variantId=$administrator.variantId}
```

Последний запрос возвращает `null`; попытка изменить чужой вариант также возвращает `null`, удалить — `false`. Если передать `input.userId=-2` от обычного пользователя, владельцем всё равно будет `-1`. Администратор может изменить пользовательский вариант, сохранив владельца. Недоступный или отсутствующий `componentId` при сохранении состава даёт `FORBIDDEN`.

Удалите созданные варианты:

```powershell
.\requests\Invoke-GraphQL.ps1 -Operation DeleteCalcVariant -Endpoint $endpoint -AccessToken $user.accessToken -Variables @{variantId=$ordinary.variantId}
.\requests\Invoke-GraphQL.ps1 -Operation DeleteCalcVariant -Endpoint $endpoint -AccessToken $admin.accessToken -Variables @{variantId=$administrator.variantId}
```

Если токен истёк во время проверки, войдите заново. Пустой состав подходит для хранения, но не для расчёта.

## 4. Проверить смену пароля

Используйте отдельного временного пользователя, созданного [операторской командой](authentication.md#обслуживание-аккаунтов). Получите две сессии, затем вызовите `ChangePassword` с неверным прежним паролем: ожидается отказ без изменения пароля. Новый пароль короче 12 символов или только из пробелов даёт `VALIDATION_FAILED`.

С правильным прежним и допустимым новым паролем операция проходит. Старый пароль и все прежние токены обновления больше не работают, новый пароль работает. Прежние токены доступа действуют до конца своего срока. Операторский сброс и блокировка также отзывают все токены обновления; сброс активирует отключённый аккаунт.

## Что проверяют автоматические тесты

Для полного набора используйте [tests/README.md](../tests/README.md). Он проверяет лимит попыток, истечение токенов, одновременное обновление, хеши паролей, перезапуск и вложенные связи. Эти сценарии не требуют изменения настроек рабочего сервера вручную.

Проверки импортных записей и связей разных владельцев используют прямой SQL только в отдельной тестовой БД. В действующем API обычные Add/Update отвергают `isImport=true`. Уже сохранённый импорт нельзя менять (`IMPORTED_DATA_READ_ONLY`) или удалять (`IMPORTED_DATA_DELETE_FORBIDDEN`) ни одной роли, включая замену состава и снятие флага. Чужие `parent`, `parentId`, `children` скрываются; состав своего варианта остаётся видимым, но ссылка на чужой справочник равна `null`.

Защита при прямой работе с объектами БД (IMP-01), импорт (IMP-03) и создание дочернего варианта (VAR-01) ещё не реализованы. Ручная проверка не подтверждает эти будущие возможности. Подробности нынешнего поведения: [вход и права](authentication.md).
