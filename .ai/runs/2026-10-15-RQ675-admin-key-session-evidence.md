# RQ675 evidence

- Owner decision: sessionStorage + masked input + key clearing
- key never written to localStorage, URL or logs (test asserts localStorage empty)
- stored key used on reload so active import detection works without re-entry
- rejected key cleared automatically
- import/cancel/delete/cleanup/restore flows only changed in where the key comes from
- PR #166 merged as a249fc5f
