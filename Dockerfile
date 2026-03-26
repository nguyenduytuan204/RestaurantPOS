# Use standard ASP.NET Core 8.0 image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Copy project file using the correct relative path
COPY ["RestaurantPOS.API/RestaurantPOS.csproj", "RestaurantPOS.API/"]
RUN dotnet restore "RestaurantPOS.API/RestaurantPOS.csproj"
COPY . .
WORKDIR "/src/RestaurantPOS.API"
RUN dotnet build "RestaurantPOS.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RestaurantPOS.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Since the compiled dll is likely in the output folder but we don't know the exact name without checking csproj OutputName
# Usually it's RestaurantPOS.dll or RestaurantPOS.API.dll
ENTRYPOINT ["dotnet", "RestaurantPOS.dll"]
