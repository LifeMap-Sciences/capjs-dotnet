# capjs-dotnet

Thin .NET wrapper around [Cap](https://github.com/tiagozip/cap) — the privacy-first, self-hosted CAPTCHA built on proof-of-work, RSW time-lock puzzles, and optional browser instrumentation.

The library does **not** reimplement Cap's challenge algorithm. It calls upstream [`capjs-core`](https://www.npmjs.com/package/capjs-core) through [`Jering.Javascript.NodeJS`](https://github.com/JeringTech/Javascript.NodeJS), keeping the .NET surface small (~150 LOC) and delegating cryptography, RSW math, and instrumentation script generation to the library Cap's maintainer ships and tests. The .NET side owns three things: HTTP entry points, redeem-token storage, and JWT-signature replay protection.

## Why

If your app is .NET (ASP.NET MVC 5 / Framework 4.8 or newer) and you want Cap's protections without running Cap's standalone Bun server as a sidecar, embed the bridge in-process.

## Install

```xml
<PackageReference Include="CapNet" Version="0.1.0" />
```

Ships the .NET library. The Node bridge (`bridge.js` + `node_modules`) lives in the repo's `CapNet.Bridge.Js/` folder; deploy it next to your application binaries and point `CapService` at its path. Node.exe must be available on the host (the typical Jering-hosting requirement).

## Usage

```csharp
using CapNet;
using CapNet.Challenges;
using CapNet.Storage;
using Jering.Javascript.NodeJS;

// At startup
var services = new ServiceCollection();
services.AddNodeJS();
var node = services.BuildServiceProvider().GetRequiredService<INodeJSService>();

var cap = new CapService(
    secret:      Environment.GetEnvironmentVariable("CAP_SECRET"),       // ≥16 UTF-8 bytes
    node:        node,
    bridgePath:  Path.Combine(AppContext.BaseDirectory, "bridge", "bridge.js"),
    state:       new MyAppCacheAdapter(),                                 // ICapStateStore — see below
    defaults:    new ChallengeOptions
    {
        ChallengeCount       = 30,
        ChallengeSize        = 32,
        ChallengeDifficulty  = 3,
        Instrumentation      = new { blockAutomatedBrowsers = true, obfuscationLevel = 3 },
        Scope                = "my-app",
    });

// In your captcha controller
[HttpPost, Route("cap/challenge")]
public async Task<HttpResponseMessage> Challenge()
    => Json(await cap.IssueChallengeJsonAsync());

[HttpPost, Route("cap/redeem")]
public async Task<HttpResponseMessage> Redeem(HttpRequestMessage req)
{
    var body    = await req.Content.ReadAsStringAsync();
    var outcome = await cap.RedeemAsync(body);
    return new HttpResponseMessage(outcome.HttpStatus)
    {
        Content = new StringContent(outcome.ResponseJson, Encoding.UTF8, "application/json"),
    };
}

// In your registration / login action filter
bool ok = await cap.VerifyRedeemTokenAsync(submittedToken);
```

### The `ICapStateStore` contract

CapNet keeps no in-process state. It needs a tiny key/value store with TTL, which you wire to whatever cache your app already runs — `IMemoryCache`, `IDistributedCache`, Redis via StackExchange, Azure Cache for Redis, etc. The library prefixes its keys (`capnet:nonce:…`, `capnet:redeem:…`), so the namespace is self-contained.

```csharp
public interface ICapStateStore
{
    Task PutAsync(string key, string value, TimeSpan ttl);
    Task<string> GetAsync(string key);
    Task RemoveAsync(string key);
}
```

For a single-process dev box, use the bundled `MemoryCapStateStore`. For multi-server deployments, write a 15-line adapter against your existing distributed cache.

## What you get for free, vs what stays your job

| | CapNet handles | Your app handles |
|---|---|---|
| Challenge generation (PoW / RSW / instrumentation) | ✅ via capjs-core | |
| JWT sign/verify | ✅ via capjs-core | |
| Redeem-token TTL | ✅ via `ICapStateStore` | |
| Replay protection | ✅ via `ICapStateStore` | |
| HTTP plumbing | | one controller, three routes |
| Form integration / hidden field | | yours |
| Action filter wiring | | yours |
| Backing cache (memory or Redis) | | you provide via `ICapStateStore` |
| RSW keypair persistence | | you persist once, load on startup |

## Architecture

```
Browser (Cap widget)
    │
    │  POST /cap/challenge       ← capjs-core's JSON, verbatim
    │  POST /cap/redeem
    ▼
ASP.NET (your app)
    │
    │  CapService.IssueChallengeJsonAsync
    │  CapService.RedeemAsync
    ▼
Jering.Javascript.NodeJS  ──►  bridge.js  ──►  capjs-core (npm)
    │                              │
    │  ICapStateStore (your impl)  │
    └─►  redis / memory cache      │
                                   └──►  generateChallenge / validateChallenge
```

## Limitations / known issues

- **Widget v0.1.51 has a format-2 speculative-fetch race.** When the response is `format: 2`, the widget can settle its internal state machine before `solve()` registers its listener, hanging forever. Use format 1 (sha256-pow + optional top-level instrumentation) with the upstream widget. The library exposes format 2 / RSW correctly for clients that drive the protocol directly (see `CapNet.E2E/tests/format2-all-protocols.spec.ts`).
- **`capjs-core` is pre-1.0** — only versions `0.1.x` are published. Pin a specific version transitively; treat each bump as a deliberate release of CapNet.
- **Node.exe required on every host.** Jering manages a child-process pool. If "no Node on the auth tier" is a hard policy, this library isn't the right shape — run Cap's standalone Bun server out-of-process instead.

## Development

```powershell
# Build everything
dotnet build CapNet.sln

# Run the demo (http://localhost:5500/)
dotnet run --project CapNet.Demo.Web -- http://localhost:5500/

# Run unit + E2E tests
npm --prefix CapNet.E2E install
npm --prefix CapNet.E2E exec playwright install chromium
npm --prefix CapNet.E2E test
```

The demo loads the real `@cap.js/widget` from jsDelivr, so a network connection is required for the manual smoke test.

### Building from source

If you've cloned the repo and want to consume your local build from another solution (e.g. to try an unreleased change):

```powershell
./scripts/pack-local.ps1                       # writes ./packages/CapNet.<version>.nupkg
```

Then in the consumer's `nuget.config`, add the folder as a source and reference the version you built.

## Credits

This is a wrapper. The whole captcha — PoW, RSW, instrumentation, the JS bridge between server and widget — is the work of [Tiago Rangel](https://github.com/tiagozip) and the Cap contributors. If you find CapNet useful, please star the upstream [Cap repo](https://github.com/tiagozip/cap) and consider [supporting Tiago directly](https://github.com/sponsors/tiagozip).

## License

MIT. Cap and `capjs-core` are also MIT.
