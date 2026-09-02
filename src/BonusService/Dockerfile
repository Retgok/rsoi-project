FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8050
ENV ASPNETCORE_URLS=http://+:8050

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["SharedAuth/SharedAuth.csproj", "SharedAuth/"]
COPY ["SharedEvents/SharedEvents.csproj", "SharedEvents/"]
COPY ["BonusService/BonusService.csproj", "BonusService/"]
RUN dotnet restore "./BonusService/BonusService.csproj"
COPY . .
WORKDIR "/src/BonusService"
RUN dotnet build "./BonusService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./BonusService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BonusService.dll"]
