FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
# Kestrel в Production слушает :5000 (appsettings.Production.json)
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["ITCafe.Api/ITCafe.Api.csproj", "ITCafe.Api/"]
RUN dotnet restore "ITCafe.Api/ITCafe.Api.csproj"
COPY . .
WORKDIR "/src/ITCafe.Api"
RUN dotnet build "ITCafe.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ITCafe.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ITCafe.Api.dll"]
