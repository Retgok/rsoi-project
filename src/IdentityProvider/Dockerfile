FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8090
ENV ASPNETCORE_URLS=http://+:8090

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["SharedAuth/SharedAuth.csproj", "SharedAuth/"]
COPY ["SharedEvents/SharedEvents.csproj", "SharedEvents/"]
COPY ["IdentityProvider/IdentityProvider.csproj", "IdentityProvider/"]
RUN dotnet restore "./IdentityProvider/IdentityProvider.csproj"
COPY . .
WORKDIR "/src/IdentityProvider"
RUN dotnet build "./IdentityProvider.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./IdentityProvider.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IdentityProvider.dll"]
