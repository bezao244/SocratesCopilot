"""Juiz local (Fase 3): faithfulness e answer relevancy para casos A, via qwen3:8b no Ollama.

Uso: python workbench/juiz.py [--resultados workbench/resultados-<data>.jsonl]
"""
import glob
import json
import sys
import time
import urllib.request

API = "http://127.0.0.1:11434/api/chat"


def pergunta_juiz(prompt):
    body = json.dumps({
        "model": "qwen3:8b",
        "stream": False,
        "think": False,
        "format": "json",
        "messages": [{"role": "user", "content": prompt}],
        "options": {"temperature": 0, "num_ctx": 8192},
    }, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(API, data=body, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=300) as r:
        data = json.loads(r.read().decode("utf-8"))
    return json.loads(data["message"]["content"])


def faithfulness(resposta, trechos):
    prompt = (
        "Você é um avaliador de fidelidade de respostas de IA. "
        "Liste as afirmações factuais da resposta abaixo e, para cada uma, diga se é "
        "apoiada pelos trechos fornecidos (true) ou não (false). "
        "Responda SOMENTE com JSON: {\"afirmacoes\":[{\"texto\":\"...\",\"apoiada\":true|false}]}.\n\n"
        f"RESPOSTA:\n{resposta[:4000]}\n\nTRECHOS:\n{trechos[:6000]}"
    )
    return pergunta_juiz(prompt)


def relevancia(resposta, pergunta):
    prompt = (
        "Você é um avaliador de relevância de respostas de IA. "
        "Dada a pergunta e a resposta abaixo, dê uma nota de 0.0 a 1.0 indicando o quanto "
        "a resposta realmente responde à pergunta (1.0 = responde completamente). "
        "Responda SOMENTE com JSON: {\"nota\":0.87}.\n\n"
        f"PERGUNTA: {pergunta}\n\nRESPOSTA: {resposta[:4000]}"
    )
    return pergunta_juiz(prompt)


def main():
    resultados_path = None
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--resultados" and i + 1 < len(args):
            resultados_path = args[i + 1]
    if resultados_path is None:
        resultados_path = sorted(glob.glob("workbench/resultados-*.jsonl"))[-1]
    saida = f"workbench/juiz-{time.strftime('%Y%m%d-%H%M')}.jsonl"
    casos = []
    with open(resultados_path, encoding="utf-8") as f:
        for linha in f:
            r = json.loads(linha)
            if r["desfecho"] == "A" and r["categoria_esperada"] == "A" and r["suite"] != "conflito":
                casos.append(r)
    print(f"casos a julgar: {len(casos)} | saida {saida}", flush=True)
    with open(saida, "a", encoding="utf-8") as f:
        for r in casos:
            resposta = r["resposta"]
            trechos = "\n---\n".join(
                f"{s.get('title', '')} | {s.get('location', '')}: {s.get('excerpt', '')}"
                for s in r["fontes_retornadas"]
            )
            faith, rel = None, None
            try:
                jf = faithfulness(resposta, trechos)
                af = jf.get("afirmacoes", [])
                if af:
                    faith = round(sum(1 for a in af if a.get("apoiada")) / len(af), 3)
            except Exception as e:
                print(f"  caso {r['id']}: faithfulness falhou ({e})", flush=True)
            try:
                jr = relevancia(resposta, r["pergunta"])
                nota = float(jr.get("nota", 0))
                rel = round(nota, 3)
            except Exception as e:
                print(f"  caso {r['id']}: relevancia falhou ({e})", flush=True)
            f.write(json.dumps({
                "id": r["id"], "faithfulness": faith, "answer_relevancy": rel,
                "resposta": resposta,
            }, ensure_ascii=False) + "\n")
            f.flush()
            print(f"caso {r['id']}: faithfulness {faith} | relevancia {rel}", flush=True)
    print("FIM", flush=True)


if __name__ == "__main__":
    main()
