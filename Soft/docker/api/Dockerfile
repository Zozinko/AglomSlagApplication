FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AglomSlagServer/AglomSlagServer.slnx AglomSlagServer/

COPY AglomSlagServer/*.csproj AglomSlagServer/
COPY DBStructure/*.csproj DBStructure/
COPY SlagModeSolver/SlagModeSolver.csproj SlagModeSolver/

RUN dotnet restore AglomSlagServer/AglomSlagServer.slnx

COPY . .

RUN dotnet publish AglomSlagServer/AglomSlagServer.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    libgfortran5 \
    libz-dev \
    libc6 && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "AglomSlagServer.dll"]