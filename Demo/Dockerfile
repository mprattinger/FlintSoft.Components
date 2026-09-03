# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG APP_VERSION=0.0.0-local
ARG APP_FILE_VERSION=0.0.0.0
ARG APP_ASSEMBLY_VERSION=0.0.0.0
WORKDIR /src
COPY ["Demo/Demo.csproj", "Demo/"]
COPY ["FlintSoft.Components/FlintSoft.Components.csproj", "FlintSoft.Components/"]
RUN dotnet restore "./Demo/Demo.csproj"
COPY . .
WORKDIR "/src/Demo"
RUN dotnet build "./Demo.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:Version=$APP_VERSION /p:FileVersion=$APP_FILE_VERSION /p:AssemblyVersion=$APP_ASSEMBLY_VERSION

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG APP_VERSION=0.0.0-local
ARG APP_FILE_VERSION=0.0.0.0
ARG APP_ASSEMBLY_VERSION=0.0.0.0
RUN dotnet publish "./Demo.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:Version=$APP_VERSION /p:FileVersion=$APP_FILE_VERSION /p:AssemblyVersion=$APP_ASSEMBLY_VERSION

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
ARG APP_VERSION=0.0.0-local
WORKDIR /app
COPY --from=publish /app/publish .
ENV APP_VERSION=$APP_VERSION
LABEL org.opencontainers.image.version="${APP_VERSION}" \
      org.opencontainers.image.source="https://github.com/mprattinger/FlintSoft.Components" \
      org.opencontainers.image.description="FlintSoft Components demo application"
ENTRYPOINT ["dotnet", "Demo.dll"]