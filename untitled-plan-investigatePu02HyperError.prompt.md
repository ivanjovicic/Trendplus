Plan — Investigate 'PU02 hyper error' connection timeout

Goal
- Identify the root cause of the runtime log entry: "[PU02] could not complete HTTP request to instance: hyper error: connection error (source: Operation timed out (os error 110))" and determine whether it originates from the app, ingress/proxy, or an outbound HTTP call (e.g., storage upload).

Background
- The failing request was an `OPTIONS /api/access-import/run` followed by a timeout ~35s later.
- Candidate causes: ingress/proxy unable to connect to instance; instance unresponsive or blocked by long-running sync IO; outbound network call from the request (likely `_fileStorage.UploadAsync` to S3/MinIO) causing the thread to stall.

Steps
1) Collect logs around the timestamp
   - Search app logs for the correlationId and for "PU02"/"hyper error" around the event time.
2) Identify which component emitted `[PU02]`
   - Confirm whether the tag is from platform/ingress logs or from the application.
3) Instrument the app (if necessary)
   - Add short debug logs around `PrepareQueuedSourceAsync()` and `_fileStorage.UploadAsync()` logging start/finish and elapsed ms.
   - Add timing logs around `EnqueueAsync()` and `ClaimNextAsync()` where appropriate.
4) Reproduce the request locally while tailing logs
   - Run the same `POST /api/access-import/run` (small file) and capture logs and correlation IDs.
5) Test connectivity to external endpoints
   - Verify S3/MinIO endpoint reachability and run a small upload test.
6) Inspect platform/ingress metrics and logs
   - Check instance health, CPU/memory, network, and if the ingress recorded connection errors.

Commands (examples)
- Search local logs (PowerShell):

  Select-String -Path stdout.txt,stderr.txt -Pattern "PU02","hyper error","Operation timed out" -Context 3,6

- Tail logs while reproducing (PowerShell):

  Get-Content .\stdout.txt -Wait -Tail 200

- Test API (local):

  curl -v -X OPTIONS http://127.0.0.1:8080/api/access-import/run
  curl -v -X POST http://127.0.0.1:8080/api/access-import/run -F "file=@/path/to/TRENDPLUS.accdb" -H "X-Admin-Key: <key>"

- Test storage endpoint reachability:

  # PowerShell
  Test-NetConnection -ComputerName <s3-host> -Port 443

  # curl
  curl -v https://<s3-endpoint>/

  # aws cli (if custom endpoint)
  aws s3 ls s3://your-bucket --endpoint-url https://your-storage-endpoint

- Provider logs (example for Fly):

  flyctl logs --app <app-name> --since 10m

Mitigations to consider
- Move large/slow uploads out of the synchronous request path (background worker or presigned client upload).
- Reduce upload timeouts and add retries with backoff in `_fileStorage.UploadAsync`.
- Increase instance resources or instance count if CPU/IO saturation observed.
- Adjust ingress/proxy timeouts where provider allows.

Next actions
- Add debug timing logs around `_fileStorage.UploadAsync` and redeploy to capture whether uploads coincide with the timeouts.
- Reproduce the `/api/access-import/run` request while tailing app and provider logs.

Questions for you
- Where is the app hosted (Fly/Neon/Kubernetes/Docker local)?
- Do you want me to add the debug logging patch now? (I can implement and run tests.)
