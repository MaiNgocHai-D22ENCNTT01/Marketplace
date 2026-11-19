FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src
COPY Marketplace.Domain/Marketplace.Domain.csproj Marketplace.Domain/
COPY Marketplace.Application/Marketplace.Application.csproj Marketplace.Application/
COPY Marketplace.WebClient/Marketplace.WebClient.csproj Marketplace.WebClient/
RUN dotnet restore Marketplace.WebClient/Marketplace.WebClient.csproj
COPY Marketplace.Domain/ Marketplace.Domain/
COPY Marketplace.Application/ Marketplace.Application/
COPY Marketplace.WebClient/ Marketplace.WebClient/
COPY deploy/ deploy/
RUN dotnet publish Marketplace.WebClient/Marketplace.WebClient.csproj -c Release --no-restore -m:1 -o /out /p:UseAppHost=false /p:DebugType=None /p:DebugSymbols=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production PORT=10000
COPY --from=build --chown=app:app /out/ ./
USER app
EXPOSE 10000
CMD ["sh", "-c", "exec dotnet Marketplace.WebClient.dll --urls http://0.0.0.0:${PORT:-10000}"]
