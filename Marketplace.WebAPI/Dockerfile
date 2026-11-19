FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src
COPY Marketplace.Domain/Marketplace.Domain.csproj Marketplace.Domain/
COPY Marketplace.Application/Marketplace.Application.csproj Marketplace.Application/
COPY Marketplace.Infrastructure/Marketplace.Infrastructure.csproj Marketplace.Infrastructure/
COPY Marketplace.WebAPI/Marketplace.WebAPI.csproj Marketplace.WebAPI/
RUN dotnet restore Marketplace.WebAPI/Marketplace.WebAPI.csproj
COPY Marketplace.Domain/ Marketplace.Domain/
COPY Marketplace.Application/ Marketplace.Application/
COPY Marketplace.Infrastructure/ Marketplace.Infrastructure/
COPY Marketplace.WebAPI/ Marketplace.WebAPI/
COPY deploy/ deploy/
RUN dotnet publish Marketplace.WebAPI/Marketplace.WebAPI.csproj -c Release --no-restore -m:1 -o /out /p:UseAppHost=false /p:DebugType=None /p:DebugSymbols=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production PORT=10000
COPY --from=build --chown=app:app /out/ ./
USER app
EXPOSE 10000
CMD ["sh", "-c", "exec dotnet Marketplace.WebAPI.dll --urls http://0.0.0.0:${PORT:-10000}"]
