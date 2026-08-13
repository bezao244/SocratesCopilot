"""Runner do workbench: executa os casos do golden set contra a API real do copiloto.

Uso: python workbench/runner.py [--casos workbench/casos.jsonl] [--saida workbench/resultados-<data>.jsonl] [--ate 39]
"""
import json
import sys
import time
import uuid
import urllib.request
import urllib.error

API = "http://127.0.0.1:5081/api/v1/chat/messages"

PADROES_RNE = [
    "não encontrou uma informação", "não encontrou informa", "não encontrei",
    "informação confiável", "atendimento humano", "não tenho informações",
    "sem informação", "não há informação", "não disponho",
]


def classifica_desfecho(desfecho, resposta, error_message):
    if desfecho in ("400", "429", "E"):
        return desfecho
    if desfecho == "RC":
        return "RC"
    if desfecho == "RNE":
        return "RNE"
    texto = (resposta or "").lower()
    for p in PADROES_RNE:
        if p in texto:
            return "RNE"
    return "A"


def roda_caso(caso):
    if caso["id"] == 39:
        texto = "Pergunta de teste muito longa. " + ("x" * 2100)
    else:
        texto = caso["pergunta"]
    session_id = "eval-" + str(uuid.uuid4())
    body = json.dumps({"sessionId": session_id, "message": texto}, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(API, data=body, headers={"Content-Type": "application/json"})
    t0 = time.perf_counter()
    deltas = []
    fontes = []
    desfecho = None
    error_message = None
    lat_busca = None
    ttft = None
    status = None
    raw_events = []
    try:
        with urllib.request.urlopen(req, timeout=300) as r:
            status = r.status
            for linha in r:
                linha = linha.decode("utf-8", errors="replace").strip()
                if not linha:
                    continue
                try:
                    ev = json.loads(linha)
                except json.JSONDecodeError:
                    continue
                raw_events.append(ev.get("type"))
                agora = time.perf_counter() - t0
                tipo = ev.get("type")
                if tipo == "delta":
                    if ttft is None:
                        ttft = agora
                    if ev.get("text"):
                        deltas.append(ev["text"])
                elif tipo == "sources":
                    lat_busca = agora
                    fontes = ev.get("sources") or []
                elif tipo == "refusal":
                    lat_busca = agora
                    error_message = ev.get("errorMessage")
                    desfecho = "RC" if error_message and "conflito" in error_message.lower() else "RNE"
                elif tipo == "error":
                    desfecho = "E"
                    error_message = ev.get("errorMessage")
                elif tipo == "done":
                    if lat_busca is None:
                        lat_busca = agora
            if desfecho is None:
                desfecho = "A" if deltas else "E"
    except urllib.error.HTTPError as e:
        status = e.code
        try:
            error_message = e.read().decode("utf-8", errors="replace")[:300]
        except Exception:
            error_message = str(e)
        if e.code == 400:
            desfecho = "400"
        elif e.code == 429:
            desfecho = "429"
        else:
            desfecho = "E"
    except Exception as e:
        desfecho = "E"
        error_message = str(e)[:300]
    total = time.perf_counter() - t0
    resposta = "".join(deltas)
    return {
        "id": caso["id"],
        "categoria_esperada": caso["categoria"],
        "suite": caso.get("suite", "principal"),
        "pergunta": texto[:200] + ("..." if len(texto) > 200 else ""),
        "desfecho": classifica_desfecho(desfecho, resposta, error_message),
        "http_status": status,
        "error_message": error_message,
        "fontes_retornadas": fontes,
        "resposta": resposta,
        "latencia_busca_s": round(lat_busca, 3) if lat_busca is not None else None,
        "ttft_s": round(ttft, 3) if ttft is not None else None,
        "latencia_total_s": round(total, 3),
        "eventos": raw_events,
    }


def main():
    casos_path = "workbench/casos.jsonl"
    saida = None
    ate = 39
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--casos" and i + 1 < len(args):
            casos_path = args[i + 1]
        elif a == "--saida" and i + 1 < len(args):
            saida = args[i + 1]
        elif a == "--ate" and i + 1 < len(args):
            ate = int(args[i + 1])
    if saida is None:
        saida = f"workbench/resultados-{time.strftime('%Y%m%d-%H%M')}.jsonl"
    casos = []
    with open(casos_path, encoding="utf-8") as f:
        for linha in f:
            linha = linha.strip()
            if linha:
                casos.append(json.loads(linha))
    print(f"Casos carregados: {len(casos)} | rodando ate id {ate} -> saida {saida}", flush=True)
    with open(saida, "a", encoding="utf-8") as f:
        for caso in sorted(casos, key=lambda c: c["id"]):
            if caso["id"] > ate:
                continue
            if caso["id"] == 40:
                print("caso 40: executado separadamente na fase 6 (429)", flush=True)
                continue
            res = roda_caso(caso)
            f.write(json.dumps(res, ensure_ascii=False) + "\n")
            f.flush()
            print(f"caso {caso['id']}: {res['desfecho']} | busca {res['latencia_busca_s']}s | ttft {res['ttft_s']}s | total {res['latencia_total_s']}s | fontes {len(res['fontes_retornadas'])}", flush=True)
    print("FIM", flush=True)


if __name__ == "__main__":
    main()
