FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["QuickBite.API/QuickBite.API.csproj", "QuickBite.API/"]
RUN dotnet restore "QuickBite.API/QuickBite.API.csproj"

COPY . .
RUN dotnet publish "QuickBite.API/QuickBite.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "QuickBite.API.dll"]
