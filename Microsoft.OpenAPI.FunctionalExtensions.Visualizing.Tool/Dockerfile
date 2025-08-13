FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool/Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool.fsproj", "Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool/"]
RUN dotnet restore "Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool/Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool.fsproj"
COPY . .
WORKDIR "/src/Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool"
RUN dotnet build "Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool.fsproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool.fsproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool.dll"]
