FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/BuildingBlocks/SneakerShop.BuildingBlocks/SneakerShop.BuildingBlocks.csproj", "src/BuildingBlocks/SneakerShop.BuildingBlocks/"]
COPY ["src/Services/Cart/Cart.Api/Cart.Api.csproj", "src/Services/Cart/Cart.Api/"]
RUN dotnet restore "src/Services/Cart/Cart.Api/Cart.Api.csproj"
COPY src/ src/
RUN dotnet publish "src/Services/Cart/Cart.Api/Cart.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Cart.Api.dll"]
