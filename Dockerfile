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
COPY global.json Directory.Build.props Directory.Packages.props Yawss.sln .editorconfig ./
COPY src/ src/
COPY tests/ tests/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/Yawss.Server/Yawss.Server.csproj -r linux-musl-${TARGETARCH}
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/Yawss.Server/Yawss.Server.csproj -c Release -r linux-musl-${TARGETARCH} \
      --self-contained true --no-restore -o /app

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine AS runtime
# sane-backends provides libsane for USB scanners; sane-utils is handy for `scanimage -L` diagnostics.
RUN apk add --no-cache sane-backends sane-utils libusb tini \
 # hpaio (HPLIP) aborts the whole process when no D-Bus is available; HP all-in-ones work over eSCL instead.
 && rm -f /etc/sane.d/dll.d/hpaio && sed -i 's/^hpaio/#hpaio/' /etc/sane.d/dll.conf \
 # camera/video backends are irrelevant for document scanning and make every probe take ~20 s
 && for b in gphoto2 v4l dc210 dc240 dc25 qcam stv680 pnm net; do rm -f /etc/sane.d/dll.d/$b; sed -i "s/^$b\$/#$b/" /etc/sane.d/dll.conf; done \
 && addgroup -S yawss && adduser -S -G yawss yawss \
 && mkdir -p /data/sessions /data/consume && chown -R yawss:yawss /data
WORKDIR /app
COPY --from=server /app/ ./
COPY --from=web /src/web/build/ ./wwwroot/
COPY yawss.example.yaml /etc/yawss/yawss.example.yaml
ENV YAWSS_CONFIG=/etc/yawss/yawss.yaml \
    DOTNET_gcServer=0 \
    DOTNET_GCHeapHardLimit=0x18000000
EXPOSE 8080
VOLUME ["/data"]
HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://127.0.0.1:8080/healthz || exit 1
# SANE needs access to USB devices; run with --device /dev/bus/usb or as root if udev groups do not match.
USER yawss
ENTRYPOINT ["/sbin/tini", "--", "/app/Yawss.Server"]
