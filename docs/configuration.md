# Конфигурация и запуск

Все команды выполняются из корня репозитория. Docker с Linux containers и Compose v2 достаточно для запуска; .NET и PostgreSQL на хосте не нужны.

## Быстрый старт: четыре шага

1. Клонировать проект с GitHub и перейти в корень клона.
2. Скопировать `Soft/.env.example` в `Soft/.env` и заполнить доступы БД, ключ JWT и доступы двух демонстрационных ролей.
3. Выполнить `docker compose --env-file Soft/.env -f Soft/Docker-compose.yml up -d --build`.
4. Открыть `http://localhost:5000/graphql` или выполнить Login и запросы по [requests/README.md](../requests/README.md).

Первый запуск автоматически создаёт БД, применяет миграции и создаёт демонстрационных пользователя и администратора. Дополнительная команда bootstrap не нужна. PostgreSQL должен стать healthy, migrator завершиться с кодом 0, API — работать. Проверить состояние можно командой `docker compose --env-file Soft/.env -f Soft/Docker-compose.yml ps -a`.

## Один файл настроек

```powershell
Copy-Item Soft/.env.example Soft/.env
```

На Linux: `cp Soft/.env.example Soft/.env`. Уже заполненный файл не перезаписывать. `Soft/.env` исключён из Git и используется Docker и Development-запуском из IDE.

| Параметры | Значение |
|---|---|
| `DB_HOST`, `DB_PORT` | для IDE `localhost`, `5432`; Compose задаёт `postgres`, `5432` внутри сети |
| `DB_USER`, `DB_PASSWORD` | доступы PostgreSQL; обязательны |
| `JWT_SIGNING_KEY` | случайный секрет не короче 32 UTF-8 байт |
| `JWT_ISSUER`, `JWT_AUDIENCE` | шаблон и Compose используют `aglomslag-local`, `aglomslag-clients` |
| `DEMO_USER_EMAIL`, `DEMO_USER_PASSWORD` | вход обычного пользователя, ID `-1` |
| `DEMO_ADMIN_EMAIL`, `DEMO_ADMIN_PASSWORD` | вход администратора, ID `-2` |
| `DEMO_BOOTSTRAP` | создание демонстрационных аккаунтов при старте; по умолчанию `true` в Compose, `false` вне Compose |

Пароли аккаунтов — 12..1024 символа, не только пробелы. Email двух ролей должны различаться. Параметры времени жизни JWT/refresh необязательны; значения и права описаны в [authentication.md](authentication.md).

API доступен по HTTP и в Production: хост `http://localhost:5000/graphql`, порт контейнера `8080`. В браузере доступны GraphQL IDE, SDL и introspection. Все бизнес-методы, кроме Login, требуют JWT.

Повторный `up` и restart сохраняют данные и пароли существующих аккаунтов. Изменение демонстрационного пароля в `.env` не сбрасывает уже сохранённый пароль; для этого есть явная операторская команда. Данные находятся в volume `postgres_data`; `down -v` их удаляет.

## Хост / Rider

После применения миграций Docker можно запустить API из IDE с SDK `10.0.402`:

```powershell
dotnet restore Soft/AglomSlagServer/AglomSlagServer.slnx
dotnet run --project Soft/AglomSlagServer --launch-profile http
```

Endpoint: `http://localhost:5093/graphql`. Development-загрузчик читает тот же `Soft/.env`; для нестандартного пути есть `AGLOMSLAG_ENV_FILE`. Для автоматического создания demo-аккаунтов вне Compose установить `DEMO_BOOTSTRAP=true`. Production получает настройки окружением процесса.

## Значения и приоритет

Использовать строки `KEY='literal value'`: одинарные кавычки сохраняют `$`, `#`, `;`, пробелы и двойные кавычки. Для одинакового поведения Compose и Development не включать одинарную кавычку и перевод строки в такое значение. Загрузчик не выполняет подстановки, escapes и код; inline-комментарии, `export`, дубли и многострочные значения не поддерживаются.

Окружение процесса имеет приоритет над `.env`. Для БД приоритет: `ConnectionStrings__SlagModeContext` / `ConnectionStrings__AuthContext`, затем `CONNECTION_SLAG` / `CONNECTION_AUTH`, затем `DB_HOST`, `DB_PORT`, `DB_USER`, `DB_PASSWORD`. Удалить устаревшие overrides shell/IDE, если нужно использовать файл. Строки подключения собираются через NpgsqlConnectionStringBuilder.

## Смена пароля существующей БД

`POSTGRES_PASSWORD` инициализирует только пустой кластер. Изменение `.env` не меняет пароль созданной роли.

1. Сохранить резервную копию и остановить API: `docker compose --env-file Soft/.env -f Soft/Docker-compose.yml stop graphql-api`; остановить API в IDE, если он работает.
2. Открыть `docker compose --env-file Soft/.env -f Soft/Docker-compose.yml exec postgres psql -U имя_роли -d postgres`, подставив текущий `DB_USER`.
3. В psql выполнить `\password`, ввести новый пароль и выйти через `\q`.
4. Обновить `DB_PASSWORD` в `Soft/.env` и выполнить `docker compose --env-file Soft/.env -f Soft/Docker-compose.yml up -d --force-recreate`.

При отказе исправить настройки или вернуть прежний пароль через psql. Volume сохранять.

Диагностика: `docker compose --env-file Soft/.env -f Soft/Docker-compose.yml logs --tail 100 migrator graphql-api`. Проверки изолированного стенда описаны в [tests/README.md](../tests/README.md), правила файлов и журналов — в [repository-management.md](repository-management.md).
