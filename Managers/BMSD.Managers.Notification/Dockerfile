#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Managers/BMSD.Managers.Notification/BMSD.Managers.Notification.csproj", "Managers/BMSD.Managers.Notification/"]
RUN dotnet restore "Managers/BMSD.Managers.Notification/BMSD.Managers.Notification.csproj"
COPY . .
WORKDIR "/src/Managers/BMSD.Managers.Notification"
RUN dotnet build "BMSD.Managers.Notification.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BMSD.Managers.Notification.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BMSD.Managers.Notification.dll"]