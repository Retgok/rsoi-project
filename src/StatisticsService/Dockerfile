FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8040
ENV ASPNETCORE_URLS=http://+:8040

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["SharedAuth/SharedAuth.csproj", "SharedAuth/"]
COPY ["SharedEvents/SharedEvents.csproj", "SharedEvents/"]
COPY ["StatisticsService/StatisticsService.csproj", "StatisticsService/"]
RUN dotnet restore "./StatisticsService/StatisticsService.csproj"
COPY . .
WORKDIR "/src/StatisticsService"
RUN dotnet build "./StatisticsService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./StatisticsService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "StatisticsService.dll"]
