import { createRequire } from "node:module";

const require = createRequire(import.meta.url);

// Tailwind's enhanced-resolve escapes '#' as a NUL byte in filesystem paths.
// Its resolver hook lets these package imports retain the real path on Windows.
globalThis.__tw_resolve = (id, base) => {
  if (!base?.includes("#")) return;
  if (id === "tailwindcss/theme.css" || id === "tailwindcss/utilities.css") {
    return require.resolve(id);
  }
};
