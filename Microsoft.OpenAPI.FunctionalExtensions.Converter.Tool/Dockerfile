FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Microsoft.OpenAPI.FunctionalExtensions.Converter.Tool/Microsoft.OpenAPI.FunctionalExtensions.Converter.Tool.fsproj", "Microsoft.OpenAPI.FunctionalExtensions.Converter.Tool/"]
RUN dotnet restore "Microsoft.OpenAPI.FunctionalExtensions.Converter.Tool/Microsoft.OpenAPI.FunctionalExtensions.Converter.Tool.fsproj"
COPY . .
WORKDIR "/src/Microsoft.OpenAPI.FunctionalExtensions.Converter.Tool"
RUN dotnet build "Microsoft.OpenAPI.FunctionalExtensions.Converter.Tool.fsproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Microsoft.OpenAPI.FunctionalExtensions.Converter.Tool.fsproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Microsoft.OpenAPI.FunctionalExtensions.Converter.Tool.dll"]
