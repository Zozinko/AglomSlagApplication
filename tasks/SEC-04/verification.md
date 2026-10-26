> Актуализация запуска 26.10.2026: обычный HTTP, публичная GraphQL IDE и автоматические demo-аккаунты; итоговый прогон 92/92 + .NET CredentialChecks. См. [протокол упрощения](../SEC-02/http-simplification.md). Ниже сохранён исторический протокол первоначальной реализации.

# SEC-04 — проверка

Дата: 26.10.2026. Реализация и тесты независимо прочитаны/проверены тимлидом перед запуском. Субагенты тесты не запускали; финальный общий прогон выполнен один раз, без ошибок и повторов.

Identity V3 PBKDF2-HMAC-SHA512 210000 со случайной солью, bounded envelope и dummy KDF, корректный/неверный пароль, отказ plaintext/V2/повреждённого хеша, rehash, создание аккаунта, ChangePassword с текущим паролем и лимитом, операторские reset/disable с отзывом refresh, очищенный/отключённый legacy seed. 4 PostgreSQL-теста + исполняемые .NET CredentialChecks; общий rate-limit проверен отдельным инфраструктурным тестом.

## Общий результат

26.10.2026 — финальный единый прогон
Команда: $env:API_TEST_DOTNET=<локальный runtime 10.0.13>; ./tests/Run-ApiTests.ps1 -UpdateSchema
Compose-проект: aglom-api-tests-505937606691
Сборка Docker Release: успешно, 0 ошибок; прежние 9 CS8618 агломерата.
Исполняемые .NET CredentialChecks: успешно (настроенный KDF, правильный/неправильный пароль, dummy cost, границы).
Node API: 59/59
Infrastructure authentication: 3/3
Infrastructure ownership: 5/5
Infrastructure passwords: 4/4
Infrastructure sessions: 6/6
Infrastructure secrets: 5/5
Infrastructure runtime: 9/9
Итого Node: 91/91, 0 failed/cancelled/skipped/todo. Exit code 0.
SDL обновлён операторским offline export и проверен тестом контракта.
Изолированные контейнеры, сеть, образы, временные сертификаты удалены runner finally.
Рабочие контейнеры/БД/volume не изменялись. Секреты не включены в этот протокол.

## Документация и границы

Настройка и миграция: [authentication.md](../../docs/authentication.md), [configuration.md](../../docs/configuration.md). Все четыре сценария ручной приёмки: [manual-auth-check.md](../../docs/manual-auth-check.md). [Операции и примеры](../../requests/README.md), [SDL](../../requests/schema.graphql). Журналы находятся только в logs/.

JSON refresh согласован пользователем. Access после Logout/reset/block/изменения Admin живёт до exp; refresh скользящий, история хранится без автоматической очистки. Production требует доверенный клиенту PFX/HTTPS; локальный самоподписанный сертификат подготовлен, системное доверие не менялось. Доступы сгенерированы только в Soft/.env. SecureCredentials отключает прежние аккаунты и очищает прежние хеши: нужен явный reset/bootstrap. Legacy владельцы назначены -1 по явной политике учебного стенда.

IMP-01 (все ORM-обходы/гонки импорта), IMP-03 (импорт), VAR-01 (новая команда child), WEB-01/FE-AUTH-01 (браузерное хранение/CSRF/CORS) не входят в эти четыре реализации. Текущие API update/delete импортов и существующие parent/child-ссылки защищены и проверены; будущие команды обязаны использовать ту же политику владельца. Известный необязательный вывод Npgsql о libgssapi_krb5 на Linux не помешал password-auth и успешным проверкам обеих БД; Kerberos не заявлен.
