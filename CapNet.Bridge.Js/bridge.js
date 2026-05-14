// Bridge between .NET (via Jering.Javascript.NodeJS) and the ESM-only capjs-core package.
//
// All structured data crosses the boundary as JSON strings — Jering uses System.Text.Json
// internally and we use Newtonsoft on the .NET side, so going through strings sidesteps the
// serializer mismatch. We deliberately do NOT pass capjs-core's `consumeNonce` / `signToken`
// callbacks — those concerns live in the .NET wrapper so all state is in one place.

let _capPromise = null;
function getCap() {
  if (!_capPromise) _capPromise = import("capjs-core");
  return _capPromise;
}

module.exports = {
  generateChallenge: async (secret, optsJson) => {
    const cap = await getCap();
    const opts = optsJson ? JSON.parse(optsJson) : {};
    const result = await cap.generateChallenge(secret, opts);
    return JSON.stringify(result);
  },

  validateChallenge: async (secret, bodyJson, optsJson) => {
    const cap = await getCap();
    const body = bodyJson ? JSON.parse(bodyJson) : {};
    const opts = optsJson ? JSON.parse(optsJson) : {};
    const result = await cap.validateChallenge(secret, body, opts);
    return JSON.stringify(result);
  },

  /**
   * Generates a fresh RSW keypair. Expensive (~700 ms at 2048 bits). Callers should persist the
   * result and reuse it across challenges.
   */
  generateRswKeypair: async (bits) => {
    const cap = await getCap();
    const kp = cap.generateRswKeypair(bits || 2048);
    return JSON.stringify(cap.serializeRswKeypair(kp));
  },
};
