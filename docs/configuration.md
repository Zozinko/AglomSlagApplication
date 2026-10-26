# Конфигурация и запуск

Все команды выполняются из корня репозитория. Для обычного запуска нужны Docker с Linux containers и Compose v2. Устанавливать .NET и PostgreSQL на компьютер не требуется.

## Быстрый старт: четыре шага

1. Клонировать проект с GitHub и перейти в папку проекта.
2. Выполнить `Copy-Item Soft/.env.example Soft/.env` и заполнить `Soft/.env`, как описано ниже. На Linux команда копирования: `cp Soft/.env.example Soft/.env`.
3. Выполнить `docker compose --env-file Soft/.env -f Soft/Docker-compose.yml up -d --build`.
4. Открыть `http://localhost:5000/graphql` или выполнить вход и запросы по [инструкции requests](../requests/README.md).

Первый запуск создаёт БД, применяет миграции и создаёт демонстрационного пользователя и администратора. Дополнительная команда создания пользователей не нужна. Проверить запуск:

```powershell
docker compose --env-file Soft/.env -f Soft/Docker-compose.yml ps -a
```

Ожидаемый результат: PostgreSQL имеет состояние `healthy`, `migrator` завершился с кодом `0`, `graphql-api` работает.

## Что заполнить в Soft/.env

Не перезаписывайте уже заполненный файл. Он исключён из Git; Docker и запуск из IDE в режиме Development используют один и тот же файл.

В шаблоне 15 основных настроек. Для первого запуска достаточно заполнить четыре пустых значения: `DB_PASSWORD`, `JWT_SIGNING_KEY`, `DEMO_USER_PASSWORD`, `DEMO_ADMIN_PASSWORD`. Проверьте имена пользователей и email; остальные значения можно оставить из шаблона.

| Настройки | Что означают |
|---|---|
| `DB_HOST`, `DB_PORT` | `localhost`, `5432` для запуска из IDE; внутри Docker Compose задаёт `postgres`, `5432` |
| `DB_USER`, `DB_PASSWORD` | имя и пароль PostgreSQL; обязательны |
| `JWT_SIGNING_KEY` | случайный секрет для подписи токенов, не короче 32 UTF-8 байт |
| `JWT_ISSUER`, `JWT_AUDIENCE` | проверяемые имена сервера и клиента; оставить `aglomslag-local`, `aglomslag-clients` |
| `JWT_ACCESS_SECONDS`, `JWT_CLOCK_SKEW_SECONDS`, `JWT_REFRESH_DAYS` | сроки токенов и допуск часов; оставить `300`, `5`, `7` |
| `DEMO_BOOTSTRAP` | создавать демонстрационные аккаунты при запуске; в Compose по умолчанию `true`, вне Compose — `false` |
| `DEMO_USER_EMAIL`, `DEMO_USER_PASSWORD` | вход пользователя, ID `-1` |
| `DEMO_ADMIN_EMAIL`, `DEMO_ADMIN_PASSWORD` | вход администратора, ID `-2` |

Пароли аккаунтов — от 12 до 1024 символов, не только пробелы. Email двух ролей должны различаться. Пять полей в конце шаблона (`BOOTSTRAP_EMAIL`, `BOOTSTRAP_PASSWORD`, `BOOTSTRAP_NAME`, `TARGET_USER_ID`, `NEW_PASSWORD`) нужны только для [обслуживания аккаунтов](authentication.md#обслуживание-аккаунтов); при обычном запуске оставить пустыми.

API работает по HTTP во всех окружениях, включая Production: `http://localhost:5000/graphql`, порт контейнера `8080`. В браузере открывается редактор запросов GraphQL. Получение схемы (`/graphql?sdl` и introspection) и GET доступны без входа; все бизнес-методы, кроме `login`, требуют токен доступа.

Повторный запуск сохраняет данные и пароли. Изменение демонстрационного пароля в `.env` не сбрасывает пароль существующего аккаунта. Данные находятся в Docker volume `postgres_data`; команда `down -v` удаляет их.

## Запуск из Rider или другой IDE

После применения миграций можно запустить API из IDE с SDK `10.0.402`:

```powershell
dotnet restore Soft/AglomSlagServer/AglomSlagServer.slnx
dotnet run --project Soft/AglomSlagServer --launch-profile http
```

Адрес: `http://localhost:5093/graphql`. В режиме Development сервер читает тот же `Soft/.env`; другой путь можно задать через `AGLOMSLAG_ENV_FILE`. Для создания демонстрационных аккаунтов вне Compose нужно `DEMO_BOOTSTRAP=true`. В режиме Production настройки передаются через окружение процесса.

## Если настройки не применяются

В `.env` используйте строки `KEY='literal value'`: одинарные кавычки сохраняют `$`, `#`, `;`, пробелы и двойные кавычки. Внутри такого значения не должно быть одинарной кавычки или перевода строки. Загрузчик не подставляет переменные и не исполняет код; комментарии после значения, `export`, повторяющиеся ключи и многострочные значения не поддерживаются.

Переменные окружения процесса важнее `.env`. Для подключения БД порядок такой: `ConnectionStrings__SlagModeContext` / `ConnectionStrings__AuthContext`, затем `CONNECTION_SLAG` / `CONNECTION_AUTH`, затем `DB_HOST`, `DB_PORT`, `DB_USER`, `DB_PASSWORD`. Удалите прежние значения из настроек IDE или оболочки, если хотите использовать файл. Сервер собирает строки подключения через `NpgsqlConnectionStringBuilder`.

Посмотреть последние сообщения запуска:

```powershell
docker compose --env-file Soft/.env -f Soft/Docker-compose.yml logs --tail 100 migrator graphql-api
```

## Смена пароля существующей БД

`POSTGRES_PASSWORD` задаёт пароль только при создании пустой БД. Для существующей роли одного изменения `.env` недостаточно.

1. Сохранить резервную копию. Остановить API командой `docker compose --env-file Soft/.env -f Soft/Docker-compose.yml stop graphql-api` и остановить API в IDE, если он работает.
2. Открыть `docker compose --env-file Soft/.env -f Soft/Docker-compose.yml exec postgres psql -U имя_роли -d postgres`, подставив текущий `DB_USER`.
3. В psql выполнить `\password`, ввести новый пароль и выйти через `\q`.
4. Обновить `DB_PASSWORD` в `Soft/.env` и выполнить `docker compose --env-file Soft/.env -f Soft/Docker-compose.yml up -d --force-recreate`.

Если подключение не работает, исправить настройки или вернуть прежний пароль через psql. Volume сохранять.

[Автоматические тесты](../tests/README.md) · [Правила ведения репозитория](repository-management.md)
