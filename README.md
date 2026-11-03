# NoAuth

[![NuGet Version](https://img.shields.io/nuget/v/Skaar.NoAuth.svg)](https://www.nuget.org/packages/Skaar.NoAuth)

A fake OAuth2/OpenID Connect server for local development and testing.
It grants all requests for tokens. Tokens are kept in memory only.

![Icon](./resources/icon.svg)

## Run as a dotnet tool

```shell
dotnet tool install --global Skaar.NoAuth
noauth
```

By default it listens to `https://localhost:44367` (using the ASP.NET Core development certificate,
see `dotnet dev-certs https --trust`). The front page lists all endpoints.

A file with login and client profiles may be given as the first argument (a file, or a folder containing
`loginProfiles.json`). Otherwise `loginProfiles.json` in the working directory is used, falling back to the
sample file installed with the tool.

```shell
noauth ./my-profiles.json
```

## Run in a container

```shell
docker build -t noauth .
docker run --rm -p 8080:8080 noauth
```

Inside the container the server listens to `http://+:8080` (`ASPNETCORE_URLS`).
`NoAuth__BaseUrl` is the url clients use to reach the server, and is used in the discovery document and redirects.
Mount your own profiles with `-v ./my-profiles.json:/app/loginProfiles.json`.

### HTTPS in a container

OAuth requires TLS (only `http://localhost` redirect uris are exempt), and many client libraries refuse
metadata or authorities over plain http. Kestrel takes a certificate from environment variables.
The simplest is the ASP.NET Core development certificate, which is already trusted on your machine:

```shell
dotnet dev-certs https -ep ~/.aspnet/https/noauth.pfx -p changeit
dotnet dev-certs https --trust

docker run --rm -p 8443:8443 \
  -e ASPNETCORE_URLS=https://+:8443 \
  -e NoAuth__BaseUrl=https://localhost:8443 \
  -e ASPNETCORE_Kestrel__Certificates__Default__Path=/https/noauth.pfx \
  -e ASPNETCORE_Kestrel__Certificates__Default__Password=changeit \
  -v ~/.aspnet/https:/https:ro \
  noauth
```

The development certificate is only valid for `localhost`. When other containers call NoAuth by its container
name, use docker compose instead.

### Docker compose

[compose.yaml](compose.yaml) runs NoAuth with HTTPS, reachable as `https://localhost:8443` from the host and as
`https://noauth:8443` from other containers on the same network. The certificate is created with
[mkcert](https://github.com/FiloSottile/mkcert), which also makes it trusted on your machine:

```shell
./scripts/create-certs.sh   # creates certs/noauth.pfx and certs/rootCA.pem (git ignored)
docker compose up --build
```

`NoAuth__DeriveUrlsFromRequest` makes the urls in the discovery document follow the name the client used,
so both the browser (`localhost`) and other containers (`noauth`) get urls they can reach. The issuer is the same.

Containers calling NoAuth must trust `certs/rootCA.pem`. For Debian based images (including the .NET images),
add it to the system trust store as root, e.g. in the Dockerfile of the calling service:

```dockerfile
COPY certs/rootCA.pem /usr/local/share/ca-certificates/mkcert-rootCA.crt
RUN update-ca-certificates
```

## Configuration

All settings are in the `NoAuth` section of [appsettings.json](src/Skaar.NoAuth/appsettings.json), and can be
overridden with environment variables (`NoAuth__Issuer=...`) or command line arguments (`--NoAuth:Issuer=...`).

| Setting                         | Description                                                                           |
|---------------------------------|---------------------------------------------------------------------------------------|
| `Issuer`                        | The `iss` claim of issued tokens.                                                     |
| `BaseUrl`                       | The public url of the server. Also the url to listen to, unless urls are set.         |
| `Paths`                         | The path of every endpoint. Change a path to move an endpoint, empty to disable it.   |
| `Oidc:Strategy`                 | `WebForm` shows a login form. `GenerateRandom` logs in a random user without asking.  |
| `Oidc:RememberUser`             | With `GenerateRandom`: reuse the same user for later logins in the browser (off by default). |
| `UseReferenceTokens`            | Issue opaque access tokens instead of JWTs.                                           |
| `Strict`                        | Enforce OAuth 2.1 rules that are relaxed by default (see below).                      |
| `DeriveUrlsFromRequest`         | Use the scheme and host of each request (not `BaseUrl`) in discovery and redirects.   |
| `TokenTtlInSeconds`             | Lifetime of issued tokens.                                                            |

## Endpoints (default paths)

| Path                                     | Description                                                              |
|------------------------------------------|--------------------------------------------------------------------------|
| `/.well-known/openid-configuration`      | Discovery document.                                                      |
| `/favicon.png`                           | The icon of the html pages.                                              |
| `/.well-known/oauth-authorization-server` | Authorization server metadata (RFC 8414). Same document as discovery.   |
| `/.well-known/openid-configuration/jwks` | Signing keys.                                                            |
| `/connect/token`, `/v1/oauth2/tokens`    | Token endpoint (`client_credentials`, `authorization_code`, `refresh_token`). |
| `/connect/authorize`                     | Authorization endpoint (code flow).                                      |
| `/connect/login`                         | Login form.                                                              |
| `/connect/userinfo`                      | User info for access tokens from the code flow.                          |
| `/connect/introspect`                    | Token introspection (RFC 7662).                                          |
| `/connect/revoke`                        | Token revocation (RFC 7009), for access and refresh tokens.              |
| `/connect/register`                      | Dynamic client registration (RFC 7591). Accepts any client.              |
| `/connect/endsession`                    | Logout.                                                                  |
| `/api/token?client_id=&scope=&resource=&ttl_minutes=` | Get a token as plain text. With `ttl_minutes` a new token is always issued, and 0 or less gives an expired token. `DELETE` removes the tokens of a `client_id`. |
| `/api/tokens`                            | All issued tokens. `DELETE` removes all.                                 |
| `/api/log`                               | The log. `DELETE` clears it.                                             |
| `/api/configuration`                     | The runtime configuration.                                               |
| `/api/loginprofiles`                     | Login and client profiles. `POST` adds, `DELETE /api/loginprofiles/{name}` removes. The schema is at `/api/loginprofiles/schema.json`. |

## Standards

- **OAuth 2.1 / RFC 9700:** PKCE (RFC 7636) is verified whenever a `code_challenge` is sent. Only the code flow is
  supported (no implicit or password grant). Refresh tokens are rotated on every use.
- **JWT access tokens (RFC 9068):** `typ: at+jwt`, with `sub` (the user, or the client for client credentials),
  `client_id`, `jti`, `iat` and `scope` as a space separated string. Access tokens from the code flow include the
  user's claims, so they can be used to test APIs.
- **Resource indicators (RFC 8707):** `resource` sets the `aud` of access tokens. The non-standard `audience`
  parameter is also accepted.
- **Issuer in authorization responses (RFC 9207):** redirects to the client include `iss`.
- **MCP clients:** authorization server metadata, PKCE, resource indicators and dynamic client registration are
  supported. Any `client_id` is accepted, including client id metadata document urls (the document is not fetched).
  The protected resource metadata (RFC 9728) is served by the MCP server itself, pointing to NoAuth.

Tokens are kept in memory for 24 hours after they expire, so refresh tokens can be used until then.
Claims set by NoAuth itself (`sub`, `iss`, `aud`, `exp`, `nonce`, `client_id`, `scope`, the name claims, etc.)
cannot be overridden from the login form or client profiles.

By default NoAuth is lenient. With `Strict` enabled:

- PKCE with `S256` is required, and `plain` is not accepted.
- Authorization codes are single-use and expire after 10 minutes.
- The `client_id` and `redirect_uri` of the token request must match the authorization request.
- Refresh tokens can only be used by the client they were issued to.
- A `code_verifier` without a `code_challenge` is rejected, and `resource` must be an absolute uri.

## Login and client profiles

[loginProfiles.json](src/Skaar.NoAuth/loginProfiles.json) contains templates shown in the login form,
and clients with claims (e.g. `scope`) added to every token issued to that `client_id`.
See [the schema](src/Skaar.NoAuth/loginProfiles.schema.json).

## Development

```shell
dotnet build
dotnet test
```

Tests use [xUnit v3](https://xunit.net) with [Shouldly](https://docs.shouldly.org), run on Microsoft.Testing.Platform
(configured in [global.json](global.json)). Integration tests run the server in memory with `TestServer`,
configured with command line arguments (e.g. `--NoAuth:Strict=true`).

Pushing to `main` builds, tests and publishes the dotnet tool to NuGet when `VersionPrefix` in
[Skaar.NoAuth.csproj](src/Skaar.NoAuth/Skaar.NoAuth.csproj) is a new version.
It uses NuGet trusted publishing (no API key): the workflow needs a trusted publishing policy on nuget.org
and the `NUGET_USER` secret (the nuget.org user name).
