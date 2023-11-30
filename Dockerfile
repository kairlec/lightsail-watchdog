ARG mirrors
##################################
FROM mcr.microsoft.com/dotnet/sdk:8.0 as build

COPY . /src

WORKDIR /src

RUN dotnet restore
RUN dotnet publish -c Release -o /publish -p:PublishSingleFile=true --self-contained true

##################################
FROM debian:bookworm-slim as runtime-offical

RUN apt update && apt install -y \
    libicu72 \
    libssl3

##################################
FROM debian:bookworm-slim as runtime-ustc

RUN sed -i 's/deb.debian.org/mirrors.ustc.edu.cn/g' /etc/apt/sources.list.d/debian.sources

RUN apt update && apt install -y \
    libicu72 \
    libssl3

##################################
FROM debian:bookworm-slim as runtime-aliyun

RUN sed -i 's/deb.debian.org/mirrors.aliyun.com/g' /etc/apt/sources.list.d/debian.sources

RUN apt update && apt install -y \
    libicu72 \
    libssl3

##################################
FROM debian:bookworm-slim as runtime-tuna

RUN sed -i 's/deb.debian.org/mirrors.tuna.tsinghua.edu.cn/g' /etc/apt/sources.list.d/debian.sources

RUN apt update && apt install -y \
    libicu72 \
    libssl3

##################################
FROM runtime-${mirrors}

COPY --from=build ./publish/lightsail-watchdog /usr/local/bin/lightsail-watchdog

RUN chmod +x /usr/local/bin/lightsail-watchdog

ENTRYPOINT ["/usr/local/bin/lightsail-watchdog"]