using System;
using System.IO;

namespace CapNet.Assets
{
    /// <summary>
    /// Resolves filesystem paths to the client-side Cap assets (widget JS + WASM solver) that
    /// ship inside <c>CapNet.Bridge.Js</c>'s <c>node_modules</c>. Consumers self-host these by
    /// streaming the files from their own MVC/Web API controller — eliminating the third-party
    /// CDN dependency that the upstream widget defaults to.
    ///
    /// <para>
    /// The npm dependencies <c>@cap.js/widget</c> and <c>@cap.js/wasm</c> are declared in the
    /// bridge's <c>package.json</c> and installed alongside <c>capjs-core</c> by the same
    /// <c>npm ci</c> invocation. Once the bridge is provisioned, these assets are available
    /// at the paths this class resolves.
    /// </para>
    /// </summary>
    public static class CapAssets
    {
        /// <summary>Absolute path to the widget bundle (<c>cap.min.js</c>).</summary>
        public static string WidgetScriptPath(string bridgeRoot)
        {
            return ResolveAndCheck(bridgeRoot, "node_modules/@cap.js/widget/cap.min.js");
        }

        /// <summary>Absolute path to the floating-variant widget bundle.</summary>
        public static string WidgetFloatingScriptPath(string bridgeRoot)
        {
            return ResolveAndCheck(bridgeRoot, "node_modules/@cap.js/widget/cap-floating.min.js");
        }

        /// <summary>Absolute path to the WASM SHA-256 solver (<c>cap_wasm_bg.wasm</c>).</summary>
        public static string WasmModulePath(string bridgeRoot)
        {
            return ResolveAndCheck(bridgeRoot, "node_modules/@cap.js/wasm/browser/cap_wasm_bg.wasm");
        }

        /// <summary>Absolute path to the widget's optional CSS.</summary>
        public static string WidgetStylesheetPath(string bridgeRoot)
        {
            return ResolveAndCheck(bridgeRoot, "node_modules/@cap.js/widget/cap.css");
        }

        private static string ResolveAndCheck(string bridgeRoot, string relative)
        {
            if (string.IsNullOrEmpty(bridgeRoot)) throw new ArgumentNullException(nameof(bridgeRoot));
            string full = Path.GetFullPath(Path.Combine(bridgeRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(full))
            {
                throw new FileNotFoundException(
                    $"Cap asset not found at {full}. Run `npm ci` against the bridge to install @cap.js/widget and @cap.js/wasm.",
                    full);
            }
            return full;
        }
    }
}
