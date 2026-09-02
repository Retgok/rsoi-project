FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8060
ENV ASPNETCORE_URLS=http://+:8060

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["SharedAuth/SharedAuth.csproj", "SharedAuth/"]
COPY ["SharedEvents/SharedEvents.csproj", "SharedEvents/"]
COPY ["FlightService/FlightService.csproj", "FlightService/"]
RUN dotnet restore "./FlightService/FlightService.csproj"
COPY . .
WORKDIR "/src/FlightService"
RUN dotnet build "./FlightService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./FlightService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FlightService.dll"]
