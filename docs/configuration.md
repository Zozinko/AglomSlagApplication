# Конфигурация и запуск

Все команды выполняются из корня репозитория. Правила размещения файлов: [repository-management.md](repository-management.md).

## Один env-файл для Docker и IDE

Скопировать безопасный шаблон один раз и в редакторе заполнить непустой `DB_PASSWORD`:

```powershell
Copy-Item Soft/.env.example Soft/.env
```

На Linux: `cp Soft/.env.example Soft/.env`. Не перезаписывать уже заполненный `.env`. В нём находятся `DB_HOST=localhost`, `DB_PORT=5432`, `DB_USER` и `DB_PASSWORD`. Единственный рабочий файл находится в `Soft` и исключён из Git. Шаблон `.env.example` содержит пустой пароль и не позволяет запустить стенд без настройки.

Compose берёт из этого же файла доступы, а внутри Docker-сети явно задаёт `DB_HOST=postgres`, `DB_PORT=5432`. Базы — `SlagMode` и `Auth`. Отдельные env-файлы для Docker и Development не нужны.

## Docker

```powershell
docker compose --env-file Soft/.env -f Soft/Docker-compose.yml config --quiet
docker compose --env-file Soft/.env -f Soft/Docker-compose.yml up -d --build
docker compose --env-file Soft/.env -f Soft/Docker-compose.yml ps -a
```

Ожидаются PostgreSQL healthy, migrator Exited (0), API Up. Базы и миграции создаются автоматически; API доступен по `http://localhost:5000/graphql`. .NET и PostgreSQL на хосте не нужны. Данные хранятся в именованном volume; `down -v` удаляет их.

Для сохранения диагностики использовать `logs/`, например:

```powershell
docker compose --env-file Soft/.env -f Soft/Docker-compose.yml logs --tail 100 migrator graphql-api > logs/docker.log
```

Секреты передаются при запуске, не через build args или слои Dockerfile. Все три build context — `Soft`, поэтому единый `Soft/.env` в них не попадает; `.dockerignore` дополнительно исключает вложенные env-файлы.

## Хост / Rider

Профили `http`/`https` запускают Development-загрузчик, который читает тот же единый `Soft/.env` на один уровень выше content root `Soft/AglomSlagServer`. Для нестандартного content root можно задать `AGLOMSLAG_ENV_FILE` путём к этому файлу. Production локальный файл не читает: переменные ему передаёт Compose.

```powershell
dotnet restore Soft/AglomSlagServer/AglomSlagServer.slnx
dotnet run --project Soft/AglomSlagServer --launch-profile http
```

Для сборки нужен SDK `10.0.402`. Endpoint профиля `http`: `http://localhost:5093/graphql`; `https` дополнительно использует порт 7165.

## Значения и приоритет

Использовать по одной строке `KEY='literal value'`. Одинарные кавычки сохраняют `$`, `#`, `;`, пробелы и двойные кавычки; NpgsqlConnectionStringBuilder экранирует пароль при построении строки подключения. Для одинакового поведения Compose и Development не использовать в таком значении одинарную кавычку или перевод строки. Произвольные значения можно передать напрямую через окружение процесса.

Development-загрузчик снимает внешние одинарные/двойные кавычки, не выполняет подстановок и код. Пустые строки и строки, начинающиеся с `#`, пропускаются; inline-комментарии, escapes, `export`, дубли ключей и многострочные значения не поддерживаются. Сначала разбирается весь файл, затем значения добавляются в окружение процесса.

Уже заданное окружение процесса имеет приоритет над `.env`; параметры командной строки ASP.NET также сохраняют приоритет. Для каждой БД настройки разрешаются в порядке: `ConnectionStrings__SlagModeContext` / `ConnectionStrings__AuthContext`, затем `CONNECTION_SLAG` / `CONNECTION_AUTH`, затем обязательные `DB_HOST`, `DB_PORT`, `DB_USER`, `DB_PASSWORD`. Удалить устаревшие overrides из shell/IDE, если требуется использовать именно `.env`.

Неверные или отсутствующие настройки завершают запуск без вывода секрета. SQL-таймаут миграций — 180 секунд; таймауты обычных запросов API не переопределяются. Для ручного EF CLI с хоста задать `ASPNETCORE_ENVIRONMENT=Development` и запускать команду без `--connection`, чтобы пароль не попал в список процессов.

## Смена пароля существующей БД

`POSTGRES_PASSWORD` инициализирует только пустой кластер. Изменение `.env` не меняет пароль уже созданной роли.

1. Сохранить резервную копию и остановить потребителей: `docker compose --env-file Soft/.env -f Soft/Docker-compose.yml stop graphql-api`. Убедиться, что migrator завершился; остановить host-приложение, если оно запущено.
2. Открыть `docker compose --env-file Soft/.env -f Soft/Docker-compose.yml exec postgres psql -U имя_роли -d postgres`, заменив `имя_роли` текущим `DB_USER`.
3. Выполнить в psql `\password`, дважды ввести новый пароль в скрытых приглашениях, затем `\q`.
4. Изменить `DB_PASSWORD` в единственном `Soft/.env`, обновить внешние overrides, если они есть.
5. Выполнить `docker compose --env-file Soft/.env -f Soft/Docker-compose.yml up -d --force-recreate`. Проверить обе БД, migrator и чтение данных; перезапустить host-приложение.

При отказе исправить настройки потребителей или вернуть прежний пароль через ту же процедуру. Не удалять volume. Удаление секрета из файла не удаляет историю Git; история автоматически не переписывается.

## Проверки

`./tests/Run-ApiTests.ps1` создаёт изолированный стенд с одноразовым паролем и автоматически сохраняет `logs/last-run.log`. Для host-проверки нужен ASP.NET Core runtime .NET 10; нестандартный путь задаётся в `API_TEST_DOTNET`. Скачанные зависимости задачи находятся в `tasks/SEC-01/local/`, протокол — [tasks/SEC-01/verification.md](../tasks/SEC-01/verification.md).

JWT/bootstrap-вход ещё не реализованы и остаются отдельными задачами. Будущие ключи и начальные доступы также должны поступать через общий `.env`/окружение. Хеши пользовательских паролей и refresh-токенов остаются в Auth-БД.
