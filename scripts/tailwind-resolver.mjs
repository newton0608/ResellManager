import { createRequire } from "node:module";
import { resolve } from "node:path";

const require = createRequire(import.meta.url);

// Tailwind's enhanced-resolve escapes '#' as a NUL byte in filesystem paths.
// Resolve package CSS and the public storefront CSS files directly when the workspace path contains '#'.
globalThis.__tw_resolve = (id, base) => {
  if (!base?.includes("#")) return;
  if (id === "tailwindcss/theme.css" || id === "tailwindcss/utilities.css") {
    return require.resolve(id);
  }
  if (id === "./storefront.css" || id === "./catalogo-exploracion.css") {
    return resolve(base, id);
  }
};
