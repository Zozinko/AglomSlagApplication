FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AglomSlagServer/AglomSlagServer.slnx AglomSlagServer/

COPY AglomSlagServer/*.csproj AglomSlagServer/
COPY DBStructure/*.csproj DBStructure/

RUN dotnet restore AglomSlagServer/AglomSlagServer.slnx

COPY . .

RUN dotnet publish AglomSlagServer/AglomSlagServer.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "AglomSlagServer.dll"]