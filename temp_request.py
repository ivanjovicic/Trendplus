import requests

try:
    r = requests.get('http://localhost:8080/api/analytics/vendor-sales-nivelacija/options?take=365', timeout=30)
    print('status', r.status_code)
    print(r.text)
except Exception as e:
    print('error', repr(e))
