# REPO-01: уборка репозитория

Проверено 25.10.2026, локально.

## Размещение

- Backend/frontend-планы перемещены в `tasks/TODO.md` и `tasks/TODO_Frontend.md`.
- Протоколы перенесены из `tests`/`requests` в `tasks/SEC-01/`, `tasks/bugfixes/`, `tasks/api-tests/`; относительные ссылки обновлены.
- Прежняя `.artifacts` перемещена в игнорируемую `tasks/SEC-01/local/`, включая скачанный runtime и разовые скрипты; их пути обновлены.
- Найденные `.log` из корня и `tests` перенесены в `logs/`. Runner теперь сам записывает `logs/last-run.log`. Журналы игнорируются Git, каталог сохраняется через `.gitkeep`.
- Доступы старых env-файлов совпадали. Сохранён единственный `Soft/.env` с прежними значениями, дубли удалены. Оставлен один безопасный `.env.example`. Compose переопределяет host на `postgres`, host-загрузчик читает `localhost` из общего файла.

Правила размещения описаны в [repository-management.md](../../docs/repository-management.md), настройка — в [configuration.md](../../docs/configuration.md). Оба документа и актуальные планы доступны из [arcitecture.md](../../arcitecture.md).

## Проверки

```powershell
node tasks/REPO-01/verify-links.mjs
docker compose --env-file Soft/.env -f Soft/Docker-compose.yml config --quiet
$env:API_TEST_DOTNET = Join-Path (Get-Location) 'tasks/SEC-01/local/sec01-dotnet/dotnet.exe'
./tests/Run-ApiTests.ps1
git diff --check
```

Полный прогон: **61 тест, 0 ошибок, 0 пропусков** (47 API, 5 SEC-01, 9 инфраструктурных), код завершения 0. Изолированный проект `aglom-api-tests-7eba71675026` очищен runner. Проверены обе БД и migrator, новый путь host env-файла, спецсимволы и приоритет окружения. Полный вывод — в локальных `logs/last-run.log` и `logs/repository-cleanup-tests.log`.

Проверены относительные Markdown-ссылки, отсутствие рабочих env-дублей и журналов вне `logs`, исключение env/журналов/крупных материалов из Git. Рабочие пароли, данные основного PostgreSQL и математические DLL не изменялись. Перенос файлов не требует удаления volumes.

## Перенос env в Soft перед публикацией коммита

По уточнению пользователя единственный рабочий файл и шаблон находятся в `Soft/.env` и `Soft/.env.example`. Обновлены документация, Git ignore, сообщения Compose, путь Development-загрузчика и тестовая host-фикстура. REPO-01 добавлена выполненной в TODO.

Перед коммитом проверены 77 локальных ссылок и настройки Compose командой `config --quiet`. Повторно прошли 47 API-тестов и 4 SEC-01, включая запуск на компьютере с новым расположением env. Затем Docker/Compose стали недоступны: проверка смены пароля БД и очистка временного проекта `aglom-api-tests-00eb0b7a75f6` не завершились. Созданный runner временный env-файл удалён вручную; возможные оставшиеся ресурсы относятся только к этому изолированному проекту. Ранее полный прогон 61 теста завершился успешно; повторную проверку успешной не считаем. Журнал: `logs/pre-commit-tests.log`.
