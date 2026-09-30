#!/bin/bash
set -e # Остановить выполнение при любой ошибке

echo "🔄 Запуск миграций через dotnet-ef CLI..."

# Миграция для SlagModeContext
echo "👉 Применение миграций для SlagModeContext..."
dotnet ef database update \
    --context SlagModeContext \
    --project DBStructure \
    --startup-project AglomSlagServer \
    --connection "$CONNECTION_SLAG"

# Миграция для AuthContext
echo "👉 Применение миграций для AuthContext..."
dotnet ef database update \
    --context AuthContext \
    --project DBStructure \
    --startup-project AglomSlagServer \
    --connection "$CONNECTION_AUTH"

echo "✅ Все миграции успешно применены!"