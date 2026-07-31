#!/usr/bin/env python3
"""
data_lint.py — 신규 데이터 시트(스키마 계약서: docs/codex_claude회의/14) 검증 린트.

역할:
  - 새 구조 워크북(.xlsx)을 읽어 PK 유니크 / FK 존재 / enum 소속 / 레거시 유입 / 필수 시트 존재를 검사.
  - 결과를 마크다운 리포트로 출력 (AI/사람 공유용, 회의 5번 "참조 관계 추적 도구" 경량 실현).

전제:
  - 5행 헤더 규칙: r1 메뉴/설명, r2 목적, r3 필드명, r4 타입, r5 설명, r6~ 데이터.
    (필드명 행을 자동 탐지 보조하되 기본은 3행으로 가정.)
  - 아직 시트가 안 만들어졌으면 'MISSING(skip)', 데이터가 비었으면 데이터 규칙 skip.
    → 회의 전 골격 단계에서도 그대로 돌아가는 skeleton.

사용법:
  python tools/data_lint.py <workbook.xlsx> [-o report.md]

주의: Assets/ 밖(레포 루트 tools/)에 둔다 — Unity 빌드에 파이썬을 섞지 않기 위함.
"""
import sys
import argparse

try:
    import openpyxl
except ImportError:
    print("openpyxl 가 필요합니다:  python -m pip install openpyxl")
    sys.exit(2)

# ── 레거시 토큰: 신규 시트에 유입되면 FAIL (사장된 구 부족) ──
LEGACY_TOKENS = ("GUNNER", "NINJA", "WIZARD", "MAGICIAN")

# ── 스키마 계약(14 문서) 요약 ──
# fk: {컬럼: (대상시트, 대상컬럼, nullable)}
SCHEMA = {
    "Enum_Grade":      {"pk": ["grade_key"]},
    "Enum_Job":        {"pk": ["job_key"]},
    "Enum_Keyword":    {"pk": ["keyword_key"]},
    "Enum_Category":   {"pk": ["category_key"]},
    "Enum_Difficulty": {"pk": ["difficulty_key"]},  # 9-1 catch: 계약서에 추가돼야 함

    "Unit_Def": {
        "pk": ["unit_key"],
        "fk": {
            "job":      ("Enum_Job", "job_key", False),
            "category": ("Enum_Category", "category_key", False),
        },
    },
    "Unit_Stat": {
        "pk": ["unit_key", "grade"],
        "unique": ["runtime_id"],
        "fk": {
            "unit_key": ("Unit_Def", "unit_key", False),
            "grade":    ("Enum_Grade", "grade_key", False),
            "skill_id": ("Skill_Def", "skill_id", True),
        },
    },
    "Unit_Keyword": {
        "pk": ["unit_key", "keyword_key"],
        "fk": {
            "unit_key":    ("Unit_Def", "unit_key", False),
            "keyword_key": ("Enum_Keyword", "keyword_key", False),
        },
    },
    "Drone_Stat": {
        "pk": ["unit_key", "grade"],
        "fk": {"unit_key": ("Unit_Def", "unit_key", False)},
        # grade nullable (등급 없는 DRONE_RALLY) → grade FK는 데이터 있을 때만 검사
    },
    "Effect_Def": {"pk": ["effect_key"]},
    "Skill_Def": {
        "pk": ["skill_id"],
        "fk": {"action_key": ("Effect_Def", "effect_key", False)},
    },
    "Skill_Effect": {
        "pk": ["skill_id", "effect_key", "grade"],
        "fk": {
            "skill_id":   ("Skill_Def", "skill_id", False),
            "effect_key": ("Effect_Def", "effect_key", False),
            "grade":      ("Enum_Grade", "grade_key", False),
        },
    },
    "Choose_Data": {
        "pk": ["choose_id"],
        "unique": ["choose_key"],
        "fk": {
            "grade":      ("Enum_Grade", "grade_key", False),
            "effect_key": ("Effect_Def", "effect_key", True),
        },
        "legacy_scan": ["effect_key", "choose_key"],
    },
    "Totem_Data": {
        "pk": ["totem_id"],
        "fk": {
            "grade":      ("Enum_Grade", "grade_key", False),
            "effect_key": ("Effect_Def", "effect_key", True),
        },
    },
    "Forge_Ingame":   {"pk": ["forge_key"]},
    "Upgrade_Outgame": {"pk": ["upgrade_key"]},
    "Round_Data": {
        "pk": ["round_id"],
        "fk": {"difficulty": ("Enum_Difficulty", "difficulty_key", False)},
    },
    "Round_Boss_Exp": {
        "pk": ["boss_id"],
        "fk": {"round_id": ("Round_Data", "round_id", False)},
    },
    "Round_Levelup": {"pk": ["level"]},
}

TYPE_KEYWORDS = {"int", "float", "string", "bool", "#"}


def norm(v):
    """셀 값을 비교용 문자열로 정규화. 1000.0 → '1000'."""
    if v is None:
        return ""
    if isinstance(v, float) and v.is_integer():
        return str(int(v))
    return str(v).strip()


def find_header_row(ws, pk_first):
    """필드명 행 탐지: pk 첫 컬럼명이 있는 행(1~6). 없으면 3(계약 기본)."""
    for r in range(1, 7):
        for c in range(1, ws.max_column + 1):
            if norm(ws.cell(r, c).value).lower() == pk_first.lower():
                return r
    return 3


def load_sheet(ws, pk_first):
    """5행 헤더 파싱 → (field->col 맵, [행 dict...])."""
    hr = find_header_row(ws, pk_first)
    fields = {}
    for c in range(1, ws.max_column + 1):
        name = norm(ws.cell(hr, c).value)
        if name and not name.startswith("#"):
            fields[name] = c
    rows = []
    for r in range(hr + 1, ws.max_row + 1):
        # 타입/설명 행 및 빈 행 skip: pk 첫 컬럼 셀 기준
        pkcell = norm(ws.cell(r, fields.get(pk_first, 1)).value)
        if pkcell == "" or pkcell.lower() in TYPE_KEYWORDS or pkcell.startswith("#"):
            continue
        row = {name: norm(ws.cell(r, col).value) for name, col in fields.items()}
        rows.append(row)
    return fields, rows


class Report:
    def __init__(self):
        self.lines = []
        self.fail = 0
        self.warn = 0
        self.skip = 0
        self.ok = 0

    def add(self, level, sheet, msg):
        icon = {"FAIL": "🔴", "WARN": "🟡", "SKIP": "⚪", "OK": "🟢"}[level]
        self.lines.append(f"- {icon} **[{level}]** `{sheet}` — {msg}")
        setattr(self, level.lower() if level != "OK" else "ok",
                getattr(self, level.lower() if level != "OK" else "ok") + 1)


def lint(path):
    rep = Report()
    wb = openpyxl.load_workbook(path, data_only=True)
    present = set(wb.sheetnames)

    # 1차: 존재하는 시트 로드
    loaded = {}   # sheet -> (fields, rows)
    for sheet, spec in SCHEMA.items():
        if sheet not in present:
            rep.add("SKIP", sheet, "시트 미생성 (골격 단계면 정상)")
            continue
        try:
            loaded[sheet] = load_sheet(wb[sheet], spec["pk"][0])
        except Exception as e:  # noqa
            rep.add("FAIL", sheet, f"파싱 오류: {e}")

    # FK 대상 값 집합
    target_values = {}
    for sheet, (fields, rows) in loaded.items():
        for col in fields:
            target_values[(sheet, col)] = {r.get(col, "") for r in rows}

    # 2차: 규칙 검사
    for sheet, spec in SCHEMA.items():
        if sheet not in loaded:
            continue
        fields, rows = loaded[sheet]

        # 헤더에 PK 컬럼 존재?
        for pk in spec["pk"]:
            if pk not in fields:
                rep.add("FAIL", sheet, f"PK 컬럼 `{pk}` 헤더에 없음")

        if not rows:
            rep.add("SKIP", sheet, "데이터 없음 (헤더만 — 데이터 규칙 skip)")
            continue

        # PK 유니크(복합 포함)
        seen = set()
        for r in rows:
            key = tuple(r.get(pk, "") for pk in spec["pk"])
            if key in seen:
                rep.add("FAIL", sheet, f"PK 중복: {dict(zip(spec['pk'], key))}")
            seen.add(key)

        # unique 컬럼
        for uc in spec.get("unique", []):
            vals = [r.get(uc, "") for r in rows if r.get(uc, "") != ""]
            if len(vals) != len(set(vals)):
                rep.add("FAIL", sheet, f"unique 컬럼 `{uc}` 중복 존재")

        # FK 존재
        for col, (tsheet, tcol, nullable) in spec.get("fk", {}).items():
            if col not in fields:
                continue
            tset = target_values.get((tsheet, tcol))
            if tset is None:
                rep.add("WARN", sheet, f"FK `{col}` 대상 `{tsheet}` 미로드 — 검증 보류")
                continue
            for r in rows:
                v = r.get(col, "")
                if v == "" or v.upper() == "NONE":
                    if not nullable:
                        rep.add("FAIL", sheet, f"필수 FK `{col}` 비어있음 (row pk={tuple(r.get(p,'') for p in spec['pk'])})")
                    continue
                if v not in tset:
                    rep.add("FAIL", sheet, f"FK 깨짐: `{col}`=`{v}` 가 `{tsheet}.{tcol}` 에 없음")

        # 레거시 유입 스캔
        for col in spec.get("legacy_scan", []):
            if col not in fields:
                continue
            for r in rows:
                v = r.get(col, "").upper()
                if any(tok in v for tok in LEGACY_TOKENS):
                    rep.add("FAIL", sheet, f"레거시 토큰 유입: `{col}`=`{r.get(col)}` (사장 부족 — 신규 등록 금지)")

        if not any(l.startswith("- 🔴") and f"`{sheet}`" in l for l in rep.lines[-20:]):
            rep.add("OK", sheet, f"{len(rows)}행 검사 통과")

    return rep


def main():
    ap = argparse.ArgumentParser(description="신규 데이터 시트 린트 (스키마 계약서 14 기준)")
    ap.add_argument("workbook", help="검사할 .xlsx 경로")
    ap.add_argument("-o", "--out", help="마크다운 리포트 출력 경로 (없으면 stdout)")
    args = ap.parse_args()

    rep = lint(args.workbook)
    header = (
        f"# 데이터 린트 리포트\n\n"
        f"- 대상: `{args.workbook}`\n"
        f"- 결과: 🔴 FAIL {rep.fail} / 🟡 WARN {rep.warn} / ⚪ SKIP {rep.skip} / 🟢 OK {rep.ok}\n"
        f"- 판정: {'❌ 실패 (FAIL 있음)' if rep.fail else '✅ 통과'}\n\n---\n\n"
    )
    body = "\n".join(rep.lines)
    out = header + body + "\n"

    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            f.write(out)
        print(f"리포트 작성: {args.out}  (FAIL {rep.fail} / WARN {rep.warn})")
    else:
        try:
            print(out)
        except UnicodeEncodeError:
            sys.stdout.buffer.write(out.encode("utf-8"))

    sys.exit(1 if rep.fail else 0)


if __name__ == "__main__":
    main()
