"""Assemble actual Unity captures of the retained navy concept."""
from pathlib import Path
from PIL import Image

OUT = Path(__file__).resolve().parents[2] / "outputs/drone-concepts"


def gif(folder, target, delay=33):
    frames = []
    for path in sorted((OUT / folder).glob("*.png")):
        image = Image.open(path).convert("RGB")
        image = image.resize((540, round(image.height * 540 / image.width)), Image.Resampling.LANCZOS)
        frames.append(image.quantize(colors=160))
    if frames:
        frames[0].save(OUT / target, save_all=True, append_images=frames[1:], duration=delay, loop=0, disposal=2)


def main():
    gif("frames1", "motion1.gif")
    gif("closeframes1", "closing.gif")
    gif("popupframes1", "popupmotion1.gif")
    gif("readyframes", "readiness.gif", 50)
    gif("upgradeframes", "upgrade.gif")
    (OUT / "index.html").write_text('''<!doctype html><html lang="ko"><meta charset="utf-8">
<title>드론 덱 · 네이비 시안</title><style>body{background:#252c43;color:#fff3e4;font:18px system-ui;margin:32px}button{padding:12px;margin:5px;border:0;border-radius:8px;background:#ffce54;cursor:pointer}img{display:block;width:min(540px,100%);margin-top:20px}</style>
<h1>알팡 · 드론 덱 강화</h1><p>두 번째 네이비 시안. 원형 알팡 강화와 개별 유닛 강화.</p>
<button onclick="show('concept1.png')">목록</button><button onclick="show('chief.png')">알팡 전체 강화</button>
<button onclick="show('closed.png')">접힌 드론 탭</button>
<button onclick="show('closing.gif')">빠른 접힘</button>
<button onclick="show('popup1.png')">유닛 정보</button><button onclick="show('motion1.gif')">제자리 팝</button>
<button onclick="show('bonuses.png')">레벨 보너스</button><button onclick="show('bonuses-bottom.png')">보너스 아래쪽</button>
<button onclick="show('upgrade.gif')">강화·공격</button><button onclick="show('readiness.gif')">골드 심머</button>
<img id="preview" src="concept1.png"><script>function show(p){document.getElementById('preview').src=p+'?t='+Date.now()}</script></html>''', encoding="utf-8")
    for name in ["concept2.png", "concept3.png", "popup2.png", "popup3.png", "motion2.gif", "motion3.gif", "popupmotion2.gif", "popupmotion3.gif", "comparison.png"]:
        (OUT / name).unlink(missing_ok=True)
    print("Created navy review and motion previews.")


if __name__ == "__main__":
    main()
