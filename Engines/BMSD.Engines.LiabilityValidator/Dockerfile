#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Engines/BMSD.Engines.LiabilityValidator/BMSD.Engines.LiabilityValidator.csproj", "Engines/BMSD.Engines.LiabilityValidator/"]
RUN dotnet restore "Engines/BMSD.Engines.LiabilityValidator/BMSD.Engines.LiabilityValidator.csproj"
COPY . .
WORKDIR "/src/Engines/BMSD.Engines.LiabilityValidator"
RUN dotnet build "BMSD.Engines.LiabilityValidator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BMSD.Engines.LiabilityValidator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BMSD.Engines.LiabilityValidator.dll"]