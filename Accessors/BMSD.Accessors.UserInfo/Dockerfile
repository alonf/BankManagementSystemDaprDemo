#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Accessors/BMSD.Accessors.UserInfo/BMSD.Accessors.UserInfo.csproj", "Accessors/BMSD.Accessors.UserInfo/"]
RUN dotnet restore "Accessors/BMSD.Accessors.UserInfo/BMSD.Accessors.UserInfo.csproj"
COPY . .
WORKDIR "/src/Accessors/BMSD.Accessors.UserInfo"
RUN dotnet build "BMSD.Accessors.UserInfo.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BMSD.Accessors.UserInfo.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BMSD.Accessors.UserInfo.dll"]