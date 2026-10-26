# Ручная приёмка SEC-02, SEC-03, OWN-01, SEC-04

Все команды из корня проекта. Для Docker выполнить [обычный запуск](configuration.md): миграции и demo-аккаунты создаются автоматически. Рабочие email/пароли находятся только в локальном `Soft/.env`; не копировать их в отчёты. API Docker: `http://localhost:5000/graphql`, IDE: `http://localhost:5093/graphql`. HTTP доступен также в Production.

## Вход и защита SEC-02

Ниже `$endpoint` — HTTP Docker; для IDE поменять только это значение. `Get-Credential` запрашивает email/пароль без записи в командную историю. Присваивания сохраняют токены в памяти и не выводят их:

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

Ожидание: IDs `-1`/`-2`, `admin=false/true`, Hello успешен. В output пользователя нет пароля/хеша. Повторить Login с неправильным email и неправильным паролем: одинаковые `INVALID_CREDENTIALS`. Прежний `test@test.test`/`Password` не работает.

Вызвать Hello, CalcVariants, CalculateSlagMode и AddBlastFurnace без `-AccessToken`: ошибка `AUTH_NOT_AUTHENTICATED`, записи не создаются. Повторить с испорченным access. В API-клиенте отправить смешанный документ `mutation { login(input:{...}) { accessToken } addBlastFurnace(bfName:"must-not-save") { bfId } }` без заголовка: защищённая часть запрещена, печь отсутствует при последующем чтении администратором. Вход в соседнем поле не авторизует другие поля этого документа. Проверить `query { ...F } fragment F on Query { a:hello }` без JWT — тоже отказ.

Открыть `/graphql` в браузере: IDE доступна без входа. `/graphql?sdl` и introspection возвращают схему также в Production. GET-запрос бизнес-поля `hello` без JWT возвращает `AUTH_NOT_AUTHENTICATED`; с JWT выполняется. Доступность схемы и IDE не открывает бизнес-данные. Лимит по умолчанию20 в минуту: после превышения Login/refresh/ChangePassword возвращают `LOGIN_RATE_LIMITED`, через новое окно вход снова возможен. Не выполнять эту часть одновременно с остальной проверкой входов с того же IP.

Для настоящего истечения без ручного изготовления JWT временно задать `JWT_ACCESS_SECONDS='5'`, `JWT_CLOCK_SKEW_SECONDS='0'`, пересоздать API и войти заново. Через6 секунд Hello со старым access отклоняется; перейти к refresh ниже. После проверки вернуть значения и пересоздать API. Изменение issuer/audience/signing key на сервере и пересоздание API также делает прежний access недействительным; восстановить настройки и войти снова.

## Refresh и Logout SEC-03

```powershell
$oldRefresh = $user.refreshToken
$user = (.\requests\Invoke-GraphQL.ps1 -Operation Login -Endpoint $endpoint -Variables @{
    input = @{ refreshToken = $oldRefresh }
}).data.login
.\requests\Invoke-GraphQL.ps1 -Operation Hello -Endpoint $endpoint -AccessToken $user.accessToken
```

Ожидание: новая пара, новый refresh, прежняя личность. Обновление работает также без действующего access и с истёкшим access в заголовке. Передача refresh вместе с email или password запрещена и не расходует корректный refresh. Неверный refresh возвращает `INVALID_CREDENTIALS`.

Повторить Login с `$oldRefresh`: отказ. Затем попробовать **новый** `$user.refreshToken`: тоже отказ, семейство отозвано. Получить новую сессию паролем. В двух API-клиентах почти одновременно отправить один refresh: выдаётся только одна новая пара; replay отзывает её refresh. При потере ответа не повторять запрос со старым токеном, войти паролем.

Получить две независимые сессии паролем. Выполнить `Logout` с access первой; её refresh больше не работает, refresh второй работает. Logout без access запрещён; повтор Logout с ещё действующим access безопасен. Ранее выданный JWT продолжает работать до `exp`: это ожидаемый контракт.

Перезапустить только API (`docker compose --env-file Soft/.env -f Soft/Docker-compose.yml restart graphql-api`): неиспользованный refresh остаётся рабочим, погашенный — запрещённым. Для проверки срока refresh на отдельном тестовом стенде установить конкретной сессии `ExpiresAt` в прошлое средствами SQL; API отклоняет её и не создаёт замену. Не менять рабочие сессии других пользователей.

## Владельцы OWN-01

После новых входов вызвать `CalcVariants` и `ComponentGuides` под обоими токенами. Пользователь видит только свои записи, администратор — обоих владельцев. В `BlastFurnaces` общие печи доступны обоим, их `calcVariants` также фильтруются. Печь создаёт/меняет/удаляет только администратор.

Для обратимой проверки создать по варианту от каждой роли:

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

Последний запрос возвращает `null`; чужой update — `null`, delete — `false`. Администратор может изменить обычный вариант пользователя, и владелец остаётся `-1`. В произвольном GraphQL selection добавить `userId` и проверить его. Передать `input.userId=-2` при создании/обновлении от пользователя: владелец всё равно `-1`. Передать `admin` во вход Login — ошибка схемы. Аналогично проверить присвоение владельца и доступ к справочнику; чужой `componentId` при сохранении состава даёт `FORBIDDEN`.

Для проверки вложенных связей на отдельном тестовом стенде создать через API варианты обеих ролей, доверенным SQL связать `ParentId` между владельцами. Пользователь не получает чужие `parent`, `parentId`, `children`; администратор видит связи. Если администратор сохранил в пользовательском варианте снимок с чужим справочником, сам снимок видим владельцу, а `componentGuide=null`.

В отдельной тестовой БД пометить подготовленный вариант пользователя `IsImport=true` через SQL (импорт ещё не реализован). Обе роли получают `IMPORTED_DATA_READ_ONLY` при любом update/замене состава/снятии флага и `IMPORTED_DATA_DELETE_FORBIDDEN` при delete. Сравнить скаляры, владельца и строки состава в SQL до/после: изменений нет. Обычный Add/Update с `isImport=true` отклоняется. Полный обход через прямые ORM-команды и создание child относятся к IMP-01/VAR-01 и здесь ещё не реализованы.

Удалить созданные ручные варианты через API. Для SQL-фикстуры использовать отдельную одноразовую БД; не снимать флаг импорта на реальных данных ради очистки. Повторный запуск с автоматическим demo-bootstrap не меняет пароли и не дублирует пользователей/seed. В Auth существуют `-1/-2`; все владельцы seed в SlagMode ссылаются на эти ID.

## Пароли SEC-04

Использовать отдельного одноразового пользователя через `--bootstrap-user`, чтобы не менять демонстрационные доступы. Двум аккаунтам задать одинаковый пароль: в SQL `PasswordHash` различается, ни одно значение не равно паролю. Правильный пароль даёт Login, неправильный — `INVALID_CREDENTIALS`. Запрос поля `passwordHash` в GraphQL отклоняется схемой; в JWT и логах пароля/хеша нет.

Получить две сессии этого пользователя, вызвать `ChangePassword` с неверным прежним паролем — отказ и прежний пароль остаётся рабочим. Пустой/короткий новый пароль даёт `VALIDATION_FAILED` без изменения записи. С правильным прежним и новым допустимым паролем операция успешна; старый пароль и все прежние refresh запрещены, новый пароль работает. Access до `exp` остаётся действующим.

Операторские `--reset-password` и `--disable-user` из [authentication.md](authentication.md) также отзывают все refresh; блокировка запрещает Login, явный reset активирует аккаунт с новым паролем. После обновления прежней БД пароль `Password` не работает до явного reset/bootstrap. Проверку автоматического rehash независимого старого IdentityV3 выполняет автоматический PostgreSQL-тест; не понижать стоимость рабочего хеша вручную.
