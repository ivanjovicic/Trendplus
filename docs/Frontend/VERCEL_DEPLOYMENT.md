# Vercel configuration ownership

The repository-root `vercel.json` is the canonical configuration when the Vercel project root is the repository root. Its install/build/output commands target `Klijent/clientapp`. `Klijent/clientapp/vercel.json` is retained for deployments whose Vercel project root is configured directly to `Klijent/clientapp`; Vercel reads the configuration relative to that selected project root.

Both configs intentionally keep the SPA rewrite and cache headers in parity: HTML/routes revalidate, while hashed `/assets/` files are immutable. When changing those rules, update both files and review the configured Vercel project root before deployment. Neither file should be removed until the project root is confirmed in Vercel settings.
