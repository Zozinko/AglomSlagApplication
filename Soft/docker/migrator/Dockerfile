FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Устанавливаем глобальный инструмент EF Core
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

# Копируем файлы проекта для restore
COPY AglomSlagServer/AglomSlagServer.slnx AglomSlagServer/
COPY AglomSlagServer/*.csproj AglomSlagServer/
COPY DBStructure/*.csproj DBStructure/
COPY SlagModeSolver/*.csproj SlagModeSolver/

# Восстанавливаем пакеты
RUN dotnet restore AglomSlagServer/AglomSlagServer.slnx

# Копируем весь исходный код (включая папки Migrations)
COPY . .

# Копируем скрипт запуска и даём ему права на выполнение
COPY docker/migrator/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

# Точка входа - наш скрипт
ENTRYPOINT ["/entrypoint.sh"]