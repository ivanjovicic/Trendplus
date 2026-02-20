import requests
import time
import random


def safe_request(url, headers, session=None, retries=3, timeout=15):
    sess = session or requests.Session()

    for i in range(retries):
        try:
            resp = sess.get(url, headers=headers, timeout=timeout)
            if resp.ok:
                return resp.text
            print(f"❌ HTTP {resp.status_code} for {url}")
        except Exception as e:
            print("Request error:", e)

        sleep_time = 1.5 + random.random() * 1.5
        print(f"Retry in {sleep_time:.1f}s…")
        time.sleep(sleep_time)

    return None
