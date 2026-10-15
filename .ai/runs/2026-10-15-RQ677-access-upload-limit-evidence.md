# RQ677 evidence

- Owner decision 100 MiB with clear rejection
- global Kestrel limit was 25 MiB (Program.cs:158), raised per request only for /run, /jobs, /preview
- declared oversized body -> 413 Serbian message before any file or batch
- UI shows limit and blocks >100 MiB file
- storage: s3 provider uploads to durable storage, local provider keeps only a web-local copy; render.yaml sets no Storage__Provider -> RQ678
- PR #167 merged as 741c2fa5
