# YAWSS — Yet Another Web Scanner Service

Headless scanning service for a home server. Attach a scanner (USB/SANE or
network/eSCL), open the mobile-first web UI, scan until the feeder is empty,
review the pages, and save a PDF straight into your paperless-ngx consume
folder.

> ## ⚠️ No authentication — LAN only
>
> YAWSS has **no login, no tokens, no TLS**. It is designed for a trusted
> home network and must **never be exposed to the internet**. Bind it to a
> LAN address, keep it behind your firewall, and do not put it behind a
> public reverse proxy.

## Status

Early development. See [SPEC.md](SPEC.md) for the full specification and the
commit plan that drives the build order.

## Quickstart

_Coming with the first runnable server commit._

## Configuration

Everything is configured through a single `yawss.yaml`. A commented example
will live in `yawss.example.yaml`.

## Development

```sh
nix develop          # dotnet 8, node 22, pnpm, sane-backends, pre-commit
pre-commit install
dotnet test
```

## License

MIT — see [LICENSE](LICENSE).
