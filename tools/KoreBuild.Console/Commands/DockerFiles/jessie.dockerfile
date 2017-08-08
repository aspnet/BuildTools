FROM microsoft/dotnet:2.0-runtime-deps-jessie

RUN apt-get update && \
    apt-get install -y git && \
    apt-get install -y curl && \
    apt-get install -y unzip && \
    apt-get install -y apt-transport-https

ADD ./ ./

RUN rm -f ./korebuild-lock.txt

ENTRYPOINT ["./build.sh"]
