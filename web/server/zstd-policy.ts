import "server-only";
import { createRequire, syncBuiltinESMExports } from "node:module";

export function disableZstdDecompression(): void {
  const zlib = createRequire(`${process.cwd()}/package.json`)("node:zlib") as Record<string, unknown>;
  const methods = ["createZstdDecompress", "ZstdDecompress", "zstdDecompress", "zstdDecompressSync"];
  if (methods.some(name => typeof zlib[name] !== "function")) {
    throw new Error("Cannot install the Zstandard decompression policy.");
  }
  // Node embeds a vulnerable zstd copy independently of the OS packages.
  // Built-in fetch reads this module's factory even after fetch initializes.
  function rejectZstd(): never {
    throw new Error("Zstandard decompression is disabled.");
  }
  for (const name of methods) {
    Object.defineProperty(zlib, name, { value: rejectZstd, configurable: true, writable: true });
  }
  syncBuiltinESMExports();
}
