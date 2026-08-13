"""Caso 40: verifica o rate limit (8 perguntas/10 min) — a 9a chamada deve dar 429.

Uso: python workbench/caso40_rate_limit.py
"""
import json
import time
import urllib.error
import urllib.request

API = "http://127.0.0.1:5081/api/v1/chat/messages"


def chama(i):
    body = json.dumps({
        "sessionId": f"eval-ratelimit-{i}",
        "message": "Qual o horário de atendimento telefônico?",
    }, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(API, data=body, headers={"Content-Type": "application/json"})
    t0 = time.perf_counter()
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            for _ in r:
                pass
        return r.status, round(time.perf_counter() - t0, 2), None
    except urllib.error.HTTPError as e:
        corpo = ""
        try:
            corpo = e.read().decode("utf-8", errors="replace")[:200]
        except Exception:
            pass
        return e.code, round(time.perf_counter() - t0, 2), corpo


def main():
    resultados = []
    for i in range(1, 10):
        status, seg, corpo = chama(i)
        resultados.append(status)
        print(f"chamada {i}: HTTP {status} em {seg}s" + (f" | {corpo}" if status != 200 else ""), flush=True)
    n429 = resultados.count(429)
    ok = n429 == 1 and resultados[-1] == 429
    print(f"RESULTADO: {'OK - 9a chamada bloqueada (429)' if ok else 'FALHOU'} (429s: {n429})", flush=True)
    with open("workbench/caso40-resultado.json", "w", encoding="utf-8") as f:
        json.dump({"status_por_chamada": resultados, "ok": ok, "horario": time.strftime("%Y-%m-%d %H:%M")}, f, ensure_ascii=False)


if __name__ == "__main__":
    main()
