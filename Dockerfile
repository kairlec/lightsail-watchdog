FROM mcr.microsoft.com/dotnet/sdk:8.0 as build

COPY . /src

WORKDIR /src

RUN dotnet restore
RUN dotnet publish -c Release -o /publish -p:PublishSingleFile=true --self-contained true

FROM debian:bookworm-slim

RUN apt update && apt install -y \
    libicu72 \
    libssl3

COPY --from=build ./publish/lightsail-watchdog /usr/local/bin/lightsail-watchdog

RUN chmod +x /usr/local/bin/lightsail-watchdog

ENTRYPOINT ["/usr/local/bin/lightsail-watchdog"]