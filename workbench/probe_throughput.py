"""Probe de throughput (§4.1): mede tokens/s de geracao e prefill do modelo local.

Uso: python workbench/probe_throughput.py
"""
import json
import statistics
import sys
import time
import urllib.request

API = "http://127.0.0.1:11434/api/chat"
PROMPT = (
    "Explique, em detalhes, o processo de atualização cadastral da empresa, "
    "os prazos de resposta do atendimento, as políticas de troca e reembolso "
    "e as condições de pagamento, listando cada etapa com clareza."
)


def probe():
    body = json.dumps({
        "model": "empresa-copiloto:v1",
        "stream": False,
        "think": False,
        "messages": [{"role": "user", "content": PROMPT}],
        "options": {"num_ctx": 8192, "num_predict": 512, "temperature": 0.1},
    }, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(API, data=body, headers={"Content-Type": "application/json"})
    t0 = time.perf_counter()
    with urllib.request.urlopen(req, timeout=300) as r:
        data = json.loads(r.read().decode("utf-8"))
    wall = time.perf_counter() - t0
    eval_count = data.get("eval_count", 0)
    eval_dur = data.get("eval_duration", 0) or 1
    pe_count = data.get("prompt_eval_count", 0)
    pe_dur = data.get("prompt_eval_duration", 0) or 1
    return {
        "geracao_tok_s": round(eval_count / (eval_dur / 1e9), 1),
        "prefill_tok_s": round(pe_count / (pe_dur / 1e9), 1),
        "eval_count": eval_count,
        "prompt_eval_count": pe_count,
        "wall_s": round(wall, 3),
    }


def main():
    n = 5
    res = []
    for i in range(1, n + 1):
        r = probe()
        tag = "warm-up" if i == 1 else f"run {i - 1}"
        res.append((tag, r))
        print(f"{tag}: geracao {r['geracao_tok_s']} tok/s | prefill {r['prefill_tok_s']} tok/s | {r['eval_count']} tok | wall {r['wall_s']}s", flush=True)
    saida = f"workbench/throughput-{time.strftime('%Y%m%d-%H%M')}.jsonl"
    with open(saida, "w", encoding="utf-8") as f:
        for tag, r in res:
            f.write(json.dumps({"run": tag, **r}, ensure_ascii=False) + "\n")
    runs = [r for tag, r in res if tag != "warm-up"]
    for chave in ("geracao_tok_s", "prefill_tok_s"):
        vals = [r[chave] for r in runs]
        print(f"{chave}: media {statistics.mean(vals):.1f} | mediana {statistics.median(vals):.1f} | p95 {sorted(vals)[-1] if len(vals) <= 4 else vals[0]}", flush=True)
    print(f"salvo em {saida}", flush=True)


if __name__ == "__main__":
    main()
