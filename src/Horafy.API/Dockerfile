# ── Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia apenas os csproj primeiro para aproveitar cache do Docker
COPY ["src/Horafy.API/Horafy.API.csproj", "src/Horafy.API/"]
COPY ["src/Horafy.Application/Horafy.Application.csproj", "src/Horafy.Application/"]
COPY ["src/Horafy.Domain/Horafy.Domain.csproj", "src/Horafy.Domain/"]
COPY ["src/Horafy.Infrastructure/Horafy.Infrastructure.csproj", "src/Horafy.Infrastructure/"]
COPY ["src/Horafy.Shared/Horafy.Shared.csproj", "src/Horafy.Shared/"]

RUN dotnet restore "src/Horafy.API/Horafy.API.csproj"

# Copia o restante e compila
COPY . .
WORKDIR /src/src/Horafy.API
RUN dotnet build "Horafy.API.csproj" -c Release -o /app/build

# ── Stage 2: Publish
FROM build AS publish
RUN dotnet publish "Horafy.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Stage 3: Runtime (imagem mínima)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Usuário não-root por segurança
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Horafy.API.dll"]
