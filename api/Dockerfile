FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Neuracode.Crm.Api/Neuracode.Crm.Api.csproj", "src/Neuracode.Crm.Api/"]
RUN dotnet restore "src/Neuracode.Crm.Api/Neuracode.Crm.Api.csproj"
COPY . .
WORKDIR "/src/src/Neuracode.Crm.Api"
RUN dotnet build "Neuracode.Crm.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Neuracode.Crm.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["sh", "-c", "dotnet Neuracode.Crm.Api.dll --urls http://+:${PORT:-5000}"]
