FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY ./src .

RUN dotnet restore
RUN dotnet publish --no-restore -o /app



FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine

LABEL org.opencontainers.image.source="https://github.com/nevse/sherlockohms-telegram-bot"

RUN apk --no-cache add libstdc++ icu libc6-compat

WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["./TelegramBot"]