using System;
using System.IO;
using System.Web.Http;
using CapNet;
using CapNet.Challenges;
using CapNet.Storage;
using Jering.Javascript.NodeJS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.StaticFiles;
using Newtonsoft.Json.Linq;
using Owin;

namespace CapNet.Demo.Web
{
    public class Startup
    {
        public static CapService Service { get; private set; }
        public static ChallengeOptions ArmedOptions { get; private set; }
        public static ChallengeOptions TestOptions { get; private set; }
        public static ChallengeOptions Format2Options { get; private set; }
        private static ServiceProvider _serviceProvider;

        public void Configuration(IAppBuilder app)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string bridgePath = Path.Combine(baseDir, "bridge", "bridge.js");

            var services = new ServiceCollection();
            services.AddNodeJS();
            _serviceProvider = services.BuildServiceProvider();
            var node = _serviceProvider.GetRequiredService<INodeJSService>();

            // Demo placeholder. Real deployments should read this from Key Vault / env var /
            // protected config — never hard-code a captcha secret. Must be at least 16 UTF-8 bytes.
            string secret = Environment.GetEnvironmentVariable("CAPNET_DEMO_SECRET")
                ?? "demo-secret-of-at-least-sixteen-bytes!";

            Service = new CapService(
                secret: secret,
                node: node,
                bridgePath: bridgePath,
                state: new MemoryCapStateStore());

            JObject keypair = LoadOrGenerateKeypair(baseDir).GetAwaiter().GetResult();

            // Armed mode: format 2, all three protocols, instrumentation blocks automated browsers.
            // This is what a production deployment would look like.
            // Armed mode: format 1 (the only one upstream @cap.js/widget actually drives reliably
            // today — v0.1.51 has a state-machine race on format 2). sha256-pow + instrumentation
            // with blockAutomatedBrowsers=true is the realistic production shape.
            ArmedOptions = new ChallengeOptions
            {
                ChallengeCount = 30,
                ChallengeSize = 32,
                ChallengeDifficulty = 3,
                Instrumentation = new
                {
                    blockAutomatedBrowsers = true,
                    // Level 3 strips whitespace only — skips javascript-obfuscator's RC4 string
                    // encoding and control-flow flattening, which occasionally produce env-check
                    // false positives on real browsers (manifests as `[cap] Instrumentation failed`).
                    obfuscationLevel = 3,
                },
                Scope = "demo",
            };

            TestOptions = new ChallengeOptions
            {
                ChallengeCount = 30,
                ChallengeSize = 32,
                ChallengeDifficulty = 3,
                Instrumentation = new
                {
                    blockAutomatedBrowsers = false,
                    obfuscationLevel = 1,
                },
                Scope = "demo",
            };

            // Format 2 envelope with all three protocols. Driven by a custom JS solver in tests —
            // the upstream widget can't consume this yet. Proves the .NET library forwards every
            // option to capjs-core correctly.
            Format2Options = new ChallengeOptions
            {
                Format = 2,
                Protocols = new[] { "sha256-pow", "rsw", "instrumentation" },
                ChallengeCount = 6,
                ChallengeSize = 16,
                ChallengeDifficulty = 2,
                Keypair = keypair,
                T = 1_000,
                Instrumentation = new
                {
                    blockAutomatedBrowsers = false,
                    obfuscationLevel = 1,
                },
                Scope = "demo",
            };

            var config = new HttpConfiguration();
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            config.MapHttpAttributeRoutes();
            app.UseWebApi(config);

            string webRoot = Path.Combine(baseDir, "wwwroot");
            var fileServer = new FileServerOptions
            {
                FileSystem = new PhysicalFileSystem(webRoot),
                EnableDefaultFiles = true,
            };
            fileServer.DefaultFilesOptions.DefaultFileNames = new[] { "index.html" };
            app.UseFileServer(fileServer);
        }

        private static async System.Threading.Tasks.Task<JObject> LoadOrGenerateKeypair(string baseDir)
        {
            string keypairPath = Path.Combine(baseDir, "rsw-keypair.json");
            string json;
            if (File.Exists(keypairPath))
            {
                Console.WriteLine($"[CapNet.Demo] Loading RSW keypair from {keypairPath}");
                json = File.ReadAllText(keypairPath);
            }
            else
            {
                Console.WriteLine("[CapNet.Demo] Generating RSW keypair (2048-bit, ~700ms)…");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                json = await Service.GenerateRswKeypairJsonAsync(2048).ConfigureAwait(false);
                sw.Stop();
                File.WriteAllText(keypairPath, json);
                Console.WriteLine($"[CapNet.Demo] Generated and cached in {sw.ElapsedMilliseconds} ms");
            }
            return JObject.Parse(json);
        }
    }
}
