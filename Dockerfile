# syntax=docker/dockerfile:1.7
# Multi-stage build: SvelteKit frontend -> trimmed self-contained .NET server -> minimal Alpine runtime with SANE.

FROM --platform=$BUILDPLATFORM node:22-alpine AS web
WORKDIR /src/web
RUN corepack enable && corepack prepare pnpm@latest --activate
COPY web/package.json web/pnpm-lock.yaml web/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY web/ ./
RUN pnpm build

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS server
ARG TARGETARCH
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props Yass.sln .editorconfig ./
COPY src/ src/
COPY tests/ tests/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/Yass.Server/Yass.Server.csproj -r linux-musl-${TARGETARCH}
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/Yass.Server/Yass.Server.csproj -c Release -r linux-musl-${TARGETARCH} \
      --self-contained true --no-restore -o /app

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine AS runtime
# sane-backends provides libsane for USB scanners; sane-utils is handy for `scanimage -L` diagnostics.
RUN apk add --no-cache sane-backends sane-utils libusb tini \
 # hpaio (HPLIP) aborts the whole process when no D-Bus is available; HP all-in-ones work over eSCL instead.
 && rm -f /etc/sane.d/dll.d/hpaio && sed -i 's/^hpaio/#hpaio/' /etc/sane.d/dll.conf \
 # camera/video backends are irrelevant for document scanning and make every probe take ~20 s
 && for b in gphoto2 v4l dc210 dc240 dc25 qcam stv680 pnm net; do rm -f /etc/sane.d/dll.d/$b; sed -i "s/^$b\$/#$b/" /etc/sane.d/dll.conf; done \
 && addgroup -S yass && adduser -S -G yass yass \
 && mkdir -p /data/sessions /data/consume && chown -R yass:yass /data
WORKDIR /app
COPY --from=server /app/ ./
COPY --from=web /src/web/build/ ./wwwroot/
COPY yass.example.yaml /etc/yass/yass.example.yaml
ENV YASS_CONFIG=/etc/yass/yass.yaml \
    DOTNET_gcServer=0 \
    DOTNET_GCHeapHardLimit=0x18000000
EXPOSE 8080
VOLUME ["/data"]
HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://127.0.0.1:8080/healthz || exit 1
# SANE needs access to USB devices; run with --device /dev/bus/usb or as root if udev groups do not match.
USER yass
ENTRYPOINT ["/sbin/tini", "--", "/app/Yass.Server"]
