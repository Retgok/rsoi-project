FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8070
ENV ASPNETCORE_URLS=http://+:8070

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["SharedAuth/SharedAuth.csproj", "SharedAuth/"]
COPY ["SharedEvents/SharedEvents.csproj", "SharedEvents/"]
COPY ["TicketService/TicketService.csproj", "TicketService/"]
RUN dotnet restore "./TicketService/TicketService.csproj"
COPY . .
WORKDIR "/src/TicketService"
RUN dotnet build "./TicketService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./TicketService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TicketService.dll"]
