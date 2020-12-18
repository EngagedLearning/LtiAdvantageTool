# Produces Kubernetes-ready, most compact version of APS running on Alpine linux. Built in two stages: 
# a) build stage builds the app and is test ready (with docker run)
# b) run stage optimized for speed and size, currently just 30M when pushed to ECR repo.
# ----------- Build Stage -------------
FROM mcr.microsoft.com/dotnet/core/sdk:3.1-alpine AS build

WORKDIR /LtiAdvantage/src/LtiAdvantage
COPY ./LtiAdvantage/src/LtiAdvantage/LtiAdvantage.csproj .
RUN dotnet restore LtiAdvantage.csproj -r linux-musl-x64
COPY ./LtiAdvantage/src/LtiAdvantage .
RUN dotnet build LtiAdvantage.csproj -r linux-musl-x64

WORKDIR /LtiAdvantage/src/LtiAdvantage.IdentityModel
COPY ./LtiAdvantage/src/LtiAdvantage.IdentityModel/LtiAdvantage.IdentityModel.csproj .
RUN dotnet restore LtiAdvantage.IdentityModel.csproj -r linux-musl-x64
COPY ./LtiAdvantage/src/LtiAdvantage.IdentityModel .
RUN dotnet build LtiAdvantage.IdentityModel.csproj -r linux-musl-x64

WORKDIR /LtiAdvantageTool/src
COPY ./LtiAdvantageTool/src/AdvantageTool.csproj .
RUN dotnet restore AdvantageTool.csproj -r linux-musl-x64
COPY ./LtiAdvantageTool/src .
# RUN dotnet build LtiAdvantage.IdentityModel.csproj -r linux-musl-x64

# Publish standalone app
RUN dotnet publish AdvantageTool.csproj -c Release -r linux-musl-x64 -o /app \
        -p:PublishSingleFile=true -p:PublishTrimmed=true \
        -p:CrossGenDuringPublish=false -p:PublishReadyToRun=false --no-restore
# Note: publishreadytorun=true will maybe start faster but docker is 60M larger

ENV ASPNETCORE_URLS http://*:3030
EXPOSE 3030

# To be cloud-native APS should reads it's aws secrets from env vars 
# that are injected at the time of running the container in kubernetes cluster.
# ENV AWS_ACCESS_KEY_ID=""
# ENV AWS_SECRET_ACCESS_KEY=""
# ENV AWS_DEFAULT_REGION="us-west-2"

# ---------- Final Build Stage ------------
# Get only runtime deps, not dotnet
FROM mcr.microsoft.com/dotnet/core/runtime-deps:3.1-alpine
ARG SHORT_SHA_ARG

COPY --from=build /app .

ENV ASPNETCORE_URLS http://*:3030
EXPOSE 3030

# To be cloud-native APS should reads it's aws secrets from env vars 
# that are injected at the time of running the container in kubernetes cluster.
ENV AWS_ACCESS_KEY_ID=""
ENV AWS_SECRET_ACCESS_KEY=""
ENV AWS_DEFAULT_REGION="us-west-2"
ENV SHORT_SHA=$SHORT_SHA_ARG

ENTRYPOINT ["./AdvantageTool"]