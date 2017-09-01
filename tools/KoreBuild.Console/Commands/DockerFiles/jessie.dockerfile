FROM microsoft/dotnet:2.0-runtime-deps-jessie

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        git \
# KoreBuild dependencies
        curl \
        unzip \
        apt-transport-https \
    && rm -rf /var/lib/apt/lists/*

ADD . .

ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true

ENTRYPOINT ["./build.sh"]
